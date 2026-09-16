-- =====================================================================
-- 11_assign_manager_cbt1017.sql
--
-- Sets CBT1017 as the direct manager (Carbynetech_Employee.ManagerEmployeeId)
-- for the 16 employees Babu listed on 2026-09-15.
--
-- NOTE on "manager access screens": in this app, Team / Approvals /
-- Reports navigation is NOT a role you grant - it's computed automatically
-- from the reporting hierarchy (see AccessControlService.GetAccessProfileAsync).
-- The moment these 16 rows point to CBT1017 as their manager, CBT1017 will
-- see those screens the next time they log in - no separate role/permission
-- change needed, and per Babu's confirmation, ADMIN (the separate, much
-- broader Master Data role) is deliberately NOT being granted here.
--
-- Safe to re-run: re-running just re-applies the same ManagerEmployeeId.
-- Prints which of the 16 codes (if any) don't exist, so a typo doesn't
-- silently do nothing.
-- =====================================================================

USE [Carbynetech_TimeSheet_Application];
GO
SET NOCOUNT ON;

DECLARE @Now datetime2 = SYSUTCDATETIME();
DECLARE @ManagerCode nvarchar(20) = 'CBT1017';
DECLARE @ManagerId int = (SELECT EmployeeId FROM Carbynetech_Employee WHERE EmployeeCode = @ManagerCode);

IF @ManagerId IS NULL
BEGIN
    PRINT CONCAT('ERROR: manager EmployeeCode ', @ManagerCode, ' not found - nothing was changed.');
    RETURN;
END

DECLARE @Reports TABLE (EmployeeCode nvarchar(20));
INSERT INTO @Reports (EmployeeCode) VALUES
    ('CBT1143'), ('CBT1323'), ('CBT1299'), ('CBT1245'),
    ('CBT1252'), ('CBT1290'), ('CBT1324'), ('CBT1144'),
    ('CBT1259'), ('CBT1346'), ('CBT1262'), ('CBT1336'),
    ('CBT1263'), ('CBT1291'), ('CBT1185'), ('CBT1235');

-- Flag any codes that don't exist in the DB, so they're not silently skipped.
SELECT r.EmployeeCode AS MissingEmployeeCode
FROM @Reports r
LEFT JOIN Carbynetech_Employee e ON e.EmployeeCode = r.EmployeeCode
WHERE e.EmployeeId IS NULL;

UPDATE emp
SET emp.ManagerEmployeeId = @ManagerId,
    emp.UpdatedAt = @Now
FROM Carbynetech_Employee emp
JOIN @Reports r ON r.EmployeeCode = emp.EmployeeCode
WHERE emp.EmployeeId <> @ManagerId; -- guard against CBT1017 being its own manager if it were ever in the list

PRINT CONCAT(@@ROWCOUNT, ' employee row(s) updated to report to ', @ManagerCode, ' (expect 16 - fewer means some codes above were missing).');
PRINT '';
PRINT 'CBT1017 will see Team / Approvals (Level 1, and Level 2 if any of these 16 already have their own reports) / Reports on next login - this is automatic, nothing else to run.';
GO
