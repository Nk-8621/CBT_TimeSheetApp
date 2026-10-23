-- =====================================================================
-- 22_grant_all_roles_to_cbt1268_for_testing.sql
--
-- TEMPORARY, for testing: gives employee CBT1268 every product-access role
-- in Carbynetech_Role (EMP / LEAD / L2 / ADMIN ...) so they can open every
-- screen. Remove it again with the ROLLBACK block at the bottom.
--
-- Important - what roles do and don't unlock (see AccessControlService.cs):
--   * ADMIN role  -> Master Data, Team Compliance, Reports.
--   * Level 1 Approvals + Request Approval -> NOT a role. Shown only when
--     the person HAS a manager AND at least one employee reports to them.
--   * Level 2 Approvals -> NOT a role. Shown only when someone reporting
--     to them has their own direct reports (or they're at the top with reports).
-- Step 3 below prints these facts for CBT1268 so you can see which screens
-- will appear.
--
-- Idempotent: safe to re-run; only inserts roles CBT1268 doesn't have yet.
-- =====================================================================

USE [Carbynetech_TimeSheet_Application];
GO
SET NOCOUNT ON;

DECLARE @EmployeeCode nvarchar(15) = 'CBT1268';
DECLARE @EmployeeId int = (SELECT EmployeeId FROM Carbynetech_Employee WHERE EmployeeCode = @EmployeeCode);

IF @EmployeeId IS NULL
BEGIN
    PRINT CONCAT('ERROR: no employee with code ', @EmployeeCode, ' - nothing changed.');
    RETURN;
END

-- Step 1: roles BEFORE the change (note these down - needed for rollback).
PRINT 'Roles before:';
SELECT r.Code, r.Name, er.AssignedAt
FROM Carbynetech_EmployeeRole er
JOIN Carbynetech_Role r ON r.RoleId = er.RoleId
WHERE er.EmployeeId = @EmployeeId;

-- Step 2: grant every role the employee doesn't already have.
INSERT INTO Carbynetech_EmployeeRole (EmployeeId, RoleId, AssignedAt)
SELECT @EmployeeId, r.RoleId, SYSUTCDATETIME()
FROM Carbynetech_Role r
WHERE NOT EXISTS (
    SELECT 1 FROM Carbynetech_EmployeeRole er
    WHERE er.EmployeeId = @EmployeeId AND er.RoleId = r.RoleId
);
PRINT CONCAT(@@ROWCOUNT, ' role(s) granted to ', @EmployeeCode, '.');

PRINT 'Roles after:';
SELECT r.Code, r.Name, er.AssignedAt
FROM Carbynetech_EmployeeRole er
JOIN Carbynetech_Role r ON r.RoleId = er.RoleId
WHERE er.EmployeeId = @EmployeeId;

-- Step 3: approval screens depend on the reporting chain, not roles.
SELECT
    e.EmployeeCode,
    e.FullName,
    m.EmployeeCode AS ManagerCode,
    (SELECT COUNT(*) FROM Carbynetech_Employee d
      WHERE d.ManagerEmployeeId = e.EmployeeId) AS DirectReports,
    (SELECT COUNT(*) FROM Carbynetech_Employee d
      JOIN Carbynetech_Employee g ON g.ManagerEmployeeId = d.EmployeeId
      WHERE d.ManagerEmployeeId = e.EmployeeId) AS ReportsOfReports,
    CASE WHEN e.ManagerEmployeeId IS NOT NULL
          AND EXISTS (SELECT 1 FROM Carbynetech_Employee d WHERE d.ManagerEmployeeId = e.EmployeeId)
         THEN 'Yes' ELSE 'No' END AS SeesLevel1AndRequestApproval,
    CASE WHEN EXISTS (SELECT 1 FROM Carbynetech_Employee d
                      JOIN Carbynetech_Employee g ON g.ManagerEmployeeId = d.EmployeeId
                      WHERE d.ManagerEmployeeId = e.EmployeeId)
           OR (e.ManagerEmployeeId IS NULL
               AND EXISTS (SELECT 1 FROM Carbynetech_Employee d WHERE d.ManagerEmployeeId = e.EmployeeId))
         THEN 'Yes' ELSE 'No' END AS SeesLevel2
FROM Carbynetech_Employee e
LEFT JOIN Carbynetech_Employee m ON m.EmployeeId = e.ManagerEmployeeId
WHERE e.EmployeeId = @EmployeeId;
GO

-- =====================================================================
-- ROLLBACK (run when testing is done). Put the role codes CBT1268 should
-- KEEP (from "Roles before" above) in the list, then run only this block.
-- =====================================================================
/*
USE [Carbynetech_TimeSheet_Application];
DELETE er
FROM Carbynetech_EmployeeRole er
JOIN Carbynetech_Role r ON r.RoleId = er.RoleId
JOIN Carbynetech_Employee e ON e.EmployeeId = er.EmployeeId
WHERE e.EmployeeCode = 'CBT1268'
  AND r.Code NOT IN ('ADMIN');   -- <- roles to keep
PRINT CONCAT(@@ROWCOUNT, ' role(s) removed from CBT1268.');
*/
