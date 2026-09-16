-- =====================================================================
-- 18_widen_project_defaultbillable_to_string.sql
--
-- Carbynetech_Project.DefaultBillable changes from bit (Billable/
-- Non-billable only) to nvarchar(20) holding the same 3 values already
-- used everywhere else in the app for classification - "Billable",
-- "NonBillable", "PartialBillable" (see BillingClassificationRules.cs
-- and TimeEntryClassification on the frontend). This lets a project's
-- default classification (shown on Master Data > Projects, and what a
-- new task line pre-fills to) be set to Partial-Billable too.
--
-- Existing data mapping: 1 -> 'Billable', 0 -> 'NonBillable'. No
-- existing project becomes Partial-Billable automatically - that's a
-- brand new option someone has to explicitly pick going forward.
--
-- Idempotent: checks the column's current type first and does nothing
-- if it's already nvarchar (i.e. this has already been run).
--
-- Pattern: add new column, backfill it, drop the old one, rename the
-- new one into its place - same additive-then-swap approach used for
-- the Project Type rename in script 07.
-- =====================================================================

USE [Carbynetech_TimeSheet_Application];
GO
SET NOCOUNT ON;

IF EXISTS (
    SELECT 1 FROM sys.columns c
    JOIN sys.types ty ON ty.user_type_id = c.user_type_id
    WHERE c.object_id = OBJECT_ID('Carbynetech_Project') AND c.name = 'DefaultBillable' AND ty.name = 'bit'
)
BEGIN
    PRINT 'Carbynetech_Project.DefaultBillable is still bit - migrating.';

    ALTER TABLE Carbynetech_Project ADD DefaultBillable_New nvarchar(20) NULL;

    UPDATE Carbynetech_Project
    SET DefaultBillable_New = CASE WHEN DefaultBillable = 1 THEN 'Billable' ELSE 'NonBillable' END;
    PRINT CONCAT(@@ROWCOUNT, ' Project row(s) backfilled (1 -> Billable, 0 -> NonBillable).');

    ALTER TABLE Carbynetech_Project ALTER COLUMN DefaultBillable_New nvarchar(20) NOT NULL;

    ALTER TABLE Carbynetech_Project DROP COLUMN DefaultBillable;

    EXEC sp_rename 'Carbynetech_Project.DefaultBillable_New', 'DefaultBillable', 'COLUMN';

    PRINT 'Done - Carbynetech_Project.DefaultBillable is now nvarchar(20) (Billable/NonBillable/PartialBillable).';
END
ELSE IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Carbynetech_Project') AND name = 'DefaultBillable')
BEGIN
    PRINT 'Carbynetech_Project.DefaultBillable is already a string column - nothing to do.';
    SELECT DefaultBillable, COUNT(*) AS ProjectCount FROM Carbynetech_Project GROUP BY DefaultBillable;
END
ELSE
    PRINT 'ERROR: Carbynetech_Project.DefaultBillable column not found at all - unexpected schema state, look closer before proceeding.';
GO
