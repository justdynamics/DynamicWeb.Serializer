<#
.SYNOPSIS
Fully autonomous Swift-2.2 → CleanDB E2E round-trip pipeline.

.DESCRIPTION
End-to-end, unattended execution of the Phase 38.1 gap-closure pipeline:

  1. Stop both DW hosts (Swift-2.2, CleanDB) via taskkill on ports 54035 + 58217
  2. Detect or install sqlpackage.exe
  3. Restore Swift-2.2 from tools/swift2.2.0-20260129-database.zip via
     sqlpackage Import (drops + re-creates the DB)
  4. Reseed Administrator password to 'Administrator1' if token auth fails
  5. Apply cleanup scripts 01..09 in order against Swift-2.2
  6. Build + deploy the DynamicWeb.Serializer DLL to both hosts'
     bin/Debug/net10.0 directories
  7. Start Swift-2.2 host on https://localhost:54035 and wait for ready
  8. Purge CleanDB via tools/purge-cleandb.sql
  9. Apply tools/swift22-cleanup/cleandb-align-schema.sql
 10. Start CleanDB host on https://localhost:58217 and wait for ready
 11. POST SerializerSerialize?mode=deploy against Swift-2.2 (expect HTTP 200)
 12. POST SerializerSerialize?mode=seed against Swift-2.2 (expect HTTP 200)
 13. Mirror Swift-2.2 wwwroot/Files/System/Serializer/SerializeRoot to
     CleanDB's filesystem (per Phase 38.1-01 Deviation 1)
 14. POST SerializerDeserialize?mode=deploy against CleanDB (expect HTTP 200)
 15. POST SerializerDeserialize?mode=seed against CleanDB (expect HTTP 200)
 16. Run tools/smoke/Test-BaselineFrontend.ps1 (expect exit 0 AND non-vacuous)
 17. Assert EcomProducts row count: 2051 on Swift-2.2 AND 2051 on CleanDB
 18. Assert no baselines/Swift2.2/_sql/EcomShopGroupRelation/GROUP253$$SHOP19.yml
 19. Stop both hosts
 20. Emit pass/fail summary JSON

All steps write logs to a timestamped run directory under
.planning/phases/38.1-close-phase-38-deferrals/pipeline-runs/<yyyyMMdd-HHmmss>/.
Each step fails loudly (throw with message) and leaves evidence on disk.

Exit code:
  0 = full success (all 4 API calls HTTP 200, smoke non-vacuous,
      EcomProducts preserved, no orphan YAMLs)
  1 = any step failed (see run directory logs)
  2 = prerequisite missing (sqlpackage install failed, DLL build failed,
      host paths invalid)

.PARAMETER SqlServer
SQL Server instance. Default 'localhost\SQLEXPRESS'.

.PARAMETER SwiftDb
Swift-2.2 database name. Default 'Swift-2.2'.

.PARAMETER CleanDb
CleanDB database name. Default 'Swift-CleanDB'.

.PARAMETER SwiftHostPath
Absolute path to the Swift-2.2 DW host project root.
Default: C:\Projects\Solutions\swift.test.forsync\Swift2.2\Dynamicweb.Host.Suite

.PARAMETER CleanDbHostPath
Absolute path to the Swift-CleanDB DW host project root.
Default: C:\Projects\Solutions\swift.test.forsync\Swift.CleanDB\Dynamicweb.Host.Suite

.PARAMETER SwiftHostUrl
Public URL of the Swift-2.2 host. Default 'https://localhost:54035'.

.PARAMETER CleanDbHostUrl
Public URL of the CleanDB host. Default 'https://localhost:58217'.

.PARAMETER SkipBacpacRestore
For debugging only. When set, skip step 3 and assume Swift-2.2 is already
in the expected state.

.PARAMETER Mode
Pipeline mode. `Default` runs the canonical Phase 38.1 gap-closure pipeline.
`DeployThenTweakThenSeed` (Phase 39 D-15) additionally exercises Deploy -> tweak
-> Seed live acceptance: customer tweak preservation, Mail1SenderEmail fill via
XML-element merge, and D-09 idempotency (second Seed pass writes zero fields).

.PARAMETER TweakPageId
(DeployThenTweakThenSeed mode) Page.ID whose column is tweaked between Deploy
and the second Seed pass to prove destination-wins field-level merge.
Default: 9643 (Swift 2.2 Sign-in page).

.PARAMETER TweakColumnName
(DeployThenTweakThenSeed mode) Page column to tweak. Must be a column that
appears in the Seed YAML (else the acceptance is vacuous). Default: MenuText.

.PARAMETER TweakValue
(DeployThenTweakThenSeed mode) The sentinel string injected as the tweak.
Default: Phase39-Tweak-Sentinel.

.PARAMETER PaymentRowPaymentId
(DeployThenTweakThenSeed mode) EcomPayments.PaymentId whose
PaymentGatewayParameters XML is asserted to contain `Mail1SenderEmail` after
Seed. Default: PaymentCard (Swift 2.2 canonical payment method).

.EXAMPLE
pwsh tools/e2e/full-clean-roundtrip.ps1

.EXAMPLE
pwsh tools/e2e/full-clean-roundtrip.ps1 -SqlServer '.\SQLEXPRESS'

.EXAMPLE
pwsh tools/e2e/full-clean-roundtrip.ps1 -Mode DeployThenTweakThenSeed

.NOTES
Phase 38.1 Plan 03 Task 2 (D-38.1-19 / D-38.1-20 recipe codification).
Phase 39 Plan 03 Task 1 (D-15 live acceptance gate — DeployThenTweakThenSeed mode).
#>

