-- Purge Swift-CleanDB for a fresh Deploy+Seed round-trip test (2026-04-20).
-- Iterates all ItemType_* tables via INFORMATION_SCHEMA then wipes core Content + Ecom.

USE [Swift-CleanDB];
SET NOCOUNT ON;

BEGIN TRAN;
EXEC sp_MSforeachtable "ALTER TABLE ? NOCHECK CONSTRAINT ALL";

-- Dynamic DELETE on every ItemType_* table (Swift ItemType field-value storage)
DECLARE @sql NVARCHAR(MAX) = N'';
SELECT @sql = @sql + N'DELETE FROM [' + TABLE_NAME + N'];' + CHAR(10)
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_NAME LIKE 'ItemType_%';
EXEC sp_executesql @sql;

-- Core content graph
DELETE FROM [Paragraph];
DELETE FROM [GridRow];
DELETE FROM [Page];
DELETE FROM [Area];
DELETE FROM [UrlPath];

-- Ecom relation tables (delete before parents)
DELETE FROM [EcomGroupProductRelation];
DELETE FROM [EcomVariantOptionsProductRelation];
DELETE FROM [EcomShopGroupRelation];
DELETE FROM [EcomShopLanguageRelation];
DELETE FROM [EcomMethodCountryRelation];
DELETE FROM [EcomVatCountryRelations];

-- Ecom core tables covered by the Deploy+Seed config
DELETE FROM [EcomDiscountTranslation];
DELETE FROM [EcomDiscount];
DELETE FROM [EcomVariantsOptions];
DELETE FROM [EcomVariantGroups];
DELETE FROM [EcomProducts];
DELETE FROM [EcomGroups];
DELETE FROM [EcomOrderStateRules];
DELETE FROM [EcomOrderStates];
DELETE FROM [EcomOrderFlow];
DELETE FROM [EcomShippings];
DELETE FROM [EcomPayments];
DELETE FROM [EcomShops];
DELETE FROM [EcomVatGroups];
DELETE FROM [EcomLanguages];
DELETE FROM [EcomCurrencies];
DELETE FROM [EcomCountryText];
DELETE FROM [EcomCountries];

-- Reseed identity columns where present (ignore failures)
BEGIN TRY DBCC CHECKIDENT ('Area',            RESEED, 0); END TRY BEGIN CATCH END CATCH
BEGIN TRY DBCC CHECKIDENT ('Page',            RESEED, 0); END TRY BEGIN CATCH END CATCH
BEGIN TRY DBCC CHECKIDENT ('Paragraph',       RESEED, 0); END TRY BEGIN CATCH END CATCH
BEGIN TRY DBCC CHECKIDENT ('GridRow',         RESEED, 0); END TRY BEGIN CATCH END CATCH
BEGIN TRY DBCC CHECKIDENT ('UrlPath',         RESEED, 0); END TRY BEGIN CATCH END CATCH
BEGIN TRY DBCC CHECKIDENT ('EcomShops',       RESEED, 0); END TRY BEGIN CATCH END CATCH
BEGIN TRY DBCC CHECKIDENT ('EcomPayments',    RESEED, 0); END TRY BEGIN CATCH END CATCH
BEGIN TRY DBCC CHECKIDENT ('EcomShippings',   RESEED, 0); END TRY BEGIN CATCH END CATCH
BEGIN TRY DBCC CHECKIDENT ('EcomVatGroups',   RESEED, 0); END TRY BEGIN CATCH END CATCH
BEGIN TRY DBCC CHECKIDENT ('EcomOrderFlow',   RESEED, 0); END TRY BEGIN CATCH END CATCH
BEGIN TRY DBCC CHECKIDENT ('EcomOrderStates', RESEED, 0); END TRY BEGIN CATCH END CATCH

COMMIT TRAN;
-- Re-enable FK with NOCHECK (WITHOUT verifying existing data) so the re-enable
-- doesn't abort on empty parent tables. Deserialize will repopulate referenced
-- rows; FKs remain structurally enforced for new writes after re-enable.
BEGIN TRY EXEC sp_MSforeachtable "ALTER TABLE ? CHECK CONSTRAINT ALL"; END TRY BEGIN CATCH END CATCH

SELECT 'Area' AS T, COUNT(*) AS N FROM [Area]
UNION ALL SELECT 'Page',          COUNT(*) FROM [Page]
UNION ALL SELECT 'Paragraph',     COUNT(*) FROM [Paragraph]
UNION ALL SELECT 'GridRow',       COUNT(*) FROM [GridRow]
UNION ALL SELECT 'EcomShops',     COUNT(*) FROM [EcomShops]
UNION ALL SELECT 'EcomCountries', COUNT(*) FROM [EcomCountries]
UNION ALL SELECT 'EcomProducts',  COUNT(*) FROM [EcomProducts]
UNION ALL SELECT 'UrlPath',       COUNT(*) FROM [UrlPath];
