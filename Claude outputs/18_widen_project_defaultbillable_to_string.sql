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
-- Pattern: add new column, backfill it, drop the old one, rename the
-- new one into its place - same additive-then-swap approach used for
-- the Project Type rename in script 07.
--
-- Every step below is independently idempotent - it checks its own
-- pre-condition before doing anything - rather than one big up-front
-- "already migrated?" check. Two things forced that:
--
-- 1) Each ALTER TABLE / UPDATE that touches the new DefaultBillable_New
--    column runs via EXEC('...') dynamic SQL. Reason: without this,
--    referencing a column added earlier in the SAME batch from a later
--    ALTER COLUMN statement fails with "Invalid column name" (Msg 207)
--    - SQL Server validates that statement against the schema snapshot
--    from before the batch started, not the mid-batch state. EXEC()
--    forces each statement to compile fresh at execution time.
--
-- 2) A first attempt at this script got through adding/backfilling/
--    NOT-NULL'ing DefaultBillable_New, then failed on DROP COLUMN
--    DefaultBillable because SQL Server won't drop a column that a
--    constraint still references - here, the old bit column's
--    auto-generated default constraint (name varies per environment,
--    e.g. DF__Carbynete__Defau__xxxxxxxx). Since each ALTER TABLE
--    commits on its own (no explicit transaction), that left the table
--    with BOTH columns present. Re-running the original all-or-nothing
--    version would then fail again trying to re-ADD DefaultBillable_New.
--    Per-step guards mean this script picks up from exactly wherever a
--    previous run stopped, and does nothing once fully migrated.
-- =====================================================================

USE [Carbynetech_TimeSheet_Application];
GO
SET NOCOUNT ON;

-- Step 1: add the new string column, if it isn't there yet.
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Carbynetech_Project') AND name = 'DefaultBillable' )
   AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Carbynetech_Project') AND name = 'DefaultBillable_New')
BEGIN
    EXEC('ALTER TABLE Carbynetech_Project ADD DefaultBillable_New nvarchar(20) NULL;');
    PRINT 'Added Carbynetech_Project.DefaultBillable_New (nvarchar(20)).';
END

-- Step 2: backfill any not-yet-backfilled rows from the old bit column.
-- Safe to re-run - the WHERE clause only touches rows still NULL.
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Carbynetech_Project') AND name = 'DefaultBillable_New')
   AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Carbynetech_Project') AND name = 'DefaultBillable')
BEGIN
    DECLARE @RowsBackfilled INT;
    EXEC('UPDATE Carbynetech_Project SET DefaultBillable_New = CASE WHEN DefaultBillable = 1 THEN ''Billable'' ELSE ''NonBillable'' END WHERE DefaultBillable_New IS NULL;');
    SET @RowsBackfilled = @@ROWCOUNT;
    PRINT CONCAT(@RowsBackfilled, ' Project row(s) backfilled just now (1 -> Billable, 0 -> NonBillable).');
END

-- Step 3: lock it down to NOT NULL once every row has a value.
IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('Carbynetech_Project') AND name = 'DefaultBillable_New' AND is_nullable = 1
)
BEGIN
    EXEC('ALTER TABLE Carbynetech_Project ALTER COLUMN DefaultBillable_New nvarchar(20) NOT NULL;');
    PRINT 'Carbynetech_Project.DefaultBillable_New set to NOT NULL.';
END

-- Step 4: drop the old bit column - and whatever default constraint SQL
-- Server auto-generated for it - once the new column is ready to take
-- over. Looks the constraint name up rather than hardcoding it, since
-- SQL Server names these DF__<table>__<column>__<random suffix>, which
-- differs per environment/run.
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Carbynetech_Project') AND name = 'DefaultBillable_New')
   AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Carbynetech_Project') AND name = 'DefaultBillable')
BEGIN
    DECLARE @ConstraintName NVARCHAR(200);
    DECLARE @DropConstraintSql NVARCHAR(400);
    SELECT @ConstraintName = dc.name
    FROM sys.default_constraints dc
    JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID('Carbynetech_Project') AND c.name = 'DefaultBillable';

    IF @ConstraintName IS NOT NULL
    BEGIN
        -- Built into a variable first, then EXEC'd - EXEC('literal' + expr)
        -- only accepts literals/variables joined by +, not a function call
        -- like QUOTENAME() inline, so QUOTENAME has to be resolved here.
        SET @DropConstraintSql = 'ALTER TABLE Carbynetech_Project DROP CONSTRAINT ' + QUOTENAME(@ConstraintName) + ';';
        EXEC(@DropConstraintSql);
        PRINT CONCAT('Dropped default constraint ', @ConstraintName, ' on the old DefaultBillable column.');
    END

    EXEC('ALTER TABLE Carbynetech_Project DROP COLUMN DefaultBillable;');
    PRINT 'Dropped old Carbynetech_Project.DefaultBillable (bit) column.';
END

-- Step 5: rename the new column into the old one's place.
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Carbynetech_Project') AND name = 'DefaultBillable_New')
   AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Carbynetech_Project') AND name = 'DefaultBillable')
BEGIN
    EXEC sp_rename 'Carbynetech_Project.DefaultBillable_New', 'DefaultBillable', 'COLUMN';
    PRINT 'Renamed DefaultBillable_New -> DefaultBillable.';
END

-- Final status.
IF EXISTS (
    SELECT 1 FROM sys.columns c
    JOIN sys.types ty ON ty.user_type_id = c.user_type_id
    WHERE c.object_id = OBJECT_ID('Carbynetech_Project') AND c.name = 'DefaultBillable' AND ty.name IN ('nvarchar', 'varchar')
)
BEGIN
    PRINT 'Done - Carbynetech_Project.DefaultBillable is now a string column (Billable/NonBillable/PartialBillable).';
    SELECT DefaultBillable, COUNT(*) AS ProjectCount FROM Carbynetech_Project GROUP BY DefaultBillable;
END
ELSE
    PRINT 'ERROR: Carbynetech_Project.DefaultBillable is not a string column after this run - unexpected schema state, look closer before proceeding.';
GO
