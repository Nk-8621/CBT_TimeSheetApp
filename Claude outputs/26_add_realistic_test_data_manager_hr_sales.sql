-- =====================================================================
-- 26_add_realistic_test_data_manager_hr_sales.sql
--
-- TEMPORARY, realistic test data for August 2026 (weeks starting 3, 10,
-- 17, 24 Aug - the same weeks as script 25). Remove it with
-- 27_remove_realistic_test_data_manager_hr_sales.sql.
--
-- People:
--   CBT1152  existing employee - used as the MANAGER (real reporting line)
--   CBT1128  existing employee - used as HR          (real reporting line)
--   TEST005  Rohit Verma, Sales Executive - NEW test employee,
--            reports to CBT1152
--
-- August looks like normal past work: every week submitted and approved by
-- the person's real Level 1 / Level 2 approvers (same routing rule as the
-- app). Rohit's week of 10 Aug was returned once, corrected and approved.
-- WFH / leave (all approved, hours reduced to match):
--   CBT1152  Fri 28 Aug second-half leave
--   CBT1128  Fri  7 Aug WFH, Fri 21 Aug full-day leave
--   TEST005  Wed 12 Aug first-half leave, Wed 26 Aug WFH
--
-- SAFETY FOR REAL EMPLOYEES
--   * Any August week where CBT1152 / CBT1128 already have a week record or
--     time entries is SKIPPED - real data is never touched.
--   * Every row this script writes has a timestamp ending in .777 ms. That
--     invisible marker is how script 27 removes ONLY these rows.
--
-- Master data created only if missing (same as before): department Sales,
-- accounts "HR (Internal)" / "Sales (Internal)", projects INT-HROPS
-- "HR Operations" and INT-SALES "Sales & Pre-Sales". The manager logs on a
-- Billable project he is already allocated to (else the first active one).
--
-- Safe to re-run: earlier marked rows are removed and rewritten.
-- Runs in one transaction: all or nothing.
-- =====================================================================

USE [Carbynetech_TimeSheet_Application];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @MonthStart date = '2026-08-03', @MonthEnd date = '2026-08-30';
-- Marker: a fixed August timestamp ending in .777 ms for "created" columns.
DECLARE @Mark datetime2 = '2026-08-01T09:00:00.777';

-- ---- People ------------------------------------------------------------------
DECLARE @MgrId int, @MgrDeptId int, @MgrLocationId int;
SELECT @MgrId = EmployeeId, @MgrDeptId = DepartmentId, @MgrLocationId = LocationId
FROM Carbynetech_Employee WHERE EmployeeCode = 'CBT1152' AND IsActive = 1;
DECLARE @HrId int = (SELECT EmployeeId FROM Carbynetech_Employee WHERE EmployeeCode = 'CBT1128' AND IsActive = 1);

IF @MgrId IS NULL OR @HrId IS NULL
BEGIN
    PRINT 'ERROR: CBT1152 or CBT1128 not found / not active - nothing changed.';
    RETURN;
END

-- ---- A Billable project for the manager: one he is allocated to if possible ---
DECLARE @MgrProjectId int, @MgrModuleId int, @MgrTaskA int, @MgrTaskB int;
SELECT TOP 1 @MgrProjectId = p.ProjectId, @MgrModuleId = m.ModuleId
FROM Carbynetech_Project p
JOIN Carbynetech_Module m ON m.ProjectId = p.ProjectId
LEFT JOIN Carbynetech_EmployeeProjectAllocation a ON a.ProjectId = p.ProjectId AND a.EmployeeId = @MgrId
WHERE p.IsActive = 1 AND p.Code NOT LIKE 'INT-%'
  AND COALESCE(a.Classification, p.DefaultBillable) = 'Billable'
  AND EXISTS (SELECT 1 FROM Carbynetech_Task t WHERE t.ModuleId = m.ModuleId)
ORDER BY CASE WHEN a.EmployeeId IS NULL THEN 1 ELSE 0 END,
         (SELECT COUNT(*) FROM Carbynetech_Task t WHERE t.ModuleId = m.ModuleId) DESC, p.ProjectId;
