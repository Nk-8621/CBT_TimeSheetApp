-- =====================================================================
-- 41_techeff_dry_run_readiness.sql
--
-- TECHEFF DRY RUN - READINESS CHECK (Nov 2026) - READ ONLY.
-- Nothing is inserted, updated or deleted.
--
-- Finds every employee connected to "TechEff" and checks they can sign in,
-- see the right screens, log time and have it approved. Run it BEFORE the
-- dry run (to fix gaps) and AFTER it (sections 7-8 show what was logged,
-- submitted and approved).
--
-- Who counts as TechEff (any of these):
--   * department name/code contains the search text (including any
--     sub-departments), as main or additional department
--   * primary client (account) name contains the search text
--   * allocated to a project whose name/code or client name contains it
--
-- Result sets (each starts with a Section column):
--   0. What matched the search (check the scope is right)
--   1. Summary
--   2. Employee readiness - one row per person, Ready = YES/NO + issues
--   3. Approval route - who approves each person at Level 1 / Level 2
--   4. Approvers to involve in the dry run
--   5. Projects in scope
--   6. Week off + holidays for the dry-run weeks (this week and last week)
--   7. Timesheet status - this week and last week
--   8. Open items - pending approvals and pending WFH/leave requests
--
-- HOW TO RUN AND SHARE:
--   1. In SSMS select the Carbynetech_Timesheet_App database.
--   2. Press Ctrl+Shift+F (Results to File), then Execute (F5).
--      SSMS asks for a file name - save it, e.g. TechEff_readiness.rpt.
--      Every result set goes into that one file. Send that file back.
--   3. Press Ctrl+D afterwards to switch back to Results to Grid.
--
-- Change @Search below if the department/client is spelled differently
-- (e.g. N'Tech Eff').
-- =====================================================================

SET NOCOUNT ON;

DECLARE @Search NVARCHAR(100) = N'TechEff';
DECLARE @Like   NVARCHAR(102) = N'%' + @Search + N'%';

-- Dry-run weeks: Monday of this week and of last week.
-- (1900-01-01 was a Monday, so this does not depend on SET DATEFIRST.)
DECLARE @Today      DATE = CAST(GETDATE() AS DATE);
DECLARE @ThisMonday DATE = DATEADD(DAY, -(DATEDIFF(DAY, '19000101', @Today) % 7), @Today);
DECLARE @LastMonday DATE = DATEADD(DAY, -7, @ThisMonday);

IF OBJECT_ID('tempdb..#Dept') IS NOT NULL DROP TABLE #Dept;
IF OBJECT_ID('tempdb..#Acct') IS NOT NULL DROP TABLE #Acct;
IF OBJECT_ID('tempdb..#Proj') IS NOT NULL DROP TABLE #Proj;
IF OBJECT_ID('tempdb..#Scope') IS NOT NULL DROP TABLE #Scope;
IF OBJECT_ID('tempdb..#Emp') IS NOT NULL DROP TABLE #Emp;

-- ---- Matching departments (plus all their sub-departments) ----
;WITH d AS (
    SELECT DepartmentId, ParentDepartmentId, Code, Name, 0 AS Lvl
    FROM Carbynetech_Department
    WHERE Name LIKE @Like OR Code LIKE @Like
    UNION ALL
    SELECT c.DepartmentId, c.ParentDepartmentId, c.Code, c.Name, d.Lvl + 1
    FROM Carbynetech_Department c
    JOIN d ON c.ParentDepartmentId = d.DepartmentId
)
SELECT DISTINCT DepartmentId, Code, Name INTO #Dept FROM d;

-- ---- Matching clients / accounts ----
SELECT AccountId, Name, AccountType INTO #Acct
FROM Carbynetech_Account
WHERE Name LIKE @Like;

