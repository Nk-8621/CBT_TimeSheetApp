-- =====================================================================
-- 29_test_remove_manager_of_cbt1268.sql
--
-- TEMPORARY, for testing: CBT1268 gets NO manager (top of the hierarchy).
--
-- His current manager is first saved in table Test28_ManagerBackup (the
-- same backup table script 28 uses), so the ROLLBACK block of script 28
-- also puts CBT1268's manager back. A rollback block is also below.
-- Safe to re-run: the backup is taken only once.
-- =====================================================================

USE [Carbynetech_TimeSheet_Application];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF NOT EXISTS (SELECT 1 FROM Carbynetech_Employee WHERE EmployeeCode = 'CBT1268')
BEGIN
    PRINT 'ERROR: employee CBT1268 not found. Nothing changed.';
    RETURN;
END

BEGIN TRANSACTION;

-- ---- Step 1: back up the current manager (only the first time) ----------
IF OBJECT_ID('dbo.Test28_ManagerBackup') IS NULL
BEGIN
    CREATE TABLE dbo.Test28_ManagerBackup (
        EmployeeId        int PRIMARY KEY,
        EmployeeCode      nvarchar(15) NOT NULL,
        ManagerEmployeeId int NULL,
        BackedUpAt        datetime2 NOT NULL
    );
END

INSERT INTO dbo.Test28_ManagerBackup (EmployeeId, EmployeeCode, ManagerEmployeeId, BackedUpAt)
SELECT e.EmployeeId, e.EmployeeCode, e.ManagerEmployeeId, SYSUTCDATETIME()
FROM Carbynetech_Employee e
WHERE e.EmployeeCode = 'CBT1268'
  AND NOT EXISTS (SELECT 1 FROM dbo.Test28_ManagerBackup b WHERE b.EmployeeId = e.EmployeeId);

-- ---- Step 2: remove the manager ------------------------------------------
UPDATE Carbynetech_Employee
SET ManagerEmployeeId = NULL, UpdatedAt = SYSUTCDATETIME()
WHERE EmployeeCode = 'CBT1268';
PRINT 'CBT1268 now has no manager.';

COMMIT TRANSACTION;

-- ---- Step 3: show the result ---------------------------------------------
SELECT e.EmployeeCode, e.FullName, e.ManagerEmployeeId AS ManagerNow,
       b.ManagerEmployeeId AS OldManagerId_Backup
FROM Carbynetech_Employee e
LEFT JOIN dbo.Test28_ManagerBackup b ON b.EmployeeId = e.EmployeeId
WHERE e.EmployeeCode = 'CBT1268';
GO


-- =====================================================================
-- ROLLBACK (only CBT1268) - run this block ALONE when testing is done.
-- Not needed if you run script 28's rollback: that restores everyone in
-- Test28_ManagerBackup, CBT1268 included.
-- =====================================================================
/*
USE [Carbynetech_TimeSheet_Application];
GO
UPDATE e
SET e.ManagerEmployeeId = b.ManagerEmployeeId, e.UpdatedAt = SYSUTCDATETIME()
FROM Carbynetech_Employee e
JOIN dbo.Test28_ManagerBackup b ON b.EmployeeId = e.EmployeeId
WHERE e.EmployeeCode = 'CBT1268';

DELETE FROM dbo.Test28_ManagerBackup WHERE EmployeeCode = 'CBT1268';
GO
*/