SELECT @MgrTaskA = MIN(TaskId), @MgrTaskB = MAX(TaskId) FROM Carbynetech_Task WHERE ModuleId = @MgrModuleId;

IF @MgrProjectId IS NULL
BEGIN
    PRINT 'ERROR: no active Billable project with a module and a task - nothing changed.';
    RETURN;
END

BEGIN TRANSACTION;

-- ---- Departments / accounts / internal projects (only if missing) ------------------
DECLARE @HrDeptId int = (SELECT DepartmentId FROM Carbynetech_Employee WHERE EmployeeId = @HrId);

DECLARE @SalesDeptId int = (SELECT TOP 1 DepartmentId FROM Carbynetech_Department
                            WHERE Code = 'SALES' OR Name = 'Sales' ORDER BY DepartmentId);
IF @SalesDeptId IS NULL
BEGIN
    INSERT INTO Carbynetech_Department (Code, Name, ParentDepartmentId, CreatedAt) VALUES ('SALES', 'Sales', NULL, @Mark);
    SET @SalesDeptId = SCOPE_IDENTITY();
    PRINT 'Created department Sales.';
END

DECLARE @HrAccountId int = (SELECT AccountId FROM Carbynetech_Account WHERE Name = 'HR (Internal)');
IF @HrAccountId IS NULL
BEGIN
    INSERT INTO Carbynetech_Account (DepartmentId, Name, AccountType, CreatedAt) VALUES (@HrDeptId, 'HR (Internal)', 'Internal', @Mark);
    SET @HrAccountId = SCOPE_IDENTITY();
END

DECLARE @SalesAccountId int = (SELECT AccountId FROM Carbynetech_Account WHERE Name = 'Sales (Internal)');
IF @SalesAccountId IS NULL
BEGIN
    INSERT INTO Carbynetech_Account (DepartmentId, Name, AccountType, CreatedAt) VALUES (@SalesDeptId, 'Sales (Internal)', 'Internal', @Mark);
    SET @SalesAccountId = SCOPE_IDENTITY();
END

INSERT INTO Carbynetech_Project (AccountId, Code, Name, DefaultBillable, IsActive, CreatedAt, BillingType, NeedsReview)
SELECT v.AccountId, v.Code, v.Name, 'NonBillable', 1, @Mark, 'Others', 0
FROM (VALUES (@HrAccountId, 'INT-HROPS', 'HR Operations'),
             (@SalesAccountId, 'INT-SALES', 'Sales & Pre-Sales')) v(AccountId, Code, Name)
WHERE NOT EXISTS (SELECT 1 FROM Carbynetech_Project p WHERE p.Code = v.Code);

DECLARE @HrProjectId int    = (SELECT ProjectId FROM Carbynetech_Project WHERE Code = 'INT-HROPS');
DECLARE @SalesProjectId int = (SELECT ProjectId FROM Carbynetech_Project WHERE Code = 'INT-SALES');

DECLARE @Structure TABLE (ProjectId int, ModuleName nvarchar(150), TaskName nvarchar(150));
INSERT INTO @Structure VALUES
    (@HrProjectId,    'Recruitment',         'Screening & Interviews'),
    (@HrProjectId,    'Payroll',             'Payroll Processing'),
    (@HrProjectId,    'Employee Engagement', 'Events & Celebrations'),
    (@SalesProjectId, 'Lead Generation',     'Prospecting'),
    (@SalesProjectId, 'Client Meetings',     'Demos & Discussions'),
    (@SalesProjectId, 'Proposals',           'Proposal Preparation');

INSERT INTO Carbynetech_Module (ProjectId, Name, CreatedAt)
SELECT s.ProjectId, s.ModuleName, @Mark FROM @Structure s
WHERE NOT EXISTS (SELECT 1 FROM Carbynetech_Module m WHERE m.ProjectId = s.ProjectId AND m.Name = s.ModuleName);

INSERT INTO Carbynetech_Task (ModuleId, Name, CreatedAt)
SELECT m.ModuleId, s.TaskName, @Mark
FROM @Structure s
JOIN Carbynetech_Module m ON m.ProjectId = s.ProjectId AND m.Name = s.ModuleName
WHERE NOT EXISTS (SELECT 1 FROM Carbynetech_Task t WHERE t.ModuleId = m.ModuleId AND t.Name = s.TaskName);

