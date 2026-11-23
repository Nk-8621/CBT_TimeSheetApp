-- =====================================================================
-- 37_allow_half_day_leave_day_type.sql
--
-- HALF DAY LEAVE (Nov 2026): employees can now pick "Half Day Leave" as
-- the day status in Add Task. It is saved as 'LH' in
-- Carbynetech_DayTypeOverride.DayType.
--
-- What this script does:
--   Replaces the CHECK constraint on Carbynetech_DayTypeOverride.DayType
--   so it allows 'LH' as well: W, WFH, L, LH, H, O.
--   No existing rows are changed.
--
-- Without it, saving Half Day Leave fails with "An unexpected error
-- occurred" (the database rejects 'LH').
--
-- Select the timesheet database before running (no USE line on purpose).
-- Idempotent - skips the change if the constraint already allows 'LH'.
-- A ROLLBACK block is at the bottom (commented out).
-- =====================================================================

SET NOCOUNT ON;

PRINT '--- DayType CHECK constraint BEFORE ---';
SELECT cc.name, cc.definition
FROM sys.check_constraints cc
WHERE cc.parent_object_id = OBJECT_ID('Carbynetech_DayTypeOverride')
  AND CHARINDEX('[DayType]', cc.definition) > 0;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    -- Find the existing CHECK constraint on the DayType column (by what it
    -- checks, so it works whatever the constraint is called).
    DECLARE @OldName SYSNAME, @OldDefinition NVARCHAR(MAX);
    SELECT TOP 1 @OldName = cc.name, @OldDefinition = cc.definition
    FROM sys.check_constraints cc
    WHERE cc.parent_object_id = OBJECT_ID('Carbynetech_DayTypeOverride')
      AND CHARINDEX('[DayType]', cc.definition) > 0;

    IF @OldDefinition IS NOT NULL AND CHARINDEX('''LH''', @OldDefinition) > 0
    BEGIN
        PRINT 'DayType constraint already allows LH - skipped.';
    END
    ELSE
    BEGIN
        IF @OldName IS NOT NULL
        BEGIN
            DECLARE @Sql NVARCHAR(400) = N'ALTER TABLE Carbynetech_DayTypeOverride DROP CONSTRAINT ' + QUOTENAME(@OldName) + N';';
            EXEC sp_executesql @Sql;
            PRINT CONCAT('Dropped old constraint ', @OldName, '.');
        END

        ALTER TABLE Carbynetech_DayTypeOverride
            ADD CONSTRAINT CK_Carbynetech_DayTypeOverride_DayType
            CHECK (DayType IN ('W', 'WFH', 'L', 'LH', 'H', 'O'));
        PRINT 'Added CK_Carbynetech_DayTypeOverride_DayType allowing W, WFH, L, LH, H, O.';
    END

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    PRINT 'FAILED - nothing was changed: ' + ERROR_MESSAGE();
END CATCH
GO

PRINT '--- DayType CHECK constraint AFTER ---';
SELECT cc.name, cc.definition
FROM sys.check_constraints cc
WHERE cc.parent_object_id = OBJECT_ID('Carbynetech_DayTypeOverride')
  AND CHARINDEX('[DayType]', cc.definition) > 0;
GO

-- =====================================================================
-- ROLLBACK (run only to undo this script). Half Day Leave rows are
-- turned back into Working first, otherwise the old rule can't be added.
-- =====================================================================
-- UPDATE Carbynetech_DayTypeOverride SET DayType = 'W' WHERE DayType = 'LH';
-- ALTER TABLE Carbynetech_DayTypeOverride DROP CONSTRAINT CK_Carbynetech_DayTypeOverride_DayType;
-- ALTER TABLE Carbynetech_DayTypeOverride
--     ADD CONSTRAINT CK_Carbynetech_DayTypeOverride_DayType
--     CHECK (DayType IN ('W', 'WFH', 'L', 'H', 'O'));
