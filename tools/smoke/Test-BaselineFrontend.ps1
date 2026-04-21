<#
.SYNOPSIS
Post-deserialize frontend smoke test for a DynamicWeb Swift baseline deploy.

.DESCRIPTION
Enumerates every PageActive = 1 page under the target area in the DW database,
hits the public Friendly URL for each, buckets responses into 2xx/3xx/4xx/5xx,
and reports. Exits 1 on any 5xx or transport error, 0 otherwise. Exits 2 on
page-enumeration failure (SQL module missing, connection refused, etc.).

Phase 38 D-38-13. LOCAL-DEV ONLY - never deploy to customer sites.

.PARAMETER HostUrl
Public hostname of the DW target. Default https://localhost:58217 (CleanDB).

.PARAMETER AreaId
The PageAreaID to enumerate. Default 3 (Swift 2 area on CleanDB).

.PARAMETER LangPrefix
Friendly URL language prefix. Default /en-us. Use empty string ('') to disable.

.PARAMETER SqlServer
SQL Server instance hosting the DW database. Default 'localhost\SQLEXPRESS'.

.PARAMETER SqlDatabase
Name of the DW database. Default 'Swift-CleanDB'.

.PARAMETER SqlUser
Optional SQL Server login name. When blank, Integrated Security (current
Windows identity) is used.

.PARAMETER SqlPassword
Password paired with -SqlUser. Ignored when -SqlUser is blank.

.EXAMPLE
pwsh tools/smoke/Test-BaselineFrontend.ps1

.EXAMPLE
pwsh tools/smoke/Test-BaselineFrontend.ps1 -HostUrl https://myhost:58217 -AreaId 3 -LangPrefix /en-us

.EXAMPLE
pwsh tools/smoke/Test-BaselineFrontend.ps1 -SqlServer db.local -SqlDatabase MyDb -SqlUser sa -SqlPassword 'secret'
#>
param(
    [string]$HostUrl = 'https://localhost:58217',
    [int]$AreaId = 3,
    [string]$LangPrefix = '/en-us',
    [string]$SqlServer = 'localhost\SQLEXPRESS',
    [string]$SqlDatabase = 'Swift-CleanDB',
    [string]$SqlUser = '',
    [string]$SqlPassword = ''
)

Write-Host "=== DynamicWeb.Serializer Frontend Smoke Test ===" -ForegroundColor Cyan
Write-Host "Host:       $HostUrl"
Write-Host "AreaID:     $AreaId"
Write-Host "LangPrefix: $LangPrefix"
Write-Host "SqlServer:  $SqlServer"
Write-Host "Database:   $SqlDatabase"
if ([string]::IsNullOrEmpty($SqlUser)) {
    Write-Host "Auth:       Integrated Security"
} else {
    Write-Host "Auth:       SQL login '$SqlUser'"
}
Write-Host ""

# --- Build connection string -------------------------------------------------
if ([string]::IsNullOrEmpty($SqlUser)) {
    $connectionString = "Server=$SqlServer;Database=$SqlDatabase;Integrated Security=true;TrustServerCertificate=true"
} else {
    $connectionString = "Server=$SqlServer;Database=$SqlDatabase;User ID=$SqlUser;Password=$SqlPassword;TrustServerCertificate=true"
}

# --- Ensure Invoke-Sqlcmd is available --------------------------------------
if (-not (Get-Command Invoke-Sqlcmd -ErrorAction SilentlyContinue)) {
    try {
        Import-Module SqlServer -ErrorAction Stop
    } catch {
        Write-Host "ERROR: 'SqlServer' module not installed. Install with:" -ForegroundColor Red
        Write-Host "  Install-Module -Name SqlServer -Scope CurrentUser -Force -AcceptLicense -AllowClobber" -ForegroundColor Yellow
        exit 2
    }
}

# --- Enumerate active pages -------------------------------------------------
$pageQuery = @"
SELECT PageID, PageMenuText, PageUrlName
FROM Page
WHERE PageAreaID = $AreaId
  AND PageActive = 1
  AND (PageDeleted = 0 OR PageDeleted IS NULL)
ORDER BY PageID;
"@

try {
    $rows = Invoke-Sqlcmd -ConnectionString $connectionString -Query $pageQuery -ErrorAction Stop
} catch {
    Write-Host "ERROR: Could not enumerate pages - $_" -ForegroundColor Red
    Write-Host "Check -SqlServer / -SqlDatabase / credentials and that the DW schema is present." -ForegroundColor Yellow
    exit 2
}

# Invoke-Sqlcmd returns $null for zero rows, a single DataRow for one, or an array for many.
if ($null -eq $rows) {
    $rowCount = 0
} elseif ($rows -is [System.Array]) {
    $rowCount = $rows.Count
} else {
    $rowCount = 1
    $rows = @($rows)
}