[CmdletBinding()]
param(
    [string]$SqlServer       = 'localhost\SQLEXPRESS',
    [string]$SwiftDb         = 'Swift-2.2',
    [string]$CleanDb         = 'Swift-CleanDB',
    [string]$SwiftHostPath   = 'C:\Projects\Solutions\swift.test.forsync\Swift2.2\Dynamicweb.Host.Suite',
    [string]$CleanDbHostPath = 'C:\Projects\Solutions\swift.test.forsync\Swift.CleanDB\Dynamicweb.Host.Suite',
    [string]$SwiftHostUrl    = 'https://localhost:54035',
    [string]$CleanDbHostUrl  = 'https://localhost:58217',
    [switch]$SkipBacpacRestore,

    # Phase 39 D-15 acceptance mode. Default preserves Phase 38.1 behaviour.
    [ValidateSet('Default', 'DeployThenTweakThenSeed')]
    [string]$Mode = 'Default',

    # DeployThenTweakThenSeed knobs — identify the Page row + column to tweak
    # between Deploy and Seed to prove field-level destination-wins merge.
    [int]$TweakPageId           = 9643,
    [string]$TweakColumnName    = 'MenuText',
    [string]$TweakValue         = 'Phase39-Tweak-Sentinel',

    # D-15 Mail1SenderEmail acceptance — the EcomPayments row whose
    # PaymentGatewayParameters XML must contain Mail1SenderEmail post-Seed.
    [string]$PaymentRowPaymentId = 'PaymentCard'
)

$ErrorActionPreference = 'Stop'
$script:repoRoot  = (Get-Location).Path
$script:bacpacZip = Join-Path $script:repoRoot 'tools/swift2.2.0-20260129-database.zip'

# ============================================================================
# Run directory — all logs + evidence land here
# ============================================================================
$ts = (Get-Date -Format 'yyyyMMdd-HHmmss')
$runDir = Join-Path $script:repoRoot ".planning/phases/38.1-close-phase-38-deferrals/pipeline-runs/$ts"
New-Item -ItemType Directory -Force -Path $runDir | Out-Null
$script:runDir = $runDir

# ============================================================================
# Helper functions
# ============================================================================

function Write-Step {
    param([string]$Msg)
    Write-Host "`n=== $Msg ===" -ForegroundColor Cyan
    Add-Content -Path (Join-Path $script:runDir 'pipeline.log') -Value "[$((Get-Date).ToString('HH:mm:ss'))] $Msg"
}

function Write-Evidence {
    param([string]$Name, [string]$Content)
    $Content | Out-File -Encoding utf8 (Join-Path $script:runDir $Name)
}

function Stop-HostOnPort {
    param([int]$Port, [string]$Label)
    try {
        $conns = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
        if ($conns) {
            foreach ($c in $conns) {
                $procId = $c.OwningProcess
                if ($procId -and $procId -ne 0) {
                    Write-Host "  Stopping $Label PID $procId on port $Port"
                    try { & taskkill /F /PID $procId 2>&1 | Out-Null } catch {}
                }
            }
            Start-Sleep -Seconds 2
        } else {
            Write-Host "  Port $Port not listening — nothing to stop ($Label)"
        }
    } catch {
        Write-Host "  Warning: Could not query port $Port — $($_.Exception.Message)"
    }
}

function Resolve-SqlPackage {
    $candidates = @(
        "${env:USERPROFILE}\.dotnet\tools\sqlpackage.exe",
        "${env:ProgramFiles}\Microsoft SQL Server\170\DAC\bin\SqlPackage.exe",
        "${env:ProgramFiles}\Microsoft SQL Server\160\DAC\bin\SqlPackage.exe",
        "${env:ProgramFiles}\Microsoft SQL Server\150\DAC\bin\SqlPackage.exe",
        "${env:ProgramFiles}\Microsoft SQL Server\140\DAC\bin\SqlPackage.exe",
        "${env:ProgramFiles(x86)}\Microsoft SQL Server\160\DAC\bin\SqlPackage.exe",
        "${env:ProgramFiles(x86)}\Microsoft SQL Server\150\DAC\bin\SqlPackage.exe"
    )
    foreach ($c in $candidates) {
        if (Test-Path $c) {
            Write-Host "  Found sqlpackage at: $c"
            return $c
        }
    }

    Write-Host "  sqlpackage not found in standard locations — attempting dotnet tool install"
    $installLog = Join-Path $script:runDir 'sqlpackage-install.log'
    & dotnet tool install --global microsoft.sqlpackage *>&1 | Tee-Object -FilePath $installLog | Out-Host
    # Exit code 1 on dotnet tool install == "already installed" in some versions — treat as acceptable
    if ($LASTEXITCODE -ne 0 -and $LASTEXITCODE -ne 1) {
        throw "Failed to install sqlpackage via 'dotnet tool install --global microsoft.sqlpackage' (exit code $LASTEXITCODE). See $installLog. Install manually from https://learn.microsoft.com/sql/tools/sqlpackage/sqlpackage-download"
    }

    $candidate = "${env:USERPROFILE}\.dotnet\tools\sqlpackage.exe"
    if (-not (Test-Path $candidate)) {
        throw "sqlpackage still not present after install attempt at $candidate. Check $installLog"
    }
    # Ensure the dotnet tools dir is on PATH for this session
    if ($env:PATH -notlike "*${env:USERPROFILE}\.dotnet\tools*") {
        $env:PATH = "${env:USERPROFILE}\.dotnet\tools;$env:PATH"
    }
    Write-Host "  Installed sqlpackage at: $candidate"
    return $candidate
}

function Invoke-Sqlcmd-File {
    param(
        [string]$Server,
        [string]$Database,
        [string]$ScriptPath,
        [string]$LogPath
    )
    & sqlcmd -S $Server -E -d $Database -b -i $ScriptPath *>&1 | Tee-Object -FilePath $LogPath | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "sqlcmd failed on '$ScriptPath' (exit $LASTEXITCODE). See $LogPath"
    }
    if (Select-String -Path $LogPath -Pattern 'ABORT|ROLLBACK\b' -Quiet) {
        throw "Script '$ScriptPath' hit an ABORT / ROLLBACK path — see $LogPath"
    }
}

