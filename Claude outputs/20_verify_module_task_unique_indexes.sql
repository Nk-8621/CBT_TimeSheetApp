-- =====================================================================
-- 20_verify_module_task_unique_indexes.sql
--
-- The EF Core model (ModuleConfiguration / WorkTaskConfiguration) already
-- declares:
--   Carbynetech_Module: UNIQUE (ProjectId, Name)
--   Carbynetech_Task:   UNIQUE (ModuleId, Name)
-- but this app has no EF Core Migrations, so a Fluent API declaration by
-- itself proves nothing about what actually exists in THIS database - it
-- only takes effect if a matching index/constraint was created by hand at
-- some point. This script checks for both, and creates whichever one is
-- missing, so the two live tables definitely match the app's own model.
--
-- This is the DB-level backstop behind the new app-level duplicate checks
-- (RequireNoDuplicateModuleNameAsync / RequireNoDuplicateTaskNameAsync in
-- MasterDataService.cs) - same belt-and-suspenders reasoning as
-- Carbynetech_Project.Code and the Carbynetech_Holiday date/location/
-- client index (scripts 18 and 19).
--
-- Idempotent: does nothing to a table whose unique index already exists.
-- Safe to run regardless of the outcome - widening/adding a unique index
-- only ever fails if genuine duplicate (ProjectId, Name) or (ModuleId,
-- Name) rows already exist, which the checks below report clearly instead
-- of a generic error.
-- =====================================================================

USE [Carbynetech_TimeSheet_Application];
GO
SET NOCOUNT ON;

-- ---- Carbynetech_Module (ProjectId, Name) ----
IF EXISTS (
    SELECT 1 FROM sys.indexes i
    WHERE i.object_id = OBJECT_ID('Carbynetech_Module')
      AND i.is_unique = 1
      AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id) = 2
      AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND c.name = 'ProjectId')
      AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND c.name = 'Name')
)
    PRINT 'Carbynetech_Module already has a unique index on (ProjectId, Name) - nothing to do.';
ELSE
BEGIN
    -- Report any existing duplicates first, so a failure below points
    -- straight at the offending rows instead of a bare constraint error.
    IF EXISTS (
        SELECT ProjectId, Name FROM Carbynetech_Module
        GROUP BY ProjectId, Name HAVING COUNT(*) > 1
    )
    BEGIN
        PRINT 'NOT created: Carbynetech_Module already has duplicate (ProjectId, Name) rows. Resolve these first, then re-run this script:';
        SELECT ProjectId, Name, COUNT(*) AS DupCount
        FROM Carbynetech_Module
        GROUP BY ProjectId, Name HAVING COUNT(*) > 1;
    END
    ELSE
    BEGIN
        CREATE UNIQUE NONCLUSTERED INDEX IX_Carbynetech_Module_ProjectId_Name
            ON Carbynetech_Module (ProjectId, Name);
        PRINT 'Created unique index IX_Carbynetech_Module_ProjectId_Name on (ProjectId, Name).';
    END
END
GO

-- ---- Carbynetech_Task (ModuleId, Name) ----
IF EXISTS (
    SELECT 1 FROM sys.indexes i
    WHERE i.object_id = OBJECT_ID('Carbynetech_Task')
      AND i.is_unique = 1
      AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id) = 2
      AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND c.name = 'ModuleId')
      AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND c.name = 'Name')
)
    PRINT 'Carbynetech_Task already has a unique index on (ModuleId, Name) - nothing to do.';
ELSE
BEGIN
    IF EXISTS (
        SELECT ModuleId, Name FROM Carbynetech_Task
        GROUP BY ModuleId, Name HAVING COUNT(*) > 1
    )
    BEGIN
        PRINT 'NOT created: Carbynetech_Task already has duplicate (ModuleId, Name) rows. Resolve these first, then re-run this script:';
        SELECT ModuleId, Name, COUNT(*) AS DupCount
        FROM Carbynetech_Task
        GROUP BY ModuleId, Name HAVING COUNT(*) > 1;
    END
    ELSE
    BEGIN
        CREATE UNIQUE NONCLUSTERED INDEX IX_Carbynetech_Task_ModuleId_Name
            ON Carbynetech_Task (ModuleId, Name);
        PRINT 'Created unique index IX_Carbynetech_Task_ModuleId_Name on (ModuleId, Name).';
    END
END
GO
