-- =====================================================================
-- 09_seed_sap_customer_accounts_and_projects.sql
--
-- Adds the Master Data (Accounts + Projects) Babu supplied on 2026-09-15:
-- 12 SAP-division customer/internal Accounts, and 7 of the 8 Projects
-- listed against them.
--
-- Idempotent: safe to re-run. Accounts are matched by Name (unique),
-- Projects by Code (unique) - re-running only inserts what's missing,
-- it never overwrites an existing row.
--
-- ASSUMPTIONS - please read and confirm/correct before running:
--
--   1. Department: all 12 accounts are filed under a Department with
--      Code = 'SAP' (created below if it doesn't already exist). If your
--      real SAP department already exists under a different Code, change
--      @SapDeptCode below instead of letting this create a second one.
--
--   2. "SAP-Internal" (the Type column for that one row) has no matching
--      AccountType in code - only Customer/Internal exist - so it is
--      mapped to AccountType = Internal here.
--
--   3. Project.Code (unique, max 20 chars) wasn't supplied for any row,
--      so one was generated per your naming rule ("prefix with
--      department" - e.g. Berco under SAP -> SAP_BERCO): SAP_<ACCOUNT>,
--      e.g. SAP_ETHIHAD, SAP_TVSEUROGRIP. Project Name is allowed to
--      repeat (only Code is unique), which is why "SAP Support - AMS"
--      is fine appearing 3 times against 3 different accounts/codes.
--
--   4. The "Customer PO" column in your paste was "Yes" for every row -
--      that reads as answering "is there a PO on file?", not an actual
--      PO number, so Project.CustomerPO is left NULL here. Fill in the
--      real PO numbers later from the Master Data > Projects screen.
--
--   5. The trailing "Yes" / "Active" columns are mapped to
--      DefaultBillable = 1 and IsActive = 1 for all 7 rows.
--
--   6. "Energy Management - IOT" lists its account as "VST", which is
--      NOT one of the 12 accounts you pasted. This script assumes VST
--      already exists as an Account from earlier work, and will only
--      print a warning (inserting nothing for that one project) if it's
--      missing, rather than guess at a Department for a brand-new VST
--      account.
--
--   7. Spelling kept exactly as given ("Ethihad", "TVS Eurpgrip") - edit
--      the VALUES list below first if these are typos for "Etihad" /
--      "TVS Eurogrip".
--
--   8. The 8th project row in your paste ("...Others" billing type) had
--      no Project Name and no Account - it looked cut off. It is
--      deliberately NOT included here. Send over the missing Project
--      Name + Account and I'll add it in a follow-up script.
--
--   9. ProjectTypeId and ProjectTech are left NULL - classify these from
--      the Master Data > Projects screen (retroactive classification)
--      once you decide each project's type.
-- =====================================================================

USE [Carbynetech_TimeSheet_Application];
GO
SET NOCOUNT ON;

DECLARE @SapDeptCode nvarchar(30) = 'SAP';
DECLARE @Now datetime2 = SYSUTCDATETIME();

-- ---------------------------------------------------------------------
-- Step 1: ensure the SAP department exists
-- ---------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM Carbynetech_Department WHERE Code = @SapDeptCode)
BEGIN
    INSERT INTO Carbynetech_Department (Code, Name, ParentDepartmentId, CreatedAt)
    VALUES (@SapDeptCode, 'SAP', NULL, @Now);
    PRINT 'Created Department: SAP';
END
ELSE
    PRINT 'Department SAP already exists - reusing it.';

DECLARE @SapDeptId int = (SELECT DepartmentId FROM Carbynetech_Department WHERE Code = @SapDeptCode);

-- ---------------------------------------------------------------------
-- Step 2: the 12 Accounts (11 Customer + 1 Internal)
-- ---------------------------------------------------------------------
DECLARE @Accounts TABLE (Name nvarchar(200), AccountType nvarchar(20));
INSERT INTO @Accounts (Name, AccountType) VALUES
    ('Amararaja',       'Customer'),
    ('Gland Pharma',    'Customer'),
    ('Berco',           'Customer'),
    ('Schott',          'Customer'),
    ('Wockhardt',       'Customer'),
    ('LTTS',            'Customer'),
    ('DBMSC Steel',     'Customer'),
    ('Ethihad',         'Customer'),
    ('TVS Eurpgrip',    'Customer'),
    ('TVS Air Springs', 'Customer'),
    ('Coromandel',      'Customer'),
    ('SAP-Internal',    'Internal');

MERGE Carbynetech_Account AS target
USING @Accounts AS src
    ON target.Name = src.Name
WHEN NOT MATCHED BY TARGET THEN
    INSERT (DepartmentId, Name, AccountType, CreatedAt)
    VALUES (@SapDeptId, src.Name, src.AccountType, @Now);

PRINT CONCAT(@@ROWCOUNT, ' new Account row(s) inserted (0 means all 12 already existed).');

-- ---------------------------------------------------------------------
-- Step 3: flag the one account this batch does NOT create (VST)
-- ---------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM Carbynetech_Account WHERE Name = 'VST')
    PRINT 'WARNING: Account "VST" not found - "Energy Management - IOT" will be SKIPPED below. Everything else in this script still runs normally.';
ELSE
    PRINT 'Account "VST" found - "Energy Management - IOT" will be linked to it.';

-- ---------------------------------------------------------------------
-- Step 4: the 7 Projects (8th/incomplete row intentionally excluded - see assumption 8)
-- ---------------------------------------------------------------------
DECLARE @Projects TABLE (
    Code nvarchar(20), Name nvarchar(200), AccountName nvarchar(200),
    BillingType nvarchar(30), DefaultBillable bit, IsActive bit
);
INSERT INTO @Projects (Code, Name, AccountName, BillingType, DefaultBillable, IsActive) VALUES
    ('SAP_AMARARAJA',    'PRPO Automation',               'Amararaja',       'Fixed',       1, 1),
    ('SAP_ETHIHAD',      'SAP Support - AMS',              'Ethihad',         'Consumption', 1, 1),
    ('SAP_TVSEUROGRIP',  'SAP Support - AMS',              'TVS Eurpgrip',    'Consumption', 1, 1),
    ('SAP_TVSAIRSPRINGS','SAP Support - AMS',              'TVS Air Springs', 'Consumption', 1, 1),
    ('SAP_COROMANDEL',   'Coromandel - Resource staffing', 'Coromandel',      'T&M',         1, 1),
    ('SAP_DBMSCSTEEL',   'DBMSC Analytics Support',        'DBMSC Steel',     'Consumption', 1, 1),
    ('VST_ENERGYMGMT',   'Energy Management - IOT',        'VST',             'Fixed',       1, 1);

MERGE Carbynetech_Project AS target
USING (
    SELECT p.Code, p.Name, a.AccountId, p.BillingType, p.DefaultBillable, p.IsActive
    FROM @Projects p
    JOIN Carbynetech_Account a ON a.Name = p.AccountName
) AS src
    ON target.Code = src.Code
WHEN NOT MATCHED BY TARGET THEN
    INSERT (AccountId, Code, Name, DefaultBillable, IsActive, BillingType, CustomerPO, CreatedAt)
    VALUES (src.AccountId, src.Code, src.Name, src.DefaultBillable, src.IsActive, src.BillingType, NULL, @Now);

PRINT CONCAT(@@ROWCOUNT, ' new Project row(s) inserted (VST row included only if the VST account already existed; 6 max otherwise).');

PRINT '';
PRINT 'Done. Review the Master Data > Accounts and Projects screens to confirm, then fill in CustomerPO numbers and Project Type classification.';
GO
