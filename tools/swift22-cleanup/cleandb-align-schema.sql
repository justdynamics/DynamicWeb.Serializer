-- cleandb-align-schema.sql — Add the 10 drift columns to CleanDB that Swift-2.2
-- ships but upstream DW NuGet 10.23.9 on CleanDB does not.
--
-- Context: Phase 38.1-01 Task 6 Part B Deviation 2 applied these 10 schema
-- additions on CleanDB by hand (see .planning/phases/38.1-close-phase-38-deferrals/
-- 38.1-01-e2e-results.md lines 27-44). This script codifies that operation so the
-- Plan 04 full E2E pipeline can run unattended.
--
-- Columns:
--   Area.AreaHtmlType                       nvarchar(10)  NULL  — Swift-only layout classifier
--   Area.AreaLayoutPhone                    nvarchar(255) NULL  — per-device layout override
--   Area.AreaLayoutTablet                   nvarchar(255) NULL  — per-device layout override
--   EcomGroups.GroupPageIDRel               int           NULL  — legacy group→page FK
--   EcomProducts.ProductPeriodId            nvarchar(50)  NULL  — legacy subscription-period FK
--   EcomProducts.ProductVariantGroupCounter int           NULL  — legacy variant-group cache
--   EcomProducts.ProductPriceMatrixPeriod   int           NULL  — legacy matrix-pricing period
--   EcomProducts.ProductOptimizedFor        nvarchar(255) NULL  — legacy storefront hint
--   EcomProducts.MyVolume                   nvarchar(50)  NULL  — legacy custom field
--   EcomProducts.MyDouble                   float         NULL  — legacy custom field
--
-- Rationale: docs/baselines/env-bucket.md §"DW NuGet version alignment" —
-- aligning DW NuGet versions is the supported remediation; this script makes
-- the target schema match the source schema so the serializer's
-- TargetSchemaCache warn-and-skip mechanism is never triggered for these
-- columns during strict-mode deserialize.
--
-- Idempotent: every ALTER is guarded by a column-existence check so
-- re-running is a safe no-op.
-- Run against: Swift-CleanDB (the TARGET DB), NOT Swift-2.2.

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRAN;

IF COL_LENGTH('Area', 'AreaHtmlType') IS NULL
BEGIN
    ALTER TABLE [Area] ADD [AreaHtmlType] nvarchar(10) NULL;
    PRINT 'Added Area.AreaHtmlType';
END
ELSE
    PRINT 'Skipped Area.AreaHtmlType — already exists';

IF COL_LENGTH('Area', 'AreaLayoutPhone') IS NULL
BEGIN
    ALTER TABLE [Area] ADD [AreaLayoutPhone] nvarchar(255) NULL;
    PRINT 'Added Area.AreaLayoutPhone';
END
ELSE
    PRINT 'Skipped Area.AreaLayoutPhone — already exists';

IF COL_LENGTH('Area', 'AreaLayoutTablet') IS NULL
BEGIN
    ALTER TABLE [Area] ADD [AreaLayoutTablet] nvarchar(255) NULL;
    PRINT 'Added Area.AreaLayoutTablet';
END
ELSE
    PRINT 'Skipped Area.AreaLayoutTablet — already exists';

IF COL_LENGTH('EcomGroups', 'GroupPageIDRel') IS NULL
BEGIN
    ALTER TABLE [EcomGroups] ADD [GroupPageIDRel] int NULL;
    PRINT 'Added EcomGroups.GroupPageIDRel';
END
ELSE
    PRINT 'Skipped EcomGroups.GroupPageIDRel — already exists';

IF COL_LENGTH('EcomProducts', 'ProductPeriodId') IS NULL
BEGIN
    ALTER TABLE [EcomProducts] ADD [ProductPeriodId] nvarchar(50) NULL;
    PRINT 'Added EcomProducts.ProductPeriodId';
END
ELSE
    PRINT 'Skipped EcomProducts.ProductPeriodId — already exists';

IF COL_LENGTH('EcomProducts', 'ProductVariantGroupCounter') IS NULL
BEGIN
    ALTER TABLE [EcomProducts] ADD [ProductVariantGroupCounter] int NULL;
    PRINT 'Added EcomProducts.ProductVariantGroupCounter';
END
ELSE
    PRINT 'Skipped EcomProducts.ProductVariantGroupCounter — already exists';

IF COL_LENGTH('EcomProducts', 'ProductPriceMatrixPeriod') IS NULL
BEGIN
    ALTER TABLE [EcomProducts] ADD [ProductPriceMatrixPeriod] int NULL;
    PRINT 'Added EcomProducts.ProductPriceMatrixPeriod';
END
ELSE
    PRINT 'Skipped EcomProducts.ProductPriceMatrixPeriod — already exists';

IF COL_LENGTH('EcomProducts', 'ProductOptimizedFor') IS NULL
BEGIN
    ALTER TABLE [EcomProducts] ADD [ProductOptimizedFor] nvarchar(255) NULL;
    PRINT 'Added EcomProducts.ProductOptimizedFor';
END
ELSE
    PRINT 'Skipped EcomProducts.ProductOptimizedFor — already exists';

IF COL_LENGTH('EcomProducts', 'MyVolume') IS NULL
BEGIN
    ALTER TABLE [EcomProducts] ADD [MyVolume] nvarchar(50) NULL;
    PRINT 'Added EcomProducts.MyVolume';
END
ELSE
    PRINT 'Skipped EcomProducts.MyVolume — already exists';

IF COL_LENGTH('EcomProducts', 'MyDouble') IS NULL
BEGIN
    ALTER TABLE [EcomProducts] ADD [MyDouble] float NULL;
    PRINT 'Added EcomProducts.MyDouble';
END
ELSE
    PRINT 'Skipped EcomProducts.MyDouble — already exists';

COMMIT TRAN;
PRINT 'Done — cleandb-align-schema.sql';
