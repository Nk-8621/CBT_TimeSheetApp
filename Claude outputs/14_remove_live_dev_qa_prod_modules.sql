-- =====================================================================
-- 14_remove_live_dev_qa_prod_modules.sql
--
-- Removes the LIVE "DEV"/"QA"/"PROD" Modules (and their Tasks) sitting
-- on the actual projects "TechEff for India Business", "Techeff for
-- Europe - Chevigny", and "SCFS" - the ones visible on the Master Data
-- > Modules screen. This is different from script 13, which only
-- removed the reusable Project Type TEMPLATES and never touched these.
--
-- Matches by Module Name + Project (not by ModuleId range) - an ID
-- range like "BETWEEN 1 AND 39" isn't safe here, since ModuleId is one
-- shared auto-increment sequence across every project in the system,
-- not per-project. A range could catch modules from unrelated projects
-- that happen to fall in that ID window. Matching by name/project only
-- touches exactly what's intended.
--
-- SAFETY CHECK: if any Carbynetech_TimeEntry rows already point at these
-- Modules/Tasks (i.e. someone has logged timesheet hours against them),
-- this script prints what it found and STOPS without deleting anything.
-- Deleting a Task/Module that has logged hours would either fail on the
-- same FK_Carbynetech_TimeEntry_Task/Module constraint you just hit, or
-- (if you removed that constraint) silently destroy real timesheet
-- history - so this is a deliberate stop, not a bug.
--
-- Correct delete order once the safety check is clear: Tasks first
-- (children), then Modules (parents) - which is what caused your error.
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

-- ---------------------------------------------------------------------
-- Safety check: any timesheet hours already logged against these?
-- ---------------------------------------------------------------------
IF EXISTS (
    SELECT 1 FROM Carbynetech_TimeEntry te
    WHERE te.ModuleId IN (SELECT ModuleId FROM @TargetModules)
       OR te.TaskId IN (SELECT TaskId FROM Carbynetech_Task WHERE ModuleId IN (SELECT ModuleId FROM @TargetModules))
)
BEGIN
    PRINT '';
    PRINT 'STOPPED: timesheet hours are already logged against one or more of these Modules/Tasks - deleting would destroy real data. Nothing was changed. Details:';
    SELECT te.TimeEntryId, te.EmployeeId, te.WeekStartDate, te.ModuleId, te.TaskId,
           (te.MondayHours + te.TuesdayHours + te.WednesdayHours + te.ThursdayHours + te.FridayHours + te.SaturdayHours + te.SundayHours) AS TotalHours
    FROM Carbynetech_TimeEntry te
    WHERE te.ModuleId IN (SELECT ModuleId FROM @TargetModules)
       OR te.TaskId IN (SELECT TaskId FROM Carbynetech_Task WHERE ModuleId IN (SELECT ModuleId FROM @TargetModules));
    RETURN;
END

-- ---------------------------------------------------------------------
-- Clear to delete: Tasks first (children), then Modules (parents)
-- ---------------------------------------------------------------------
DELETE FROM Carbynetech_Task WHERE ModuleId IN (SELECT ModuleId FROM @TargetModules);
PRINT CONCAT(@@ROWCOUNT, ' Task row(s) deleted.');

DELETE FROM Carbynetech_Module WHERE ModuleId IN (SELECT ModuleId FROM @TargetModules);
PRINT CONCAT(@@ROWCOUNT, ' Module row(s) deleted (expect 9 - 3 modules x 3 projects).');

PRINT '';
PRINT 'Done.';
GO
