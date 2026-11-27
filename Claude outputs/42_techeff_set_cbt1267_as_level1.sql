-- =====================================================================
-- 42_techeff_set_cbt1267_as_level1.sql
--
-- TECHEFF DRY RUN (Nov 2026): make Durga Prasad (CBT1267) the Level 1
-- approver - i.e. the manager - of every active employee allocated to:
--     * Techeff for Europe - Chevigny
--     * TechEff for India Business
--
-- How Level 1 works in the app: Level 1 = the employee's manager,
-- Level 2 = the manager's manager. So this script sets each employee's
-- ManagerEmployeeId to CBT1267. CBT1267's own manager becomes their
-- Level 2 approver. (If CBT1267 has NO manager, Level 1 is skipped and
-- CBT1267 approves at Level 2 instead - section B warns if so.)
--
-- SAFE BY DEFAULT: @Apply = 0 only PREVIEWS what would change.
-- Set @Apply = 1 and run again to make the change.
--
-- ROLLBACK: every employee's previous manager is saved in
-- Carbynetech_DryRun_ManagerBackup before it is changed. Run
-- 42_ROLLBACK_techeff_restore_previous_managers.sql to put them back.
--
-- Skipped on purpose (listed in section C):
--   * CBT1267 themselves
--   * anyone above CBT1267 in the reporting line (would create a loop)
--   * anyone with no manager or a Vice President / Chief designation
--     (they are senior leadership; giving them a manager would make them
--     start filling timesheets)
--   * anyone who already reports to CBT1267
--
-- Select the Carbynetech_Timesheet_App database before running.
-- =====================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Apply BIT = 0;                         -- 0 = preview only, 1 = make the change
DECLARE @ApproverCode NVARCHAR(20) = N'CBT1267';

DECLARE @Projects TABLE (Name NVARCHAR(200) PRIMARY KEY);
INSERT INTO @Projects (Name) VALUES
    (N'Techeff for Europe - Chevigny'),
    (N'TechEff for India Business');

-- ---------------------------------------------------------------------
-- Checks
-- ---------------------------------------------------------------------
DECLARE @ApproverId INT, @ApproverName NVARCHAR(200), @ApproverActive BIT, @ApproverManagerId INT;
SELECT @ApproverId = EmployeeId, @ApproverName = FullName, @ApproverActive = IsActive, @ApproverManagerId = ManagerEmployeeId
FROM Carbynetech_Employee WHERE EmployeeCode = @ApproverCode;

IF @ApproverId IS NULL
BEGIN
    RAISERROR('Employee %s not found - nothing changed.', 16, 1, @ApproverCode);
    RETURN;
END
IF @ApproverActive = 0
BEGIN
    RAISERROR('Employee %s is deactivated - nothing changed.', 16, 1, @ApproverCode);
    RETURN;
END

-- A. Projects found?
SELECT 'A. Projects' AS Section, pr.Name AS Requested,
       p.ProjectId, p.Code, p.Name AS FoundAs,
       CASE WHEN p.ProjectId IS NULL THEN '*** NOT FOUND - check the name ***'
            WHEN p.IsActive = 0 THEN 'Found but INACTIVE'
            ELSE 'OK' END AS Result
FROM @Projects pr
LEFT JOIN Carbynetech_Project p ON p.Name = pr.Name;

IF EXISTS (SELECT 1 FROM @Projects pr WHERE NOT EXISTS (SELECT 1 FROM Carbynetech_Project p WHERE p.Name = pr.Name))
BEGIN
    RAISERROR('One or more projects were not found (see section A) - nothing changed.', 16, 1);
    RETURN;
END