-- ---- Sales test employee ---------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM Carbynetech_Employee WHERE EmployeeCode = 'TEST005')
BEGIN
    INSERT INTO Carbynetech_Employee
        (EmployeeCode, FullName, Initials, DepartmentId, LocationId, Designation, JobTitleRaw,
         ManagerEmployeeId, Email, IsActive, CreatedAt, MustChangePassword, LoginAccessGrantedAt, IsExternal)
    VALUES ('TEST005', 'Rohit Verma', 'RV', @SalesDeptId, @MgrLocationId, 'Sales Executive', 'Sales Executive',
            @MgrId, 'test005@carbynetech.com', 1, @Mark, 0, @Mark, 0);
    PRINT 'Created TEST005 (Rohit Verma, reports to CBT1152).';
END
DECLARE @SalesId int = (SELECT EmployeeId FROM Carbynetech_Employee WHERE EmployeeCode = 'TEST005');

-- ---- Remove rows from an earlier run (marked .777 only) ---------------------------
DECLARE @People TABLE (EmployeeId int PRIMARY KEY);
INSERT INTO @People VALUES (@MgrId), (@HrId), (@SalesId);

DELETE a FROM Carbynetech_ApprovalEvent a
JOIN Carbynetech_WeekRecord wr ON wr.WeekRecordId = a.WeekRecordId
WHERE wr.EmployeeId IN (SELECT EmployeeId FROM @People) AND wr.WeekStartDate BETWEEN @MonthStart AND @MonthEnd
  AND DATEPART(millisecond, wr.CreatedAt) = 777;
DELETE FROM Carbynetech_WeekRecord
WHERE EmployeeId IN (SELECT EmployeeId FROM @People) AND WeekStartDate BETWEEN @MonthStart AND @MonthEnd
  AND DATEPART(millisecond, CreatedAt) = 777;
DELETE FROM Carbynetech_TimeEntry
WHERE EmployeeId IN (SELECT EmployeeId FROM @People) AND WeekStartDate BETWEEN @MonthStart AND @MonthEnd
  AND DATEPART(millisecond, CreatedAt) = 777;
DELETE FROM Carbynetech_DayTypeRequest
WHERE EmployeeId IN (SELECT EmployeeId FROM @People) AND RequestDate BETWEEN @MonthStart AND @MonthEnd
  AND DATEPART(millisecond, SubmittedAt) = 777;
DELETE FROM Carbynetech_DayTypeOverride
WHERE EmployeeId IN (SELECT EmployeeId FROM @People) AND EntryDate BETWEEN @MonthStart AND @MonthEnd
  AND DATEPART(millisecond, CreatedAt) = 777;

-- ---- Weeks to fill: skip any week that already has real data -------------------------
DECLARE @Weeks TABLE (WeekIdx int PRIMARY KEY, WeekStart date);
INSERT INTO @Weeks VALUES (0, '2026-08-03'), (1, '2026-08-10'), (2, '2026-08-17'), (3, '2026-08-24');

DECLARE @Fill TABLE (EmployeeId int, WeekIdx int, WeekStart date, PRIMARY KEY (EmployeeId, WeekIdx));
INSERT INTO @Fill
SELECT p.EmployeeId, w.WeekIdx, w.WeekStart
FROM @People p CROSS JOIN @Weeks w
WHERE NOT EXISTS (SELECT 1 FROM Carbynetech_WeekRecord wr WHERE wr.EmployeeId = p.EmployeeId AND wr.WeekStartDate = w.WeekStart)
  AND NOT EXISTS (SELECT 1 FROM Carbynetech_TimeEntry t WHERE t.EmployeeId = p.EmployeeId AND t.WeekStartDate = w.WeekStart);

SELECT e.EmployeeCode, w.WeekStart AS SkippedWeek_RealDataExists
FROM @People p
JOIN Carbynetech_Employee e ON e.EmployeeId = p.EmployeeId
CROSS JOIN @Weeks w
WHERE NOT EXISTS (SELECT 1 FROM @Fill f WHERE f.EmployeeId = p.EmployeeId AND f.WeekIdx = w.WeekIdx);

