-- =====================================================================
-- 27_remove_realistic_test_data_manager_hr_sales.sql
--
-- Removes what 26_add_realistic_test_data_manager_hr_sales.sql added:
--   * CBT1152 / CBT1128 (REAL employees): ONLY the rows script 26 wrote -
--     recognised by their timestamp ending in .777 ms. Their real
--     timesheets, requests and allocations are left untouched.
--   * TEST005 (Rohit Verma) and all of his data.
--   * Projects INT-HROPS / INT-SALES with modules and tasks, unless real
--     employees have logged time on them (then they are kept).
--   * Accounts "HR (Internal)" / "Sales (Internal)" and department Sales,
--     each only if nothing else uses it.
-- Runs in one transaction.
-- =====================================================================

USE [Carbynetech_TimeSheet_Application];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @RealIds TABLE (EmployeeId int PRIMARY KEY);
INSERT INTO @RealIds SELECT EmployeeId FROM Carbynetech_Employee WHERE EmployeeCode IN ('CBT1152', 'CBT1128');

DECLARE @SalesId int = (SELECT EmployeeId FROM Carbynetech_Employee WHERE EmployeeCode = 'TEST005');

IF EXISTS (SELECT 1 FROM Carbynetech_Employee WHERE ManagerEmployeeId = @SalesId)
BEGIN
    PRINT 'ERROR: someone reports to TEST005 - move them first. Nothing removed.';
    RETURN;
END

BEGIN TRANSACTION;

-- ---- CBT1152 / CBT1128: only the marked (.777) rows --------------------------------
DELETE a FROM Carbynetech_ApprovalEvent a
JOIN Carbynetech_WeekRecord w ON w.WeekRecordId = a.WeekRecordId
WHERE w.EmployeeId IN (SELECT EmployeeId FROM @RealIds) AND DATEPART(millisecond, w.CreatedAt) = 777;
DELETE FROM Carbynetech_WeekRecord
WHERE EmployeeId IN (SELECT EmployeeId FROM @RealIds) AND DATEPART(millisecond, CreatedAt) = 777;
PRINT CONCAT(@@ROWCOUNT, ' test week record(s) removed for CBT1152 / CBT1128.');

DELETE FROM Carbynetech_TimeEntry
WHERE EmployeeId IN (SELECT EmployeeId FROM @RealIds) AND DATEPART(millisecond, CreatedAt) = 777;
PRINT CONCAT(@@ROWCOUNT, ' test timesheet line(s) removed for CBT1152 / CBT1128.');

DELETE FROM Carbynetech_DayTypeRequest
WHERE EmployeeId IN (SELECT EmployeeId FROM @RealIds) AND DATEPART(millisecond, SubmittedAt) = 777;
DELETE FROM Carbynetech_DayTypeOverride
WHERE EmployeeId IN (SELECT EmployeeId FROM @RealIds) AND DATEPART(millisecond, CreatedAt) = 777;
-- A test allocation goes only if no real time entry still uses it.
DELETE a FROM Carbynetech_EmployeeProjectAllocation a
WHERE a.EmployeeId IN (SELECT EmployeeId FROM @RealIds) AND DATEPART(millisecond, a.AssignedAt) = 777
  AND NOT EXISTS (SELECT 1 FROM Carbynetech_TimeEntry t WHERE t.EmployeeId = a.EmployeeId AND t.ProjectId = a.ProjectId);

