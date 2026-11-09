-- =====================================================================
-- 31_add_on_duty_request_type.sql
--   (renumbered from 28 - that number was already used by
--    28_test_approval_chain_cbt1267_cbt1268.sql)
--
-- Allows 'OnDuty' as a Carbynetech_DayTypeRequest.RequestType, backing the
-- new "On Duty" option on the Requests screen (a full 8h working day spent
-- away on official duty, shown as OD on the timesheet grid).
--
-- What this does:
--   1. Replaces CK_Carbynetech_DayTypeRequest_RequestType so it allows
--      WFH / LeaveFirstHalf / LeaveSecondHalf / LeaveFull / OnDuty.
--
-- Carbynetech_DayTypeOverride is NOT changed: an On Duty day is resolved
-- from the request itself (same as leave requests), never written to the
-- override table, so that table's CHECK constraint stays as it is.
--
-- Idempotent: safe to re-run - skips the change if the constraint already
-- allows 'OnDuty'.
--
-- SAFE BY DEFAULT: runs inside an open transaction; review what's printed,
-- then explicitly COMMIT or ROLLBACK at the bottom.
-- =====================================================================

USE [Carbynetech_TimeSheet_Application];
GO
SET NOCOUNT ON;

BEGIN TRANSACTION AddOnDutyRequestType;

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
          AND CHARINDEX('OnDuty', cc.definition) > 0)
    BEGIN
        PRINT 'RequestType constraint already allows OnDuty - skipped.';
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
            CHECK (RequestType IN ('WFH', 'LeaveFirstHalf', 'LeaveSecondHalf', 'LeaveFull', 'OnDuty'));
        PRINT 'Replaced CK_Carbynetech_DayTypeRequest_RequestType to allow OnDuty.';
    END
END TRY
BEGIN CATCH
    PRINT 'Step 1 FAILED: ' + ERROR_MESSAGE();
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION AddOnDutyRequestType;
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
-- COMMIT TRANSACTION AddOnDutyRequestType;
-- ROLLBACK TRANSACTION AddOnDutyRequestType;