-- ---- Approval routing (same rule as WeekApprovalService) -----------------------------
-- Level 1 = manager, Level 2 = manager's manager. If the manager has no
-- manager, Level 1 is skipped and the manager approves at Level 2.
DECLARE @Routing TABLE (EmployeeId int PRIMARY KEY, EmployeeName nvarchar(150), SkipL1 bit,
                        L1Id int NULL, L1Name nvarchar(150) NULL, L2Id int NULL, L2Name nvarchar(150) NULL);
INSERT INTO @Routing
SELECT e.EmployeeId, e.FullName,
       CASE WHEN s.EmployeeId IS NULL THEN 1 ELSE 0 END,
       CASE WHEN s.EmployeeId IS NULL THEN NULL ELSE m.EmployeeId END,
       CASE WHEN s.EmployeeId IS NULL THEN NULL ELSE m.FullName END,
       COALESCE(s.EmployeeId, m.EmployeeId), COALESCE(s.FullName, m.FullName)
FROM @People p
JOIN Carbynetech_Employee e ON e.EmployeeId = p.EmployeeId
LEFT JOIN Carbynetech_Employee m ON m.EmployeeId = e.ManagerEmployeeId
LEFT JOIN Carbynetech_Employee s ON s.EmployeeId = m.ManagerEmployeeId;

IF EXISTS (SELECT 1 FROM @Routing WHERE L2Id IS NULL)
BEGIN
    ROLLBACK TRANSACTION;
    PRINT 'ERROR: CBT1152 or CBT1128 has no manager (top of hierarchy, no timesheet) - nothing changed.';
    RETURN;
END

-- ---- Project allocations (only if missing; marked) ---------------------------------
INSERT INTO Carbynetech_EmployeeProjectAllocation (EmployeeId, ProjectId, AssignedAt, Classification, BillingCategory)
SELECT v.EmployeeId, v.ProjectId, @Mark, v.Classification, v.Category
FROM (VALUES (@MgrId,   @MgrProjectId,   'Billable',    'AMS'),
             (@HrId,    @HrProjectId,    'NonBillable', 'OH'),
             (@SalesId, @SalesProjectId, 'NonBillable', 'OH')) v(EmployeeId, ProjectId, Classification, Category)
WHERE NOT EXISTS (SELECT 1 FROM Carbynetech_EmployeeProjectAllocation a WHERE a.EmployeeId = v.EmployeeId AND a.ProjectId = v.ProjectId);

-- ---- WFH / leave requests (dropped if that week is skipped or the day already has a request) ---
DECLARE @Requests TABLE (EmployeeId int, RequestDate date, RequestType nvarchar(20), Note nvarchar(500));
INSERT INTO @Requests
SELECT v.EmployeeId, v.RequestDate, v.RequestType, v.Note
FROM (VALUES
    (@MgrId,   CAST('2026-08-28' AS date), 'LeaveSecondHalf', N'Personal work in the afternoon'),
    (@HrId,    CAST('2026-08-07' AS date), 'WFH',             N'Internet installation at home'),
    (@HrId,    CAST('2026-08-21' AS date), 'LeaveFull',       N'Family function'),
    (@SalesId, CAST('2026-08-12' AS date), 'LeaveFirstHalf',  N'Doctor appointment'),
    (@SalesId, CAST('2026-08-26' AS date), 'WFH',             N'Preparing proposal documents from home')
) v(EmployeeId, RequestDate, RequestType, Note)
WHERE EXISTS (SELECT 1 FROM @Fill f WHERE f.EmployeeId = v.EmployeeId AND v.RequestDate BETWEEN f.WeekStart AND DATEADD(day, 6, f.WeekStart))
  AND NOT EXISTS (SELECT 1 FROM Carbynetech_DayTypeRequest d WHERE d.EmployeeId = v.EmployeeId AND d.RequestDate = v.RequestDate);

