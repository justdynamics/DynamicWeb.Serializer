-- 06-delete-orphan-ecomshopgrouprelation.sql — Delete the single orphan row
-- (ShopGroupShopId='SHOP19', ShopGroupGroupId='GROUP253') from
-- [EcomShopGroupRelation] in Swift 2.2. SHOP19 is not a valid ShopId
-- (only 9 shops exist: SHOP1/5/6/7/8/9/14/27/28). This orphan triggers the
-- FK re-enable escalation in SqlTableProvider on every deserialize.
-- Closes Phase 38 B.4 (D-38-08) / Phase 38.1 B.4.1 (D-38.1-05).
--
-- Re-runnable. Transaction-wrapped. Asserts expected count before DELETE.
--
-- Investigation: .planning/phases/38-production-ready-baseline-hardening-retroactive-tests-for-37/38-03-b4-investigation.md

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRAN;

PRINT '--- Before ---';
DECLARE @before INT = (
    SELECT COUNT(*) FROM [EcomShopGroupRelation]
    WHERE ShopGroupShopId = 'SHOP19' AND ShopGroupGroupId = 'GROUP253'
);
PRINT CONCAT('SHOP19/GROUP253 rows before: ', @before);

IF @before <> 1
BEGIN
    PRINT 'ABORT: expected exactly 1 SHOP19/GROUP253 orphan row; aborting without DELETE.';
    ROLLBACK TRAN;
    RETURN;
END

DELETE FROM [EcomShopGroupRelation]
 WHERE ShopGroupShopId = 'SHOP19'
   AND ShopGroupGroupId = 'GROUP253';

PRINT '--- After ---';
DECLARE @after INT = (
    SELECT COUNT(*) FROM [EcomShopGroupRelation]
    WHERE ShopGroupShopId = 'SHOP19' AND ShopGroupGroupId = 'GROUP253'
);
PRINT CONCAT('SHOP19/GROUP253 rows after: ', @after);

IF @after <> 0
BEGIN
    PRINT 'ABORT: post-DELETE count should be 0; rolling back.';
    ROLLBACK TRAN;
    RETURN;
END

-- Also verify the overall orphan count across all shops (defensive — catches
-- new orphans that were not in the investigation scope).
PRINT '--- Overall orphan verification ---';
SELECT 'remaining-orphans' AS stage, COUNT(*) AS n
  FROM [EcomShopGroupRelation] r
 WHERE NOT EXISTS (SELECT 1 FROM [EcomShops] s WHERE s.ShopId = r.ShopGroupShopId);

COMMIT TRAN;
PRINT 'Done — 06-delete-orphan-ecomshopgrouprelation.sql';
