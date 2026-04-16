# DynamicWeb.Serializer

A DynamicWeb AppStore app that serializes and deserializes database state to/from YAML files on disk, enabling full database content to be version-controlled and deployed alongside code.

## How It Works

DynamicWeb.Serializer uses **predicates** to define which data to synchronize. Predicates can target content trees (pages, grids, paragraphs), SQL tables (ecommerce settings, users, etc.), or other data groups. Data is serialized to YAML files in a mirror-tree folder layout.

### Default Flow: Folder-Based Sync

This is the primary workflow for teams using source control (Git) to move content between environments.

```
Source Environment                          Target Environment
(e.g. Development)                          (e.g. Staging)

  DynamicWeb DB                               DynamicWeb DB
       |                                           ^
       | Serialize                                 | Deserialize
       | (Management API)                          | (Management API after deploy)
       v                                           |
  Files/System/Serializer/                   Files/System/Serializer/
       SerializeRoot/                              SerializeRoot/
         Swift 2/                                    Swift 2/
           area.yml                                    area.yml
           Customer Center/                            Customer Center/
             page.yml                                    page.yml
             grid-row-1/                                 grid-row-1/
               grid-row.yml                                grid-row.yml
               paragraph-c1-1.yml                          paragraph-c1-1.yml
               ...                                         ...
```

**Steps:**

1. **Configure predicates** in the admin UI (Settings > Database > Serialize > Predicates) pointing to the data you want to sync
2. **Serialize** -- run via Management API:
   ```bash
   curl -X POST https://source.example.com/Admin/Api/SerializerSerialize \
     -H "Authorization: Bearer CLD.your-api-key"
   ```
3. **Commit the YAML files** to your Git repository
4. **Deploy to target environment** -- the YAML files arrive via Git pull/deploy pipeline
5. **Deserialize** -- trigger immediately after deploy:
   ```bash
   curl -X POST https://target.example.com/Admin/Api/SerializerDeserialize \
     -H "Authorization: Bearer CLD.your-api-key"
   ```
6. Content is matched by **PageUniqueId (GUID)** -- existing pages are updated, new pages are created

### Ad-Hoc Flow: Single Page Export/Import

For moving individual pages between environments without a full sync cycle (e.g. moving a landing page from staging to production).

**Export (Serialize):**

1. Navigate to any page in the DynamicWeb admin content tree
2. Open the page edit screen
3. Click **"Serialize subtree"** in the Actions menu
4. A zip file downloads containing the page and all its children as YAML
5. The zip is also saved to `Files/System/Serializer/Download/`

**Import (Deserialize):**

1. Upload the zip file to `Files/System/Serializer/Upload/` on the target environment (via DW Files manager or FTP)
2. Navigate to the zip file in **Files** (asset management)
3. Click **"Import to database"** in the file's action menu
4. Select the **target area** to import into (supports importing into a different area than the source)
5. Review the import preview showing pages and grid rows found in the zip
6. Click **Save** to execute the import -- content is matched by GUID, new pages are created

## Content Model

DynamicWeb.Serializer serializes the full DynamicWeb content hierarchy:

| Level | What's serialized |
|-------|-------------------|
| **Area** | Area metadata, area properties (with configurable column exclusions) |
| **Page** | Name, sort order, item type, item fields, property fields (Icon, SubmenuType) |
| **Grid Row** | Layout settings (spacing, container width, visual properties) |
| **Paragraph** | Content, item type fields, column attribution |

Identity is based on **PageUniqueId (GUID)**, not numeric IDs. This means content can move between environments where numeric IDs differ.

## Permissions

DynamicWeb.Serializer serializes and restores **explicit page permissions**. Pages using inherited permissions (no explicit overrides) are left unchanged during deserialization -- only pages with a `permissions` section in their YAML file are affected.

### What Gets Serialized

Only explicit (non-inherited) page permissions are serialized. Each permission entry includes:

- **Owner** -- the role or group name (e.g. `Anonymous`, `Marketing Team`)
- **Owner type** -- `Role` or `Group`
- **Permission level** -- one of: `None`, `Read`, `Edit`, `Create`, `Delete`, `All`

Example YAML snippet for a page with explicit permissions:

```yaml
permissions:
  - owner: Anonymous
    ownerType: role
    level: none
    levelValue: 1
  - owner: AuthenticatedFrontend
    ownerType: role
    level: read
    levelValue: 4
```

