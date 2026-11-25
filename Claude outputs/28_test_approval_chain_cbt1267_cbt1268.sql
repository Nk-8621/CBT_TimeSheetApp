-- =====================================================================
-- 28_test_approval_chain_cbt1267_cbt1268.sql
--
-- TEMPORARY, for testing the two approval levels:
--   CBT1269, CBT1266, CBT1274  -> report to CBT1267
--   CBT1267                    -> reports to CBT1268
--
-- Result (the app works this out from the reporting line):
--   For CBT1269 / CBT1266 / CBT1274:
--     Level 1 approver = CBT1267 (their manager)
--     Level 2 approver = CBT1268 (their manager's manager)
--   For CBT1267's own week: CBT1268 approves it (Level 1, or Level 2
--   directly if CBT1268 has no manager of his own).
--
-- Before changing anything, the current managers of these 4 employees are
-- saved in table Test28_ManagerBackup. Run the ROLLBACK block at the bottom
-- when testing is done to put them back exactly as they were.
-- Safe to re-run: the backup is taken only once.
-- =====================================================================

USE [Carbynetech_TimeSheet_Application];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Level1Id int = (SELECT EmployeeId FROM Carbynetech_Employee WHERE EmployeeCode = 'CBT1267');
DECLARE @Level2Id int = (SELECT EmployeeId FROM Carbynetech_Employee WHERE EmployeeCode = 'CBT1268');

-- ---- Checks ------------------------------------------------------------
IF @Level1Id IS NULL OR @Level2Id IS NULL
   OR (SELECT COUNT(*) FROM Carbynetech_Employee WHERE EmployeeCode IN ('CBT1269', 'CBT1266', 'CBT1274')) <> 3
BEGIN
    PRINT 'ERROR: one or more of CBT1266 / CBT1267 / CBT1268 / CBT1269 / CBT1274 not found. Nothing changed.';
    RETURN;
END

-- CBT1268 must not already report (directly or higher up) to any of the
-- four, otherwise the new reporting line would go round in a circle.
;WITH Chain AS (
    SELECT EmployeeId, ManagerEmployeeId, 0 AS Step
    FROM Carbynetech_Employee WHERE EmployeeId = @Level2Id
    UNION ALL
    SELECT e.EmployeeId, e.ManagerEmployeeId, c.Step + 1
    FROM Carbynetech_Employee e
    JOIN Chain c ON e.EmployeeId = c.ManagerEmployeeId
    WHERE c.Step < 50
)
SELECT EmployeeId INTO #ChainOfCbt1268 FROM Chain;

IF EXISTS (SELECT 1 FROM #ChainOfCbt1268 ch
           JOIN Carbynetech_Employee e ON e.EmployeeId = ch.EmployeeId
           WHERE e.EmployeeCode IN ('CBT1267', 'CBT1269', 'CBT1266', 'CBT1274'))
BEGIN
    PRINT 'ERROR: CBT1268 reports (directly or higher up) to one of the four employees - this would create a loop. Nothing changed.';
    DROP TABLE #ChainOfCbt1268;
    RETURN;
END
DROP TABLE #ChainOfCbt1268;

BEGIN TRANSACTION;

-- ---- Step 1: back up current managers (only the first time) -------------
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
WHERE e.EmployeeCode IN ('CBT1267', 'CBT1269', 'CBT1266', 'CBT1274')
  AND NOT EXISTS (SELECT 1 FROM dbo.Test28_ManagerBackup b WHERE b.EmployeeId = e.EmployeeId);

-- ---- Step 2: set the new reporting line --------------------------------
UPDATE Carbynetech_Employee
SET ManagerEmployeeId = @Level1Id, UpdatedAt = SYSUTCDATETIME()
WHERE EmployeeCode IN ('CBT1269', 'CBT1266', 'CBT1274');
PRINT CONCAT(@@ROWCOUNT, ' employee(s) now report to CBT1267.');

UPDATE Carbynetech_Employee
SET ManagerEmployeeId = @Level2Id, UpdatedAt = SYSUTCDATETIME()
WHERE EmployeeId = @Level1Id;
PRINT 'CBT1267 now reports to CBT1268.';

COMMIT TRANSACTION;

-- ---- Step 3: show the result ---------------------------------------------
SELECT
    e.EmployeeCode,
    e.FullName,
    m.EmployeeCode  AS Level1_Manager,
    mm.EmployeeCode AS Level2_ManagersManager,
    b.ManagerEmployeeId AS OldManagerId_Backup
FROM Carbynetech_Employee e
LEFT JOIN Carbynetech_Employee m  ON m.EmployeeId  = e.ManagerEmployeeId
LEFT JOIN Carbynetech_Employee mm ON mm.EmployeeId = m.ManagerEmployeeId
LEFT JOIN dbo.Test28_ManagerBackup b ON b.EmployeeId = e.EmployeeId
WHERE e.EmployeeCode IN ('CBT1267', 'CBT1269', 'CBT1266', 'CBT1274')
ORDER BY e.EmployeeCode;

-- Weeks already waiting for approval will now go to the NEW approvers.
SELECT e.EmployeeCode, w.WeekStartDate, w.Status
FROM Carbynetech_WeekRecord w
JOIN Carbynetech_Employee e ON e.EmployeeId = w.EmployeeId
WHERE e.EmployeeCode IN ('CBT1267', 'CBT1269', 'CBT1266', 'CBT1274')
  AND w.Status IN ('PendingL1', 'PendingL2');
GO


-- =====================================================================
-- ROLLBACK - run this block ALONE when testing is done.
-- Puts back the managers saved in Test28_ManagerBackup, then drops it.
-- =====================================================================
/*
USE [Carbynetech_TimeSheet_Application];
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;

UPDATE e
SET e.ManagerEmployeeId = b.ManagerEmployeeId, e.UpdatedAt = SYSUTCDATETIME()
FROM Carbynetech_Employee e
JOIN dbo.Test28_ManagerBackup b ON b.EmployeeId = e.EmployeeId;
PRINT CONCAT(@@ROWCOUNT, ' employee(s) restored to their original manager.');

DROP TABLE dbo.Test28_ManagerBackup;

COMMIT TRANSACTION;
GO
*/
