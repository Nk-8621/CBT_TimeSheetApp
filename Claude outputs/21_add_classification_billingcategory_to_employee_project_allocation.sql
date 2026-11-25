-- =====================================================================
-- 21_add_classification_billingcategory_to_employee_project_allocation.sql
--
-- New feature: an employee's billing classification (Billable/NonBillable/
-- PartialBillable) and billing category (AMS/T&M/FB/OH) are now set by the
-- admin at project-ALLOCATION time - one decision per (employee, project)
-- pair - instead of being picked by the employee on every timesheet line.
-- This adds the two columns that hold that decision on
-- Carbynetech_EmployeeProjectAllocation (see EmployeeProjectAllocation.cs /
-- EmployeeProjectAllocationConfiguration in PeopleConfigurations.cs).
--
-- Same vocabulary as everywhere else (Carbynetech_Project.DefaultBillable,
-- Carbynetech_TimeEntry.Classification/BillingCategory) - see
-- BillingClassificationRules.cs for the allowed combinations.
--
-- Existing rows (allocations made before this feature shipped) are
-- backfilled from that project's own DefaultBillable, with no billing
-- category guessed - BillingCategory stays NULL for those until an admin
-- revisits that allocation from Master Data > Resources > Projects and
-- fills it in. Nothing blocks on this: BillingClassificationRules.Validate
-- always allows a NULL category regardless of classification, so existing
-- employees keep logging time against these allocations without
-- interruption.
--
-- Idempotent - every step checks its own pre-condition, same convention as
-- scripts 18/19/20. Safe to re-run from any partially-applied state.
-- =====================================================================

USE [Carbynetech_TimeSheet_Application];
GO
SET NOCOUNT ON;

-- Step 1: add Classification (nullable for now - backfilled in step 2,
-- locked to NOT NULL in step 3).
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Carbynetech_EmployeeProjectAllocation') AND name = 'Classification')
BEGIN
    EXEC('ALTER TABLE Carbynetech_EmployeeProjectAllocation ADD Classification nvarchar(20) NULL;');
    PRINT 'Added Carbynetech_EmployeeProjectAllocation.Classification (nvarchar(20)).';
END
ELSE
    PRINT 'Carbynetech_EmployeeProjectAllocation.Classification already exists - skipping add.';
GO

-- Step 2: backfill any not-yet-backfilled rows from the allocated
-- project's own DefaultBillable. Safe to re-run - only touches rows still
-- NULL.
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Carbynetech_EmployeeProjectAllocation') AND name = 'Classification')
BEGIN
    DECLARE @RowsBackfilled INT;
    EXEC('
        UPDATE epa
        SET Classification = p.DefaultBillable
        FROM Carbynetech_EmployeeProjectAllocation epa
        JOIN Carbynetech_Project p ON p.ProjectId = epa.ProjectId
        WHERE epa.Classification IS NULL;
    ');
    SET @RowsBackfilled = @@ROWCOUNT;
    PRINT CONCAT(@RowsBackfilled, ' allocation row(s) backfilled just now (Classification <- that project''s DefaultBillable).');
END
GO

-- Step 3: lock Classification to NOT NULL once every row has a value.
IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('Carbynetech_EmployeeProjectAllocation') AND name = 'Classification' AND is_nullable = 1
)
BEGIN
    IF EXISTS (SELECT 1 FROM Carbynetech_EmployeeProjectAllocation WHERE Classification IS NULL)
    BEGIN
        PRINT 'NOT locked to NOT NULL yet: some rows still have a NULL Classification (their Project may itself be missing DefaultBillable). Re-run this script after checking:';
        SELECT EmployeeId, ProjectId FROM Carbynetech_EmployeeProjectAllocation WHERE Classification IS NULL;
    END
    ELSE
    BEGIN
        EXEC('ALTER TABLE Carbynetech_EmployeeProjectAllocation ALTER COLUMN Classification nvarchar(20) NOT NULL;');
        PRINT 'Carbynetech_EmployeeProjectAllocation.Classification set to NOT NULL.';
    END
END
GO

-- Step 4: add BillingCategory (nullable - stays that way permanently;
-- PartialBillable never takes one, and NULL is a normal, fully-supported
-- state for Billable/NonBillable too, same as Carbynetech_TimeEntry).
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Carbynetech_EmployeeProjectAllocation') AND name = 'BillingCategory')
BEGIN
    EXEC('ALTER TABLE Carbynetech_EmployeeProjectAllocation ADD BillingCategory nvarchar(10) NULL;');
    PRINT 'Added Carbynetech_EmployeeProjectAllocation.BillingCategory (nvarchar(10)).';
END
ELSE
    PRINT 'Carbynetech_EmployeeProjectAllocation.BillingCategory already exists - skipping add.';
GO

-- Final status.
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Carbynetech_EmployeeProjectAllocation') AND name = 'Classification' AND is_nullable = 0)
   AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Carbynetech_EmployeeProjectAllocation') AND name = 'BillingCategory')
BEGIN
    PRINT 'Done - Classification (NOT NULL) and BillingCategory (nullable) are both present on Carbynetech_EmployeeProjectAllocation.';
    SELECT Classification, BillingCategory, COUNT(*) AS AllocationCount
    FROM Carbynetech_EmployeeProjectAllocation
    GROUP BY Classification, BillingCategory
    ORDER BY Classification, BillingCategory;
END
ELSE
    PRINT 'ERROR: Classification/BillingCategory are not both in their expected final state - unexpected schema state, look closer before proceeding.';
GO