### How Permissions Are Restored

During deserialization, permissions are resolved and applied using a **source-wins** model:

- **Roles** (`Anonymous`, `AuthenticatedFrontend`, `AuthenticatedBackend`, `Administrator`) are matched by name. Role names are identical across all DynamicWeb environments, so no resolution is needed.
- **User groups** are matched by group name on the target environment (case-insensitive). The group's numeric ID may differ between environments -- DynamicWeb.Serializer resolves the correct ID by searching for a group with the same name.
- **Existing explicit permissions** on the target page are cleared before applying the serialized permissions. The serialized state is the single source of truth.
- **Pages without a `permissions` section** in their YAML file are left untouched -- existing permissions (including inherited) are preserved.

### Safety Fallback

If a serialized group permission references a user group that **does not exist** on the target environment:

1. The group permission is **skipped** (it cannot be applied without a matching group)
2. As a safety measure, **Anonymous access is set to None** (deny) on that page to prevent accidental public exposure
3. The fallback is **logged as a warning**

This ensures that missing groups never result in a page being unintentionally accessible to anonymous users.

**Recommendation:** Ensure user groups exist on all target environments before deploying content with group-based permissions.

### Permission Logging

All permission actions are logged:

- **Applied** -- each role or group permission successfully set on a page
- **Skipped** -- a group permission was skipped because the group was not found on the target
- **Safety fallback triggered** -- Anonymous was set to None due to a missing group

Check the Management API response or log files for details.

## Configuration

All settings are managed from the DynamicWeb admin UI at **Settings > Database > Serialize**, or by editing the config file directly at `Files/Serializer.config.json`.

### Settings Screen

| Setting | Description |
|---------|-------------|
| **Output Directory** | Top-level folder relative to `Files/System`. Subfolders are created automatically: `SerializeRoot`, `Upload`, `Download` |
| **Log Level** | Logging verbosity: Info, Debug, Warn, Error |
| **Dry Run** | When enabled, sync operations log what would happen without making changes |
| **Conflict Strategy** | How to handle conflicts. Currently: Source Wins (serialized files overwrite target) |

### Predicate Management

Predicates define which data to synchronize. Manage them at **Settings > Database > Serialize > Predicates**.

#### Content Predicates

| Field | Description |
|-------|-------------|
| **Name** | Unique name for this predicate |
| **Provider Type** | `Content` -- syncs DW content trees |
| **Area** | DynamicWeb area containing the content tree |
| **Page** | Root page for the predicate (content tree picker) |
| **Excludes** | Paths to exclude from sync (one per line) |
| **Exclude Fields** | Item type field names to exclude from serialization |
| **Exclude XML Elements** | XML element names to strip from embedded XML blobs |
| **Exclude Area Columns** | Area table columns to exclude from serialization (SelectMultiDual populated from database schema) |

#### SqlTable Predicates

| Field | Description |
|-------|-------------|
| **Name** | Unique name for this predicate |
| **Provider Type** | `SqlTable` -- syncs arbitrary SQL tables |
| **Table** | SQL table name (e.g., `EcomOrderFlow`) |
| **Name Column** | Column used as natural key for row identity (optional) |
| **Compare Columns** | Columns used for change detection (optional) |
| **Service Caches** | DW service cache types to clear after deserialization (one per line) |
| **Exclude Fields** | Column names to exclude from serialization (SelectMultiDual populated from table schema) |
| **XML Columns** | Columns containing XML that should be pretty-printed in YAML (SelectMultiDual populated from table schema) |
| **Exclude XML Elements** | XML element names to strip from embedded XML blobs (one per line) |

#### Global Exclusion Maps

These are set in the config file (not per-predicate) and apply across all predicates:

| Setting | Description |
|---------|-------------|
| **excludeFieldsByItemType** | Map of item type system name to list of field names to exclude |
| **excludeXmlElementsByType** | Map of XML type name to list of element names to exclude |

### Item Type Management

Browse and configure item type field exclusions at **Settings > Database > Serialize > Item Types**. Item types are organized by category in a hierarchical tree mirroring DW's item type structure.

### Embedded XML Management

Configure XML element exclusions at **Settings > Database > Serialize > Embedded XML**. Each XML type shows which elements can be excluded from serialization.