-- ---- Matching projects (by own name/code, or by a matching client) ----
SELECT p.ProjectId, p.Code, p.Name, p.AccountId INTO #Proj
FROM Carbynetech_Project p
WHERE p.Name LIKE @Like OR p.Code LIKE @Like
   OR p.AccountId IN (SELECT AccountId FROM #Acct);

-- ---- Employees in scope, with how they matched ----
SELECT e.EmployeeId,
       STRING_AGG(m.MatchedBy, ', ') AS MatchedBy
INTO #Scope
FROM Carbynetech_Employee e
JOIN (
    SELECT EmployeeId, 'Department' AS MatchedBy FROM Carbynetech_Employee WHERE DepartmentId IN (SELECT DepartmentId FROM #Dept)
    UNION
    SELECT EmployeeId, 'Additional department' FROM Carbynetech_EmployeeDepartment WHERE DepartmentId IN (SELECT DepartmentId FROM #Dept)
    UNION
    SELECT EmployeeId, 'Primary client' FROM Carbynetech_Employee WHERE PrimaryAccountId IN (SELECT AccountId FROM #Acct)
    UNION
    SELECT EmployeeId, 'Project allocation' FROM Carbynetech_EmployeeProjectAllocation WHERE ProjectId IN (SELECT ProjectId FROM #Proj)
) m ON m.EmployeeId = e.EmployeeId
GROUP BY e.EmployeeId;

-- ---- Employee facts (same rules as AccessControlService / WeekApprovalService) ----
SELECT
    e.EmployeeId, e.EmployeeCode, e.FullName, e.Designation,
    dpt.Name                                             AS Department,
    s.MatchedBy,
    e.IsActive,
    e.Email,
    CASE WHEN e.EntraObjectId IS NULL THEN 0 ELSE 1 END  AS HasSignedInBefore,
    e.ManagerEmployeeId,
    m.EmployeeCode                                       AS ManagerCode,
    m.FullName                                           AS ManagerName,
    m.IsActive                                           AS ManagerIsActive,
    m.Email                                              AS ManagerEmail,
    m.ManagerEmployeeId                                  AS ManagersManagerId,
    mm.EmployeeCode                                      AS ManagersManagerCode,
    mm.FullName                                          AS ManagersManagerName,
    mm.IsActive                                          AS ManagersManagerIsActive,
    mm.Email                                             AS ManagersManagerEmail,
    e.PrimaryAccountId,
    pa.Name                                              AS PrimaryClient,
    CASE WHEN e.ManagerEmployeeId IS NULL THEN 1 ELSE 0 END AS IsTopOfHierarchy,
    CASE WHEN e.ManagerEmployeeId IS NULL OR e.Designation LIKE '%Vice President%' OR e.Designation LIKE '%Chief%'
         THEN 1 ELSE 0 END                               AS IsLeadership,
    (SELECT COUNT(*) FROM Carbynetech_Employee r WHERE r.ManagerEmployeeId = e.EmployeeId AND r.IsActive = 1) AS ActiveDirectReports,
    CASE WHEN EXISTS (SELECT 1 FROM Carbynetech_EmployeeRole er JOIN Carbynetech_Role ro ON ro.RoleId = er.RoleId
                      WHERE er.EmployeeId = e.EmployeeId AND ro.Code = 'ADMIN') THEN 1 ELSE 0 END AS IsAdmin,
    (SELECT COUNT(*) FROM Carbynetech_EmployeeProjectAllocation a JOIN Carbynetech_Project p ON p.ProjectId = a.ProjectId
      WHERE a.EmployeeId = e.EmployeeId AND p.IsActive = 1) AS ActiveProjects,
    (SELECT STRING_AGG(p.Name, '; ') FROM Carbynetech_EmployeeProjectAllocation a JOIN Carbynetech_Project p ON p.ProjectId = a.ProjectId
      WHERE a.EmployeeId = e.EmployeeId AND p.IsActive = 1) AS ProjectNames,
    (SELECT COUNT(*) FROM Carbynetech_UserStory us
      WHERE us.EmployeeId = e.EmployeeId AND us.IsActive = 1
        AND us.StartDate <= DATEADD(DAY, 6, @ThisMonday) AND us.EndDate >= @LastMonday) AS StoriesCoveringDryRun
INTO #Emp
FROM #Scope s
JOIN Carbynetech_Employee e         ON e.EmployeeId = s.EmployeeId
LEFT JOIN Carbynetech_Department dpt ON dpt.DepartmentId = e.DepartmentId
LEFT JOIN Carbynetech_Employee m    ON m.EmployeeId  = e.ManagerEmployeeId
LEFT JOIN Carbynetech_Employee mm   ON mm.EmployeeId = m.ManagerEmployeeId
LEFT JOIN Carbynetech_Account pa    ON pa.AccountId  = e.PrimaryAccountId;

-- =====================================================================
-- 0. What matched the search
-- =====================================================================
SELECT '0. Scope' AS Section, 'Department' AS MatchedOn, Code, Name,
       (SELECT COUNT(*) FROM Carbynetech_Employee x WHERE x.DepartmentId = d.DepartmentId AND x.IsActive = 1) AS ActiveEmployees
FROM #Dept d
UNION ALL
SELECT '0. Scope', 'Client / account', CAST(a.AccountType AS NVARCHAR(50)), a.Name,
       (SELECT COUNT(*) FROM Carbynetech_Employee x WHERE x.PrimaryAccountId = a.AccountId AND x.IsActive = 1)
FROM #Acct a
UNION ALL
SELECT '0. Scope', 'Project', p.Code, p.Name,
       (SELECT COUNT(*) FROM Carbynetech_EmployeeProjectAllocation x
        JOIN Carbynetech_Employee y ON y.EmployeeId = x.EmployeeId
        WHERE x.ProjectId = p.ProjectId AND y.IsActive = 1)
FROM #Proj p
UNION ALL
SELECT '0. Scope', '*** NOTHING MATCHED - change @Search at the top ***', NULL, @Search, 0
WHERE NOT EXISTS (SELECT 1 FROM #Dept) AND NOT EXISTS (SELECT 1 FROM #Acct) AND NOT EXISTS (SELECT 1 FROM #Proj)
ORDER BY MatchedOn, Name;

-- =====================================================================
-- 2 (computed first, shown second). Readiness issues per employee
-- =====================================================================
IF OBJECT_ID('tempdb..#Issues') IS NOT NULL DROP TABLE #Issues;

SELECT e.EmployeeId, x.Severity, x.Issue
INTO #Issues
FROM #Emp e
CROSS APPLY (VALUES
    -- Blockers: the person cannot complete the dry run.
    (CASE WHEN e.IsActive = 0 THEN 'BLOCKER' END,
     'Employee is deactivated - cannot sign in'),
    (CASE WHEN e.IsActive = 1 AND (e.Email IS NULL OR LTRIM(RTRIM(e.Email)) = '') THEN 'BLOCKER' END,
     'No email - Microsoft sign-in cannot match this employee'),
    (CASE WHEN e.IsActive = 1 AND e.Email IS NOT NULL AND LTRIM(RTRIM(e.Email)) <> ''
               AND (SELECT COUNT(*) FROM Carbynetech_Employee z WHERE LOWER(LTRIM(RTRIM(z.Email))) = LOWER(LTRIM(RTRIM(e.Email)))) > 1
          THEN 'BLOCKER' END,
     'Email is shared with another employee - sign-in may open the wrong record'),
    (CASE WHEN e.IsActive = 1 AND e.IsLeadership = 0 AND e.ManagerEmployeeId IS NOT NULL AND e.ManagerIsActive = 0 THEN 'BLOCKER' END,
     'Manager is deactivated - submitted weeks will get stuck'),
    (CASE WHEN e.IsActive = 1 AND e.IsLeadership = 0 AND e.ManagerEmployeeId IS NOT NULL AND e.ManagerName IS NULL THEN 'BLOCKER' END,
     'Manager record is missing'),
    (CASE WHEN e.IsActive = 1 AND e.IsLeadership = 0 AND e.ManagersManagerId IS NOT NULL AND e.ManagersManagerIsActive = 0 THEN 'BLOCKER' END,
     'Level 2 approver (manager''s manager) is deactivated - Level 2 will get stuck'),
    (CASE WHEN e.IsActive = 1 AND e.IsLeadership = 0
               AND (e.ManagerEmail IS NULL OR LTRIM(RTRIM(e.ManagerEmail)) = '') AND e.ManagerEmployeeId IS NOT NULL THEN 'BLOCKER' END,
     'Manager has no email - cannot sign in to approve'),
    -- Warnings: the dry run works but something may look wrong.
    (CASE WHEN e.IsActive = 1 AND e.ManagerEmployeeId IS NULL THEN 'WARNING' END,
     'No manager - treated as senior leadership: no My Timesheet, cannot take part as an employee'),
    (CASE WHEN e.IsActive = 1 AND e.ManagerEmployeeId IS NOT NULL AND e.IsLeadership = 1 THEN 'WARNING' END,
     'Designation contains Vice President/Chief - treated as leadership, no My Timesheet'),
    (CASE WHEN e.IsActive = 1 AND e.IsLeadership = 0 AND e.ActiveProjects = 0 THEN 'WARNING' END,
     'No active project allocated - can only log to the department''s internal work'),
    (CASE WHEN e.IsActive = 1 AND e.IsLeadership = 0 AND e.PrimaryAccountId IS NULL THEN 'WARNING' END,
     'Primary client not set - Sat/Sun week off and company-wide holidays only'),
    -- Information: expected for a first dry run, but useful to know.
    (CASE WHEN e.IsActive = 1 AND e.HasSignedInBefore = 0 THEN 'INFO' END,
     'Has never signed in - first sign-in will link the Microsoft account by email'),
    (CASE WHEN e.IsActive = 1 AND e.IsLeadership = 0 AND e.StoriesCoveringDryRun = 0 THEN 'INFO' END,
     'No active user story covering this/last week - must create one before logging time')
) x(Severity, Issue)
WHERE x.Severity IS NOT NULL;

-- =====================================================================
-- 1. Summary
-- =====================================================================
SELECT '1. Summary' AS Section,
    @Search                                                                          AS SearchText,
    @LastMonday                                                                      AS DryRunWeek1,
    @ThisMonday                                                                      AS DryRunWeek2,
    (SELECT COUNT(*) FROM #Emp)                                                      AS EmployeesFound,
    (SELECT COUNT(*) FROM #Emp WHERE IsActive = 1)                                   AS Active,
    (SELECT COUNT(*) FROM #Emp WHERE IsActive = 1 AND IsLeadership = 0)              AS FillTimesheet,
    (SELECT COUNT(*) FROM #Emp WHERE IsActive = 1 AND ActiveDirectReports > 0)       AS AreApprovers,
    (SELECT COUNT(*) FROM #Emp e WHERE IsActive = 1
        AND NOT EXISTS (SELECT 1 FROM #Issues i WHERE i.EmployeeId = e.EmployeeId AND i.Severity = 'BLOCKER')) AS ReadyActive,
    (SELECT COUNT(DISTINCT EmployeeId) FROM #Issues WHERE Severity = 'BLOCKER')     AS WithBlockers,
    (SELECT COUNT(DISTINCT EmployeeId) FROM #Issues WHERE Severity = 'WARNING')     AS WithWarnings,
    (SELECT COUNT(*) FROM #Emp WHERE IsActive = 1 AND HasSignedInBefore = 0)         AS NeverSignedIn;

-- =====================================================================
-- 2. Employee readiness (one row per person)
-- =====================================================================
SELECT '2. Readiness' AS Section,
    e.EmployeeCode, e.FullName, e.Designation, e.Department, e.MatchedBy,
    CASE WHEN e.IsActive = 1 THEN 'Active' ELSE 'Inactive' END AS Status,
    e.Email,
    CASE WHEN EXISTS (SELECT 1 FROM #Issues i WHERE i.EmployeeId = e.EmployeeId AND i.Severity = 'BLOCKER') THEN 'NO'
         WHEN EXISTS (SELECT 1 FROM #Issues i WHERE i.EmployeeId = e.EmployeeId AND i.Severity = 'WARNING') THEN 'YES (check warnings)'
         ELSE 'YES' END                                                   AS Ready,
    CASE WHEN e.IsLeadership = 1 THEN 'No (leadership)' ELSE 'Yes' END     AS FillsTimesheet,
    CONCAT_WS(', ',
        CASE WHEN e.IsLeadership = 0 THEN 'My Work' END,
        CASE WHEN e.ActiveDirectReports > 0 THEN 'Approvals' END,
        CASE WHEN e.ActiveDirectReports > 0 OR e.IsAdmin = 1 THEN 'Insights' END,
        CASE WHEN e.IsAdmin = 1 THEN 'Configure' END,
        'Notifications')                                                  AS MenusVisible,
    ISNULL(e.PrimaryClient, '(not set)')                                  AS PrimaryClient,
    e.ActiveProjects,
    ISNULL(e.ProjectNames, '')                                            AS Projects,
    e.StoriesCoveringDryRun,
    CASE WHEN e.HasSignedInBefore = 1 THEN 'Yes' ELSE 'No' END            AS SignedInBefore,
    ISNULL((SELECT STRING_AGG(CONCAT(i.Severity, ': ', i.Issue), ' | ')
            FROM #Issues i WHERE i.EmployeeId = e.EmployeeId), '')        AS Issues
FROM #Emp e
ORDER BY CASE WHEN e.IsActive = 1 THEN 0 ELSE 1 END,
         CASE WHEN EXISTS (SELECT 1 FROM #Issues i WHERE i.EmployeeId = e.EmployeeId AND i.Severity = 'BLOCKER') THEN 0 ELSE 1 END,
         e.FullName;

-- =====================================================================
-- 3. Approval route
--    Level 1 = manager; Level 2 = manager's manager.
--    If the manager has no manager, Level 1 is skipped and the manager
--    approves at Level 2.
-- =====================================================================
SELECT '3. Approval route' AS Section,
    e.EmployeeCode, e.FullName,
    CASE WHEN e.IsLeadership = 1 THEN '- (does not fill a timesheet)'
         WHEN e.ManagerEmployeeId IS NULL THEN '(no manager)'
         WHEN e.ManagersManagerId IS NULL THEN '(skipped - manager is top of hierarchy)'
         ELSE CONCAT(e.ManagerName, ' (', e.ManagerCode, ')') END                     AS Level1Approver,
    CASE WHEN e.IsLeadership = 1 THEN '-'
         WHEN e.ManagerEmployeeId IS NULL THEN '(no manager)'
         WHEN e.ManagersManagerId IS NULL THEN CONCAT(e.ManagerName, ' (', e.ManagerCode, ')')
         ELSE CONCAT(e.ManagersManagerName, ' (', e.ManagersManagerCode, ')') END     AS Level2Approver,
    CASE WHEN e.IsLeadership = 1 THEN '-'
         WHEN e.ManagerEmployeeId IS NULL THEN 'NO'
         WHEN e.ManagerIsActive = 0 THEN 'NO - manager inactive'
         WHEN e.ManagersManagerId IS NOT NULL AND e.ManagersManagerIsActive = 0 THEN 'NO - Level 2 inactive'
         ELSE 'YES' END                                                                AS RouteWorks
FROM #Emp e
WHERE e.IsActive = 1
ORDER BY e.FullName;

-- =====================================================================
-- 4. Approvers to involve in the dry run
--    (they must sign in to approve; they may be outside TechEff)
-- =====================================================================
;WITH Route AS (
    SELECT CASE WHEN ManagersManagerId IS NULL THEN NULL ELSE ManagerEmployeeId END AS L1Id,
           CASE WHEN ManagersManagerId IS NULL THEN ManagerEmployeeId ELSE ManagersManagerId END AS L2Id
    FROM #Emp
    WHERE IsActive = 1 AND IsLeadership = 0 AND ManagerEmployeeId IS NOT NULL
),
Approver AS (
    SELECT L1Id AS EmployeeId, 'Level 1' AS ApprovesAt FROM Route WHERE L1Id IS NOT NULL
    UNION ALL
    SELECT L2Id, 'Level 2' FROM Route WHERE L2Id IS NOT NULL
)
SELECT '4. Approvers' AS Section,
    a.EmployeeCode, a.FullName, a.Designation,
    STRING_AGG(x.ApprovesAt, ', ') WITHIN GROUP (ORDER BY x.ApprovesAt) AS ApprovesAt,
    SUM(x.Cnt)                                                          AS TechEffPeopleToApprove,
    CASE WHEN a.IsActive = 1 THEN 'Active' ELSE 'INACTIVE' END          AS Status,
    ISNULL(a.Email, '(NO EMAIL)')                                       AS Email,
    CASE WHEN a.EntraObjectId IS NULL THEN 'No' ELSE 'Yes' END          AS SignedInBefore,
    CASE WHEN EXISTS (SELECT 1 FROM #Scope s WHERE s.EmployeeId = a.EmployeeId) THEN 'Yes' ELSE 'No' END AS IsInTechEff
FROM (SELECT EmployeeId, ApprovesAt, COUNT(*) AS Cnt FROM Approver GROUP BY EmployeeId, ApprovesAt) x
JOIN Carbynetech_Employee a ON a.EmployeeId = x.EmployeeId
GROUP BY a.EmployeeId, a.EmployeeCode, a.FullName, a.Designation, a.IsActive, a.Email, a.EntraObjectId
ORDER BY a.FullName;

-- =====================================================================
-- 5. Projects TechEff employees can log time to
-- =====================================================================
SELECT '5. Projects' AS Section,
    p.Code, p.Name AS Project, acc.Name AS Client,
    CASE WHEN p.IsActive = 1 THEN 'Active' ELSE 'INACTIVE' END                        AS Status,
    p.StartDate, p.EndDate,
    COUNT(DISTINCT al.EmployeeId)                                                      AS TechEffEmployees,
    (SELECT COUNT(*) FROM Carbynetech_Module mo WHERE mo.ProjectId = p.ProjectId)       AS Modules,
    (SELECT COUNT(*) FROM Carbynetech_Module mo JOIN Carbynetech_Task t ON t.ModuleId = mo.ModuleId
      WHERE mo.ProjectId = p.ProjectId)                                                AS Tasks,
    (SELECT COUNT(*) FROM Carbynetech_ProjectStoryName n WHERE n.ProjectId = p.ProjectId AND n.IsActive = 1) AS ProjectStoryNames,
    CONCAT_WS(' | ',
        CASE WHEN p.IsActive = 0 THEN 'Inactive project still allocated' END,
        CASE WHEN p.StartDate IS NULL THEN 'No start date' END,
        CASE WHEN p.EndDate IS NOT NULL AND p.EndDate < @LastMonday THEN 'Ended before the dry run' END,
        CASE WHEN p.StartDate IS NOT NULL AND p.StartDate > DATEADD(DAY, 6, @ThisMonday) THEN 'Starts after the dry run' END,
        CASE WHEN p.ProjectLeadEmployeeId IS NULL AND p.ProjectManagerEmployeeId IS NULL THEN 'No lead/manager' END,
        CASE WHEN p.NeedsReview = 1 THEN 'Needs review' END)                          AS Issues
FROM Carbynetech_EmployeeProjectAllocation al
JOIN #Emp e ON e.EmployeeId = al.EmployeeId AND e.IsActive = 1
JOIN Carbynetech_Project p   ON p.ProjectId = al.ProjectId
JOIN Carbynetech_Account acc ON acc.AccountId = p.AccountId
GROUP BY p.ProjectId, p.Code, p.Name, acc.Name, p.IsActive, p.StartDate, p.EndDate,
         p.ProjectLeadEmployeeId, p.ProjectManagerEmployeeId, p.NeedsReview
ORDER BY acc.Name, p.Name;

-- =====================================================================
-- 6. Week off and holidays in the dry-run weeks
--    (same rule as DayTypeResolutionService: a custom client week off
--    wins; otherwise the primary client's default; otherwise Sat,Sun)
-- =====================================================================
SELECT '6. Week off & holidays' AS Section,
    e.EmployeeCode, e.FullName,
    COALESCE(
        (SELECT TOP 1 cw.WeekOffDays
         FROM Carbynetech_EmployeeWeekOff ew
         JOIN Carbynetech_ClientWeekOff cw ON cw.AccountId = ew.AccountId
              AND cw.EffectiveFrom <= @Today AND (cw.EffectiveTo IS NULL OR cw.EffectiveTo >= @Today)
         WHERE ew.EmployeeId = e.EmployeeId
           AND ew.EffectiveFrom <= @Today AND (ew.EffectiveTo IS NULL OR ew.EffectiveTo >= @Today)),
        NULLIF(LTRIM(RTRIM(pa.WeekOffDays)), ''),
        'Sat,Sun')                                                     AS WeekOffToday,
    CASE WHEN EXISTS (SELECT 1 FROM Carbynetech_EmployeeWeekOff ew WHERE ew.EmployeeId = e.EmployeeId
                        AND ew.EffectiveFrom <= @Today AND (ew.EffectiveTo IS NULL OR ew.EffectiveTo >= @Today))
         THEN 'Custom client week off'
         WHEN e.PrimaryAccountId IS NOT NULL THEN 'Primary client default'
         ELSE 'Company default' END                                    AS WeekOffSource,
    ISNULL((SELECT STRING_AGG(CONCAT(CONVERT(VARCHAR(10), h.HolidayDate, 23), ' ', h.Name), '; ')
            FROM Carbynetech_Holiday h
            WHERE h.HolidayDate BETWEEN @LastMonday AND DATEADD(DAY, 6, @ThisMonday)
              AND (h.AccountId IS NULL OR h.AccountId = e.PrimaryAccountId)), '(none)') AS HolidaysInDryRunWeeks
FROM #Emp e
LEFT JOIN Carbynetech_Account pa ON pa.AccountId = e.PrimaryAccountId
WHERE e.IsActive = 1 AND e.IsLeadership = 0
ORDER BY e.FullName;

-- =====================================================================
-- 7. Timesheet status - last week and this week
--    Run again AFTER the dry run to see what was logged and approved.
-- =====================================================================
SELECT '7. Timesheet status' AS Section,
    e.EmployeeCode, e.FullName, wk.WeekStart,
    ISNULL(CAST(w.Status AS NVARCHAR(20)),
           CASE WHEN te.Lines > 0 THEN 'Draft' ELSE 'Not started' END) AS WeekStatus,
    ISNULL(te.Lines, 0)                                                AS TaskLines,
    ISNULL(te.TotalHours, 0)                                           AS HoursLogged,
    ISNULL(te.Billable, 0)                                             AS Billable,
    ISNULL(te.NonBillable, 0)                                          AS NonBillable,
    w.SubmittedAt,
    w.RejectionReason                                                  AS ReturnReason,
    (SELECT TOP 1 ae.EventText FROM Carbynetech_ApprovalEvent ae
     WHERE ae.WeekRecordId = w.WeekRecordId ORDER BY ae.EventTimestamp DESC, ae.ApprovalEventId DESC) AS LastTrailEvent
FROM #Emp e
CROSS JOIN (VALUES (@LastMonday), (@ThisMonday)) wk(WeekStart)
LEFT JOIN Carbynetech_WeekRecord w ON w.EmployeeId = e.EmployeeId AND w.WeekStartDate = wk.WeekStart
OUTER APPLY (
    SELECT COUNT(*) AS Lines,
           SUM(t.MondayHours + t.TuesdayHours + t.WednesdayHours + t.ThursdayHours + t.FridayHours + t.SaturdayHours + t.SundayHours) AS TotalHours,
           SUM(CASE WHEN t.Classification = 'Billable' THEN t.MondayHours + t.TuesdayHours + t.WednesdayHours + t.ThursdayHours + t.FridayHours + t.SaturdayHours + t.SundayHours ELSE 0 END) AS Billable,
           SUM(CASE WHEN t.Classification = 'NonBillable' THEN t.MondayHours + t.TuesdayHours + t.WednesdayHours + t.ThursdayHours + t.FridayHours + t.SaturdayHours + t.SundayHours ELSE 0 END) AS NonBillable
    FROM Carbynetech_TimeEntry t
    WHERE t.EmployeeId = e.EmployeeId AND t.WeekStartDate = wk.WeekStart
) te
WHERE e.IsActive = 1 AND e.IsLeadership = 0
ORDER BY e.FullName, wk.WeekStart;

-- =====================================================================
-- 8. Open items - weeks waiting for approval, pending WFH/leave requests
-- =====================================================================
SELECT '8. Open items' AS Section,
    'Week awaiting approval' AS Item,
    e.EmployeeCode, e.FullName,
    CONVERT(VARCHAR(10), w.WeekStartDate, 23)                         AS ForDate,
    CAST(w.Status AS NVARCHAR(20))                                    AS Detail,
    DATEDIFF(DAY, w.SubmittedAt, GETUTCDATE())                        AS DaysWaiting
FROM #Emp e
JOIN Carbynetech_WeekRecord w ON w.EmployeeId = e.EmployeeId
WHERE w.Status IN ('PendingL1', 'PendingL2')
UNION ALL
SELECT '8. Open items', 'WFH/leave request pending',
    e.EmployeeCode, e.FullName,
    CONVERT(VARCHAR(10), r.RequestDate, 23),
    CAST(r.RequestType AS NVARCHAR(20)),
    DATEDIFF(DAY, r.SubmittedAt, GETUTCDATE())
FROM #Emp e
JOIN Carbynetech_DayTypeRequest r ON r.EmployeeId = e.EmployeeId
WHERE r.Status = 'Pending'
ORDER BY Item, FullName, ForDate;

DROP TABLE #Issues;
DROP TABLE #Emp;
DROP TABLE #Scope;
DROP TABLE #Proj;
DROP TABLE #Acct;
DROP TABLE #Dept;