function Invoke-Sqlcmd-Scalar {
    param(
        [string]$Server,
        [string]$Database,
        [string]$Query
    )
    $raw = & sqlcmd -S $Server -E -d $Database -h -1 -W -Q "SET NOCOUNT ON; $Query" 2>&1
    $line = ($raw | Where-Object { $_ -match '^\s*-?\d+\s*$' } | Select-Object -First 1)
    if (-not $line) {
        throw "sqlcmd query returned no numeric scalar. Query: $Query. Raw output: $($raw -join ' | ')"
    }
    return [int]$line.Trim()
}

function Invoke-Sqlcmd-StringScalar {
    <#
    .SYNOPSIS
    Phase 39 D-15 helper — returns the first non-blank line of a sqlcmd result
    as a string (no numeric cast). Used for XML-column reads
    (PaymentGatewayParameters) and tweak-preservation assertions (Page.MenuText).

    .DESCRIPTION
    Mirrors Invoke-Sqlcmd-Scalar but returns text. Uses:
      -h -1            suppress headers
      -W               trim trailing whitespace
      -s '|'           one column expected — bar separator is safe
      -y 0 -Y 0        unlimited variable-width column length
    Caller-supplied MaxCharsReturned (default 100000) guards against pathological
    payloads bloating the pipeline log.
    #>
    param(
        [Parameter(Mandatory=$true)][string]$Server,
        [Parameter(Mandatory=$true)][string]$Database,
        [Parameter(Mandatory=$true)][string]$Query,
        [int]$MaxCharsReturned = 100000
    )
    $raw = & sqlcmd -S $Server -E -d $Database -h -1 -W -s '|' -y 0 -Y 0 -Q "SET NOCOUNT ON; $Query" 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "sqlcmd failed on $Database (exit $LASTEXITCODE). Query: $Query. Raw: $($raw -join ' | ')"
    }
    $nonBlank = $raw | Where-Object { $_ -and $_.ToString().Trim().Length -gt 0 }
    $line = $nonBlank | Select-Object -First 1
    if (-not $line) { return $null }
    $s = $line.ToString().Trim()
    if ($s.Length -gt $MaxCharsReturned) {
        $s = $s.Substring(0, $MaxCharsReturned)
    }
    return $s
}

function Start-DwHost {
    param(
        [string]$ProjectDir,
        [string]$Url,
        [string]$LogPath,
        [string]$Label
    )
    if (-not (Test-Path $ProjectDir)) {
        throw "Host project dir not found: $ProjectDir ($Label)"
    }
    $env:ASPNETCORE_URLS = $Url
    # Use --no-build only if bin dir already populated; fall back to with-build if missing
    $binPath = Join-Path $ProjectDir 'bin/Debug/net10.0'
    $useNoBuild = Test-Path $binPath
    $args = @('run', '--project', $ProjectDir, '-c', 'Debug')
    if ($useNoBuild) { $args += '--no-build' }
    Write-Host "  Starting $Label host: dotnet $($args -join ' ')"
    $proc = Start-Process -FilePath 'dotnet' -ArgumentList $args `
        -PassThru -WindowStyle Hidden `
        -RedirectStandardOutput $LogPath `
        -RedirectStandardError "$LogPath.err"
    return $proc
}

function Wait-DwReady {
    param(
        [string]$Url,
        [int]$TimeoutSec = 180,
        [string]$Label
    )
    $start = Get-Date
    while (((Get-Date) - $start).TotalSeconds -lt $TimeoutSec) {
        try {
            $r = Invoke-WebRequest -Uri "$Url/Admin/" -SkipCertificateCheck -TimeoutSec 5 -MaximumRedirection 0 -ErrorAction Stop
            if ($r.StatusCode -in 200, 301, 302) { Write-Host "  $Label ready (HTTP $($r.StatusCode))"; return }
        } catch {
            $code = 0
            try { $code = [int]$_.Exception.Response.StatusCode } catch { }
            if ($code -in 200, 301, 302, 401) { Write-Host "  $Label ready (HTTP $code)"; return }
        }
        Start-Sleep -Seconds 2
    }
    throw "Host at $Url ($Label) did not respond within ${TimeoutSec}s"
}

function Get-DwToken {
    param(
        [string]$HostUrl,
        [string]$Username = 'Administrator',
        [string]$Password = 'Administrator1'
    )
    $body = @{ Username = $Username; Password = $Password } | ConvertTo-Json
    $resp = Invoke-WebRequest -Uri "$HostUrl/Admin/TokenAuthentication/authenticate" `
        -Method POST -ContentType 'application/json' -Body $body `
        -SkipCertificateCheck -ErrorAction Stop
    $json = $resp.Content | ConvertFrom-Json
    if (-not $json.Token) { throw "Token auth against $HostUrl returned no Token in body" }
    return $json.Token
}

function Invoke-DwApi {
    param(
        [string]$HostUrl,
        [string]$Endpoint,
        [string]$LogPath
    )
    $token = Get-DwToken -HostUrl $HostUrl
    $hdr = @{ Authorization = "Bearer $token" }
    try {
        $resp = Invoke-WebRequest -Uri "$HostUrl$Endpoint" -Method POST -Headers $hdr `
            -SkipCertificateCheck -TimeoutSec 600 -ErrorAction Stop
        $code = [int]$resp.StatusCode
        "HTTP $code`n$($resp.Content)" | Out-File -Encoding utf8 $LogPath
        return @{ Code = $code; Body = $resp.Content }
    } catch {
        $code = 0
        $body = ""
        try { $code = [int]$_.Exception.Response.StatusCode } catch { }
        try {
            $stream = $_.Exception.Response.GetResponseStream()
            if ($stream) {
                $reader = New-Object System.IO.StreamReader($stream)
                $body = $reader.ReadToEnd()
            }
        } catch { }
        "HTTP $code`n$body" | Out-File -Encoding utf8 $LogPath
        return @{ Code = $code; Body = $body }
    }
}

