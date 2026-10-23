-- =====================================================================
-- 23_add_dummy_test_data_for_cbt1268.sql
--
-- TEMPORARY test data so CBT1268 can see the approval screens with
-- something in them. Remove it all with 24_remove_dummy_test_data.sql.
--
-- Creates (only if missing):
--   TEST001  Test Employee A  - manager CBT1268
--   TEST002  Test Employee B  - manager TEST001  (so CBT1268 is their Level 2)
-- and for the CURRENT week (Monday of today):
--   TEST001: 40 h submitted, waiting at Level 1      -> CBT1268 "Level 1 Approvals"
--   TEST002: 40 h, Level 1 approved by TEST001,
--            waiting at Level 2                      -> CBT1268 "Level 2 Approvals"
-- plus two pending leave requests for TEST001 next week
--                                                    -> CBT1268 "Request Approval"
-- The test employees also show on Team Compliance, Reports and Master Data.
-- Each week has one Billable, one PartialBillable and one NonBillable line
-- (on 3 different projects) so the billable split can be checked:
--   TEST001  50% billable / 25% partial / 25% non-billable
--   TEST002  25% billable / 50% partial / 25% non-billable
-- Safe to re-run: allocations and this week's lines are rewritten each time.
--
-- Rows are written exactly the way the app writes them (see
-- WeekApprovalService.SubmitAsync / ApproveLevel1Async and
-- DayTypeRequestService.SubmitAsync). Runs in one transaction: either
-- everything is added or nothing is.
-- =====================================================================

USE [Carbynetech_TimeSheet_Application];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Now datetime2 = SYSUTCDATETIME();
DECLARE @Today date = CAST(GETDATE() AS date);
-- Monday of the current week, whatever SET DATEFIRST is.
DECLARE @WeekStart date = DATEADD(day, -((DATEPART(weekday, @Today) + @@DATEFIRST - 2) % 7), @Today);

-- ---- The approver -----------------------------------------------------
DECLARE @ApproverId int, @ApproverName nvarchar(150), @ApproverDeptId int, @ApproverLocationId int, @ApproverManagerId int;
SELECT @ApproverId = EmployeeId, @ApproverName = FullName, @ApproverDeptId = DepartmentId,
       @ApproverLocationId = LocationId, @ApproverManagerId = ManagerEmployeeId
FROM Carbynetech_Employee WHERE EmployeeCode = 'CBT1268';

IF @ApproverId IS NULL
BEGIN
    PRINT 'ERROR: CBT1268 not found - nothing changed.';
    RETURN;
END
IF @ApproverManagerId IS NULL
    PRINT 'NOTE: CBT1268 has no manager, so TEST001''s week will route straight to Level 2 instead of Level 1.';

-- ---- Three real projects, one per billing classification -------------------
-- The billing type of a line comes from the employee's allocation to that
-- project (one classification per employee+project), so each type needs its
-- own project. Picks 3 active projects that each have a module with a task.
DECLARE @Picks TABLE (Slot int PRIMARY KEY, ProjectId int, ModuleId int, TaskId int);
INSERT INTO @Picks (Slot, ProjectId, ModuleId, TaskId)
SELECT TOP 3 ROW_NUMBER() OVER (ORDER BY x.ProjectId), x.ProjectId, x.ModuleId, x.TaskId
FROM (
    SELECT p.ProjectId, m.ModuleId, t.TaskId,
           ROW_NUMBER() OVER (PARTITION BY p.ProjectId ORDER BY m.ModuleId, t.TaskId) AS rn
    FROM Carbynetech_Project p
    JOIN Carbynetech_Module m ON m.ProjectId = p.ProjectId
    JOIN Carbynetech_Task t ON t.ModuleId = m.ModuleId
    WHERE p.IsActive = 1
) x
WHERE x.rn = 1
ORDER BY x.ProjectId;

IF (SELECT COUNT(*) FROM @Picks) < 3
BEGIN
    PRINT 'ERROR: need 3 active projects that each have a module with a task - nothing changed.';
    RETURN;
END

