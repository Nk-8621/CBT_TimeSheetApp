-- =====================================================================
-- 08_check_project_type_migration_state.sql
--
-- READ-ONLY diagnostic. Checks which parts of
-- 07_add_project_type_and_employee_project_allocation.sql are already
-- present in this database, without changing anything. Safe to run any
-- number of times, in any state (before the old script, after the old
-- script, after the new script, mid-way, whatever).
--
-- For each of the 7 steps in the migration script, this prints a
-- PRESENT / MISSING line (or the actual column widths, where that's the
-- thing worth seeing) so you can tell exactly what 07_...sql would still
-- need to do on this database before you run it.
-- =====================================================================

USE [Carbynetech_TimeSheet_Application];
GO
SET NOCOUNT ON;

PRINT '=====================================================================';
PRINT ' STEP 1 — Carbynetech_TaskCategory -> Carbynetech_ProjectType rename';
PRINT '=====================================================================';
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Carbynetech_ProjectType')
BEGIN
    PRINT 'PRESENT: Carbynetech_ProjectType exists (renamed already).';
    SELECT COUNT(*) AS ProjectType_RowCount FROM Carbynetech_ProjectType;
    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Carbynetech_ProjectType') AND name = 'ProjectTypeId')
        PRINT '  PK column already named ProjectTypeId.';
    ELSE
        PRINT '  WARNING: PK column is not named ProjectTypeId — unexpected state, look closer before running 07_...sql.';
END
ELSE IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Carbynetech_TaskCategory')
BEGIN
    PRINT 'MISSING: still called Carbynetech_TaskCategory — rename has not run yet.';
    SELECT COUNT(*) AS TaskCategory_RowCount FROM Carbynetech_TaskCategory;
END
ELSE
    PRINT 'ERROR: neither Carbynetech_TaskCategory nor Carbynetech_ProjectType exists — unexpected schema state.';
GO

PRINT '';
PRINT '=====================================================================';
PRINT ' STEP 2 — Project Type template tables';
PRINT '=====================================================================';
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Carbynetech_ProjectTypeModuleTemplate')
BEGIN
    PRINT 'PRESENT: Carbynetech_ProjectTypeModuleTemplate exists.';
    SELECT COUNT(*) AS ProjectTypeModuleTemplate_RowCount FROM Carbynetech_ProjectTypeModuleTemplate;
END
ELSE PRINT 'MISSING: Carbynetech_ProjectTypeModuleTemplate does not exist.';

IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Carbynetech_ProjectTypeTaskTemplate')
BEGIN
    PRINT 'PRESENT: Carbynetech_ProjectTypeTaskTemplate exists.';
    SELECT COUNT(*) AS ProjectTypeTaskTemplate_RowCount FROM Carbynetech_ProjectTypeTaskTemplate;
END
ELSE PRINT 'MISSING: Carbynetech_ProjectTypeTaskTemplate does not exist.';
GO

PRINT '';
PRINT '=====================================================================';
PRINT ' STEP 3 — Carbynetech_Module.TaskCategoryId -> ProjectTypeId';
PRINT '=====================================================================';
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Carbynetech_Module') AND name = 'ProjectTypeId')
BEGIN
    PRINT 'PRESENT: Carbynetech_Module.ProjectTypeId exists (renamed already).';
    SELECT
        c.name        AS ColumnName,
        ty.name       AS DataType,
        c.is_nullable AS IsNullable
    FROM sys.columns c
    JOIN sys.types ty ON ty.user_type_id = c.user_type_id
    WHERE c.object_id = OBJECT_ID('Carbynetech_Module') AND c.name = 'ProjectTypeId';

    SELECT fk.name AS FkName,
           CASE fk.delete_referential_action WHEN 0 THEN 'NO ACTION' WHEN 1 THEN 'CASCADE' WHEN 2 THEN 'SET NULL' WHEN 3 THEN 'SET DEFAULT' END AS OnDeleteAction
    FROM sys.foreign_keys fk
    JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
    JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
    WHERE fk.parent_object_id = OBJECT_ID('Carbynetech_Module') AND c.name = 'ProjectTypeId';

    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Carbynetech_Module') AND name = 'ProjectTypeId' AND is_nullable = 1)
        PRINT '  Nullable: yes (expected).';
    ELSE
        PRINT '  Nullable: NO — 07_...sql''s Step 3b still needs to run (ALTER COLUMN ... NULL).';
END
ELSE IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Carbynetech_Module') AND name = 'TaskCategoryId')
    PRINT 'MISSING: Carbynetech_Module still has TaskCategoryId — rename has not run yet.';
ELSE
    PRINT 'ERROR: neither TaskCategoryId nor ProjectTypeId found on Carbynetech_Module — unexpected state.';
GO