function Try-DwToken {
    param([string]$HostUrl)
    try {
        $t = Get-DwToken -HostUrl $HostUrl
        if ($t) { return @{ Ok = $true; Code = 200 } }
        return @{ Ok = $false; Code = 0 }
    } catch {
        $code = 0
        try { $code = [int]$_.Exception.Response.StatusCode } catch { }
        return @{ Ok = $false; Code = $code; Error = $_.Exception.Message }
    }
}

# ============================================================================
# Main pipeline
# ============================================================================

Write-Step "Run directory: $runDir"
Write-Step "Pipeline start — $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"

$pipelineStartUtc = (Get-Date).ToUniversalTime()

# ----- Step 1: Stop both hosts ------------------------------------------------
Write-Step 'Step 1: Stop any running DW hosts (ports 54035 + 58217)'
Stop-HostOnPort -Port 54035 -Label 'Swift-2.2'
Stop-HostOnPort -Port 58217 -Label 'CleanDB'

# ----- Step 2: Detect/install sqlpackage --------------------------------------
Write-Step 'Step 2: Detect/install sqlpackage.exe'
$sqlpackage = Resolve-SqlPackage

# ----- Step 3: Bacpac restore -------------------------------------------------
Write-Step 'Step 3: Restore Swift-2.2 from bacpac'
if ($SkipBacpacRestore) {
    Write-Host '  -SkipBacpacRestore set — skipping bacpac restore. Assuming Swift-2.2 already matches expected state.'
} else {
    if (-not (Test-Path $script:bacpacZip)) {
        throw "Bacpac zip not found at $script:bacpacZip — cannot restore Swift-2.2"
    }
    $bacpacTmpDir = Join-Path $runDir 'bacpac'
    New-Item -ItemType Directory -Force -Path $bacpacTmpDir | Out-Null
    Write-Host "  Unzipping $script:bacpacZip -> $bacpacTmpDir"
    Expand-Archive -LiteralPath $script:bacpacZip -DestinationPath $bacpacTmpDir -Force

    # Find the .bacpac file inside the extracted tree (support nested layouts)
    $bacpac = Get-ChildItem -Path $bacpacTmpDir -Filter '*.bacpac' -Recurse | Select-Object -First 1
    if (-not $bacpac) {
        throw "No .bacpac file found inside $script:bacpacZip after Expand-Archive"
    }
    Write-Host "  Bacpac file: $($bacpac.FullName)"

    # Drop existing DB (if any) via sqlcmd master
    $dropSql = "IF DB_ID('$SwiftDb') IS NOT NULL BEGIN ALTER DATABASE [$SwiftDb] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$SwiftDb]; END"
    Write-Host "  Dropping existing database [$SwiftDb] (if any)"
    & sqlcmd -S $SqlServer -E -d master -b -Q $dropSql *>&1 | Tee-Object -FilePath (Join-Path $runDir 'bacpac-drop.log') | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "sqlcmd drop of [$SwiftDb] failed (exit $LASTEXITCODE). See $runDir/bacpac-drop.log"
    }

    # Import via sqlpackage
    Write-Host "  sqlpackage /Action:Import -> [$SwiftDb]"
    $importLog = Join-Path $runDir 'bacpac-import.log'
    & $sqlpackage /Action:Import /SourceFile:$($bacpac.FullName) `
        /TargetConnectionString:"Server=$SqlServer;Database=$SwiftDb;Integrated Security=true;TrustServerCertificate=true" `
        *>&1 | Tee-Object -FilePath $importLog | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "sqlpackage Import failed (exit $LASTEXITCODE). See $importLog"
    }
}

# ----- Step 4: Reseed Administrator password (conditional) --------------------
# The bacpac ships with Administrator credentials. If token auth fails post-restore,
# we halt and point the operator at tools/e2e/reseed-admin.sql (a documented
# fallback path). The README explains manual reseed + re-run with -SkipBacpacRestore.
#
# NOTE: In principle the pipeline could PBKDF2-compute the DW password hash and
# UPDATE AccessUser directly — but the exact DW hash format (iterations, column
# names, salt shape) is version-specific and brittle. The defensive default is:
# attempt token auth after the Swift-2.2 host boots in Step 7, and if it fails
# with 401, surface a clear error with the SQL fallback path.
Write-Step 'Step 4: Defer Administrator password check until host is up (see Step 7 readiness)'

# ----- Step 5: Apply cleanup scripts 01..09 -----------------------------------
Write-Step 'Step 5: Apply cleanup scripts 01..09 against Swift-2.2'
$scripts = @(
    '00-backup.sql',
    '01-null-orphan-page-refs.sql',
    '02-delete-test-page.sql',
    '03-delete-orphan-areas.sql',
    '04-delete-soft-deleted-pages.sql',
    '05-null-stale-template-refs.sql',
    '06-delete-orphan-ecomshopgrouprelation.sql',
    '07-delete-stale-email-gridrows.sql',
    '08-null-orphan-page-link-refs.sql',
    '09-fix-misconfigured-property-pages.sql'
)
foreach ($s in $scripts) {
    $scriptPath = Join-Path $script:repoRoot "tools/swift22-cleanup/$s"
    if (-not (Test-Path $scriptPath)) {
        throw "Cleanup script missing: $scriptPath"
    }
    $logName = "cleanup-$($s -replace '\.sql$', '.log')"
    $logFile = Join-Path $script:runDir $logName
    Write-Host "  Running $s"
    Invoke-Sqlcmd-File -Server $SqlServer -Database $SwiftDb -ScriptPath $scriptPath -LogPath $logFile
}

