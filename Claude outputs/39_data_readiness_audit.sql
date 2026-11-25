-- =====================================================================
-- 39_data_readiness_audit.sql
--
-- GO-LIVE DATA AUDIT (Nov 2026) - READ ONLY. Nothing is inserted, updated
-- or deleted. Run it before deploying for organisational use.
--
-- Result sets (in order):
--   1. Summary counts
--   2. Access matrix - every employee, what they can see and who approves them
--   3. Employees who fill timesheets but have NO project allocated
--   4. Sign-in gaps - no email / duplicate email
--   5. Reporting-line gaps - no manager, inactive or missing manager
--   6. Primary client not set (week off and client holidays fall back to defaults)
--   7. Project gaps - no employees, no lead/manager, needs review, inactive with allocations
--   8. Clients with no projects, departments with no employees
--   9. Holiday duplicates (same date, same client)
--  10. Test / dummy data still present
--  11. Admins and roles
--  12. Scripts 37 and 38 applied?
--
-- The access rules below copy AccessControlService.GetAccessProfileAsync:
--   * No manager, or designation contains "Vice President" / "Chief"
--     = senior leadership: does NOT fill a timesheet.
--   * Level 1 approver = has direct reports and has a manager.
--   * Level 2 approver = top of hierarchy with direct reports, OR one of
--     their direct reports has direct reports.
--   * Team Compliance + Reports = any approver or Admin. Master Data = Admin.
--   * Approval route: manager is Level 1 and manager's manager is Level 2;
--     if the manager is top of the hierarchy, Level 1 is skipped.
--
-- Select the timesheet database before running (no USE line on purpose).
-- Tip: in SSMS right-click each result grid -> "Save Results As..." (CSV),
-- or Ctrl+A, Ctrl+Shift+C on a grid to copy it with headers.
-- =====================================================================

SET NOCOUNT ON;

IF OBJECT_ID('tempdb..#Emp') IS NOT NULL DROP TABLE #Emp;

SELECT
    e.EmployeeId,
    e.EmployeeCode,
    e.FullName,
    e.Designation,
    d.Name                                              AS Department,
    e.IsActive,
    e.Email,
    e.ManagerEmployeeId,
    m.FullName                                          AS ManagerName,
    m.IsActive                                          AS ManagerIsActive,
    m.ManagerEmployeeId                                 AS ManagersManagerId,
    mm.FullName                                         AS ManagersManagerName,
    e.PrimaryAccountId,
    pa.Name                                             AS PrimaryClient,
    CASE WHEN e.ManagerEmployeeId IS NULL THEN 1 ELSE 0 END AS IsTopOfHierarchy,
    (SELECT COUNT(*) FROM Carbynetech_Employee r WHERE r.ManagerEmployeeId = e.EmployeeId) AS DirectReports,
    (SELECT COUNT(*) FROM Carbynetech_Employee r WHERE r.ManagerEmployeeId = e.EmployeeId AND r.IsActive = 1) AS ActiveDirectReports,
    CASE WHEN EXISTS (SELECT 1 FROM Carbynetech_Employee r
                      JOIN Carbynetech_Employee g ON g.ManagerEmployeeId = r.EmployeeId
                      WHERE r.ManagerEmployeeId = e.EmployeeId) THEN 1 ELSE 0 END AS HasGrandReports,
    CASE WHEN EXISTS (SELECT 1 FROM Carbynetech_EmployeeRole er JOIN Carbynetech_Role ro ON ro.RoleId = er.RoleId
                      WHERE er.EmployeeId = e.EmployeeId AND ro.Code = 'ADMIN') THEN 1 ELSE 0 END AS IsAdmin,
    (SELECT STRING_AGG(ro.Code, ', ') FROM Carbynetech_EmployeeRole er JOIN Carbynetech_Role ro ON ro.RoleId = er.RoleId
      WHERE er.EmployeeId = e.EmployeeId)               AS Roles,
    (SELECT COUNT(*) FROM Carbynetech_EmployeeProjectAllocation a JOIN Carbynetech_Project p ON p.ProjectId = a.ProjectId
      WHERE a.EmployeeId = e.EmployeeId AND p.IsActive = 1) AS ActiveProjects,
    (SELECT STRING_AGG(p.Name, '; ') FROM Carbynetech_EmployeeProjectAllocation a JOIN Carbynetech_Project p ON p.ProjectId = a.ProjectId
      WHERE a.EmployeeId = e.EmployeeId AND p.IsActive = 1) AS ProjectNames