PRINT '';
PRINT '=====================================================================';
PRINT ' STEP 4 — new Carbynetech_Project columns';
PRINT '=====================================================================';
SELECT
    c.name                                    AS ColumnName,
    ty.name                                    AS DataType,
    CASE WHEN ty.name IN ('nvarchar','nchar') AND c.max_length <> -1 THEN c.max_length / 2 ELSE c.max_length END AS MaxLength_Chars,
    c.is_nullable                              AS IsNullable
FROM sys.columns c
JOIN sys.types ty ON ty.user_type_id = c.user_type_id
WHERE c.object_id = OBJECT_ID('Carbynetech_Project')
  AND c.name IN ('ProjectTypeId','ProjectTech','BillingType','CustomerPO','Notes','NeedsReview',
                 'ProjectLeadEmployeeId','ProjectManagerEmployeeId','DeliveryHeadEmployeeId')
ORDER BY c.column_id;

PRINT '';
PRINT 'Expected by 07_...sql (Step 4):';
PRINT '  ProjectTypeId              int, null';
PRINT '  ProjectTech                nvarchar(200), null';
PRINT '  BillingType                nvarchar(30), null';
PRINT '  CustomerPO                 nvarchar(100), null';
PRINT '  Notes                      nvarchar(2000), null';
PRINT '  NeedsReview                bit, not null (default 0)';
PRINT '  ProjectLeadEmployeeId      int, null';
PRINT '  ProjectManagerEmployeeId   int, null';
PRINT '  DeliveryHeadEmployeeId     int, null';
PRINT 'Any column above missing from the result set has not been added yet.';
PRINT 'If ProjectTech / CustomerPO / Notes already exist but with a SMALLER width than shown above,';
PRINT 'that''s the old-script/new-script column-width mismatch we flagged earlier — 07_...sql''s';
PRINT '"IF NOT EXISTS" guard will skip widening them since the column already exists.';
GO

PRINT '';
PRINT '=====================================================================';
PRINT ' STEP 5 — "Pending Classification" internal Account';
PRINT '=====================================================================';
IF EXISTS (SELECT 1 FROM Carbynetech_Account WHERE Name = 'Pending Classification')
BEGIN
    PRINT 'PRESENT:';
    SELECT AccountId, DepartmentId, Name, AccountType FROM Carbynetech_Account WHERE Name = 'Pending Classification';
END
ELSE PRINT 'MISSING: no "Pending Classification" Account yet.';
GO

PRINT '';
PRINT '=====================================================================';
PRINT ' STEP 6 — the 7 seeded Project Types + their templates';
PRINT '=====================================================================';
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Carbynetech_ProjectType')
BEGIN
    ;WITH Expected(Code, Name) AS (
        SELECT 'PT_AGILE_SCRUM', 'Agile Scrum' UNION ALL
        SELECT 'PT_AMS_SUPPORT', 'AMS Support' UNION ALL
        SELECT 'PT_ACTIVATE', 'Activate' UNION ALL
        SELECT 'PT_HR', 'HR' UNION ALL
        SELECT 'PT_INSIDE_SALES', 'Inside Sales' UNION ALL
        SELECT 'PT_MARKETING', 'Marketing' UNION ALL
        SELECT 'PT_PRESALES', 'Presales'
    )
    SELECT
        e.Code,
        e.Name,
        CASE WHEN t.ProjectTypeId IS NULL THEN 'MISSING' ELSE 'PRESENT' END AS Status,
        (SELECT COUNT(*) FROM Carbynetech_ProjectTypeModuleTemplate mt WHERE mt.ProjectTypeId = t.ProjectTypeId) AS ModuleTemplateCount,
        (SELECT COUNT(*) FROM Carbynetech_ProjectTypeTaskTemplate tt
            JOIN Carbynetech_ProjectTypeModuleTemplate mt ON mt.ProjectTypeModuleTemplateId = tt.ProjectTypeModuleTemplateId
            WHERE mt.ProjectTypeId = t.ProjectTypeId) AS TaskTemplateCount
    FROM Expected e
    LEFT JOIN Carbynetech_ProjectType t ON t.Code = e.Code
    ORDER BY e.Code;
END
ELSE PRINT 'Carbynetech_ProjectType table does not exist yet — Step 1 has not run.';
GO

PRINT '';
PRINT '=====================================================================';
PRINT ' STEP 7 — Carbynetech_EmployeeProjectAllocation';
PRINT '=====================================================================';
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Carbynetech_EmployeeProjectAllocation')
BEGIN
    PRINT 'PRESENT: Carbynetech_EmployeeProjectAllocation exists.';
    SELECT COUNT(*) AS EmployeeProjectAllocation_RowCount FROM Carbynetech_EmployeeProjectAllocation;
END
ELSE PRINT 'MISSING: Carbynetech_EmployeeProjectAllocation does not exist yet.';
GO

PRINT '';
PRINT '=====================================================================';
PRINT ' Done. Compare the PRESENT/MISSING lines above against';
PRINT ' 07_add_project_type_and_employee_project_allocation.sql''s 7 steps';
PRINT ' to see exactly what it would still do if you ran it now.';
PRINT '=====================================================================';
