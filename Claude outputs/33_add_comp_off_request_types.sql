-- =====================================================================
-- 33_add_comp_off_request_types.sql
--
-- Allows 'CompOffFirstHalf', 'CompOffSecondHalf' and 'CompOffFull' as
-- Carbynetech_DayTypeRequest.RequestType, backing the new "Comp-Off - First
-- Half" / "Comp-Off - Second Half" / "Comp-Off - Full Day" options on the
-- Requests screen. A comp-off day is shown on the timesheet exactly like
-- leave (full day = 0h, half day = 4h still worked).
--
-- Run AFTER 31_add_on_duty_request_type.sql - the new constraint keeps
-- 'OnDuty' allowed.
--
-- What this does:
--   1. Replaces CK_Carbynetech_DayTypeRequest_RequestType so it allows
--      WFH / LeaveFirstHalf / LeaveSecondHalf / LeaveFull / OnDuty /
--      CompOffFirstHalf / CompOffSecondHalf / CompOffFull.
--      If an earlier draft of this script was run (it allowed a single
--      'CompOffHalf'), any such rows are relabelled 'CompOffFirstHalf' so
--      they satisfy the new constraint.
--
-- Carbynetech_DayTypeOverride is NOT changed: a comp-off day is resolved
-- from the request itself (same as leave), never written to that table.
--
-- Idempotent: safe to re-run - skips the change if the constraint already
-- allows 'CompOffSecondHalf'.
--
-- SAFE BY DEFAULT: runs inside an open transaction; review what's printed,
-- then explicitly COMMIT or ROLLBACK at the bottom.
-- =====================================================================

USE [Carbynetech_TimeSheet_Application];
GO
SET NOCOUNT ON;

BEGIN TRANSACTION AddCompOffRequestTypes;

PRINT '--- RequestType CHECK constraint BEFORE ---';
SELECT cc.name, cc.definition
FROM sys.check_constraints cc
WHERE cc.parent_object_id = OBJECT_ID('Carbynetech_DayTypeRequest')
  AND cc.name = 'CK_Carbynetech_DayTypeRequest_RequestType';
GO

-- ---------- Step 1: widen the RequestType CHECK constraint ----------
BEGIN TRY
    IF EXISTS (
        SELECT 1
        FROM sys.check_constraints cc
        WHERE cc.parent_object_id = OBJECT_ID('Carbynetech_DayTypeRequest')
          AND cc.name = 'CK_Carbynetech_DayTypeRequest_RequestType'
          AND CHARINDEX('CompOffSecondHalf', cc.definition) > 0)
    BEGIN
        PRINT 'RequestType constraint already allows the Comp-Off types - skipped.';
    END
    ELSE
    BEGIN
        IF EXISTS (
            SELECT 1
            FROM sys.check_constraints cc
            WHERE cc.parent_object_id = OBJECT_ID('Carbynetech_DayTypeRequest')
              AND cc.name = 'CK_Carbynetech_DayTypeRequest_RequestType')
            ALTER TABLE Carbynetech_DayTypeRequest DROP CONSTRAINT CK_Carbynetech_DayTypeRequest_RequestType;

        -- Only possible if an earlier draft of this script (single 'CompOffHalf') was run.
        UPDATE Carbynetech_DayTypeRequest SET RequestType = 'CompOffFirstHalf' WHERE RequestType = 'CompOffHalf';
        PRINT CAST(@@ROWCOUNT AS VARCHAR(10)) + ' CompOffHalf row(s) relabelled CompOffFirstHalf.';

        ALTER TABLE Carbynetech_DayTypeRequest
            ADD CONSTRAINT CK_Carbynetech_DayTypeRequest_RequestType
            CHECK (RequestType IN ('WFH', 'LeaveFirstHalf', 'LeaveSecondHalf', 'LeaveFull', 'OnDuty', 'CompOffFirstHalf', 'CompOffSecondHalf', 'CompOffFull'));
        PRINT 'Replaced CK_Carbynetech_DayTypeRequest_RequestType to allow the Comp-Off types.';
    END
END TRY
BEGIN CATCH
    PRINT 'Step 1 FAILED: ' + ERROR_MESSAGE();
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION AddCompOffRequestTypes;
    PRINT 'Transaction rolled back - nothing was changed.';
END CATCH
GO

PRINT '--- RequestType CHECK constraint AFTER ---';
SELECT cc.name, cc.definition
FROM sys.check_constraints cc
WHERE cc.parent_object_id = OBJECT_ID('Carbynetech_DayTypeRequest')
  AND cc.name = 'CK_Carbynetech_DayTypeRequest_RequestType';
GO

PRINT 'Open transactions: ' + CAST(@@TRANCOUNT AS VARCHAR(10)) + ' (1 = waiting for your COMMIT/ROLLBACK).';
GO

-- Review the output above, then run ONE of these:
-- COMMIT TRANSACTION AddCompOffRequestTypes;
-- ROLLBACK TRANSACTION AddCompOffRequestTypes;