# ----- Step 6: Build + deploy DLL ---------------------------------------------
Write-Step 'Step 6: Build + deploy DynamicWeb.Serializer DLL'
$dllSourceDir = Join-Path $script:repoRoot 'src/DynamicWeb.Serializer/bin/Debug/net8.0'
$dllSource    = Join-Path $dllSourceDir 'DynamicWeb.Serializer.dll'

Write-Host '  dotnet build src/DynamicWeb.Serializer'
$buildLog = Join-Path $runDir 'dotnet-build.log'
& dotnet build (Join-Path $script:repoRoot 'src/DynamicWeb.Serializer/DynamicWeb.Serializer.csproj') -c Debug *>&1 | Tee-Object -FilePath $buildLog | Out-Host
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed (exit $LASTEXITCODE). See $buildLog"
}
if (-not (Test-Path $dllSource)) {
    throw "Serializer DLL not found at $dllSource after build"
}

$swiftDllDir  = Join-Path $SwiftHostPath   'bin/Debug/net10.0'
$cleanDllDir  = Join-Path $CleanDbHostPath 'bin/Debug/net10.0'
foreach ($d in @($swiftDllDir, $cleanDllDir)) {
    if (-not (Test-Path $d)) {
        throw "Host bin dir does not exist: $d. Hosts target net10.0 — build hosts first or verify -SwiftHostPath / -CleanDbHostPath"
    }
}

$swiftDllPath = Join-Path $swiftDllDir 'DynamicWeb.Serializer.dll'
$cleanDllPath = Join-Path $cleanDllDir 'DynamicWeb.Serializer.dll'
Copy-Item -Force $dllSource $swiftDllPath
Copy-Item -Force $dllSource $cleanDllPath

$srcMd5 = (Get-FileHash -Algorithm MD5 $dllSource).Hash
$swMd5  = (Get-FileHash -Algorithm MD5 $swiftDllPath).Hash
$cdMd5  = (Get-FileHash -Algorithm MD5 $cleanDllPath).Hash
if ($srcMd5 -ne $swMd5 -or $srcMd5 -ne $cdMd5) {
    throw "DLL md5 mismatch after copy: src=$srcMd5 swift=$swMd5 clean=$cdMd5"
}
Write-Host "  DLL md5 verified on both hosts: $srcMd5"
Write-Evidence -Name 'dll-md5.txt' -Content "src=$srcMd5`nswift=$swMd5`nclean=$cdMd5"

# ----- Step 7: Start Swift-2.2 host -------------------------------------------
Write-Step 'Step 7: Start Swift-2.2 host + wait for ready'
$swiftHostLog = Join-Path $runDir 'host-swift22.log'
$swiftProc = Start-DwHost -ProjectDir $SwiftHostPath -Url $SwiftHostUrl -LogPath $swiftHostLog -Label 'Swift-2.2'
try {
    Wait-DwReady -Url $SwiftHostUrl -TimeoutSec 180 -Label 'Swift-2.2'
} catch {
    throw "Swift-2.2 host failed to start at $SwiftHostUrl within 180s. See $swiftHostLog"
}

# Administrator password check (Step 4 deferred verification)
$tokenCheck = Try-DwToken -HostUrl $SwiftHostUrl
if (-not $tokenCheck.Ok) {
    if ($tokenCheck.Code -eq 401) {
        throw "Administrator password is not 'Administrator1' on [$SwiftDb] after bacpac restore. Run tools/e2e/reseed-admin.sql against [$SwiftDb] in SSMS (see tools/e2e/README.md §Fallback), then re-run this pipeline with -SkipBacpacRestore."
    } else {
        throw "Swift-2.2 token endpoint returned unexpected status $($tokenCheck.Code) — $($tokenCheck.Error)"
    }
}
Write-Host '  Administrator token auth OK on Swift-2.2'

# ----- Step 8: Purge CleanDB --------------------------------------------------
Write-Step 'Step 8: Purge CleanDB'
$purgeScript = Join-Path $script:repoRoot 'tools/purge-cleandb.sql'
if (-not (Test-Path $purgeScript)) {
    throw "Purge script not found: $purgeScript"
}
$purgeLog = Join-Path $runDir 'purge-cleandb.log'
Invoke-Sqlcmd-File -Server $SqlServer -Database $CleanDb -ScriptPath $purgeScript -LogPath $purgeLog

# ----- Step 9: Apply cleandb-align-schema.sql ---------------------------------
Write-Step 'Step 9: Apply cleandb-align-schema.sql (10 idempotent ALTER statements)'
$alignScript = Join-Path $script:repoRoot 'tools/swift22-cleanup/cleandb-align-schema.sql'
if (-not (Test-Path $alignScript)) {
    throw "Schema-align script not found: $alignScript"
}
$alignLog = Join-Path $runDir 'schema-align.log'
Invoke-Sqlcmd-File -Server $SqlServer -Database $CleanDb -ScriptPath $alignScript -LogPath $alignLog

# ----- Step 10: Start CleanDB host --------------------------------------------
Write-Step 'Step 10: Start CleanDB host + wait for ready'
$cleanHostLog = Join-Path $runDir 'host-cleandb.log'
$cleanProc = Start-DwHost -ProjectDir $CleanDbHostPath -Url $CleanDbHostUrl -LogPath $cleanHostLog -Label 'CleanDB'
try {
    Wait-DwReady -Url $CleanDbHostUrl -TimeoutSec 180 -Label 'CleanDB'
} catch {
    throw "CleanDB host failed to start at $CleanDbHostUrl within 180s. See $cleanHostLog"
}