-- Hours available per working day: 8, 4 on a half-day leave, 0 on a full-day leave.
DECLARE @DayCapacity TABLE (EmployeeId int, WorkDate date, Capacity decimal(4,2), PRIMARY KEY (EmployeeId, WorkDate));
INSERT INTO @DayCapacity (EmployeeId, WorkDate, Capacity)
SELECT f.EmployeeId, DATEADD(day, d.n, f.WeekStart),
       COALESCE((SELECT TOP 1 CASE q.RequestType WHEN 'LeaveFull' THEN 0 WHEN 'LeaveFirstHalf' THEN 4 WHEN 'LeaveSecondHalf' THEN 4 ELSE 8 END
                 FROM @Requests q WHERE q.EmployeeId = f.EmployeeId AND q.RequestDate = DATEADD(day, d.n, f.WeekStart)), 8)
FROM @Fill f
CROSS JOIN (VALUES (0), (1), (2), (3), (4)) d(n);

-- ---- Timesheet lines --------------------------------------------------------------
-- Share of each day (manager 1/2 + 1/2, HR 1/2 + 1/4 + 1/4, Sales 3/8 + 3/8 + 1/4).
DECLARE @Lines TABLE (EmployeeId int, LineNum int, ProjectId int, ModuleId int, TaskId int, Share decimal(5,4));
INSERT INTO @Lines
SELECT @MgrId, 1, @MgrProjectId, @MgrModuleId, @MgrTaskA, 0.5 UNION ALL
SELECT @MgrId, 2, @MgrProjectId, @MgrModuleId, @MgrTaskB, 0.5 UNION ALL
SELECT @HrId, s.LineNum, @HrProjectId, m.ModuleId, t.TaskId, s.Share
FROM (VALUES (1, 'Recruitment', 0.5), (2, 'Payroll', 0.25), (3, 'Employee Engagement', 0.25)) s(LineNum, ModuleName, Share)
JOIN Carbynetech_Module m ON m.ProjectId = @HrProjectId AND m.Name = s.ModuleName
JOIN Carbynetech_Task t ON t.ModuleId = m.ModuleId
UNION ALL
SELECT @SalesId, s.LineNum, @SalesProjectId, m.ModuleId, t.TaskId, s.Share
FROM (VALUES (1, 'Lead Generation', 0.375), (2, 'Client Meetings', 0.375), (3, 'Proposals', 0.25)) s(LineNum, ModuleName, Share)
JOIN Carbynetech_Module m ON m.ProjectId = @SalesProjectId AND m.Name = s.ModuleName
JOIN Carbynetech_Task t ON t.ModuleId = m.ModuleId;

DECLARE @Notes TABLE (EmployeeId int, LineNum int, WeekIdx int, Note nvarchar(500));
INSERT INTO @Notes VALUES
    (@MgrId, 1, 0, N'Weekly status call with client and sprint review'),
    (@MgrId, 2, 0, N'Sprint planning and team task allocation'),
    (@MgrId, 1, 1, N'Client escalation call on open defects'),
    (@MgrId, 2, 1, N'Resource planning for upcoming change requests'),
    (@MgrId, 1, 2, N'Steering committee update and demo to client'),
    (@MgrId, 2, 2, N'Code review follow-ups and risk tracking'),
    (@MgrId, 1, 3, N'Monthly service review with client'),
    (@MgrId, 2, 3, N'Effort estimation for new enhancement'),
    (@HrId, 1, 0, N'Screening profiles for Java developer role'),
    (@HrId, 2, 0, N'Collecting attendance for July payroll'),
    (@HrId, 3, 0, N'Planning Independence Day celebration'),
    (@HrId, 1, 1, N'Scheduling technical interviews with panel'),
    (@HrId, 2, 1, N'Verifying reimbursement claims'),
    (@HrId, 3, 1, N'Independence Day event coordination'),
    (@HrId, 1, 2, N'Offer letters and joining formalities'),
    (@HrId, 2, 2, N'Payroll inputs for new joiners'),
    (@HrId, 3, 2, N'Employee feedback survey'),
    (@HrId, 1, 3, N'Onboarding two new joiners'),
    (@HrId, 2, 3, N'August payroll processing'),
    (@HrId, 3, 3, N'Planning Ganesh Chaturthi celebration'),
    (@SalesId, 1, 0, N'LinkedIn outreach to manufacturing companies'),
    (@SalesId, 2, 0, N'Discovery call with a pharma prospect'),
    (@SalesId, 3, 0, N'Proposal draft for SAP AMS support'),
    (@SalesId, 1, 1, N'Follow-up emails to trade fair leads'),
    (@SalesId, 2, 1, N'Product demo for a prospective SAP client'),
    (@SalesId, 3, 1, N'Pricing sheet for analytics proposal'),
    (@SalesId, 1, 2, N'Building lead list for the steel industry'),
    (@SalesId, 2, 2, N'Client visit and requirement discussion'),
    (@SalesId, 3, 2, N'Revised proposal after client feedback'),
    (@SalesId, 1, 3, N'Cold calls to shortlisted prospects'),
    (@SalesId, 2, 3, N'Contract negotiation meeting'),
    (@SalesId, 3, 3, N'Final proposal submission');

