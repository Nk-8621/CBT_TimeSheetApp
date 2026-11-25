-- =====================================================================
-- 17_remove_all_remaining_dev_qa_prod_modules.sql
--
-- Scripts 14-16 were scoped to just 3 projects (TechEff for India
-- Business, Techeff for Europe - Chevigny, SCFS). Turns out DEV/QA/PROD
-- exist much more broadly - Amararaja Distribution and Procurement
-- Analytics, Gland Pharma Analytics, Berco Analytics, Schott Analytics,
-- Internal Demo Projects, Wockhardt Analytics, and likely others below
-- what was visible in the screenshot. This version drops the project
-- filter entirely and removes DEV/QA/PROD (and their Tasks) on EVERY
-- project that still has them.
--
-- *** THIS PERMANENTLY DELETES ANY LOGGED TIMESHEET HOURS AGAINST THESE
-- MODULES/TASKS TOO, ACROSS ALL PROJECTS. THERE IS NO UNDO SHORT OF
-- RESTORING THE DATABASE FROM A BACKUP. ***
--
-- Delete order (children before parents, per the FK constraints):
--   1. Carbynetech_TimeEntry rows pointing at any remaining DEV/QA/PROD
--      Module or their Tasks
--   2. Carbynetech_Task rows under those Modules
--   3. Carbynetech_Module rows themselves
-- =====================================================================

USE [Carbynetech_TimeSheet_Application];
GO
SET NOCOUNT ON;

DECLARE @TargetModules TABLE (ModuleId int);
INSERT INTO @TargetModules (ModuleId)
SELECT ModuleId
FROM Carbynetech_Module
WHERE Name IN ('DEV', 'QA', 'PROD');

PRINT '=== All remaining DEV/QA/PROD Modules about to be deleted (any project) ===';
SELECT m.ModuleId, m.Name AS ModuleName, p.Name AS ProjectName
FROM Carbynetech_Module m
JOIN Carbynetech_Project p ON p.ProjectId = m.ProjectId
WHERE m.ModuleId IN (SELECT ModuleId FROM @TargetModules)
ORDER BY p.Name, m.Name;

PRINT '';
PRINT '=== Their Tasks about to be deleted ===';
SELECT t.TaskId, t.Name AS TaskName, m.Name AS ModuleName, p.Name AS ProjectName
FROM Carbynetech_Task t
JOIN Carbynetech_Module m ON m.ModuleId = t.ModuleId
JOIN Carbynetech_Project p ON p.ProjectId = m.ProjectId
WHERE t.ModuleId IN (SELECT ModuleId FROM @TargetModules)
ORDER BY p.Name, m.Name, t.Name;

PRINT '';
PRINT '=== TimeEntry rows about to be PERMANENTLY deleted ===';
SELECT te.TimeEntryId, te.EmployeeId, te.WeekStartDate, te.ModuleId, te.TaskId,
       (te.MondayHours + te.TuesdayHours + te.WednesdayHours + te.ThursdayHours + te.FridayHours + te.SaturdayHours + te.SundayHours) AS TotalHours
FROM Carbynetech_TimeEntry te
WHERE te.ModuleId IN (SELECT ModuleId FROM @TargetModules)
   OR te.TaskId IN (SELECT TaskId FROM Carbynetech_Task WHERE ModuleId IN (SELECT ModuleId FROM @TargetModules));

-- ---------------------------------------------------------------------
-- 1. TimeEntry rows first (children of both Module and Task)
-- ---------------------------------------------------------------------
DELETE FROM Carbynetech_TimeEntry
WHERE ModuleId IN (SELECT ModuleId FROM @TargetModules)
   OR TaskId IN (SELECT TaskId FROM Carbynetech_Task WHERE ModuleId IN (SELECT ModuleId FROM @TargetModules));
PRINT CONCAT(@@ROWCOUNT, ' TimeEntry row(s) permanently deleted.');

-- ---------------------------------------------------------------------
-- 2. Tasks next
-- ---------------------------------------------------------------------
DELETE FROM Carbynetech_Task WHERE ModuleId IN (SELECT ModuleId FROM @TargetModules);
PRINT CONCAT(@@ROWCOUNT, ' Task row(s) deleted.');

-- ---------------------------------------------------------------------
-- 3. Modules last
-- ---------------------------------------------------------------------
DELETE FROM Carbynetech_Module WHERE ModuleId IN (SELECT ModuleId FROM @TargetModules);
PRINT CONCAT(@@ROWCOUNT, ' Module row(s) deleted.');

PRINT '';
PRINT 'Done. Refresh the Master Data > Modules screen - DEV/QA/PROD should be gone everywhere now.';
GO
