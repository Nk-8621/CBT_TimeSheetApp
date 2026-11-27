-- =====================================================================
-- 45_techeff_hamsini_route_reset_week.sql
--
-- TECHEFF DRY RUN #2 (27 Nov 2026)
--   Approval route for TechEff: Level 1 = Hamsini Narayana Murthy (CBT1152),
--   Level 2 = Srinivas Karri (CBT1000, Hamsini's manager).
--
--   1. Makes Hamsini the manager of these TechEff employees:
--        CBT1267 Durga Prasad, CBT1269 Prasuna, CBT1274 Manoj,
--        CBT1284 Walmeek, CBT1230 Rajeev,
--        CBT1302 Lakshmi, CBT1312 Likhitha, CBT1120 Madhuri,
--        CBT1276 Tamarapalli Praveen, CBT1331 Umesh, CBT1132 Vinod.
--      Not included: CBT1268 Nanda kishore, CBT1128 Aravind (not TechEff),
--      CBT1152 Hamsini (she is the approver).
--   2. Clears week 16-22 Nov 2026 for those employees: task lines, the week
--      record and its approval trail, and day-type overrides for 16-22 Nov.
--      EVERYTHING deleted is copied to backup tables first.
--
-- RUN FIRST: 42_ROLLBACK_techeff_restore_previous_managers_APPLY.sql
--   (puts back the original managers from dry run #1). This script refuses
--   to apply while Carbynetech_DryRun_ManagerBackup still exists.
--
-- SAFE BY DEFAULT: @Apply = 0 only previews. Set @Apply = 1 to apply.
-- ROLLBACK: 45_ROLLBACK_techeff_hamsini_route_reset_week.sql
-- Select the Carbynetech_Timesheet_App database before running.
-- =====================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Apply BIT = 0;                        -- 0 = preview only, 1 = apply
DECLARE @L1Code NVARCHAR(20) = N'CBT1152';      -- Hamsini (Level 1)
DECLARE @Week DATE = '2026-11-16';
DECLARE @WeekEnd DATE = '2026-11-22';

DECLARE @Scope TABLE (EmployeeCode NVARCHAR(20) PRIMARY KEY);
INSERT INTO @Scope VALUES
 (N'CBT1267'),(N'CBT1269'),(N'CBT1274'),(N'CBT1284'),(N'CBT1230'),
 (N'CBT1302'),(N'CBT1312'),(N'CBT1120'),(N'CBT1276'),(N'CBT1331'),(N'CBT1132');

DECLARE @L1Id INT, @L2Id INT;
SELECT @L1Id = EmployeeId, @L2Id = ManagerEmployeeId FROM Carbynetech_Employee WHERE EmployeeCode = @L1Code AND IsActive = 1;
IF @L1Id IS NULL BEGIN RAISERROR('Level 1 approver %s not found or inactive - nothing changed.',16,1,@L1Code); RETURN; END

DECLARE @Emp TABLE (EmployeeId INT PRIMARY KEY, EmployeeCode NVARCHAR(20), FullName NVARCHAR(200), OldManagerId INT, Action NVARCHAR(100));
INSERT INTO @Emp
SELECT e.EmployeeId, e.EmployeeCode, e.FullName, e.ManagerEmployeeId,
       CASE WHEN e.ManagerEmployeeId = @L1Id THEN 'NO CHANGE - already reports to Hamsini'
            ELSE 'CHANGE manager to Hamsini' END
FROM Carbynetech_Employee e JOIN @Scope s ON s.EmployeeCode = e.EmployeeCode
WHERE e.IsActive = 1;

-- A. Approvers
SELECT 'A. Route' AS Section, l1.EmployeeCode AS Level1Code, l1.FullName AS Level1, l2.EmployeeCode AS Level2Code, l2.FullName AS Level2,
       CASE WHEN l2.EmployeeId IS NULL THEN 'WARNING: Hamsini has no manager - Level 1 would be skipped'
            WHEN l2.IsActive = 0 THEN 'WARNING: Level 2 approver inactive' ELSE 'OK' END AS Result
FROM Carbynetech_Employee l1 LEFT JOIN Carbynetech_Employee l2 ON l2.EmployeeId = l1.ManagerEmployeeId
WHERE l1.EmployeeId = @L1Id;

-- B. Employees
SELECT 'B. Employees' AS Section, x.EmployeeCode, x.FullName, ISNULL(m.FullName,'(none)') AS CurrentManager, x.Action,
       (SELECT COUNT(*) FROM Carbynetech_Employee r WHERE r.ManagerEmployeeId = x.EmployeeId AND r.IsActive = 1
          AND r.EmployeeId NOT IN (SELECT EmployeeId FROM @Emp)) AS OtherDirectReports,
       CASE WHEN x.Action LIKE 'CHANGE%' AND EXISTS (SELECT 1 FROM Carbynetech_Employee r WHERE r.ManagerEmployeeId = x.EmployeeId AND r.IsActive = 1
                 AND r.EmployeeId NOT IN (SELECT EmployeeId FROM @Emp))
            THEN 'NOTE: their other reports will have Hamsini as Level 2' ELSE '' END AS Note
FROM @Emp x LEFT JOIN Carbynetech_Employee m ON m.EmployeeId = x.OldManagerId
ORDER BY x.Action, x.FullName;

-- C. Other people whose Level 2 changes because of this
SELECT 'C. Level 2 changes for non-TechEff reports' AS Section, r.EmployeeCode, r.FullName, x.FullName AS Level1Stays, '-> Level 2 becomes Hamsini' AS Change
FROM Carbynetech_Employee r JOIN @Emp x ON x.EmployeeId = r.ManagerEmployeeId
WHERE r.IsActive = 1 AND r.EmployeeId NOT IN (SELECT EmployeeId FROM @Emp) AND x.Action LIKE 'CHANGE%';

-- D. Weeks awaiting approval that move to Hamsini's queue
SELECT 'D. Pending weeks re-routed' AS Section, x.EmployeeCode, x.FullName, w.WeekStartDate, w.Status
FROM @Emp x JOIN Carbynetech_WeekRecord w ON w.EmployeeId = x.EmployeeId
WHERE x.Action LIKE 'CHANGE%' AND w.Status IN ('PendingL1','PendingL2') AND w.WeekStartDate <> @Week;

-- E. Week 16-22 Nov data that will be DELETED (backed up first)
SELECT 'E. Week 16-22 Nov to clear' AS Section, x.EmployeeCode, x.FullName, w.Status,
       (SELECT COUNT(*) FROM Carbynetech_TimeEntry t WHERE t.EmployeeId = x.EmployeeId AND t.WeekStartDate = @Week) AS TaskLines,
       (SELECT ISNULL(SUM(t.MondayHours+t.TuesdayHours+t.WednesdayHours+t.ThursdayHours+t.FridayHours+t.SaturdayHours+t.SundayHours),0)
          FROM Carbynetech_TimeEntry t WHERE t.EmployeeId = x.EmployeeId AND t.WeekStartDate = @Week) AS Hours,
       (SELECT COUNT(*) FROM Carbynetech_TimeEntry t WHERE t.EmployeeId = x.EmployeeId AND t.WeekStartDate = @Week
          AND (t.UserStoryId IS NULL OR t.UserStoryId NOT IN (SELECT RowId FROM Carbynetech_DryRun_Created WHERE TableName = 'Carbynetech_UserStory'))) AS NonDryRunLines,
       (SELECT COUNT(*) FROM Carbynetech_DayTypeOverride d WHERE d.EmployeeId = x.EmployeeId AND d.EntryDate BETWEEN @Week AND @WeekEnd) AS DayTypeOverrides
FROM @Emp x LEFT JOIN Carbynetech_WeekRecord w ON w.EmployeeId = x.EmployeeId AND w.WeekStartDate = @Week
WHERE w.WeekRecordId IS NOT NULL
   OR EXISTS (SELECT 1 FROM Carbynetech_TimeEntry t WHERE t.EmployeeId = x.EmployeeId AND t.WeekStartDate = @Week)
   OR EXISTS (SELECT 1 FROM Carbynetech_DayTypeOverride d WHERE d.EmployeeId = x.EmployeeId AND d.EntryDate BETWEEN @Week AND @WeekEnd)
ORDER BY x.FullName;

IF @Apply = 0
BEGIN
    SELECT 'PREVIEW ONLY' AS Result,
           (SELECT COUNT(*) FROM @Emp WHERE Action LIKE 'CHANGE%') AS ManagersToChange,
           'Nothing changed. Check B-E (NonDryRunLines > 0 in E = real timesheet data), then run the _APPLY copy.' AS NextStep;
    RETURN;
END

IF OBJECT_ID('Carbynetech_DryRun_ManagerBackup') IS NOT NULL
BEGIN RAISERROR('Run 42_ROLLBACK_techeff_restore_previous_managers_APPLY.sql first - nothing changed.',16,1); RETURN; END
IF OBJECT_ID('Carbynetech_DryRun2_ManagerBackup') IS NOT NULL
BEGIN RAISERROR('Script 45 was already applied (Carbynetech_DryRun2_ManagerBackup exists) - nothing changed.',16,1); RETURN; END

BEGIN TRAN;
    -- Backups
    SELECT x.EmployeeId, x.OldManagerId AS OldManagerEmployeeId, @L1Id AS NewManagerEmployeeId, SYSUTCDATETIME() AS ChangedAt
    INTO Carbynetech_DryRun2_ManagerBackup FROM @Emp x WHERE x.Action LIKE 'CHANGE%';

    SELECT t.* INTO Carbynetech_DryRun2_TimeEntryBackup FROM Carbynetech_TimeEntry t
    WHERE t.WeekStartDate = @Week AND t.EmployeeId IN (SELECT EmployeeId FROM @Emp);

    SELECT w.* INTO Carbynetech_DryRun2_WeekRecordBackup FROM Carbynetech_WeekRecord w
    WHERE w.WeekStartDate = @Week AND w.EmployeeId IN (SELECT EmployeeId FROM @Emp);

    SELECT a.* INTO Carbynetech_DryRun2_ApprovalEventBackup FROM Carbynetech_ApprovalEvent a
    WHERE a.WeekRecordId IN (SELECT WeekRecordId FROM Carbynetech_DryRun2_WeekRecordBackup);

    SELECT d.* INTO Carbynetech_DryRun2_DayTypeOverrideBackup FROM Carbynetech_DayTypeOverride d
    WHERE d.EntryDate BETWEEN @Week AND @WeekEnd AND d.EmployeeId IN (SELECT EmployeeId FROM @Emp);

    -- Clear week 16-22 Nov
    DELETE FROM Carbynetech_ApprovalEvent   WHERE ApprovalEventId   IN (SELECT ApprovalEventId   FROM Carbynetech_DryRun2_ApprovalEventBackup);
    DELETE FROM Carbynetech_TimeEntry       WHERE TimeEntryId       IN (SELECT TimeEntryId       FROM Carbynetech_DryRun2_TimeEntryBackup);
    DELETE FROM Carbynetech_WeekRecord      WHERE WeekRecordId      IN (SELECT WeekRecordId      FROM Carbynetech_DryRun2_WeekRecordBackup);
    DELETE FROM Carbynetech_DayTypeOverride WHERE DayTypeOverrideId IN (SELECT DayTypeOverrideId FROM Carbynetech_DryRun2_DayTypeOverrideBackup);

    -- New route
    UPDATE e SET e.ManagerEmployeeId = @L1Id, e.UpdatedAt = SYSUTCDATETIME()
    FROM Carbynetech_Employee e JOIN @Emp x ON x.EmployeeId = e.EmployeeId
    WHERE x.Action LIKE 'CHANGE%';
COMMIT;

SELECT 'APPLIED' AS Result,
       (SELECT COUNT(*) FROM Carbynetech_DryRun2_ManagerBackup)        AS ManagersChanged,
       (SELECT COUNT(*) FROM Carbynetech_DryRun2_TimeEntryBackup)      AS TaskLinesCleared,
       (SELECT COUNT(*) FROM Carbynetech_DryRun2_WeekRecordBackup)     AS WeeksCleared,
       (SELECT COUNT(*) FROM Carbynetech_DryRun2_DayTypeOverrideBackup) AS DayOverridesCleared;

SELECT 'F. After' AS Section, e.EmployeeCode, e.FullName, m.FullName AS Level1, m2.FullName AS Level2
FROM @Emp x JOIN Carbynetech_Employee e ON e.EmployeeId = x.EmployeeId
LEFT JOIN Carbynetech_Employee m ON m.EmployeeId = e.ManagerEmployeeId
LEFT JOIN Carbynetech_Employee m2 ON m2.EmployeeId = m.ManagerEmployeeId
ORDER BY e.FullName;