### Config File

The config file at `Files/Serializer.config.json` is the source of truth. The admin UI reads and writes this file. Manual edits are reflected on the next screen load.

```json
{
  "outputDirectory": "Serializer",
  "logLevel": "info",
  "dryRun": false,
  "conflictStrategy": "source-wins",
  "excludeFieldsByItemType": {
    "Swift_Content": ["SystemName_Internal"]
  },
  "excludeXmlElementsByType": {
    "ParagraphModule": ["cache"]
  },
  "predicates": [
    {
      "name": "Customer Center",
      "providerType": "Content",
      "path": "/Customer Center",
      "areaId": 3,
      "pageId": 8385,
      "excludes": [],
      "excludeFields": [],
      "excludeXmlElements": [],
      "excludeAreaColumns": ["AreaDomain", "AreaSSLCertificate"]
    },
    {
      "name": "Order Flows",
      "providerType": "SqlTable",
      "table": "EcomOrderFlow",
      "nameColumn": "OrderFlowName",
      "compareColumns": "OrderFlowName,OrderFlowDescription",
      "serviceCaches": ["Dynamicweb.Ecommerce.Orders.OrderFlowService"],
      "excludeFields": [],
      "xmlColumns": ["OrderFlowXml"],
      "excludeXmlElements": ["cache"]
    }
  ]
}
```

## Folder Structure

```
Files/System/{OutputDirectory}/
  SerializeRoot/     YAML files -- Management API reads/writes here
  Upload/            Drop .zip files here for zip-based import
  Download/          Ad-hoc serialize saves zip copies here
```

## Admin UI

### Navigation Tree

The Serialize node appears under **Settings > Database** with sub-nodes:

- **Serialize** -- Settings screen (output directory, log level, dry run, conflict strategy)
- **Predicates** -- CRUD management of Content and SqlTable predicates, with per-predicate child nodes
- **Item Types** -- Browse item types by category, configure per-type field exclusions
- **Embedded XML** -- Configure per-type XML element exclusions
- **Log Viewer** -- View per-run logs with summaries and actionable advice

### Serialize Action on Pages

The **"Serialize subtree"** action appears in the Actions menu on every page edit screen, alongside Preview and Paragraphs.

## API Commands & CI/CD Integration

DynamicWeb.Serializer exposes two Management API commands for automated serialization and deserialization, enabling workflows triggered by CI/CD pipelines or Git hooks.

### Available Commands

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/Admin/Api/SerializerSerialize` | POST | Serialize all predicate-matched data to `SerializeRoot/` |
| `/Admin/Api/SerializerDeserialize` | POST | Deserialize YAML from `SerializeRoot/` into the database |

Authentication: `Authorization: Bearer {API-Key}` (Management API key)

### Example: curl

```bash
# Serialize content on source environment
curl -X POST https://source.example.com/Admin/Api/SerializerSerialize \
  -H "Authorization: Bearer CLD.your-api-key-here"

# Deserialize content on target environment (after YAML files are deployed)
curl -X POST https://target.example.com/Admin/Api/SerializerDeserialize \
  -H "Authorization: Bearer CLD.your-api-key-here"
```

### Example: GitHub Actions

```yaml
- name: Deploy content
  run: |
    # After deploying code + YAML files to the target environment
    curl -X POST ${{ secrets.DW_HOST }}/Admin/Api/SerializerDeserialize \
      -H "Authorization: Bearer ${{ secrets.DW_API_KEY }}"
```

### Git-Based Workflow

The typical CI/CD flow for content synchronization:

```
Developer commits YAML files
        |
Git push -> CI/CD pipeline
        |
Deploy to target (code + YAML files land in SerializeRoot/)
        |
POST /Admin/Api/SerializerDeserialize
        |
Content is immediately applied
```

## Installation

1. Build the project:
   ```
   dotnet build src/DynamicWeb.Serializer/ -c Release
   ```

2. Copy `DynamicWeb.Serializer.dll` to your DynamicWeb instance's `bin/` directory

3. Restart the DynamicWeb application

4. Navigate to **Settings > Database > Serialize** to configure

## Tech Stack

- .NET 8.0
- DynamicWeb 10.23.9+
- YamlDotNet 13.7.1
- System.IO.Compression (built-in)

## License

Open source -- no licensing required.