INTO #Emp
FROM Carbynetech_Employee e
LEFT JOIN Carbynetech_Department d  ON d.DepartmentId = e.DepartmentId
LEFT JOIN Carbynetech_Employee   m  ON m.EmployeeId   = e.ManagerEmployeeId
LEFT JOIN Carbynetech_Employee   mm ON mm.EmployeeId  = m.ManagerEmployeeId
LEFT JOIN Carbynetech_Account    pa ON pa.AccountId   = e.PrimaryAccountId;

ALTER TABLE #Emp ADD
    IsLeadership     BIT,
    FillsTimesheet   BIT,
    IsLevel1Approver BIT,
    IsLevel2Approver BIT;
GO

UPDATE #Emp SET
    IsLeadership     = CASE WHEN IsTopOfHierarchy = 1 OR Designation LIKE '%Vice President%' OR Designation LIKE '%Chief%' THEN 1 ELSE 0 END,
    IsLevel1Approver = CASE WHEN DirectReports > 0 AND IsTopOfHierarchy = 0 THEN 1 ELSE 0 END,
    IsLevel2Approver = CASE WHEN (IsTopOfHierarchy = 1 AND DirectReports > 0) OR HasGrandReports = 1 THEN 1 ELSE 0 END;
UPDATE #Emp SET FillsTimesheet = CASE WHEN IsLeadership = 1 THEN 0 ELSE 1 END;
GO