# ----- Steps 11-12: Serialize Deploy + Seed against Swift-2.2 -----------------
Write-Step 'Steps 11-12: Serialize Deploy + Seed (Swift-2.2 -> YAML)'
$serDeploy = Invoke-DwApi -HostUrl $SwiftHostUrl -Endpoint '/Admin/Api/SerializerSerialize?mode=deploy' -LogPath (Join-Path $script:runDir 'serialize-deploy.log')
if ($serDeploy.Code -ne 200) {
    throw "Serialize Deploy: expected HTTP 200, got $($serDeploy.Code). See serialize-deploy.log"
}
if (Select-String -Path (Join-Path $script:runDir 'serialize-deploy.log') -Pattern 'escalated|CumulativeStrictModeException' -Quiet) {
    throw "Serialize Deploy emitted strict-mode escalations. See serialize-deploy.log"
}
Write-Host '  Serialize Deploy HTTP 200 OK'

$serSeed = Invoke-DwApi -HostUrl $SwiftHostUrl -Endpoint '/Admin/Api/SerializerSerialize?mode=seed' -LogPath (Join-Path $script:runDir 'serialize-seed.log')
if ($serSeed.Code -ne 200) {
    throw "Serialize Seed: expected HTTP 200, got $($serSeed.Code). See serialize-seed.log"
}
if (Select-String -Path (Join-Path $script:runDir 'serialize-seed.log') -Pattern 'escalated|CumulativeStrictModeException' -Quiet) {
    throw "Serialize Seed emitted strict-mode escalations. See serialize-seed.log"
}
Write-Host '  Serialize Seed HTTP 200 OK'

# ----- Step 13: Cross-host SerializeRoot mirror -------------------------------
Write-Step 'Step 13: Mirror Swift-2.2 SerializeRoot -> CleanDB'
$swSerRoot = Join-Path $SwiftHostPath   'wwwroot/Files/System/Serializer/SerializeRoot'
$cdSerRoot = Join-Path $CleanDbHostPath 'wwwroot/Files/System/Serializer/SerializeRoot'
if (-not (Test-Path $swSerRoot)) {
    throw "Source SerializeRoot not found after serialize: $swSerRoot"
}
New-Item -ItemType Directory -Force -Path $cdSerRoot | Out-Null
# Remove prior stale mirror (specific deploy/seed subdirs only — never blanket delete)
$cdDeploy = Join-Path $cdSerRoot 'deploy'
$cdSeed   = Join-Path $cdSerRoot 'seed'
if (Test-Path $cdDeploy) { Remove-Item -Recurse -Force $cdDeploy }
if (Test-Path $cdSeed)   { Remove-Item -Recurse -Force $cdSeed }
Copy-Item -Recurse -Force (Join-Path $swSerRoot 'deploy') $cdSerRoot
Copy-Item -Recurse -Force (Join-Path $swSerRoot 'seed')   $cdSerRoot
Write-Host "  Mirrored $swSerRoot -> $cdSerRoot"

# ----- Steps 14-15: Deserialize Deploy + Seed against CleanDB -----------------
Write-Step 'Steps 14-15: Deserialize Deploy + Seed (YAML -> CleanDB)'
$desDeploy = Invoke-DwApi -HostUrl $CleanDbHostUrl -Endpoint '/Admin/Api/SerializerDeserialize?mode=deploy' -LogPath (Join-Path $script:runDir 'deserialize-deploy.log')
if ($desDeploy.Code -ne 200) {
    throw "Deserialize Deploy: expected HTTP 200, got $($desDeploy.Code). See deserialize-deploy.log"
}
if (Select-String -Path (Join-Path $script:runDir 'deserialize-deploy.log') -Pattern 'escalated|CumulativeStrictModeException' -Quiet) {
    throw "Deserialize Deploy emitted strict-mode escalations. See deserialize-deploy.log"
}
Write-Host '  Deserialize Deploy HTTP 200 OK'

$desSeed = Invoke-DwApi -HostUrl $CleanDbHostUrl -Endpoint '/Admin/Api/SerializerDeserialize?mode=seed' -LogPath (Join-Path $script:runDir 'deserialize-seed.log')
if ($desSeed.Code -ne 200) {
    throw "Deserialize Seed: expected HTTP 200, got $($desSeed.Code). See deserialize-seed.log"
}
if (Select-String -Path (Join-Path $script:runDir 'deserialize-seed.log') -Pattern 'escalated|CumulativeStrictModeException' -Quiet) {
    throw "Deserialize Seed emitted strict-mode escalations. See deserialize-seed.log"
}
Write-Host '  Deserialize Seed HTTP 200 OK'

