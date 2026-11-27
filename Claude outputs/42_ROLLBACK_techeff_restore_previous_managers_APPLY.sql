-- =====================================================================
-- 42_ROLLBACK_techeff_restore_previous_managers.sql
--
-- Undoes 42_techeff_set_cbt1267_as_level1.sql: puts every changed
-- employee back under the manager they had before, using
-- Carbynetech_DryRun_ManagerBackup.
--
-- Only employees whose manager is STILL the one script 42 set are
-- restored. If someone was moved again by hand since then, they are
-- left alone and listed in section A so you can decide.
--
-- SAFE BY DEFAULT: @Apply = 0 only previews. Set @Apply = 1 to restore.
-- Weeks already waiting for approval move back to the previous manager.
-- Select the Carbynetech_Timesheet_App database before running.
-- =====================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Apply BIT = 1;   -- 0 = preview only, 1 = restore

IF OBJECT_ID('Carbynetech_DryRun_ManagerBackup') IS NULL
BEGIN
    RAISERROR('Carbynetech_DryRun_ManagerBackup does not exist - script 42 was never applied. Nothing to roll back.', 16, 1);
    RETURN;
END

-- A. What will be restored
SELECT 'A. Rollback plan' AS Section,
       e.EmployeeCode, e.FullName,
       cur.FullName  AS ManagerNow,
       old.FullName  AS ManagerBefore,
       b.ChangedAt,
       CASE WHEN e.EmployeeId IS NULL THEN 'SKIP - employee no longer exists'
            WHEN ISNULL(e.ManagerEmployeeId, -1) <> b.NewManagerEmployeeId THEN 'SKIP - manager was changed again since script 42; check by hand'
            WHEN b.OldManagerEmployeeId IS NOT NULL AND old.EmployeeId IS NULL THEN 'SKIP - previous manager record no longer exists'
            ELSE 'RESTORE' END AS Action
FROM Carbynetech_DryRun_ManagerBackup b
LEFT JOIN Carbynetech_Employee e   ON e.EmployeeId   = b.EmployeeId
LEFT JOIN Carbynetech_Employee cur ON cur.EmployeeId = e.ManagerEmployeeId
LEFT JOIN Carbynetech_Employee old ON old.EmployeeId = b.OldManagerEmployeeId
ORDER BY e.FullName;

IF @Apply = 0
BEGIN
    SELECT 'PREVIEW ONLY' AS Result,
           'Nothing was changed. Review section A, then set @Apply = 1 and run again.' AS NextStep;
    RETURN;
END

BEGIN TRAN;

    DECLARE @Restored TABLE (EmployeeId INT PRIMARY KEY);

    UPDATE e
    SET e.ManagerEmployeeId = b.OldManagerEmployeeId,
        e.UpdatedAt = SYSUTCDATETIME()
    OUTPUT inserted.EmployeeId INTO @Restored
    FROM Carbynetech_Employee e
    JOIN Carbynetech_DryRun_ManagerBackup b ON b.EmployeeId = e.EmployeeId
    WHERE ISNULL(e.ManagerEmployeeId, -1) = b.NewManagerEmployeeId
      AND (b.OldManagerEmployeeId IS NULL
           OR EXISTS (SELECT 1 FROM Carbynetech_Employee o WHERE o.EmployeeId = b.OldManagerEmployeeId));

    -- Remove backup rows only for the employees actually restored;
    -- anything skipped stays in the table for a later decision.
    DELETE b FROM Carbynetech_DryRun_ManagerBackup b
    WHERE b.EmployeeId IN (SELECT EmployeeId FROM @Restored);

COMMIT;

SELECT 'ROLLED BACK' AS Result, (SELECT COUNT(*) FROM @Restored) AS EmployeesRestored,
       (SELECT COUNT(*) FROM Carbynetech_DryRun_ManagerBackup) AS StillInBackupTable;

-- Drop the backup table once nothing is left in it.
IF NOT EXISTS (SELECT 1 FROM Carbynetech_DryRun_ManagerBackup)
    DROP TABLE Carbynetech_DryRun_ManagerBackup;