-- ---------------------------------------------------------------------
-- 1. Summary
-- ---------------------------------------------------------------------
SELECT
    (SELECT COUNT(*) FROM #Emp)                                                    AS TotalEmployees,
    (SELECT COUNT(*) FROM #Emp WHERE IsActive = 1)                                 AS ActiveEmployees,
    (SELECT COUNT(*) FROM #Emp WHERE IsActive = 1 AND FillsTimesheet = 1)          AS ActiveFillTimesheet,
    (SELECT COUNT(*) FROM #Emp WHERE IsActive = 1 AND IsLeadership = 1)            AS ActiveLeadershipNoTimesheet,
    (SELECT COUNT(*) FROM #Emp WHERE IsActive = 1 AND IsLevel1Approver = 1)        AS Level1Approvers,
    (SELECT COUNT(*) FROM #Emp WHERE IsActive = 1 AND IsLevel2Approver = 1)        AS Level2Approvers,
    (SELECT COUNT(*) FROM #Emp WHERE IsActive = 1 AND IsAdmin = 1)                 AS Admins,
    (SELECT COUNT(*) FROM #Emp WHERE IsActive = 1 AND FillsTimesheet = 1 AND ActiveProjects = 0) AS ActiveWithoutProjects,
    (SELECT COUNT(*) FROM #Emp WHERE IsActive = 1 AND (Email IS NULL OR LTRIM(RTRIM(Email)) = '')) AS ActiveWithoutEmail,
    (SELECT COUNT(*) FROM #Emp WHERE IsActive = 1 AND PrimaryAccountId IS NULL)    AS ActiveWithoutPrimaryClient,
    (SELECT COUNT(*) FROM Carbynetech_Project WHERE IsActive = 1)                  AS ActiveProjects,
    (SELECT COUNT(*) FROM Carbynetech_Account)                                     AS ClientsAndInternal,
    (SELECT COUNT(*) FROM Carbynetech_Department)                                  AS Departments;

-- ---------------------------------------------------------------------
-- 2. Access matrix (every employee)
-- ---------------------------------------------------------------------
SELECT
    EmployeeCode, FullName, Designation, Department,
    CASE WHEN IsActive = 1 THEN 'Active' ELSE 'Inactive' END AS Status,
    Email,
    CASE WHEN FillsTimesheet = 1 THEN 'Yes' ELSE 'No (leadership)' END AS FillsTimesheet,
    -- Who approves this person's timesheet
    CASE WHEN FillsTimesheet = 0 THEN '-'
         WHEN ManagerEmployeeId IS NULL THEN '(no manager)'
         WHEN ManagersManagerId IS NULL THEN '(skipped - manager is top)'
         ELSE ManagerName END                                       AS Level1Approver,
    CASE WHEN FillsTimesheet = 0 THEN '-'
         WHEN ManagerEmployeeId IS NULL THEN '(no manager)'
         WHEN ManagersManagerId IS NULL THEN ManagerName
         ELSE ManagersManagerName END                               AS Level2Approver,
    -- What this person can do
    CASE WHEN IsLevel1Approver = 1 THEN 'Yes' ELSE '' END           AS ApprovesLevel1,
    CASE WHEN IsLevel2Approver = 1 THEN 'Yes' ELSE '' END           AS ApprovesLevel2,
    CASE WHEN IsAdmin = 1 THEN 'Yes' ELSE '' END                    AS IsAdmin,
    ISNULL(Roles, '')                                               AS Roles,
    CONCAT_WS(', ',
        CASE WHEN FillsTimesheet = 1 THEN 'My Work' END,
        CASE WHEN IsLevel1Approver = 1 OR IsLevel2Approver = 1 THEN 'Approvals' END,
        CASE WHEN IsLevel1Approver = 1 OR IsLevel2Approver = 1 OR IsAdmin = 1 THEN 'Insights' END,
        CASE WHEN IsAdmin = 1 THEN 'Configure' END,
        'Notifications')                                            AS MenusVisible,
    ISNULL(PrimaryClient, '(not set)')                              AS PrimaryClient,
    ActiveProjects,
    ISNULL(ProjectNames, '')                                        AS Projects,
    ActiveDirectReports
FROM #Emp
ORDER BY CASE WHEN IsActive = 1 THEN 0 ELSE 1 END, Department, FullName;

-- ---------------------------------------------------------------------
-- 3. Fill timesheets but have NO active project allocated
--    (they can still log to their department's internal work project
--    from User Stories, but billable work needs a project allocation)
-- ---------------------------------------------------------------------
SELECT EmployeeCode, FullName, Designation, Department, ManagerName AS Manager, ISNULL(PrimaryClient, '(not set)') AS PrimaryClient
FROM #Emp
WHERE IsActive = 1 AND FillsTimesheet = 1 AND ActiveProjects = 0
ORDER BY Department, FullName;

-- ---------------------------------------------------------------------
-- 4. Sign-in gaps: Microsoft sign-in is matched on email
-- ---------------------------------------------------------------------
SELECT 'No email - cannot sign in' AS Issue, EmployeeCode, FullName, Department, Email
FROM #Emp WHERE IsActive = 1 AND (Email IS NULL OR LTRIM(RTRIM(Email)) = '')
UNION ALL
SELECT 'Duplicate email', e.EmployeeCode, e.FullName, e.Department, e.Email
FROM #Emp e
WHERE e.Email IS NOT NULL AND LTRIM(RTRIM(e.Email)) <> ''
  AND (SELECT COUNT(*) FROM #Emp x WHERE LOWER(LTRIM(RTRIM(x.Email))) = LOWER(LTRIM(RTRIM(e.Email)))) > 1
ORDER BY Issue, FullName;

-- ---------------------------------------------------------------------
-- 5. Reporting-line gaps
-- ---------------------------------------------------------------------
SELECT 'No manager - treated as leadership, no timesheet, approvals come to them as Level 2' AS Issue,
       EmployeeCode, FullName, Designation, Department, ManagerName
FROM #Emp WHERE IsActive = 1 AND ManagerEmployeeId IS NULL
UNION ALL
SELECT 'Manager is inactive - approvals will get stuck', EmployeeCode, FullName, Designation, Department, ManagerName
FROM #Emp WHERE IsActive = 1 AND ManagerEmployeeId IS NOT NULL AND ManagerIsActive = 0
UNION ALL
SELECT 'Manager record missing', EmployeeCode, FullName, Designation, Department, NULL
FROM #Emp WHERE IsActive = 1 AND ManagerEmployeeId IS NOT NULL AND ManagerName IS NULL
UNION ALL
SELECT 'Designation makes them leadership (no timesheet) - check this is intended', EmployeeCode, FullName, Designation, Department, ManagerName
FROM #Emp WHERE IsActive = 1 AND ManagerEmployeeId IS NOT NULL AND IsLeadership = 1
ORDER BY Issue, FullName;

-- ---------------------------------------------------------------------
-- 6. Primary client not set: week off falls back to Sat/Sun and only
--    company-wide holidays apply (unless in a custom week off)
-- ---------------------------------------------------------------------
SELECT EmployeeCode, FullName, Designation, Department, ActiveProjects, ISNULL(ProjectNames, '') AS Projects
FROM #Emp
WHERE IsActive = 1 AND FillsTimesheet = 1 AND PrimaryAccountId IS NULL
ORDER BY Department, FullName;

-- ---------------------------------------------------------------------
-- 7. Project gaps
-- ---------------------------------------------------------------------
SELECT 'Active project with no employees allocated' AS Issue, p.Code, p.Name, a.Name AS Client
FROM Carbynetech_Project p JOIN Carbynetech_Account a ON a.AccountId = p.AccountId
WHERE p.IsActive = 1 AND NOT EXISTS (SELECT 1 FROM Carbynetech_EmployeeProjectAllocation x WHERE x.ProjectId = p.ProjectId)
UNION ALL
SELECT 'Active project with no Project Lead or Manager', p.Code, p.Name, a.Name
FROM Carbynetech_Project p JOIN Carbynetech_Account a ON a.AccountId = p.AccountId
WHERE p.IsActive = 1 AND p.ProjectLeadEmployeeId IS NULL AND p.ProjectManagerEmployeeId IS NULL
UNION ALL
SELECT 'Project flagged Needs Review', p.Code, p.Name, a.Name
FROM Carbynetech_Project p JOIN Carbynetech_Account a ON a.AccountId = p.AccountId
WHERE p.NeedsReview = 1
UNION ALL
SELECT 'Inactive project still has employees allocated', p.Code, p.Name, a.Name
FROM Carbynetech_Project p JOIN Carbynetech_Account a ON a.AccountId = p.AccountId
WHERE p.IsActive = 0 AND EXISTS (SELECT 1 FROM Carbynetech_EmployeeProjectAllocation x WHERE x.ProjectId = p.ProjectId)
UNION ALL
SELECT 'Inactive employee still allocated (project: ' + p.Name + ')', e.EmployeeCode, e.FullName, a.Name
FROM Carbynetech_EmployeeProjectAllocation x
JOIN Carbynetech_Employee e ON e.EmployeeId = x.EmployeeId
JOIN Carbynetech_Project p ON p.ProjectId = x.ProjectId
JOIN Carbynetech_Account a ON a.AccountId = p.AccountId
WHERE e.IsActive = 0
ORDER BY Issue, Name;

-- ---------------------------------------------------------------------
-- 8. Clients with no projects / departments with no employees
-- ---------------------------------------------------------------------
SELECT 'Client / internal account with no projects' AS Issue, a.Name, a.AccountType AS Detail
FROM Carbynetech_Account a
WHERE NOT EXISTS (SELECT 1 FROM Carbynetech_Project p WHERE p.AccountId = a.AccountId)
UNION ALL
SELECT 'Department with no employees', d.Name, d.Code
FROM Carbynetech_Department d
WHERE NOT EXISTS (SELECT 1 FROM Carbynetech_Employee e WHERE e.DepartmentId = d.DepartmentId)
  AND NOT EXISTS (SELECT 1 FROM Carbynetech_EmployeeDepartment ed WHERE ed.DepartmentId = d.DepartmentId)
ORDER BY Issue, Name;

-- ---------------------------------------------------------------------
-- 9. Holiday duplicates (same date, same client / company-wide)
-- ---------------------------------------------------------------------
SELECT h.HolidayDate, ISNULL(a.Name, 'All clients') AS Client, COUNT(*) AS Records,
       STRING_AGG(CONCAT(h.HolidayId, ': ', h.Name, ' (', h.Location, ')'), '; ') AS Holidays
FROM Carbynetech_Holiday h
LEFT JOIN Carbynetech_Account a ON a.AccountId = h.AccountId
GROUP BY h.HolidayDate, ISNULL(a.Name, 'All clients')
HAVING COUNT(*) > 1
ORDER BY h.HolidayDate;

-- ---------------------------------------------------------------------
-- 10. Test / dummy data still present
-- ---------------------------------------------------------------------
SELECT 'Test employee' AS Issue, EmployeeCode AS Code, FullName AS Name,
       CASE WHEN IsActive = 1 THEN 'Active' ELSE 'Inactive' END AS Detail
FROM #Emp WHERE FullName LIKE '%test%' OR FullName LIKE '%dummy%' OR FullName LIKE '%sample%'
UNION ALL
SELECT 'Test user story', CAST(s.UserStoryId AS NVARCHAR(20)), s.Name, e.FullName
FROM Carbynetech_UserStory s JOIN Carbynetech_Employee e ON e.EmployeeId = s.EmployeeId
WHERE s.Name LIKE '%test%' OR s.Name LIKE '%dummy%' OR s.Name LIKE '%bestie%'
UNION ALL
SELECT 'Test project', p.Code, p.Name, CASE WHEN p.IsActive = 1 THEN 'Active' ELSE 'Inactive' END
FROM Carbynetech_Project p WHERE p.Name LIKE '%test%' OR p.Name LIKE '%dummy%' OR p.Code LIKE '%test%'
UNION ALL
SELECT 'Timesheet weeks already in the database', CAST(COUNT(*) AS NVARCHAR(20)), 'weeks',
       CONCAT('from ', MIN(WeekStartDate), ' to ', MAX(WeekStartDate))
FROM Carbynetech_WeekRecord
ORDER BY Issue, Name;

-- ---------------------------------------------------------------------
-- 11. Admins and roles
-- ---------------------------------------------------------------------
SELECT ro.Code AS RoleCode, ro.Name AS RoleName, COUNT(er.EmployeeId) AS Employees,
       STRING_AGG(CONCAT(e.FullName, ' (', e.EmployeeCode, ')'), '; ') AS Who
FROM Carbynetech_Role ro
LEFT JOIN Carbynetech_EmployeeRole er ON er.RoleId = ro.RoleId
LEFT JOIN Carbynetech_Employee e ON e.EmployeeId = er.EmployeeId
GROUP BY ro.Code, ro.Name
ORDER BY ro.Code;

-- ---------------------------------------------------------------------
-- 12. Were scripts 37 and 38 applied?
-- ---------------------------------------------------------------------
SELECT
    CASE WHEN EXISTS (SELECT 1 FROM sys.check_constraints cc
                      WHERE cc.parent_object_id = OBJECT_ID('Carbynetech_DayTypeOverride')
                        AND CHARINDEX('''LH''', cc.definition) > 0)
         THEN 'Yes' ELSE 'NO - run 37_allow_half_day_leave_day_type.sql' END AS Script37_HalfDayLeave,
    CASE WHEN COL_LENGTH('Carbynetech_Account', 'WeekOffDays') IS NOT NULL
         THEN 'Yes' ELSE 'NO - run 38_add_client_default_week_off.sql' END AS Script38_ClientWeekOff;

DROP TABLE #Emp;