# ----- Step 15-ext: Phase 39 D-15 DeployThenTweakThenSeed sub-pipeline -------
# Runs only when -Mode DeployThenTweakThenSeed. Proves Deploy -> customer tweak
# -> Seed preserves the tweak byte-for-byte (D-15 acceptance), that
# Mail1SenderEmail is present in EcomPayments.PaymentGatewayParameters via the
# XML-element merge layer (D-15 + D-21/D-22/D-23), and that a second Seed pass
# writes zero fields (D-09 idempotency). No changes to Default-mode flow.
if ($Mode -eq 'DeployThenTweakThenSeed') {
    Write-Step 'Phase 39 D-15: Deploy -> Tweak -> Seed sub-pipeline'

    # Step 15.0 — Preflight: capture baseline state for assertions
    Write-Host '  [15.0] Preflight: capturing pre-tweak state'
    $preTweakValue = Invoke-Sqlcmd-StringScalar `
        -Server $SqlServer -Database $CleanDb `
        -Query "SELECT [$TweakColumnName] FROM Page WHERE ID=$TweakPageId"
    Write-Host "    Pre-tweak [$TweakColumnName]@Page.$TweakPageId = '$preTweakValue'"

    # Step 15.1 — Inject customer tweak between Deploy and second Seed pass.
    # Single-quote the sentinel; no user-controlled input flows into the UPDATE.
    Write-Host '  [15.1] Injecting customer tweak'
    $tweakSql = "SET NOCOUNT ON; UPDATE Page SET [$TweakColumnName] = N'$TweakValue' WHERE ID = $TweakPageId;"
    & sqlcmd -S $SqlServer -E -d $CleanDb -b -Q $tweakSql 2>&1 | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Failed to inject customer tweak (sqlcmd exit $LASTEXITCODE)" }

    $postTweakValue = Invoke-Sqlcmd-StringScalar `
        -Server $SqlServer -Database $CleanDb `
        -Query "SELECT [$TweakColumnName] FROM Page WHERE ID=$TweakPageId"
    if ($postTweakValue -ne $TweakValue) {
        throw "Tweak injection failed — expected '$TweakValue', got '$postTweakValue'"
    }
    Write-Host "    Post-tweak [$TweakColumnName]@Page.$TweakPageId = '$postTweakValue'"

    # Step 15.2 — Deserialize Seed (first pass, post-tweak).
    Write-Host '  [15.2] Deserialize Seed (first pass, post-tweak)'
    $seedPass1Log = Join-Path $script:runDir 'deserialize-seed-pass1.log'
    $desSeed1 = Invoke-DwApi -HostUrl $CleanDbHostUrl `
        -Endpoint '/Admin/Api/SerializerDeserialize?mode=seed' `
        -LogPath $seedPass1Log
    if ($desSeed1.Code -ne 200) {
        throw "Deserialize Seed pass 1: expected HTTP 200, got $($desSeed1.Code). See deserialize-seed-pass1.log"
    }
    if (Select-String -Path $seedPass1Log -Pattern 'escalated|CumulativeStrictModeException' -Quiet) {
        throw "Deserialize Seed pass 1 emitted strict-mode escalations. See deserialize-seed-pass1.log"
    }
    # D-11 log format regression guard — Seed-skip: is from the superseded
    # Phase 37-01 row-level skip. Its reappearance means Phase 39 field-level
    # merge did not engage.
    if (Select-String -Path $seedPass1Log -Pattern 'Seed-skip:' -Quiet) {
        throw "Deserialize Seed pass 1 emitted 'Seed-skip:' lines — Phase 39 D-11 regression. See deserialize-seed-pass1.log"
    }
    if (-not (Select-String -Path $seedPass1Log -Pattern 'Seed-merge:' -Quiet)) {
        throw "Deserialize Seed pass 1 emitted NO 'Seed-merge:' lines — merge branch not firing. See deserialize-seed-pass1.log"
    }
    Write-Host '    Deserialize Seed pass 1 HTTP 200 OK (Seed-merge: present, Seed-skip: absent)'

    # Step 15.3 — Assert customer tweak preserved byte-for-byte.
    Write-Host '  [15.3] Verifying customer tweak preserved'
    $postSeed1Tweak = Invoke-Sqlcmd-StringScalar `
        -Server $SqlServer -Database $CleanDb `
        -Query "SELECT [$TweakColumnName] FROM Page WHERE ID=$TweakPageId"
    if ($postSeed1Tweak -ne $TweakValue) {
        throw "Customer tweak NOT preserved — expected '$TweakValue', got '$postSeed1Tweak'. Seed merge branch overwrote a set field (D-15 acceptance failed)."
    }
    Write-Host "    [PASS] Customer tweak preserved: '$postSeed1Tweak'"

    # Step 15.4 — Assert Mail1SenderEmail present in EcomPayments XML.
    Write-Host '  [15.4] Verifying Mail1SenderEmail filled in EcomPayments XML'
    $paymentXml = Invoke-Sqlcmd-StringScalar `
        -Server $SqlServer -Database $CleanDb `
        -Query "SELECT CAST(PaymentGatewayParameters AS NVARCHAR(MAX)) FROM EcomPayments WHERE PaymentID = N'$PaymentRowPaymentId'"
    if (-not $paymentXml) {
        throw "EcomPayments row '$PaymentRowPaymentId' not found on CleanDB (or PaymentGatewayParameters is NULL)."
    }
    if ($paymentXml -notmatch 'Mail1SenderEmail') {
        throw "Mail1SenderEmail NOT present in EcomPayments.PaymentGatewayParameters after Seed (D-15 acceptance failed). XML snippet: $($paymentXml.Substring(0, [Math]::Min(400, $paymentXml.Length)))"
    }
    Write-Host '    [PASS] Mail1SenderEmail present in PaymentGatewayParameters'

    # Step 15.5 — Second Seed pass: D-09 idempotency (every Seed-merge line
    # should report 0 filled).
    Write-Host '  [15.5] Deserialize Seed (second pass — idempotency check)'
    $seedPass2Log = Join-Path $script:runDir 'deserialize-seed-pass2.log'
    $desSeed2 = Invoke-DwApi -HostUrl $CleanDbHostUrl `
        -Endpoint '/Admin/Api/SerializerDeserialize?mode=seed' `
        -LogPath $seedPass2Log
    if ($desSeed2.Code -ne 200) {
        throw "Deserialize Seed pass 2: expected HTTP 200, got $($desSeed2.Code). See deserialize-seed-pass2.log"
    }
    if (Select-String -Path $seedPass2Log -Pattern 'escalated|CumulativeStrictModeException' -Quiet) {
        throw "Deserialize Seed pass 2 emitted strict-mode escalations. See deserialize-seed-pass2.log"
    }
    # Every Seed-merge line in pass 2 should report 0 filled. A non-zero fill
    # means the merge branch wrote new fields on an already-seeded target —
    # that is a D-09 idempotency violation.
    $pass2Lines = Get-Content $seedPass2Log
    $nonZeroFills = $pass2Lines | Where-Object {
        $_ -match 'Seed-merge:.*?-\s*(\d+)\s+filled' -and [int]$Matches[1] -gt 0
    }
    if ($nonZeroFills) {
        Write-Host '    D-09 idempotency FAILED — pass 2 Seed-merge lines with non-zero fills:'
        $nonZeroFills | ForEach-Object { Write-Host "      $_" }
        throw "D-09 idempotency failed — second Seed pass wrote new fields. See deserialize-seed-pass2.log"
    }
    Write-Host '    [PASS] D-09 idempotency — second Seed pass wrote zero fields'

    # Step 15.6 — Final tweak re-check after the idempotency pass.
    Write-Host '  [15.6] Final tweak re-check after second Seed pass'
    $finalTweak = Invoke-Sqlcmd-StringScalar `
        -Server $SqlServer -Database $CleanDb `
        -Query "SELECT [$TweakColumnName] FROM Page WHERE ID=$TweakPageId"
    if ($finalTweak -ne $TweakValue) {
        throw "After second Seed pass, tweak value lost — expected '$TweakValue', got '$finalTweak'"
    }
    Write-Host "    [PASS] Customer tweak still preserved after second Seed pass: '$finalTweak'"

    Write-Host ''
    Write-Host '  [ALL PASS] Phase 39 D-15 live acceptance gate closed.'
    Write-Host '    - Customer tweak preserved byte-for-byte across Seed'
    Write-Host '    - Mail1SenderEmail present in EcomPayments.PaymentGatewayParameters'
    Write-Host '    - D-09 idempotency — second Seed pass wrote zero fields'
}