-- Hours per day (Mon-Fri) and billing category for each line.
--   TEST001: Billable 4 h (AMS) + Partial 2 h + NonBillable 2 h (OH) = 50% / 25% / 25%
--   TEST002: Billable 2 h (T&M) + Partial 4 h + NonBillable 2 h (OH) = 25% / 50% / 25%
DECLARE @Lines TABLE (EmployeeCode nvarchar(15), Slot int, Classification nvarchar(20), BillingCategory nvarchar(10), DailyHours decimal(4,2));
INSERT INTO @Lines VALUES
    ('TEST001', 1, 'Billable',        'AMS', 4),
    ('TEST001', 2, 'PartialBillable', NULL,  2),
    ('TEST001', 3, 'NonBillable',     'OH',  2),
    ('TEST002', 1, 'Billable',        'T&M', 2),
    ('TEST002', 2, 'PartialBillable', NULL,  4),
    ('TEST002', 3, 'NonBillable',     'OH',  2);

BEGIN TRANSACTION;

-- ---- Test employees ------------------------------------------------------
DECLARE @Test1Id int = (SELECT EmployeeId FROM Carbynetech_Employee WHERE EmployeeCode = 'TEST001');
IF @Test1Id IS NULL
BEGIN
    INSERT INTO Carbynetech_Employee
        (EmployeeCode, FullName, Initials, DepartmentId, LocationId, Designation, JobTitleRaw,
         ManagerEmployeeId, Email, IsActive, CreatedAt, MustChangePassword, LoginAccessGrantedAt, IsExternal)
    VALUES
        ('TEST001', 'Test Employee A', 'TA', @ApproverDeptId, @ApproverLocationId, 'Test', 'Test',
         @ApproverId, 'test001@carbynetech.com', 1, @Now, 0, @Now, 0);
    SET @Test1Id = SCOPE_IDENTITY();
    PRINT 'Created TEST001 (manager CBT1268).';
END
ELSE IF NOT EXISTS (SELECT 1 FROM Carbynetech_Employee WHERE EmployeeId = @Test1Id AND ManagerEmployeeId = @ApproverId)
BEGIN
    ROLLBACK TRANSACTION;
    PRINT 'ERROR: TEST001 already exists but its manager is not CBT1268 - nothing changed.';
    RETURN;
END

DECLARE @Test2Id int = (SELECT EmployeeId FROM Carbynetech_Employee WHERE EmployeeCode = 'TEST002');
IF @Test2Id IS NULL
BEGIN
    INSERT INTO Carbynetech_Employee
        (EmployeeCode, FullName, Initials, DepartmentId, LocationId, Designation, JobTitleRaw,
         ManagerEmployeeId, Email, IsActive, CreatedAt, MustChangePassword, LoginAccessGrantedAt, IsExternal)
    VALUES
        ('TEST002', 'Test Employee B', 'TB', @ApproverDeptId, @ApproverLocationId, 'Test', 'Test',
         @Test1Id, 'test002@carbynetech.com', 1, @Now, 0, @Now, 0);
    SET @Test2Id = SCOPE_IDENTITY();
    PRINT 'Created TEST002 (manager TEST001).';
END
ELSE IF NOT EXISTS (SELECT 1 FROM Carbynetech_Employee WHERE EmployeeId = @Test2Id AND ManagerEmployeeId = @Test1Id)
BEGIN
    ROLLBACK TRANSACTION;
    PRINT 'ERROR: TEST002 already exists but its manager is not TEST001 - nothing changed.';
    RETURN;
END

-- ---- Project allocations + timesheet lines --------------------------------
-- Replaced on every run (test employees only), so re-running this script
-- after an earlier version updates their data instead of skipping it.
DECLARE @TestEmployees TABLE (EmployeeCode nvarchar(15) PRIMARY KEY, EmployeeId int);
INSERT INTO @TestEmployees VALUES ('TEST001', @Test1Id), ('TEST002', @Test2Id);