-- ---- TEST005: everything ----------------------------------------------------------
IF @SalesId IS NOT NULL
BEGIN
    DELETE a FROM Carbynetech_ApprovalEvent a
    JOIN Carbynetech_WeekRecord w ON w.WeekRecordId = a.WeekRecordId
    WHERE w.EmployeeId = @SalesId;
    DELETE FROM Carbynetech_WeekRecord      WHERE EmployeeId = @SalesId;
    DELETE FROM Carbynetech_TimeEntry       WHERE EmployeeId = @SalesId;
    DELETE FROM Carbynetech_DayTypeRequest  WHERE EmployeeId = @SalesId;
    DELETE FROM Carbynetech_DayTypeOverride WHERE EmployeeId = @SalesId;
    DELETE FROM Carbynetech_Notification    WHERE EmployeeId = @SalesId;
    DELETE FROM Carbynetech_EmployeeProjectAllocation WHERE EmployeeId = @SalesId;
    DELETE FROM Carbynetech_EmployeeRole       WHERE EmployeeId = @SalesId;
    DELETE FROM Carbynetech_EmployeeDepartment WHERE EmployeeId = @SalesId;
    DELETE FROM Carbynetech_Employee           WHERE EmployeeId = @SalesId;
    PRINT 'TEST005 removed.';
END

-- ---- Internal test projects (kept if real employees logged time on them) ----------
DECLARE @TestProjects TABLE (ProjectId int PRIMARY KEY);
INSERT INTO @TestProjects SELECT ProjectId FROM Carbynetech_Project WHERE Code IN ('INT-HROPS', 'INT-SALES');

IF EXISTS (SELECT 1 FROM Carbynetech_TimeEntry t
           JOIN Carbynetech_Employee e ON e.EmployeeId = t.EmployeeId
           WHERE t.ProjectId IN (SELECT ProjectId FROM @TestProjects) AND e.EmployeeCode NOT LIKE 'TEST%')
BEGIN
    PRINT 'NOTE: real time entries exist on INT-HROPS / INT-SALES - projects, accounts and Sales department kept.';
END
ELSE
BEGIN
    DELETE FROM Carbynetech_TimeEntry WHERE ProjectId IN (SELECT ProjectId FROM @TestProjects);   -- TEST001/TEST002 from script 25, if any
    DELETE FROM Carbynetech_EmployeeProjectAllocation WHERE ProjectId IN (SELECT ProjectId FROM @TestProjects);
    DELETE t FROM Carbynetech_Task t
    JOIN Carbynetech_Module m ON m.ModuleId = t.ModuleId
    WHERE m.ProjectId IN (SELECT ProjectId FROM @TestProjects);
    DELETE FROM Carbynetech_Module  WHERE ProjectId IN (SELECT ProjectId FROM @TestProjects);
    DELETE FROM Carbynetech_Project WHERE ProjectId IN (SELECT ProjectId FROM @TestProjects);
    PRINT 'Projects INT-HROPS / INT-SALES removed.';

    DELETE FROM Carbynetech_Account
    WHERE Name IN ('HR (Internal)', 'Sales (Internal)')
      AND NOT EXISTS (SELECT 1 FROM Carbynetech_Project p WHERE p.AccountId = Carbynetech_Account.AccountId)
      AND NOT EXISTS (SELECT 1 FROM Carbynetech_Employee e WHERE e.PrimaryAccountId = Carbynetech_Account.AccountId)
      AND NOT EXISTS (SELECT 1 FROM Carbynetech_Holiday h WHERE h.AccountId = Carbynetech_Account.AccountId);
    PRINT CONCAT(@@ROWCOUNT, ' internal account(s) removed.');

    DELETE FROM Carbynetech_Department
    WHERE Code = 'SALES'
      AND NOT EXISTS (SELECT 1 FROM Carbynetech_Employee e WHERE e.DepartmentId = Carbynetech_Department.DepartmentId)
      AND NOT EXISTS (SELECT 1 FROM Carbynetech_EmployeeDepartment ed WHERE ed.DepartmentId = Carbynetech_Department.DepartmentId)
      AND NOT EXISTS (SELECT 1 FROM Carbynetech_Account a WHERE a.DepartmentId = Carbynetech_Department.DepartmentId)
      AND NOT EXISTS (SELECT 1 FROM Carbynetech_Department c WHERE c.ParentDepartmentId = Carbynetech_Department.DepartmentId);
    PRINT CONCAT(@@ROWCOUNT, ' Sales department removed (0 = still in use, kept).');
END

COMMIT TRANSACTION;
GO