# ----- Step 16: Smoke tool ----------------------------------------------------
Write-Step 'Step 16: Frontend smoke tool'
$smokeLog = Join-Path $script:runDir 'smoke.log'
$smokeScript = Join-Path $script:repoRoot 'tools/smoke/Test-BaselineFrontend.ps1'
& pwsh -NoProfile -File $smokeScript `
    -HostUrl $CleanDbHostUrl -AreaId 3 -LangPrefix '/en-us' `
    -SqlServer $SqlServer -SqlDatabase $CleanDb *>&1 | Tee-Object -FilePath $smokeLog | Out-Host
$smokeExit = $LASTEXITCODE
if ($smokeExit -ne 0) {
    throw "Smoke tool exited $smokeExit — see $smokeLog"
}
if (Select-String -Path $smokeLog -Pattern 'Nothing to test' -Quiet) {
    throw "Smoke tool ran but reported 'Nothing to test' (vacuous pass) — see $smokeLog"
}
Write-Host '  Smoke tool exit 0 AND non-vacuous'

# ----- Step 17: EcomProducts count assertion ----------------------------------
Write-Step 'Step 17: EcomProducts count assertion (2051 == 2051)'
$srcCount = Invoke-Sqlcmd-Scalar -Server $SqlServer -Database $SwiftDb -Query 'SELECT COUNT(*) FROM EcomProducts'
$tgtCount = Invoke-Sqlcmd-Scalar -Server $SqlServer -Database $CleanDb -Query 'SELECT COUNT(*) FROM EcomProducts'
Write-Host "  Swift-2.2 EcomProducts: $srcCount"
Write-Host "  CleanDB   EcomProducts: $tgtCount"
if ($srcCount -ne 2051) {
    throw "Swift-2.2 EcomProducts expected 2051, got $srcCount (source cleanup scripts may have over-deleted)"
}
if ($tgtCount -ne 2051) {
    throw "CleanDB EcomProducts expected 2051, got $tgtCount (C.1 preservation violated — deserialize dropped rows)"
}

# ----- Step 18: SHOP19 YAML should not exist ----------------------------------
Write-Step 'Step 18: Orphan YAML absence assertion (SHOP19)'
$orphanYaml = Join-Path $script:repoRoot 'baselines/Swift2.2/_sql/EcomShopGroupRelation/GROUP253$$SHOP19.yml'
if (Test-Path $orphanYaml) {
    throw "SHOP19 orphan YAML still present at $orphanYaml — cleanup script 06 did not take effect, or baselines/ is stale"
}
Write-Host '  SHOP19 YAML absent — OK'

# ----- Step 19: Stop both hosts ----------------------------------------------
Write-Step 'Step 19: Stop both DW hosts'
Stop-HostOnPort -Port 54035 -Label 'Swift-2.2'
Stop-HostOnPort -Port 58217 -Label 'CleanDB'

# ----- Step 20: Summary -------------------------------------------------------
Write-Step 'PIPELINE PASSED — all gates met'
$pipelineEndUtc = (Get-Date).ToUniversalTime()
$duration = ($pipelineEndUtc - $pipelineStartUtc).TotalSeconds

$summary = @{
    Disposition = 'CLOSED'
    StartUtc    = $pipelineStartUtc.ToString('o')
    EndUtc      = $pipelineEndUtc.ToString('o')
    DurationSec = [int]$duration
    HttpCodes = @{
        SerializeDeploy   = $serDeploy.Code
        SerializeSeed     = $serSeed.Code
        DeserializeDeploy = $desDeploy.Code
        DeserializeSeed   = $desSeed.Code
    }
    EcomProducts = @{ Src = $srcCount; Tgt = $tgtCount }
    SmokeExit    = $smokeExit
    DllMd5       = $srcMd5
    RunDir       = $runDir
}
$summary | ConvertTo-Json -Depth 4 | Tee-Object -FilePath (Join-Path $runDir 'summary.json') | Out-Host
exit 0
