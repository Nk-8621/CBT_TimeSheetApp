-- =====================================================================
-- 25_add_august_test_data.sql
--
-- TEMPORARY test data for August 2026 for the dummy employees TEST001 and
-- TEST002 (created by 23_add_dummy_test_data_for_cbt1268.sql - run that
-- first). Remove everything with 24_remove_dummy_test_data.sql.
--
-- What it adds (weeks starting 3, 10, 17 and 24 Aug 2026, Mon-Fri):
--   * Allocates both test employees to EVERY active project (billing type
--     = the project's default; existing allocations are kept as they are).
--   * Timesheet lines spread across all active projects that have a module
--     and a task, so every such project gets hours in August.
--     8 h per working day, split evenly over the week's lines.
--   * WFH / leave requests (they change that day's hours, as in the app):
--       TEST001 (manager CBT1268)          TEST002 (manager TEST001)
--       Wed  5 Aug  WFH         Approved   Thu  6 Aug  WFH          Approved
--       Wed 12 Aug  Full day    Approved   Thu 13 Aug  Full day     Approved
--       Wed 19 Aug  First half  Approved   Thu 20 Aug  Second half  Approved
--       Wed 26 Aug  Second half PENDING    Thu 27 Aug  WFH          Rejected
--     (TEST001's pending request shows in CBT1268's Request Approval.)
--   * Week statuses with a matching approval trail:
--       TEST001: Approved, Approved, Returned (rejected), Pending Level 1
--       TEST002: Approved, Approved, Approved, Pending Level 2
--     Level 2 approvals of TEST001's weeks are recorded against CBT1268's
--     own manager (the real Level 2 for TEST001) - test data only.
--
-- Safe to re-run: the test employees' August data is deleted and rewritten.
-- Runs in one transaction: all or nothing.
-- =====================================================================

USE [Carbynetech_TimeSheet_Application];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Now datetime2 = SYSUTCDATETIME();
DECLARE @MonthStart date = '2026-08-03';   -- first Monday of August 2026
DECLARE @MonthEnd   date = '2026-08-30';   -- Sunday of the 4th week

-- ---- People -------------------------------------------------------------
DECLARE @Test1Id int, @Test1Name nvarchar(150), @Test2Id int, @Test2Name nvarchar(150);
SELECT @Test1Id = EmployeeId, @Test1Name = FullName FROM Carbynetech_Employee WHERE EmployeeCode = 'TEST001';
SELECT @Test2Id = EmployeeId, @Test2Name = FullName FROM Carbynetech_Employee WHERE EmployeeCode = 'TEST002';

IF @Test1Id IS NULL OR @Test2Id IS NULL
BEGIN
    PRINT 'ERROR: TEST001 / TEST002 not found - run script 23 first. Nothing changed.';
    RETURN;
END

DECLARE @MgrId int, @MgrName nvarchar(150), @MgrManagerId int, @MgrManagerName nvarchar(150);
SELECT @MgrId = e.EmployeeId, @MgrName = e.FullName, @MgrManagerId = m.EmployeeId, @MgrManagerName = m.FullName
FROM Carbynetech_Employee e
LEFT JOIN Carbynetech_Employee m ON m.EmployeeId = e.ManagerEmployeeId
WHERE e.EmployeeCode = 'CBT1268';

-- Approval routing, same rule as WeekApprovalService.DetermineApprovalRoutingAsync:
-- if the manager has no manager of their own, Level 1 is skipped and the
-- manager approves at Level 2.
--   TEST001: Level 1 = CBT1268, Level 2 = CBT1268's manager (or skip-L1 case)
--   TEST002: Level 1 = TEST001, Level 2 = CBT1268
DECLARE @Routing TABLE (EmployeeId int PRIMARY KEY, EmployeeName nvarchar(150), SkipL1 bit,
                        L1Id int NULL, L1Name nvarchar(150) NULL, L2Id int, L2Name nvarchar(150));
INSERT INTO @Routing VALUES
    (@Test1Id, @Test1Name,
     CASE WHEN @MgrManagerId IS NULL THEN 1 ELSE 0 END,
     CASE WHEN @MgrManagerId IS NULL THEN NULL ELSE @MgrId END,
     CASE WHEN @MgrManagerId IS NULL THEN NULL ELSE @MgrName END,
     COALESCE(@MgrManagerId, @MgrId), COALESCE(@MgrManagerName, @MgrName)),
    (@Test2Id, @Test2Name, 0, @Test1Id, @Test1Name, @MgrId, @MgrName);

-- ---- Weeks ----------------------------------------------------------------
DECLARE @Weeks TABLE (WeekIdx int PRIMARY KEY, WeekStart date);
INSERT INTO @Weeks VALUES (0, '2026-08-03'), (1, '2026-08-10'), (2, '2026-08-17'), (3, '2026-08-24');

-- ---- Projects to log against: every active project with a module + task ---
DECLARE @Projects TABLE (Idx int PRIMARY KEY, ProjectId int, ModuleId int, TaskId int);
INSERT INTO @Projects (Idx, ProjectId, ModuleId, TaskId)
SELECT ROW_NUMBER() OVER (ORDER BY x.ProjectId) - 1, x.ProjectId, x.ModuleId, x.TaskId
FROM (
    SELECT p.ProjectId, m.ModuleId, t.TaskId,
           ROW_NUMBER() OVER (PARTITION BY p.ProjectId ORDER BY m.ModuleId, t.TaskId) AS rn
    FROM Carbynetech_Project p
    JOIN Carbynetech_Module m ON m.ProjectId = p.ProjectId
    JOIN Carbynetech_Task t ON t.ModuleId = m.ModuleId
    WHERE p.IsActive = 1
) x
WHERE x.rn = 1;

DECLARE @ProjectCount int = (SELECT COUNT(*) FROM @Projects);
IF @ProjectCount = 0
BEGIN
    PRINT 'ERROR: no active project has a module with a task - nothing changed.';
    RETURN;
END

-- Lines per employee-week: 4 lines (2 h/day each) covers up to 32 projects
-- over 2 employees x 4 weeks; 8 lines (1 h/day each) covers up to 64.
DECLARE @LinesPerWeek int = CASE WHEN @ProjectCount > 32 THEN 8 ELSE 4 END;
IF @ProjectCount > 8 * @LinesPerWeek
    PRINT CONCAT('NOTE: ', @ProjectCount, ' projects - only the first ', 8 * @LinesPerWeek, ' get August hours.');

-- ---- WFH / leave requests ---------------------------------------------------
DECLARE @Requests TABLE (EmployeeId int, RequestDate date, RequestType nvarchar(20), Status nvarchar(20),
                         ApproverId int NULL, Note nvarchar(500), DecisionComment nvarchar(500) NULL);
INSERT INTO @Requests VALUES
    (@Test1Id, '2026-08-05', 'WFH',             'Approved', @MgrId,   N'Dummy test - WFH',                NULL),
    (@Test1Id, '2026-08-12', 'LeaveFull',       'Approved', @MgrId,   N'Dummy test - full day leave',     NULL),
    (@Test1Id, '2026-08-19', 'LeaveFirstHalf',  'Approved', @MgrId,   N'Dummy test - first half leave',   NULL),
    (@Test1Id, '2026-08-26', 'LeaveSecondHalf', 'Pending',  NULL,     N'Dummy test - second half leave',  NULL),
    (@Test2Id, '2026-08-06', 'WFH',             'Approved', @Test1Id, N'Dummy test - WFH',                NULL),
    (@Test2Id, '2026-08-13', 'LeaveFull',       'Approved', @Test1Id, N'Dummy test - full day leave',     NULL),
    (@Test2Id, '2026-08-20', 'LeaveSecondHalf', 'Approved', @Test1Id, N'Dummy test - second half leave',  NULL),
    (@Test2Id, '2026-08-27', 'WFH',             'Rejected', @Test1Id, N'Dummy test - WFH',                N'Dummy test - needed in office');

-- Hours available per working day: 8, or 4 on a half-day leave, 0 on a full
-- day leave (pending and approved leave both count, rejected doesn't - same
-- as the app). WFH days are normal 8 h days.
DECLARE @DayCapacity TABLE (EmployeeId int, WorkDate date, Capacity decimal(4,2), PRIMARY KEY (EmployeeId, WorkDate));
INSERT INTO @DayCapacity (EmployeeId, WorkDate, Capacity)
SELECT r.EmployeeId, DATEADD(day, d.n, w.WeekStart),
       COALESCE((SELECT TOP 1 CASE q.RequestType WHEN 'LeaveFull' THEN 0 WHEN 'LeaveFirstHalf' THEN 4 WHEN 'LeaveSecondHalf' THEN 4 ELSE 8 END
                 FROM @Requests q
                 WHERE q.EmployeeId = r.EmployeeId AND q.RequestDate = DATEADD(day, d.n, w.WeekStart)
                   AND q.Status IN ('Pending', 'Approved')), 8)
FROM @Routing r
CROSS JOIN @Weeks w
CROSS JOIN (VALUES (0), (1), (2), (3), (4)) d(n);  -- Monday..Friday

-- ---- Week statuses ------------------------------------------------------------
DECLARE @WeekPlan TABLE (EmployeeId int, WeekIdx int, Status nvarchar(20), PRIMARY KEY (EmployeeId, WeekIdx));
INSERT INTO @WeekPlan VALUES
    (@Test1Id, 0, 'Approved'), (@Test1Id, 1, 'Approved'), (@Test1Id, 2, 'Rejected'), (@Test1Id, 3, 'PendingL1'),
    (@Test2Id, 0, 'Approved'), (@Test2Id, 1, 'Approved'), (@Test2Id, 2, 'Approved'), (@Test2Id, 3, 'PendingL2');
-- A skip-Level-1 employee never sits at PendingL1.
UPDATE wp SET Status = 'PendingL2'
FROM @WeekPlan wp JOIN @Routing r ON r.EmployeeId = wp.EmployeeId
WHERE r.SkipL1 = 1 AND wp.Status = 'PendingL1';

BEGIN TRANSACTION;

-- ---- Clear the test employees' existing August data (makes re-runs safe) ---
DELETE a FROM Carbynetech_ApprovalEvent a
JOIN Carbynetech_WeekRecord wr ON wr.WeekRecordId = a.WeekRecordId
WHERE wr.EmployeeId IN (@Test1Id, @Test2Id) AND wr.WeekStartDate BETWEEN @MonthStart AND @MonthEnd;
DELETE FROM Carbynetech_WeekRecord WHERE EmployeeId IN (@Test1Id, @Test2Id) AND WeekStartDate BETWEEN @MonthStart AND @MonthEnd;
DELETE FROM Carbynetech_TimeEntry  WHERE EmployeeId IN (@Test1Id, @Test2Id) AND WeekStartDate BETWEEN @MonthStart AND @MonthEnd;
DELETE FROM Carbynetech_DayTypeRequest  WHERE EmployeeId IN (@Test1Id, @Test2Id) AND RequestDate BETWEEN @MonthStart AND @MonthEnd;
DELETE FROM Carbynetech_DayTypeOverride WHERE EmployeeId IN (@Test1Id, @Test2Id) AND EntryDate BETWEEN @MonthStart AND @MonthEnd;

-- ---- Allocate both test employees to every active project -------------------
-- Billing type = the project's default. Billable allocations get a billing
-- category in rotation (AMS / T&M / FB), NonBillable gets OH, Partial none.
INSERT INTO Carbynetech_EmployeeProjectAllocation (EmployeeId, ProjectId, AssignedAt, Classification, BillingCategory)
SELECT r.EmployeeId, p.ProjectId, @Now, p.DefaultBillable,
       CASE p.DefaultBillable
            WHEN 'Billable'    THEN CASE p.ProjectId % 3 WHEN 0 THEN 'AMS' WHEN 1 THEN 'T&M' ELSE 'FB' END
            WHEN 'NonBillable' THEN 'OH'
            ELSE NULL END
FROM @Routing r
CROSS JOIN Carbynetech_Project p
WHERE p.IsActive = 1
  AND NOT EXISTS (SELECT 1 FROM Carbynetech_EmployeeProjectAllocation a
                  WHERE a.EmployeeId = r.EmployeeId AND a.ProjectId = p.ProjectId);
PRINT CONCAT(@@ROWCOUNT, ' project allocation(s) added.');

-- ---- Timesheet lines --------------------------------------------------------
-- Line slot -> project in rotation, so all projects are spread across the
-- 2 employees x 4 weeks. Billing type comes from the employee's allocation,
-- exactly as the app does it.
INSERT INTO Carbynetech_TimeEntry
    (EmployeeId, WeekStartDate, ProjectId, ModuleId, TaskId, Classification, BillingCategory, Note,
     MondayHours, TuesdayHours, WednesdayHours, ThursdayHours, FridayHours, SaturdayHours, SundayHours, CreatedAt)
SELECT s.EmployeeId, w.WeekStart, pr.ProjectId, pr.ModuleId, pr.TaskId, a.Classification, a.BillingCategory,
       N'Dummy test data - August',
       (SELECT Capacity FROM @DayCapacity c WHERE c.EmployeeId = s.EmployeeId AND c.WorkDate = w.WeekStart) / @LinesPerWeek,
       (SELECT Capacity FROM @DayCapacity c WHERE c.EmployeeId = s.EmployeeId AND c.WorkDate = DATEADD(day, 1, w.WeekStart)) / @LinesPerWeek,
       (SELECT Capacity FROM @DayCapacity c WHERE c.EmployeeId = s.EmployeeId AND c.WorkDate = DATEADD(day, 2, w.WeekStart)) / @LinesPerWeek,
       (SELECT Capacity FROM @DayCapacity c WHERE c.EmployeeId = s.EmployeeId AND c.WorkDate = DATEADD(day, 3, w.WeekStart)) / @LinesPerWeek,
       (SELECT Capacity FROM @DayCapacity c WHERE c.EmployeeId = s.EmployeeId AND c.WorkDate = DATEADD(day, 4, w.WeekStart)) / @LinesPerWeek,
       0, 0, @Now
FROM (
    SELECT r.EmployeeId, CASE WHEN r.EmployeeId = @Test1Id THEN 0 ELSE 1 END AS EmpIdx, w.WeekIdx, l.LineIdx
    FROM @Routing r
    CROSS JOIN @Weeks w
    CROSS JOIN (VALUES (0), (1), (2), (3), (4), (5), (6), (7)) l(LineIdx)
    WHERE l.LineIdx < @LinesPerWeek
) s
JOIN @Weeks w ON w.WeekIdx = s.WeekIdx
JOIN @Projects pr ON pr.Idx = ((s.EmpIdx * 4 + s.WeekIdx) * @LinesPerWeek + s.LineIdx) % @ProjectCount
JOIN Carbynetech_EmployeeProjectAllocation a ON a.EmployeeId = s.EmployeeId AND a.ProjectId = pr.ProjectId;
PRINT CONCAT(@@ROWCOUNT, ' timesheet line(s) written.');

-- ---- WFH / leave requests + WFH day overrides ----------------------------------
INSERT INTO Carbynetech_DayTypeRequest
    (EmployeeId, RequestDate, RequestType, Status, Note, SubmittedAt, ApproverEmployeeId, DecidedAt, DecisionComment, Source)
SELECT q.EmployeeId, q.RequestDate, q.RequestType, q.Status, q.Note,
       DATEADD(hour, 10, CAST(DATEADD(day, -2, q.RequestDate) AS datetime2)),
       q.ApproverId,
       CASE WHEN q.Status = 'Pending' THEN NULL ELSE DATEADD(hour, 15, CAST(DATEADD(day, -1, q.RequestDate) AS datetime2)) END,
       q.DecisionComment, 'Meridian'
FROM @Requests q;
PRINT CONCAT(@@ROWCOUNT, ' WFH/leave request(s) added.');

-- The app marks a WFH day in the override table when requested, and sets it
-- back to a normal working day (W) if the request is rejected.
INSERT INTO Carbynetech_DayTypeOverride (EmployeeId, EntryDate, DayType, CreatedAt)
SELECT q.EmployeeId, q.RequestDate, CASE WHEN q.Status = 'Rejected' THEN 'W' ELSE 'WFH' END, @Now
FROM @Requests q
WHERE q.RequestType = 'WFH';

-- ---- Week records + approval trail ------------------------------------------
-- One week at a time, written the same way WeekApprovalService writes them.
DECLARE @EmpId int, @WeekIdx int, @Status nvarchar(20), @WeekStart date, @WeekId int, @At datetime2;
DECLARE @EmpName nvarchar(150), @SkipL1 bit, @L1Id int, @L1Name nvarchar(150), @L2Id int, @L2Name nvarchar(150);
DECLARE @Reason nvarchar(200) = N'Dummy test - please correct the task descriptions';

DECLARE week_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT wp.EmployeeId, wp.WeekIdx, wp.Status, w.WeekStart,
           r.EmployeeName, r.SkipL1, r.L1Id, r.L1Name, r.L2Id, r.L2Name
    FROM @WeekPlan wp
    JOIN @Weeks w ON w.WeekIdx = wp.WeekIdx
    JOIN @Routing r ON r.EmployeeId = wp.EmployeeId
    ORDER BY wp.EmployeeId, wp.WeekIdx;
OPEN week_cursor;
FETCH NEXT FROM week_cursor INTO @EmpId, @WeekIdx, @Status, @WeekStart, @EmpName, @SkipL1, @L1Id, @L1Name, @L2Id, @L2Name;

WHILE @@FETCH_STATUS = 0
BEGIN
    -- Submitted on the Friday evening of that week.
    SET @At = DATEADD(hour, 18, CAST(DATEADD(day, 4, @WeekStart) AS datetime2));

    INSERT INTO Carbynetech_WeekRecord (EmployeeId, WeekStartDate, Status, SubmittedAt, RejectedByEmployeeId, RejectionReason, CreatedAt, UpdatedAt)
    VALUES (@EmpId, @WeekStart, @Status, @At,
            CASE WHEN @Status = 'Rejected' THEN COALESCE(@L1Id, @L2Id) END,
            CASE WHEN @Status = 'Rejected' THEN @Reason END,
            @At, @At);
    SET @WeekId = SCOPE_IDENTITY();

    -- 1. Submitted
    INSERT INTO Carbynetech_ApprovalEvent (WeekRecordId, EventText, EventMeta, EventStatus, ActedByEmployeeId, EventTimestamp)
    VALUES (@WeekId, CONCAT(N'Submitted by ', @EmpName), NULL, 'Ok', @EmpId, @At);

    IF @SkipL1 = 1
    BEGIN
        -- Straight to Level 2 (manager has no manager of their own).
        INSERT INTO Carbynetech_ApprovalEvent (WeekRecordId, EventText, EventMeta, EventStatus, ActedByEmployeeId, EventTimestamp)
        VALUES (@WeekId, CONCAT(N'Awaiting Level 2 — ', @L2Name, N' (reports directly to senior leadership; no Level 1 reviewer)'), 'Pending', 'Pending', NULL, DATEADD(second, 1, @At));

        IF @Status = 'Rejected'
            INSERT INTO Carbynetech_ApprovalEvent (WeekRecordId, EventText, EventMeta, EventStatus, ActedByEmployeeId, EventTimestamp)
            VALUES (@WeekId, CONCAT(N'Returned by ', @L2Name), @Reason, 'Rejected', @L2Id, DATEADD(day, 1, @At));

        IF @Status = 'Approved'
            INSERT INTO Carbynetech_ApprovalEvent (WeekRecordId, EventText, EventMeta, EventStatus, ActedByEmployeeId, EventTimestamp)
            VALUES (@WeekId, CONCAT(N'Level 2 approved — ', @L2Name), NULL, 'Ok', @L2Id, DATEADD(day, 1, @At)),
                   (@WeekId, N'Week locked', NULL, 'Ok', NULL, DATEADD(second, 1, DATEADD(day, 1, @At)));
    END
    ELSE
    BEGIN
        -- Normal route: Level 1, then Level 2.
        INSERT INTO Carbynetech_ApprovalEvent (WeekRecordId, EventText, EventMeta, EventStatus, ActedByEmployeeId, EventTimestamp)
        VALUES (@WeekId, CONCAT(N'Awaiting Level 1 — ', @L1Name), 'Pending', 'Pending', NULL, DATEADD(second, 1, @At));

        IF @Status = 'Rejected'
            INSERT INTO Carbynetech_ApprovalEvent (WeekRecordId, EventText, EventMeta, EventStatus, ActedByEmployeeId, EventTimestamp)
            VALUES (@WeekId, CONCAT(N'Returned by ', @L1Name), @Reason, 'Rejected', @L1Id, DATEADD(day, 1, @At));

        IF @Status IN ('PendingL2', 'Approved')
            INSERT INTO Carbynetech_ApprovalEvent (WeekRecordId, EventText, EventMeta, EventStatus, ActedByEmployeeId, EventTimestamp)
            VALUES (@WeekId, CONCAT(N'Level 1 approved — ', @L1Name), NULL, 'Ok', @L1Id, DATEADD(day, 1, @At)),
                   (@WeekId, CONCAT(N'Awaiting Level 2 — ', @L2Name), 'Pending', 'Pending', NULL, DATEADD(second, 1, DATEADD(day, 1, @At)));

        IF @Status = 'Approved'
            INSERT INTO Carbynetech_ApprovalEvent (WeekRecordId, EventText, EventMeta, EventStatus, ActedByEmployeeId, EventTimestamp)
            VALUES (@WeekId, CONCAT(N'Level 2 approved — ', @L2Name), NULL, 'Ok', @L2Id, DATEADD(day, 2, @At)),
                   (@WeekId, N'Week locked', NULL, 'Ok', NULL, DATEADD(second, 1, DATEADD(day, 2, @At)));
    END

    FETCH NEXT FROM week_cursor INTO @EmpId, @WeekIdx, @Status, @WeekStart, @EmpName, @SkipL1, @L1Id, @L1Name, @L2Id, @L2Name;
END
CLOSE week_cursor;
DEALLOCATE week_cursor;

COMMIT TRANSACTION;

-- ---- Summary -----------------------------------------------------------------
PRINT CONCAT('Done. ', @ProjectCount, ' active project(s) with modules/tasks; ', @LinesPerWeek, ' line(s) per employee per week.');

SELECT e.EmployeeCode, wr.WeekStartDate, wr.Status,
       SUM(t.MondayHours + t.TuesdayHours + t.WednesdayHours + t.ThursdayHours + t.FridayHours) AS TotalHours,
       SUM(CASE WHEN t.Classification = 'Billable'        THEN t.MondayHours + t.TuesdayHours + t.WednesdayHours + t.ThursdayHours + t.FridayHours ELSE 0 END) AS Billable,
       SUM(CASE WHEN t.Classification = 'PartialBillable' THEN t.MondayHours + t.TuesdayHours + t.WednesdayHours + t.ThursdayHours + t.FridayHours ELSE 0 END) AS PartialBillable,
       SUM(CASE WHEN t.Classification = 'NonBillable'     THEN t.MondayHours + t.TuesdayHours + t.WednesdayHours + t.ThursdayHours + t.FridayHours ELSE 0 END) AS NonBillable,
       COUNT(DISTINCT t.ProjectId) AS Projects
FROM Carbynetech_WeekRecord wr
JOIN Carbynetech_Employee e ON e.EmployeeId = wr.EmployeeId
LEFT JOIN Carbynetech_TimeEntry t ON t.EmployeeId = wr.EmployeeId AND t.WeekStartDate = wr.WeekStartDate
WHERE e.EmployeeCode IN ('TEST001', 'TEST002') AND wr.WeekStartDate BETWEEN @MonthStart AND @MonthEnd
GROUP BY e.EmployeeCode, wr.WeekStartDate, wr.Status
ORDER BY e.EmployeeCode, wr.WeekStartDate;

SELECT e.EmployeeCode, d.RequestDate, d.RequestType, d.Status
FROM Carbynetech_DayTypeRequest d
JOIN Carbynetech_Employee e ON e.EmployeeId = d.EmployeeId
WHERE e.EmployeeCode IN ('TEST001', 'TEST002') AND d.RequestDate BETWEEN @MonthStart AND @MonthEnd
ORDER BY e.EmployeeCode, d.RequestDate;

SELECT p.Code AS ProjectCode, p.Name AS ProjectName,
       SUM(t.MondayHours + t.TuesdayHours + t.WednesdayHours + t.ThursdayHours + t.FridayHours) AS AugustHours
FROM Carbynetech_Project p
LEFT JOIN Carbynetech_TimeEntry t ON t.ProjectId = p.ProjectId
     AND t.EmployeeId IN (SELECT EmployeeId FROM Carbynetech_Employee WHERE EmployeeCode IN ('TEST001', 'TEST002'))
     AND t.WeekStartDate BETWEEN @MonthStart AND @MonthEnd
WHERE p.IsActive = 1
GROUP BY p.Code, p.Name
ORDER BY AugustHours DESC, p.Name;
GO
