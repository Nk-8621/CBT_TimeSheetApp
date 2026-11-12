-- =====================================================================
-- 34_add_unpaid_leave_request_types.sql
--
-- Allows 'UnpaidFirstHalf', 'UnpaidSecondHalf' and 'UnpaidFull' as
-- Carbynetech_DayTypeRequest.RequestType, backing the new "Unpaid Leave"
-- option (Full day / Custom -> First or Second half) on the Requests
-- screen. An unpaid leave day is shown on the timesheet exactly like leave
-- (full day = 0h, half day = 4h still worked). The existing Leave* values
-- are now presented as "Paid Leave" - their stored values do not change.
--
-- The names are short on purpose: RequestType is NVARCHAR(20), and
-- 'UnpaidLeaveSecondHalf' would not fit.
--
-- Run AFTER 31_add_on_duty_request_type.sql and
-- 33_add_comp_off_request_types.sql - the new constraint keeps 'OnDuty'
-- and the Comp-Off values allowed.
--
-- What this does:
--   1. Replaces CK_Carbynetech_DayTypeRequest_RequestType so it allows
--      WFH / LeaveFirstHalf / LeaveSecondHalf / LeaveFull / OnDuty /
--      CompOffFirstHalf / CompOffSecondHalf / CompOffFull /
--      UnpaidFirstHalf / UnpaidSecondHalf / UnpaidFull.
--
-- Carbynetech_DayTypeOverride is NOT changed: an unpaid leave day is
-- resolved from the request itself (same as leave), never written there.
--
-- Idempotent: safe to re-run - skips the change if the constraint already
-- allows 'UnpaidSecondHalf'.
--
-- SAFE BY DEFAULT: runs inside an open transaction; review what's printed,
-- then explicitly COMMIT or ROLLBACK at the bottom.
-- =====================================================================

USE [Carbynetech_TimeSheet_Application];
GO
SET NOCOUNT ON;

BEGIN TRANSACTION AddUnpaidLeaveRequestTypes;

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
          AND CHARINDEX('UnpaidSecondHalf', cc.definition) > 0)
    BEGIN
        PRINT 'RequestType constraint already allows the Unpaid leave types - skipped.';
    END
    ELSE
    BEGIN
        IF EXISTS (
            SELECT 1
            FROM sys.check_constraints cc
            WHERE cc.parent_object_id = OBJECT_ID('Carbynetech_DayTypeRequest')
              AND cc.name = 'CK_Carbynetech_DayTypeRequest_RequestType')
            ALTER TABLE Carbynetech_DayTypeRequest DROP CONSTRAINT CK_Carbynetech_DayTypeRequest_RequestType;

        ALTER TABLE Carbynetech_DayTypeRequest
            ADD CONSTRAINT CK_Carbynetech_DayTypeRequest_RequestType
            CHECK (RequestType IN ('WFH', 'LeaveFirstHalf', 'LeaveSecondHalf', 'LeaveFull', 'OnDuty',
                                   'CompOffFirstHalf', 'CompOffSecondHalf', 'CompOffFull',
                                   'UnpaidFirstHalf', 'UnpaidSecondHalf', 'UnpaidFull'));
        PRINT 'Replaced CK_Carbynetech_DayTypeRequest_RequestType to allow the Unpaid leave types.';
    END
END TRY
BEGIN CATCH
    PRINT 'Step 1 FAILED: ' + ERROR_MESSAGE();
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION AddUnpaidLeaveRequestTypes;
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
-- COMMIT TRANSACTION AddUnpaidLeaveRequestTypes;
-- ROLLBACK TRANSACTION AddUnpaidLeaveRequestTypes;
