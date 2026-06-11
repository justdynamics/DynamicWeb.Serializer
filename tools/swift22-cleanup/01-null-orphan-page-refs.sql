-- 01-null-orphan-page-refs.sql — Null out 77 paragraph/item-field references
-- to 5 known-broken page IDs. These pages either don't exist or live in a
-- different baseline (Area 26 Digital Assets Portal).
--
-- IDs fixed here:
--   8308  — "Home" in Area 26 (separate baseline); referenced by Logo (4) + Text (1)
--   149   — non-existent; referenced by CustomerCenterApp (11), EmailButton (3),
--           EmailIcon_Item (7), EmailMenu_Item (27)
--   295   — non-existent; referenced by CheckoutApp (1), CustomerCenterApp (9)
--   140   — non-existent; referenced by CheckoutApp (1), CustomerCenterApp (9),
--           Emails.Header (1), Emails.Footer (3)
--
-- 15717 is intentionally NOT cleaned here. It looks like an orphan page ref in
-- ButtonEditor JSON but is actually a PARAGRAPH anchor (LinkType "paragraph":
-- SelectedValue holds a paragraph id; the page ref is the Link's ID= part) and
-- Paragraph 15717 exists. The original REPLACE never fired anyway (it matched
-- '"SelectedValue":"15717"' but the stored JSON is pretty-printed with a space)
-- — a silent no-op that kept this legitimate content intact by accident.
-- The serializer link extractor skips paragraph-typed SelectedValues instead.

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRAN;

PRINT '--- ID 8308 cleanup (Area 26 cross-baseline ref) ---';
PRINT 'Before:';
SELECT 'Logo.Link' AS Loc, COUNT(*) AS N FROM [ItemType_Swift-v2_Logo] WHERE Link LIKE '%Default.aspx?%=8308%';
SELECT 'Text.Text' AS Loc, COUNT(*) AS N FROM [ItemType_Swift-v2_Text] WHERE Text LIKE '%Default.aspx?%=8308%';

-- Logo.Link is a full URL; null it out
UPDATE [ItemType_Swift-v2_Logo]
   SET Link = ''
 WHERE Link LIKE '%Default.aspx?%=8308%';

-- Text.Text is HTML: strip the href to disarm the broken link but keep the anchor text
UPDATE [ItemType_Swift-v2_Text]
   SET Text = REPLACE(REPLACE(CAST(Text AS NVARCHAR(MAX)), 'Default.aspx?ID=8308', ''), 'Default.aspx?Id=8308', '')
 WHERE Text LIKE '%Default.aspx?%=8308%';

PRINT '';
PRINT '--- ID 149 cleanup (non-existent page) ---';

UPDATE [ItemType_Swift-v2_CustomerCenterApp] SET ProductListPage = '' WHERE ProductListPage LIKE '%Default.aspx?%=149%';
UPDATE [ItemType_Swift-v2_EmailButton]       SET PageLink       = '' WHERE PageLink       LIKE '%Default.aspx?%=149%';
UPDATE [ItemType_Swift-v2_EmailIcon_Item]    SET Link           = '' WHERE Link           LIKE '%Default.aspx?%=149%';
UPDATE [ItemType_Swift-v2_EmailMenu_Item]    SET Link           = '' WHERE Link           LIKE '%Default.aspx?%=149%';

PRINT '--- ID 295 cleanup (non-existent page) ---';
UPDATE [ItemType_Swift-v2_CheckoutApp]        SET UserAddressesPageLink = '' WHERE UserAddressesPageLink LIKE '%Default.aspx?%=295%';
UPDATE [ItemType_Swift-v2_CustomerCenterApp]  SET AddressesPage         = '' WHERE AddressesPage         LIKE '%Default.aspx?%=295%';

PRINT '--- ID 140 cleanup (non-existent page) ---';
UPDATE [ItemType_Swift-v2_CheckoutApp]        SET UserAccountPageLink   = '' WHERE UserAccountPageLink   LIKE '%Default.aspx?%=140%';
UPDATE [ItemType_Swift-v2_CustomerCenterApp]  SET AccountSettingsPage   = '' WHERE AccountSettingsPage   LIKE '%Default.aspx?%=140%';
UPDATE [ItemType_Swift-v2_Emails]             SET Header                = '' WHERE Header                LIKE '%Default.aspx?%=140%';
UPDATE [ItemType_Swift-v2_Emails]             SET Footer                = '' WHERE Footer                LIKE '%Default.aspx?%=140%';

PRINT '--- ID 15717: intentionally untouched (paragraph anchor, see header) ---';

PRINT '';
PRINT '--- Verify after cleanup ---';
SELECT '8308 remaining in Logo.Link' AS Loc, COUNT(*) AS N FROM [ItemType_Swift-v2_Logo]               WHERE Link     LIKE '%Default.aspx?%=8308%'
UNION ALL SELECT '8308 in Text.Text',             COUNT(*) FROM [ItemType_Swift-v2_Text]               WHERE Text     LIKE '%Default.aspx?%=8308%'
UNION ALL SELECT '149 in CustomerCenterApp',      COUNT(*) FROM [ItemType_Swift-v2_CustomerCenterApp]  WHERE ProductListPage LIKE '%Default.aspx?%=149%'
UNION ALL SELECT '149 in EmailButton',            COUNT(*) FROM [ItemType_Swift-v2_EmailButton]        WHERE PageLink LIKE '%Default.aspx?%=149%'
UNION ALL SELECT '149 in EmailIcon_Item',         COUNT(*) FROM [ItemType_Swift-v2_EmailIcon_Item]     WHERE Link     LIKE '%Default.aspx?%=149%'
UNION ALL SELECT '149 in EmailMenu_Item',         COUNT(*) FROM [ItemType_Swift-v2_EmailMenu_Item]     WHERE Link     LIKE '%Default.aspx?%=149%'
UNION ALL SELECT '295 in CheckoutApp',            COUNT(*) FROM [ItemType_Swift-v2_CheckoutApp]        WHERE UserAddressesPageLink LIKE '%Default.aspx?%=295%'
UNION ALL SELECT '295 in CustomerCenterApp',      COUNT(*) FROM [ItemType_Swift-v2_CustomerCenterApp]  WHERE AddressesPage LIKE '%Default.aspx?%=295%'
UNION ALL SELECT '140 in CheckoutApp',            COUNT(*) FROM [ItemType_Swift-v2_CheckoutApp]        WHERE UserAccountPageLink LIKE '%Default.aspx?%=140%'
UNION ALL SELECT '140 in CustomerCenterApp',      COUNT(*) FROM [ItemType_Swift-v2_CustomerCenterApp]  WHERE AccountSettingsPage LIKE '%Default.aspx?%=140%'
UNION ALL SELECT '140 in Emails.Header',          COUNT(*) FROM [ItemType_Swift-v2_Emails]             WHERE Header   LIKE '%Default.aspx?%=140%'
UNION ALL SELECT '140 in Emails.Footer',          COUNT(*) FROM [ItemType_Swift-v2_Emails]             WHERE Footer   LIKE '%Default.aspx?%=140%';

COMMIT TRAN;
PRINT 'Done.';