if ($rowCount -eq 0) {
    Write-Host "No active pages found under area $AreaId. Nothing to test." -ForegroundColor Yellow
    exit 0
}

Write-Host "Found $rowCount active page(s) to smoke-test." -ForegroundColor Green
Write-Host ""

# --- Hit each page and bucket ----------------------------------------------
$buckets = @{ '2xx' = @(); '3xx' = @(); '4xx' = @(); '5xx' = @() }
$errors = @()

foreach ($r in $rows) {
    $slug = $r.PageUrlName
    if ([string]::IsNullOrWhiteSpace($slug)) {
        $url = "$HostUrl/Default.aspx?ID=$($r.PageID)"
    } else {
        $url = "$HostUrl$LangPrefix/$slug"
    }

    $code = 0
    $body = $null
    $headers = $null
    $finalUrl = $url

    try {
        $resp = Invoke-WebRequest -Uri $url `
            -SkipCertificateCheck `
            -MaximumRedirection 5 `
            -ErrorAction Stop `
            -TimeoutSec 30
        $code = [int]$resp.StatusCode
        $body = $resp.Content
        $headers = $resp.Headers
        if ($resp.BaseResponse -and $resp.BaseResponse.RequestMessage -and $resp.BaseResponse.RequestMessage.RequestUri) {
            $finalUrl = $resp.BaseResponse.RequestMessage.RequestUri.AbsoluteUri
        }
    } catch {
        # Non-2xx throws in PS7. Extract status from the exception response when available.
        if ($_.Exception.Response) {
            try { $code = [int]$_.Exception.Response.StatusCode } catch { $code = 0 }
            try {
                $stream = $_.Exception.Response.GetResponseStream()
                if ($stream) {
                    $reader = New-Object System.IO.StreamReader($stream)
                    $body = $reader.ReadToEnd()
                }
            } catch { $body = "" }
            try { $headers = $_.Exception.Response.Headers } catch { $headers = $null }
        } else {
            $errors += [PSCustomObject]@{
                PageId = $r.PageID
                Url    = $url
                Error  = $_.Exception.Message
            }
            Write-Host "  [$($r.PageID)] $url -> TRANSPORT ERROR: $($_.Exception.Message)" -ForegroundColor Red
            continue
        }
    }

    $bucket = if ($code -ge 500) { '5xx' }
              elseif ($code -ge 400) { '4xx' }
              elseif ($code -ge 300) { '3xx' }
              elseif ($code -ge 200) { '2xx' }
              else { '5xx' }  # unexpected / zero; treat as failure

    $entry = [PSCustomObject]@{
        PageId   = $r.PageID
        MenuText = $r.PageMenuText
        Url      = $url
        FinalUrl = $finalUrl
        Code     = $code
    }
    if ($bucket -eq '5xx' -and $body) {
        $excerpt = $body.Substring(0, [Math]::Min(2000, $body.Length))
        $entry | Add-Member -NotePropertyName BodyExcerpt -NotePropertyValue $excerpt
        $entry | Add-Member -NotePropertyName Headers    -NotePropertyValue $headers
    } elseif ($bucket -eq '4xx' -and $body) {
        $excerpt = $body.Substring(0, [Math]::Min(500, $body.Length))
        $entry | Add-Member -NotePropertyName BodyExcerpt -NotePropertyValue $excerpt
    }

    $buckets[$bucket] += $entry
    $color = switch ($bucket) { '2xx' {'Green'} '3xx' {'Cyan'} '4xx' {'Yellow'} default {'Red'} }
    Write-Host "  [$($r.PageID)] $url -> $code ($bucket)" -ForegroundColor $color
}

# --- Summary ----------------------------------------------------------------
Write-Host ""
Write-Host "=== Summary ===" -ForegroundColor Cyan
Write-Host "2xx:              $($buckets['2xx'].Count)"
Write-Host "3xx:              $($buckets['3xx'].Count)"
Write-Host "4xx:              $($buckets['4xx'].Count)"
Write-Host "5xx:              $($buckets['5xx'].Count)"
Write-Host "Transport errors: $($errors.Count)"

if ($buckets['5xx'].Count -gt 0) {
    Write-Host ""
    Write-Host "=== 5xx Details ===" -ForegroundColor Red
    $buckets['5xx'] | ConvertTo-Json -Depth 4 | Write-Host
}
if ($buckets['4xx'].Count -gt 0) {
    Write-Host ""
    Write-Host "=== 4xx Details (first 500 chars) ===" -ForegroundColor Yellow
    $buckets['4xx'] | Select-Object PageId, Url, Code, BodyExcerpt | Format-Table -AutoSize
}
if ($errors.Count -gt 0) {
    Write-Host ""
    Write-Host "=== Transport Errors ===" -ForegroundColor Red
    $errors | Format-Table -AutoSize
}

if ($buckets['5xx'].Count -gt 0 -or $errors.Count -gt 0) {
    exit 1
}
exit 0