DELETE FROM Carbynetech_EmployeeProjectAllocation WHERE EmployeeId IN (@Test1Id, @Test2Id);
INSERT INTO Carbynetech_EmployeeProjectAllocation (EmployeeId, ProjectId, AssignedAt, Classification, BillingCategory)
SELECT te.EmployeeId, pk.ProjectId, @Now, l.Classification, l.BillingCategory
FROM @Lines l
JOIN @TestEmployees te ON te.EmployeeCode = l.EmployeeCode
JOIN @Picks pk ON pk.Slot = l.Slot;

DELETE FROM Carbynetech_TimeEntry WHERE EmployeeId IN (@Test1Id, @Test2Id) AND WeekStartDate = @WeekStart;
INSERT INTO Carbynetech_TimeEntry
    (EmployeeId, WeekStartDate, ProjectId, ModuleId, TaskId, Classification, BillingCategory, Note,
     MondayHours, TuesdayHours, WednesdayHours, ThursdayHours, FridayHours, SaturdayHours, SundayHours, CreatedAt)
SELECT te.EmployeeId, @WeekStart, pk.ProjectId, pk.ModuleId, pk.TaskId, l.Classification, l.BillingCategory,
       CONCAT(N'Dummy test data - ', l.Classification),
       l.DailyHours, l.DailyHours, l.DailyHours, l.DailyHours, l.DailyHours, 0, 0, @Now
FROM @Lines l
JOIN @TestEmployees te ON te.EmployeeCode = l.EmployeeCode
JOIN @Picks pk ON pk.Slot = l.Slot;
PRINT CONCAT(@@ROWCOUNT, ' timesheet line(s) written (Billable / PartialBillable / NonBillable).');

-- ---- Week records + approval trail -------------------------------------
DECLARE @Week1Id int, @Week2Id int;

IF NOT EXISTS (SELECT 1 FROM Carbynetech_WeekRecord WHERE EmployeeId = @Test1Id AND WeekStartDate = @WeekStart)
BEGIN
    INSERT INTO Carbynetech_WeekRecord (EmployeeId, WeekStartDate, Status, SubmittedAt, CreatedAt, UpdatedAt)
    VALUES (@Test1Id, @WeekStart, CASE WHEN @ApproverManagerId IS NULL THEN 'PendingL2' ELSE 'PendingL1' END, @Now, @Now, @Now);
    SET @Week1Id = SCOPE_IDENTITY();

    INSERT INTO Carbynetech_ApprovalEvent (WeekRecordId, EventText, EventMeta, EventStatus, ActedByEmployeeId, EventTimestamp)
    VALUES (@Week1Id, N'Submitted by Test Employee A', NULL, 'Ok', @Test1Id, @Now),
           (@Week1Id,
            CASE WHEN @ApproverManagerId IS NULL
                 THEN CONCAT(N'Awaiting Level 2 — ', @ApproverName, ' (reports directly to senior leadership; no Level 1 reviewer)')
                 ELSE CONCAT(N'Awaiting Level 1 — ', @ApproverName) END,
            'Pending', 'Pending', NULL, DATEADD(second, 1, @Now));
    PRINT 'TEST001 week submitted (waiting for CBT1268).';
END

IF NOT EXISTS (SELECT 1 FROM Carbynetech_WeekRecord WHERE EmployeeId = @Test2Id AND WeekStartDate = @WeekStart)
BEGIN
    INSERT INTO Carbynetech_WeekRecord (EmployeeId, WeekStartDate, Status, SubmittedAt, CreatedAt, UpdatedAt)
    VALUES (@Test2Id, @WeekStart, 'PendingL2', @Now, @Now, @Now);
    SET @Week2Id = SCOPE_IDENTITY();

    INSERT INTO Carbynetech_ApprovalEvent (WeekRecordId, EventText, EventMeta, EventStatus, ActedByEmployeeId, EventTimestamp)
    VALUES (@Week2Id, N'Submitted by Test Employee B', NULL, 'Ok', @Test2Id, @Now),
           (@Week2Id, N'Awaiting Level 1 — Test Employee A', 'Pending', 'Pending', NULL, DATEADD(second, 1, @Now)),
           (@Week2Id, N'Level 1 approved — Test Employee A', NULL, 'Ok', @Test1Id, DATEADD(second, 2, @Now)),
           (@Week2Id, CONCAT(N'Awaiting Level 2 — ', @ApproverName), 'Pending', 'Pending', NULL, DATEADD(second, 3, @Now));
    PRINT 'TEST002 week Level 1 approved (waiting for CBT1268 at Level 2).';
