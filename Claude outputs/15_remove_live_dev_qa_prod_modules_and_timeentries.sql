-- =====================================================================
-- 15_remove_live_dev_qa_prod_modules_and_timeentries.sql
--
-- Supersedes script 14 for this cleanup. Same target as before - the
-- live "DEV"/"QA"/"PROD" Modules and their Tasks on "TechEff for India
-- Business", "Techeff for Europe - Chevigny", and "SCFS" - but per
-- Babu's confirmation, this version also deletes any Carbynetech_TimeEntry
-- rows already logged against them, instead of stopping like script 14 did.
--
-- *** THIS PERMANENTLY DELETES LOGGED TIMESHEET HOURS. THERE IS NO UNDO
-- SHORT OF RESTORING THE DATABASE FROM A BACKUP. Run script 14 first
-- (or just the SELECT block below) if you have not already seen exactly
-- which TimeEntry rows exist, so you know what you're removing. ***
--
-- Delete order (children before parents, per the FK constraints):
--   1. Carbynetech_TimeEntry rows pointing at these Modules/Tasks
--   2. Carbynetech_Task rows under these Modules
--   3. Carbynetech_Module rows themselves
-- =====================================================================

USE [Carbynetech_TimeSheet_Application];
GO
SET NOCOUNT ON;

DECLARE @TargetModules TABLE (ModuleId int);
INSERT INTO @TargetModules (ModuleId)
SELECT m.ModuleId
FROM Carbynetech_Module m
JOIN Carbynetech_Project p ON p.ProjectId = m.ProjectId
WHERE m.Name IN ('DEV', 'QA', 'PROD')
  AND p.Name IN ('TechEff for India Business', 'Techeff for Europe - Chevigny', 'SCFS');

PRINT '=== Modules about to be deleted ===';
SELECT m.ModuleId, m.Name AS ModuleName, p.Name AS ProjectName
FROM Carbynetech_Module m
JOIN Carbynetech_Project p ON p.ProjectId = m.ProjectId
WHERE m.ModuleId IN (SELECT ModuleId FROM @TargetModules);

PRINT '';
PRINT '=== Their Tasks about to be deleted ===';
SELECT t.TaskId, t.Name AS TaskName, m.Name AS ModuleName
FROM Carbynetech_Task t
JOIN Carbynetech_Module m ON m.ModuleId = t.ModuleId
WHERE t.ModuleId IN (SELECT ModuleId FROM @TargetModules);

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
PRINT CONCAT(@@ROWCOUNT, ' Module row(s) deleted (expect 9 - 3 modules x 3 projects).');

PRINT '';
PRINT 'Done.';
GO