-- B. The approver and what their approval route will look like
SELECT 'B. Approver' AS Section, e.EmployeeCode, e.FullName, e.Designation,
       m.EmployeeCode AS OwnManagerCode, m.FullName AS OwnManager,
       CASE WHEN e.ManagerEmployeeId IS NULL
            THEN 'WARNING: ' + @ApproverCode + ' has no manager, so Level 1 will be SKIPPED and ' + @ApproverCode + ' will approve at Level 2.'
            WHEN m.IsActive = 0
            THEN 'WARNING: ' + @ApproverCode + '''s manager is inactive - Level 2 approvals will get stuck.'
            ELSE @ApproverCode + ' approves at Level 1; ' + m.FullName + ' approves at Level 2.' END AS ResultingRoute
FROM Carbynetech_Employee e
LEFT JOIN Carbynetech_Employee m ON m.EmployeeId = e.ManagerEmployeeId
WHERE e.EmployeeId = @ApproverId;

-- Everyone above CBT1267 (to avoid a reporting loop)
DECLARE @Above TABLE (EmployeeId INT PRIMARY KEY);
;WITH up AS (
    SELECT ManagerEmployeeId AS EmployeeId, 1 AS Lvl FROM Carbynetech_Employee WHERE EmployeeId = @ApproverId AND ManagerEmployeeId IS NOT NULL
    UNION ALL
    SELECT e.ManagerEmployeeId, up.Lvl + 1 FROM Carbynetech_Employee e JOIN up ON e.EmployeeId = up.EmployeeId
    WHERE e.ManagerEmployeeId IS NOT NULL AND up.Lvl < 50
)
INSERT INTO @Above SELECT DISTINCT EmployeeId FROM up;

-- Candidates: active employees allocated to either project
DECLARE @Cand TABLE (
    EmployeeId INT PRIMARY KEY, EmployeeCode NVARCHAR(20), FullName NVARCHAR(200), Designation NVARCHAR(200),
    Projects NVARCHAR(500), OldManagerId INT, OldManagerName NVARCHAR(200), Action NVARCHAR(200));

INSERT INTO @Cand (EmployeeId, EmployeeCode, FullName, Designation, Projects, OldManagerId, OldManagerName, Action)
SELECT e.EmployeeId, e.EmployeeCode, e.FullName, e.Designation,
       STRING_AGG(p.Name, '; '),
       e.ManagerEmployeeId, m.FullName,
       CASE
           WHEN e.EmployeeId = @ApproverId THEN 'SKIP - this is the approver'
           WHEN e.EmployeeId IN (SELECT EmployeeId FROM @Above) THEN 'SKIP - above the approver in the reporting line'
           WHEN e.ManagerEmployeeId IS NULL THEN 'SKIP - no manager (senior leadership)'
           WHEN e.Designation LIKE '%Vice President%' OR e.Designation LIKE '%Chief%' THEN 'SKIP - leadership designation'
           WHEN e.ManagerEmployeeId = @ApproverId THEN 'NO CHANGE - already reports to ' + @ApproverCode
           ELSE 'CHANGE manager to ' + @ApproverCode
       END
FROM Carbynetech_EmployeeProjectAllocation a
JOIN Carbynetech_Project p   ON p.ProjectId = a.ProjectId AND p.Name IN (SELECT Name FROM @Projects)
JOIN Carbynetech_Employee e  ON e.EmployeeId = a.EmployeeId AND e.IsActive = 1
LEFT JOIN Carbynetech_Employee m ON m.EmployeeId = e.ManagerEmployeeId
GROUP BY e.EmployeeId, e.EmployeeCode, e.FullName, e.Designation, e.ManagerEmployeeId, m.FullName;

-- C. What will happen to each employee
SELECT 'C. Employees' AS Section, EmployeeCode, FullName, Designation, Projects,
       ISNULL(OldManagerName, '(none)') AS CurrentManager, Action
FROM @Cand
ORDER BY CASE WHEN Action LIKE 'CHANGE%' THEN 0 ELSE 1 END, FullName;

-- D. Weeks already waiting for approval - they will move to the new approver's queue
SELECT 'D. Pending weeks that will be re-routed' AS Section, c.EmployeeCode, c.FullName,
       w.WeekStartDate, w.Status, c.OldManagerName AS PreviousManager
FROM @Cand c
JOIN Carbynetech_WeekRecord w ON w.EmployeeId = c.EmployeeId
WHERE c.Action LIKE 'CHANGE%' AND w.Status IN ('PendingL1', 'PendingL2')
ORDER BY c.FullName, w.WeekStartDate;

IF @Apply = 0
BEGIN
    SELECT 'PREVIEW ONLY' AS Result,
           (SELECT COUNT(*) FROM @Cand WHERE Action LIKE 'CHANGE%') AS EmployeesThatWouldChange,
           'Nothing was changed. Review sections A-D, then set @Apply = 1 and run again.' AS NextStep;
    RETURN;
END

-- ---------------------------------------------------------------------
-- Apply (with backup)
-- ---------------------------------------------------------------------
IF OBJECT_ID('Carbynetech_DryRun_ManagerBackup') IS NULL
    CREATE TABLE Carbynetech_DryRun_ManagerBackup (
        EmployeeId            INT       NOT NULL PRIMARY KEY,
        OldManagerEmployeeId  INT       NULL,
        NewManagerEmployeeId  INT       NOT NULL,
        ChangedAt             DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        Script                NVARCHAR(100) NOT NULL
    );

BEGIN TRAN;

    -- Keep the FIRST recorded manager if the script is run more than once,
    -- so the rollback always returns to the original reporting line.
    INSERT INTO Carbynetech_DryRun_ManagerBackup (EmployeeId, OldManagerEmployeeId, NewManagerEmployeeId, Script)
    SELECT c.EmployeeId, c.OldManagerId, @ApproverId, N'42_techeff_set_cbt1267_as_level1'
    FROM @Cand c
    WHERE c.Action LIKE 'CHANGE%'
      AND NOT EXISTS (SELECT 1 FROM Carbynetech_DryRun_ManagerBackup b WHERE b.EmployeeId = c.EmployeeId);

    UPDATE e
    SET e.ManagerEmployeeId = @ApproverId,
        e.UpdatedAt = SYSUTCDATETIME()
    FROM Carbynetech_Employee e
    JOIN @Cand c ON c.EmployeeId = e.EmployeeId
    WHERE c.Action LIKE 'CHANGE%';

    DECLARE @Changed INT = @@ROWCOUNT;

COMMIT;

SELECT 'APPLIED' AS Result, @Changed AS EmployeesChanged,
       'Previous managers saved in Carbynetech_DryRun_ManagerBackup. Use 42_ROLLBACK to undo.' AS Note;

-- After: confirm
SELECT 'E. After' AS Section, e.EmployeeCode, e.FullName, m.EmployeeCode AS ManagerCode, m.FullName AS ManagerNow
FROM @Cand c
JOIN Carbynetech_Employee e ON e.EmployeeId = c.EmployeeId
LEFT JOIN Carbynetech_Employee m ON m.EmployeeId = e.ManagerEmployeeId
ORDER BY e.FullName;