END

-- ---- Two pending leave requests for TEST001, next week ---------------------
INSERT INTO Carbynetech_DayTypeRequest (EmployeeId, RequestDate, RequestType, Status, Note, SubmittedAt, Source)
SELECT @Test1Id, r.RequestDate, r.RequestType, 'Pending', N'Dummy test request', @Now, 'Meridian'
FROM (VALUES (DATEADD(day, 9, @WeekStart), 'LeaveFull'),
             (DATEADD(day, 11, @WeekStart), 'LeaveFirstHalf')) r(RequestDate, RequestType)
WHERE NOT EXISTS (SELECT 1 FROM Carbynetech_DayTypeRequest d WHERE d.EmployeeId = @Test1Id AND d.RequestDate = r.RequestDate);

COMMIT TRANSACTION;

-- ---- Summary ---------------------------------------------------------------
PRINT CONCAT('Done. Week of ', CONVERT(char(10), @WeekStart, 120), '.');

-- Expected Team Compliance split per person (compare with the screen).
SELECT e.EmployeeCode, e.FullName, m.EmployeeCode AS ManagerCode, w.Status,
       SUM(t.MondayHours + t.TuesdayHours + t.WednesdayHours + t.ThursdayHours + t.FridayHours + t.SaturdayHours + t.SundayHours) AS TotalHours,
       SUM(CASE WHEN t.Classification = 'Billable'        THEN t.MondayHours + t.TuesdayHours + t.WednesdayHours + t.ThursdayHours + t.FridayHours + t.SaturdayHours + t.SundayHours ELSE 0 END) AS BillableHours,
       SUM(CASE WHEN t.Classification = 'PartialBillable' THEN t.MondayHours + t.TuesdayHours + t.WednesdayHours + t.ThursdayHours + t.FridayHours + t.SaturdayHours + t.SundayHours ELSE 0 END) AS PartialBillableHours,
       SUM(CASE WHEN t.Classification = 'NonBillable'     THEN t.MondayHours + t.TuesdayHours + t.WednesdayHours + t.ThursdayHours + t.FridayHours + t.SaturdayHours + t.SundayHours ELSE 0 END) AS NonBillableHours
FROM Carbynetech_Employee e
LEFT JOIN Carbynetech_Employee m ON m.EmployeeId = e.ManagerEmployeeId
LEFT JOIN Carbynetech_WeekRecord w ON w.EmployeeId = e.EmployeeId AND w.WeekStartDate = @WeekStart
LEFT JOIN Carbynetech_TimeEntry t ON t.EmployeeId = e.EmployeeId AND t.WeekStartDate = @WeekStart
WHERE e.EmployeeCode IN ('TEST001', 'TEST002')
GROUP BY e.EmployeeCode, e.FullName, m.EmployeeCode, w.Status;

-- The lines themselves.
SELECT e.EmployeeCode, p.Code AS ProjectCode, t.Classification, t.BillingCategory, t.MondayHours AS HoursPerDay
FROM Carbynetech_TimeEntry t
JOIN Carbynetech_Employee e ON e.EmployeeId = t.EmployeeId
JOIN Carbynetech_Project p ON p.ProjectId = t.ProjectId
WHERE e.EmployeeCode IN ('TEST001', 'TEST002') AND t.WeekStartDate = @WeekStart
ORDER BY e.EmployeeCode, t.Classification;

SELECT d.RequestDate, d.RequestType, d.Status
FROM Carbynetech_DayTypeRequest d
JOIN Carbynetech_Employee e ON e.EmployeeId = d.EmployeeId
WHERE e.EmployeeCode = 'TEST001';
GO
