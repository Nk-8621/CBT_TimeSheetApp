-- =====================================================================
-- 45_ROLLBACK_techeff_hamsini_route_reset_week.sql
--
-- Undoes 45_techeff_hamsini_route_reset_week.sql:
--   1. Removes everything logged for week 16-22 Nov 2026 by the scope
--      employees AFTER script 45 ran (dry run #2: task lines, week records,
--      approval trail, day-type overrides).
--   2. Restores the week 16-22 Nov data that script 45 backed up.
--   3. Puts back each employee's previous manager.
--   4. Drops the Carbynetech_DryRun2_* backup tables.
--
-- SAFE BY DEFAULT: @Apply = 0 only previews. Set @Apply = 1 to roll back.
-- Select the Carbynetech_Timesheet_App database before running.
-- =====================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Apply BIT = 1;   -- 0 = preview only, 1 = roll back
DECLARE @Week DATE = '2026-11-16';
DECLARE @WeekEnd DATE = '2026-11-22';

IF OBJECT_ID('Carbynetech_DryRun2_ManagerBackup') IS NULL
BEGIN RAISERROR('Script 45 was not applied (no Carbynetech_DryRun2_ManagerBackup) - nothing to roll back.',16,1); RETURN; END

DECLARE @Scope TABLE (EmployeeCode NVARCHAR(20) PRIMARY KEY);
INSERT INTO @Scope VALUES
 (N'CBT1267'),(N'CBT1269'),(N'CBT1274'),(N'CBT1284'),(N'CBT1230'),
 (N'CBT1302'),(N'CBT1312'),(N'CBT1120'),(N'CBT1276'),(N'CBT1331'),(N'CBT1132');
DECLARE @Emp TABLE (EmployeeId INT PRIMARY KEY);
INSERT INTO @Emp SELECT e.EmployeeId FROM Carbynetech_Employee e JOIN @Scope s ON s.EmployeeCode = e.EmployeeCode;

SELECT 'A. Managers to restore' AS Section, e.EmployeeCode, e.FullName, cur.FullName AS ManagerNow, old.FullName AS RestoreTo
FROM Carbynetech_DryRun2_ManagerBackup b
JOIN Carbynetech_Employee e ON e.EmployeeId = b.EmployeeId
LEFT JOIN Carbynetech_Employee cur ON cur.EmployeeId = e.ManagerEmployeeId
LEFT JOIN Carbynetech_Employee old ON old.EmployeeId = b.OldManagerEmployeeId
ORDER BY e.FullName;

SELECT 'B. Week 16-22 Nov now (will be removed)' AS Section, e.EmployeeCode, e.FullName, w.Status,
       (SELECT COUNT(*) FROM Carbynetech_TimeEntry t WHERE t.EmployeeId = e.EmployeeId AND t.WeekStartDate = @Week) AS TaskLines
FROM @Emp x JOIN Carbynetech_Employee e ON e.EmployeeId = x.EmployeeId
LEFT JOIN Carbynetech_WeekRecord w ON w.EmployeeId = e.EmployeeId AND w.WeekStartDate = @Week
WHERE w.WeekRecordId IS NOT NULL OR EXISTS (SELECT 1 FROM Carbynetech_TimeEntry t WHERE t.EmployeeId = e.EmployeeId AND t.WeekStartDate = @Week)
ORDER BY e.FullName;

SELECT 'C. Week 16-22 Nov to restore (from backup)' AS Section, e.EmployeeCode, e.FullName, w.Status,
       (SELECT COUNT(*) FROM Carbynetech_DryRun2_TimeEntryBackup t WHERE t.EmployeeId = e.EmployeeId) AS TaskLines
FROM Carbynetech_DryRun2_WeekRecordBackup w JOIN Carbynetech_Employee e ON e.EmployeeId = w.EmployeeId
ORDER BY e.FullName;

IF @Apply = 0
BEGIN
    SELECT 'PREVIEW ONLY' AS Result, 'Nothing changed. Review A-C, then run the _APPLY copy.' AS NextStep;
    RETURN;
END

