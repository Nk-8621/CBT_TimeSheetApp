-- =====================================================================
-- 24_remove_dummy_test_data.sql
--
-- Removes EVERYTHING belonging to the dummy employees TEST001 and TEST002
-- (added by 23_add_dummy_test_data_for_cbt1268.sql or by hand in the app):
-- their weeks + approval trail, time entries, WFH/leave requests, day-type
-- overrides, notifications, project allocations, roles, and the employees.
-- Touches no other employee's data. Runs in one transaction.
-- =====================================================================

USE [Carbynetech_TimeSheet_Application];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @TestIds TABLE (EmployeeId int PRIMARY KEY);
INSERT INTO @TestIds
SELECT EmployeeId FROM Carbynetech_Employee WHERE EmployeeCode IN ('TEST001', 'TEST002');

IF NOT EXISTS (SELECT 1 FROM @TestIds)
BEGIN
    PRINT 'No TEST001/TEST002 employees found - nothing to remove.';
    RETURN;
END

-- Safety: stop if any real employee reports to a test employee.
IF EXISTS (SELECT 1 FROM Carbynetech_Employee e
           WHERE e.ManagerEmployeeId IN (SELECT EmployeeId FROM @TestIds)
             AND e.EmployeeCode NOT IN ('TEST001', 'TEST002'))
BEGIN
    PRINT 'ERROR: a real employee reports to TEST001/TEST002 - move them first. Nothing removed.';
    RETURN;
END

BEGIN TRANSACTION;

DELETE a FROM Carbynetech_ApprovalEvent a
JOIN Carbynetech_WeekRecord w ON w.WeekRecordId = a.WeekRecordId
WHERE w.EmployeeId IN (SELECT EmployeeId FROM @TestIds);
PRINT CONCAT(@@ROWCOUNT, ' approval event(s) removed.');

DELETE FROM Carbynetech_WeekRecord WHERE EmployeeId IN (SELECT EmployeeId FROM @TestIds);
PRINT CONCAT(@@ROWCOUNT, ' week record(s) removed.');

DELETE FROM Carbynetech_TimeEntry WHERE EmployeeId IN (SELECT EmployeeId FROM @TestIds);
PRINT CONCAT(@@ROWCOUNT, ' time entr(y/ies) removed.');

DELETE FROM Carbynetech_DayTypeRequest WHERE EmployeeId IN (SELECT EmployeeId FROM @TestIds);
PRINT CONCAT(@@ROWCOUNT, ' WFH/leave request(s) removed.');

DELETE FROM Carbynetech_DayTypeOverride WHERE EmployeeId IN (SELECT EmployeeId FROM @TestIds);
PRINT CONCAT(@@ROWCOUNT, ' day-type override(s) removed.');

DELETE FROM Carbynetech_Notification WHERE EmployeeId IN (SELECT EmployeeId FROM @TestIds);
PRINT CONCAT(@@ROWCOUNT, ' notification(s) removed.');

DELETE FROM Carbynetech_EmployeeProjectAllocation WHERE EmployeeId IN (SELECT EmployeeId FROM @TestIds);
PRINT CONCAT(@@ROWCOUNT, ' project allocation(s) removed.');

DELETE FROM Carbynetech_EmployeeRole WHERE EmployeeId IN (SELECT EmployeeId FROM @TestIds);
DELETE FROM Carbynetech_EmployeeDepartment WHERE EmployeeId IN (SELECT EmployeeId FROM @TestIds);

-- TEST002 first: it points at TEST001 as its manager.
DELETE FROM Carbynetech_Employee WHERE EmployeeCode = 'TEST002';
DELETE FROM Carbynetech_Employee WHERE EmployeeCode = 'TEST001';
PRINT 'TEST001 / TEST002 removed.';

COMMIT TRANSACTION;
GO