-- Line rows are "created" on the Monday of their week, at 10:00.777.
INSERT INTO Carbynetech_TimeEntry
    (EmployeeId, WeekStartDate, ProjectId, ModuleId, TaskId, Classification, BillingCategory, Note,
     MondayHours, TuesdayHours, WednesdayHours, ThursdayHours, FridayHours, SaturdayHours, SundayHours, CreatedAt)
SELECT l.EmployeeId, f.WeekStart, l.ProjectId, l.ModuleId, l.TaskId, a.Classification, a.BillingCategory, n.Note,
       (SELECT Capacity FROM @DayCapacity c WHERE c.EmployeeId = l.EmployeeId AND c.WorkDate = f.WeekStart) * l.Share,
       (SELECT Capacity FROM @DayCapacity c WHERE c.EmployeeId = l.EmployeeId AND c.WorkDate = DATEADD(day, 1, f.WeekStart)) * l.Share,
       (SELECT Capacity FROM @DayCapacity c WHERE c.EmployeeId = l.EmployeeId AND c.WorkDate = DATEADD(day, 2, f.WeekStart)) * l.Share,
       (SELECT Capacity FROM @DayCapacity c WHERE c.EmployeeId = l.EmployeeId AND c.WorkDate = DATEADD(day, 3, f.WeekStart)) * l.Share,
       (SELECT Capacity FROM @DayCapacity c WHERE c.EmployeeId = l.EmployeeId AND c.WorkDate = DATEADD(day, 4, f.WeekStart)) * l.Share,
       0, 0,
       DATEADD(millisecond, 777, DATEADD(hour, 10, CAST(f.WeekStart AS datetime2)))
FROM @Lines l
JOIN @Fill f ON f.EmployeeId = l.EmployeeId
JOIN @Notes n ON n.EmployeeId = l.EmployeeId AND n.LineNum = l.LineNum AND n.WeekIdx = f.WeekIdx
JOIN Carbynetech_EmployeeProjectAllocation a ON a.EmployeeId = l.EmployeeId AND a.ProjectId = l.ProjectId;
PRINT CONCAT(@@ROWCOUNT, ' timesheet line(s) written.');

-- ---- Requests: asked 2 days before, approved by the real manager the next day ----
INSERT INTO Carbynetech_DayTypeRequest
    (EmployeeId, RequestDate, RequestType, Status, Note, SubmittedAt, ApproverEmployeeId, DecidedAt, DecisionComment, Source)
SELECT q.EmployeeId, q.RequestDate, q.RequestType, 'Approved', q.Note,
       DATEADD(millisecond, 777, DATEADD(hour, 10, CAST(DATEADD(day, -2, q.RequestDate) AS datetime2))),
       e.ManagerEmployeeId,
       DATEADD(millisecond, 777, DATEADD(hour, 15, CAST(DATEADD(day, -1, q.RequestDate) AS datetime2))),
       NULL, 'Meridian'
FROM @Requests q
JOIN Carbynetech_Employee e ON e.EmployeeId = q.EmployeeId;
PRINT CONCAT(@@ROWCOUNT, ' WFH/leave request(s) added.');

