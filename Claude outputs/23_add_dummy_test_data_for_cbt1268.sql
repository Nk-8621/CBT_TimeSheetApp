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

-- ---- A real project / module / tasks to log against ---------------------
DECLARE @ProjectId int, @ModuleId int, @TaskA int, @TaskB int, @Classification nvarchar(20);
SELECT TOP 1 @ProjectId = p.ProjectId, @ModuleId = m.ModuleId, @Classification = p.DefaultBillable
FROM Carbynetech_Project p
JOIN Carbynetech_Module m ON m.ProjectId = p.ProjectId
WHERE p.IsActive = 1
  AND EXISTS (SELECT 1 FROM Carbynetech_Task t WHERE t.ModuleId = m.ModuleId)
ORDER BY (SELECT COUNT(*) FROM Carbynetech_Task t WHERE t.ModuleId = m.ModuleId) DESC, p.ProjectId;

SELECT @TaskA = MIN(TaskId), @TaskB = MAX(TaskId) FROM Carbynetech_Task WHERE ModuleId = @ModuleId;

IF @ProjectId IS NULL
BEGIN
    PRINT 'ERROR: no active project with a module and a task - nothing changed.';
    RETURN;
END

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

-- Project allocation (so their timesheet screens make sense too).
INSERT INTO Carbynetech_EmployeeProjectAllocation (EmployeeId, ProjectId, AssignedAt, Classification, BillingCategory)
SELECT e.Id, @ProjectId, @Now, @Classification, NULL
FROM (VALUES (@Test1Id), (@Test2Id)) e(Id)
WHERE NOT EXISTS (SELECT 1 FROM Carbynetech_EmployeeProjectAllocation a WHERE a.EmployeeId = e.Id AND a.ProjectId = @ProjectId);

-- ---- Timesheet lines: 2 lines x 4 h, Mon-Fri = 40 h each -------------------
INSERT INTO Carbynetech_TimeEntry
    (EmployeeId, WeekStartDate, ProjectId, ModuleId, TaskId, Classification, BillingCategory, Note,
     MondayHours, TuesdayHours, WednesdayHours, ThursdayHours, FridayHours, SaturdayHours, SundayHours, CreatedAt)
SELECT e.Id, @WeekStart, @ProjectId, @ModuleId, l.TaskId, @Classification, NULL, l.Note,
       4, 4, 4, 4, 4, 0, 0, @Now
FROM (VALUES (@Test1Id), (@Test2Id)) e(Id)
CROSS JOIN (VALUES (@TaskA, N'Dummy test data - line 1'), (@TaskB, N'Dummy test data - line 2')) l(TaskId, Note)
WHERE NOT EXISTS (SELECT 1 FROM Carbynetech_TimeEntry t WHERE t.EmployeeId = e.Id AND t.WeekStartDate = @WeekStart);

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
PRINT CONCAT('Done. Week of ', CONVERT(char(10), @WeekStart, 120), ', project ', @ProjectId, ', module ', @ModuleId, '.');
SELECT e.EmployeeCode, e.FullName, m.EmployeeCode AS ManagerCode, w.WeekStartDate, w.Status,
       (SELECT SUM(MondayHours + TuesdayHours + WednesdayHours + ThursdayHours + FridayHours + SaturdayHours + SundayHours)
        FROM Carbynetech_TimeEntry t WHERE t.EmployeeId = e.EmployeeId AND t.WeekStartDate = w.WeekStartDate) AS TotalHours
FROM Carbynetech_Employee e
LEFT JOIN Carbynetech_Employee m ON m.EmployeeId = e.ManagerEmployeeId
LEFT JOIN Carbynetech_WeekRecord w ON w.EmployeeId = e.EmployeeId AND w.WeekStartDate = @WeekStart
WHERE e.EmployeeCode IN ('TEST001', 'TEST002');

SELECT d.RequestDate, d.RequestType, d.Status
FROM Carbynetech_DayTypeRequest d
JOIN Carbynetech_Employee e ON e.EmployeeId = d.EmployeeId
WHERE e.EmployeeCode = 'TEST001';
GO
