-- =====================================================================
-- 16_remove_live_dev_module_and_timeentries.sql
--
-- Same as script 15, but scoped to ONLY the "DEV" Module (and its Tasks
-- and any logged TimeEntry rows) on "TechEff for India Business",
-- "Techeff for Europe - Chevigny", and "SCFS" - per Babu's request to
-- trial this on DEV first, then do QA and PROD as a separate follow-up
-- once this one looks right.
--
-- *** THIS PERMANENTLY DELETES LOGGED TIMESHEET HOURS FOR "DEV". THERE
-- IS NO UNDO SHORT OF RESTORING THE DATABASE FROM A BACKUP. ***
--
-- Delete order (children before parents, per the FK constraints):
--   1. Carbynetech_TimeEntry rows pointing at the DEV Module/its Tasks
--   2. Carbynetech_Task rows under the DEV Module
--   3. Carbynetech_Module rows themselves (DEV only)
-- =====================================================================

USE [Carbynetech_TimeSheet_Application];
GO
SET NOCOUNT ON;

DECLARE @TargetModules TABLE (ModuleId int);
INSERT INTO @TargetModules (ModuleId)
SELECT m.ModuleId
FROM Carbynetech_Module m
JOIN Carbynetech_Project p ON p.ProjectId = m.ProjectId
WHERE m.Name = 'DEV'
  AND p.Name IN ('TechEff for India Business', 'Techeff for Europe - Chevigny', 'SCFS');

PRINT '=== DEV Modules about to be deleted ===';
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
PRINT CONCAT(@@ROWCOUNT, ' Module row(s) deleted (expect 3 - DEV on 3 projects).');

PRINT '';
PRINT 'Done with DEV. Once this looks right, ask and I''ll send the same thing scoped to QA, then PROD.';
GO