INSERT INTO Carbynetech_DayTypeOverride (EmployeeId, EntryDate, DayType, CreatedAt)
SELECT q.EmployeeId, q.RequestDate, 'WFH', @Mark FROM @Requests q
WHERE q.RequestType = 'WFH'
  AND NOT EXISTS (SELECT 1 FROM Carbynetech_DayTypeOverride o WHERE o.EmployeeId = q.EmployeeId AND o.EntryDate = q.RequestDate);

-- ---- Week records + approval trail --------------------------------------------
-- Submitted Friday 18:00, Level 1 approved Monday 11:00, Level 2 approved
-- Monday 16:00 (all timestamps end in .777). Rohit's 10 Aug week: returned
-- Monday 11:00, resubmitted Monday 14:00, then approved Tuesday.
DECLARE @EmpId int, @WeekStart date, @WeekId int, @Sub datetime2, @L1At datetime2, @L2At datetime2, @Returned bit;
DECLARE @EmpName nvarchar(150), @SkipL1 bit, @L1Id int, @L1Name nvarchar(150), @L2Id int, @L2Name nvarchar(150);
DECLARE @Reason nvarchar(200) = N'Please split client meeting hours by client name';

DECLARE week_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT f.EmployeeId, f.WeekStart,
           CASE WHEN f.EmployeeId = @SalesId AND f.WeekIdx = 1 THEN 1 ELSE 0 END,
           r.EmployeeName, r.SkipL1, r.L1Id, r.L1Name, r.L2Id, r.L2Name
    FROM @Fill f
    JOIN @Routing r ON r.EmployeeId = f.EmployeeId
    ORDER BY f.EmployeeId, f.WeekIdx;
OPEN week_cursor;
FETCH NEXT FROM week_cursor INTO @EmpId, @WeekStart, @Returned, @EmpName, @SkipL1, @L1Id, @L1Name, @L2Id, @L2Name;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @Sub  = DATEADD(millisecond, 777, DATEADD(hour, 18, CAST(DATEADD(day, 4, @WeekStart) AS datetime2)));  -- Fri 18:00
    SET @L1At = DATEADD(hour, 65, @Sub);   -- Mon 11:00
    SET @L2At = DATEADD(hour, 70, @Sub);   -- Mon 16:00

    INSERT INTO Carbynetech_WeekRecord (EmployeeId, WeekStartDate, Status, SubmittedAt, RejectedByEmployeeId, RejectionReason, CreatedAt, UpdatedAt)
    VALUES (@EmpId, @WeekStart, 'Approved', CASE WHEN @Returned = 1 THEN DATEADD(hour, 68, @Sub) ELSE @Sub END,
            NULL, NULL, DATEADD(millisecond, 777, DATEADD(hour, 10, CAST(@WeekStart AS datetime2))),
            CASE WHEN @Returned = 1 THEN DATEADD(hour, 94, @Sub) ELSE @L2At END);
    SET @WeekId = SCOPE_IDENTITY();

    INSERT INTO Carbynetech_ApprovalEvent (WeekRecordId, EventText, EventMeta, EventStatus, ActedByEmployeeId, EventTimestamp)
    VALUES (@WeekId, CONCAT(N'Submitted by ', @EmpName), NULL, 'Ok', @EmpId, @Sub);

    IF @Returned = 1
    BEGIN
        -- Returned by Level 1, corrected and submitted again; times shift by a day.
        INSERT INTO Carbynetech_ApprovalEvent (WeekRecordId, EventText, EventMeta, EventStatus, ActedByEmployeeId, EventTimestamp)
        VALUES (@WeekId, CONCAT(N'Awaiting Level 1 — ', COALESCE(@L1Name, @L2Name)), 'Pending', 'Pending', NULL, DATEADD(second, 1, @Sub)),
               (@WeekId, CONCAT(N'Returned by ', COALESCE(@L1Name, @L2Name)), @Reason, 'Rejected', COALESCE(@L1Id, @L2Id), @L1At),
               (@WeekId, CONCAT(N'Submitted by ', @EmpName), NULL, 'Ok', @EmpId, DATEADD(hour, 68, @Sub));
        SET @Sub  = DATEADD(hour, 68, @Sub);   -- resubmitted Mon 14:00
        SET @L1At = DATEADD(hour, 20, @Sub);   -- Tue 10:00
        SET @L2At = DATEADD(hour, 26, @Sub);   -- Tue 16:00
    END

    IF @SkipL1 = 1
        INSERT INTO Carbynetech_ApprovalEvent (WeekRecordId, EventText, EventMeta, EventStatus, ActedByEmployeeId, EventTimestamp)
        VALUES (@WeekId, CONCAT(N'Awaiting Level 2 — ', @L2Name, N' (reports directly to senior leadership; no Level 1 reviewer)'), 'Pending', 'Pending', NULL, DATEADD(second, 1, @Sub)),
               (@WeekId, CONCAT(N'Level 2 approved — ', @L2Name), NULL, 'Ok', @L2Id, @L2At),
               (@WeekId, N'Week locked', NULL, 'Ok', NULL, DATEADD(second, 1, @L2At));
    ELSE
        INSERT INTO Carbynetech_ApprovalEvent (WeekRecordId, EventText, EventMeta, EventStatus, ActedByEmployeeId, EventTimestamp)
        VALUES (@WeekId, CONCAT(N'Awaiting Level 1 — ', @L1Name), 'Pending', 'Pending', NULL, DATEADD(second, 2, @Sub)),
               (@WeekId, CONCAT(N'Level 1 approved — ', @L1Name), NULL, 'Ok', @L1Id, @L1At),
               (@WeekId, CONCAT(N'Awaiting Level 2 — ', @L2Name), 'Pending', 'Pending', NULL, DATEADD(second, 1, @L1At)),
               (@WeekId, CONCAT(N'Level 2 approved — ', @L2Name), NULL, 'Ok', @L2Id, @L2At),
               (@WeekId, N'Week locked', NULL, 'Ok', NULL, DATEADD(second, 1, @L2At));

    FETCH NEXT FROM week_cursor INTO @EmpId, @WeekStart, @Returned, @EmpName, @SkipL1, @L1Id, @L1Name, @L2Id, @L2Name;