DECLARE @sql NVARCHAR(MAX);
BEGIN TRAN;
    -- 1. Remove dry run #2 data for the week
    DELETE a FROM Carbynetech_ApprovalEvent a JOIN Carbynetech_WeekRecord w ON w.WeekRecordId = a.WeekRecordId
    WHERE w.WeekStartDate = @Week AND w.EmployeeId IN (SELECT EmployeeId FROM @Emp);
    DELETE FROM Carbynetech_TimeEntry WHERE WeekStartDate = @Week AND EmployeeId IN (SELECT EmployeeId FROM @Emp);
    DELETE FROM Carbynetech_WeekRecord WHERE WeekStartDate = @Week AND EmployeeId IN (SELECT EmployeeId FROM @Emp);
    DELETE FROM Carbynetech_DayTypeOverride WHERE EntryDate BETWEEN @Week AND @WeekEnd AND EmployeeId IN (SELECT EmployeeId FROM @Emp);

    -- 2. Restore backed-up rows with their original ids
    SET @sql = N'';
    IF COLUMNPROPERTY(OBJECT_ID('Carbynetech_WeekRecord'),'WeekRecordId','IsIdentity') = 1 SET @sql += N'SET IDENTITY_INSERT Carbynetech_WeekRecord ON; ';
    SET @sql += N'INSERT INTO Carbynetech_WeekRecord (WeekRecordId, EmployeeId, WeekStartDate, Status, SubmittedAt, RejectedByEmployeeId, RejectionReason, CreatedAt, UpdatedAt)
                  SELECT WeekRecordId, EmployeeId, WeekStartDate, Status, SubmittedAt, RejectedByEmployeeId, RejectionReason, CreatedAt, UpdatedAt FROM Carbynetech_DryRun2_WeekRecordBackup; ';
    IF COLUMNPROPERTY(OBJECT_ID('Carbynetech_WeekRecord'),'WeekRecordId','IsIdentity') = 1 SET @sql += N'SET IDENTITY_INSERT Carbynetech_WeekRecord OFF;';
    EXEC sp_executesql @sql;

    SET @sql = N'';
    IF COLUMNPROPERTY(OBJECT_ID('Carbynetech_ApprovalEvent'),'ApprovalEventId','IsIdentity') = 1 SET @sql += N'SET IDENTITY_INSERT Carbynetech_ApprovalEvent ON; ';
    SET @sql += N'INSERT INTO Carbynetech_ApprovalEvent (ApprovalEventId, WeekRecordId, EventText, EventMeta, EventStatus, ActedByEmployeeId, EventTimestamp)
                  SELECT ApprovalEventId, WeekRecordId, EventText, EventMeta, EventStatus, ActedByEmployeeId, EventTimestamp FROM Carbynetech_DryRun2_ApprovalEventBackup; ';
    IF COLUMNPROPERTY(OBJECT_ID('Carbynetech_ApprovalEvent'),'ApprovalEventId','IsIdentity') = 1 SET @sql += N'SET IDENTITY_INSERT Carbynetech_ApprovalEvent OFF;';
    EXEC sp_executesql @sql;

    SET @sql = N'';
    IF COLUMNPROPERTY(OBJECT_ID('Carbynetech_TimeEntry'),'TimeEntryId','IsIdentity') = 1 SET @sql += N'SET IDENTITY_INSERT Carbynetech_TimeEntry ON; ';
    SET @sql += N'INSERT INTO Carbynetech_TimeEntry (TimeEntryId, EmployeeId, WeekStartDate, ProjectId, ModuleId, TaskId, Classification, BillingCategory, Note, UserStoryId,
                    MondayHours, TuesdayHours, WednesdayHours, ThursdayHours, FridayHours, SaturdayHours, SundayHours, CreatedAt, UpdatedAt)
                  SELECT TimeEntryId, EmployeeId, WeekStartDate, ProjectId, ModuleId, TaskId, Classification, BillingCategory, Note, UserStoryId,
                    MondayHours, TuesdayHours, WednesdayHours, ThursdayHours, FridayHours, SaturdayHours, SundayHours, CreatedAt, UpdatedAt
                  FROM Carbynetech_DryRun2_TimeEntryBackup t
                  WHERE t.UserStoryId IS NULL OR EXISTS (SELECT 1 FROM Carbynetech_UserStory u WHERE u.UserStoryId = t.UserStoryId); ';
    IF COLUMNPROPERTY(OBJECT_ID('Carbynetech_TimeEntry'),'TimeEntryId','IsIdentity') = 1 SET @sql += N'SET IDENTITY_INSERT Carbynetech_TimeEntry OFF;';
    EXEC sp_executesql @sql;

    SET @sql = N'';
    IF COLUMNPROPERTY(OBJECT_ID('Carbynetech_DayTypeOverride'),'DayTypeOverrideId','IsIdentity') = 1 SET @sql += N'SET IDENTITY_INSERT Carbynetech_DayTypeOverride ON; ';
    SET @sql += N'INSERT INTO Carbynetech_DayTypeOverride (DayTypeOverrideId, EmployeeId, EntryDate, DayType, CreatedAt)
                  SELECT DayTypeOverrideId, EmployeeId, EntryDate, DayType, CreatedAt FROM Carbynetech_DryRun2_DayTypeOverrideBackup; ';
    IF COLUMNPROPERTY(OBJECT_ID('Carbynetech_DayTypeOverride'),'DayTypeOverrideId','IsIdentity') = 1 SET @sql += N'SET IDENTITY_INSERT Carbynetech_DayTypeOverride OFF;';
    EXEC sp_executesql @sql;

    -- 3. Managers back
    UPDATE e SET e.ManagerEmployeeId = b.OldManagerEmployeeId, e.UpdatedAt = SYSUTCDATETIME()
    FROM Carbynetech_Employee e JOIN Carbynetech_DryRun2_ManagerBackup b ON b.EmployeeId = e.EmployeeId;
    DECLARE @Restored INT = @@ROWCOUNT;

    -- 4. Clean up
    DROP TABLE Carbynetech_DryRun2_ManagerBackup;
    DROP TABLE Carbynetech_DryRun2_TimeEntryBackup;
    DROP TABLE Carbynetech_DryRun2_ApprovalEventBackup;
    DROP TABLE Carbynetech_DryRun2_WeekRecordBackup;
    DROP TABLE Carbynetech_DryRun2_DayTypeOverrideBackup;
COMMIT;

SELECT 'ROLLED BACK' AS Result, @Restored AS ManagersRestored;