END
CLOSE week_cursor;
DEALLOCATE week_cursor;

COMMIT TRANSACTION;

-- ---- Summary ------------------------------------------------------------------
PRINT 'Done.';
SELECT e.EmployeeCode, e.FullName, e.Designation, d.Name AS Department, m.EmployeeCode AS ManagerCode,
       wr.WeekStartDate, wr.Status,
       SUM(t.MondayHours + t.TuesdayHours + t.WednesdayHours + t.ThursdayHours + t.FridayHours) AS TotalHours
FROM Carbynetech_Employee e
JOIN Carbynetech_Department d ON d.DepartmentId = e.DepartmentId
LEFT JOIN Carbynetech_Employee m ON m.EmployeeId = e.ManagerEmployeeId
JOIN Carbynetech_WeekRecord wr ON wr.EmployeeId = e.EmployeeId AND wr.WeekStartDate BETWEEN @MonthStart AND @MonthEnd
                              AND DATEPART(millisecond, wr.CreatedAt) = 777
LEFT JOIN Carbynetech_TimeEntry t ON t.EmployeeId = e.EmployeeId AND t.WeekStartDate = wr.WeekStartDate
WHERE e.EmployeeCode IN ('CBT1152', 'CBT1128', 'TEST005')
GROUP BY e.EmployeeCode, e.FullName, e.Designation, d.Name, m.EmployeeCode, wr.WeekStartDate, wr.Status
ORDER BY e.EmployeeCode, wr.WeekStartDate;

SELECT e.EmployeeCode, q.RequestDate, q.RequestType, q.Status
FROM Carbynetech_DayTypeRequest q
JOIN Carbynetech_Employee e ON e.EmployeeId = q.EmployeeId
WHERE e.EmployeeCode IN ('CBT1152', 'CBT1128', 'TEST005') AND q.RequestDate BETWEEN @MonthStart AND @MonthEnd
  AND DATEPART(millisecond, q.SubmittedAt) = 777
ORDER BY e.EmployeeCode, q.RequestDate;
GO
