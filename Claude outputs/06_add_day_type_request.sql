-- =====================================================================
-- 06_add_day_type_request.sql
--
-- Adds the new Carbynetech_DayTypeRequest table backing the WFH/Leave
-- request-and-approval feature.
--
-- What this does, in order:
--   1. Creates Carbynetech_DayTypeRequest, if not already present, with
--      RequestType allowing WFH / LeaveFirstHalf / LeaveSecondHalf / LeaveFull.
--   1b. If this table was already created by an earlier run of this script
--      (back when half-day leave was a single 'LeaveHalf' value), upgrades
--      the RequestType CHECK constraint to the new four values.
--   2. Adds a supporting index on (EmployeeId, RequestDate).
--
-- Idempotent: safe to re-run - every step checks whether it's already
-- been done before doing it again.
--
-- SAFE BY DEFAULT: runs inside an open transaction that spans every
-- batch below (the transaction is a connection-level construct, not a
-- batch-level one, so GO doesn't end it); review what's printed, then
-- explicitly COMMIT or ROLLBACK at the bottom.
-- =====================================================================

USE [Carbynetech_TimeSheet_Application];
GO
SET NOCOUNT ON;

BEGIN TRANSACTION AddDayTypeRequest;

PRINT '--- Row count BEFORE (Carbynetech_DayTypeRequest) ---';
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Carbynetech_DayTypeRequest')
    SELECT COUNT(*) AS [RowCount] FROM Carbynetech_DayTypeRequest;
ELSE
    PRINT 'Table does not exist yet.';
GO

-- ---------- Step 1: create the table ----------
BEGIN TRY
    IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Carbynetech_DayTypeRequest')
    BEGIN
        CREATE TABLE Carbynetech_DayTypeRequest (
            DayTypeRequestId   INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Carbynetech_DayTypeRequest PRIMARY KEY,
            EmployeeId         INT NOT NULL,
            RequestDate        DATE NOT NULL,
            RequestType        NVARCHAR(20) NOT NULL,
            Status             NVARCHAR(20) NOT NULL CONSTRAINT DF_Carbynetech_DayTypeRequest_Status DEFAULT 'Pending',
            -- Nullable here (same convention as Carbynetech_TimeEntry.Note) -
            -- required-ness is enforced by DayTypeRequestService, not the DB.
            Note               NVARCHAR(500) NULL,
            SubmittedAt        DATETIME2 NOT NULL,
            ApproverEmployeeId INT NULL,
            DecidedAt          DATETIME2 NULL,
            DecisionComment    NVARCHAR(500) NULL,
            -- Placeholders for a future Keka integration (currently on
            -- hold) - unused today, always 'Meridian' / NULL.
            Source             NVARCHAR(20) NOT NULL CONSTRAINT DF_Carbynetech_DayTypeRequest_Source DEFAULT 'Meridian',
            ExternalRef        NVARCHAR(100) NULL,

            CONSTRAINT FK_Carbynetech_DayTypeRequest_Employee
                FOREIGN KEY (EmployeeId) REFERENCES Carbynetech_Employee(EmployeeId),
            CONSTRAINT FK_Carbynetech_DayTypeRequest_Approver
                FOREIGN KEY (ApproverEmployeeId) REFERENCES Carbynetech_Employee(EmployeeId),
            CONSTRAINT CK_Carbynetech_DayTypeRequest_RequestType
                CHECK (RequestType IN ('WFH', 'LeaveFirstHalf', 'LeaveSecondHalf', 'LeaveFull')),
            CONSTRAINT CK_Carbynetech_DayTypeRequest_Status
                CHECK (Status IN ('Pending', 'Approved', 'Rejected'))
        );
        PRINT 'Created Carbynetech_DayTypeRequest.';
    END
    ELSE
        PRINT 'Carbynetech_DayTypeRequest already exists - skipped.';
END TRY
BEGIN CATCH
    PRINT 'ERROR in Step 1 - rolling back everything.';
    PRINT ERROR_MESSAGE();
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION AddDayTypeRequest;
    THROW;
END CATCH
GO

-- ---------- Step 1b: upgrade the RequestType CHECK constraint if this ----------
-- ---------- table was already created by an earlier run of this script ----------
-- (back when half-day leave was a single 'LeaveHalf' option instead of
-- 'LeaveFirstHalf' / 'LeaveSecondHalf'). Safe no-op if the table is brand
-- new (Step 1 above already created it with the right constraint) or if
-- this was already upgraded before.
BEGIN TRY
    IF @@TRANCOUNT = 0
        PRINT 'Transaction was already rolled back (Step 1 failed) - stopping before Step 1b.';
    ELSE
    BEGIN
        DECLARE @OldRequestTypeCheck NVARCHAR(200);
        SELECT @OldRequestTypeCheck = cc.name
        FROM sys.check_constraints cc
        WHERE cc.parent_object_id = OBJECT_ID('Carbynetech_DayTypeRequest')
          AND cc.name = 'CK_Carbynetech_DayTypeRequest_RequestType'
          AND CHARINDEX('LeaveFirstHalf', cc.definition) = 0;

        IF @OldRequestTypeCheck IS NOT NULL
        BEGIN
            -- Order matters here: the OLD constraint only allows 'LeaveHalf',
            -- so it has to be dropped before relabelling those rows to
            -- 'LeaveFirstHalf' (the UPDATE itself would violate it otherwise -
            -- what happened on the second attempt at this migration); and the
            -- relabel has to happen before the NEW constraint is added, since
            -- that one no longer allows 'LeaveHalf' at all (what happened on
            -- the first attempt).
            EXEC('ALTER TABLE Carbynetech_DayTypeRequest DROP CONSTRAINT [' + @OldRequestTypeCheck + ']');

            -- Any existing 'LeaveHalf' rows (only possible if requests were
            -- already submitted against the old constraint) are relabelled
            -- as LeaveFirstHalf so they satisfy the new constraint - there's
            -- no way to recover which half was actually meant.
            UPDATE Carbynetech_DayTypeRequest SET RequestType = 'LeaveFirstHalf' WHERE RequestType = 'LeaveHalf';

            ALTER TABLE Carbynetech_DayTypeRequest
                ADD CONSTRAINT CK_Carbynetech_DayTypeRequest_RequestType
                CHECK (RequestType IN ('WFH', 'LeaveFirstHalf', 'LeaveSecondHalf', 'LeaveFull'));
            PRINT 'Upgraded CK_Carbynetech_DayTypeRequest_RequestType to allow LeaveFirstHalf/LeaveSecondHalf.';
        END
        ELSE
            PRINT 'RequestType constraint already up to date - skipped.';
    END
END TRY
BEGIN CATCH
    PRINT 'ERROR in Step 1b - rolling back everything.';
    PRINT ERROR_MESSAGE();
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION AddDayTypeRequest;
    THROW;
END CATCH
GO

-- ---------- Step 2: supporting index ----------
BEGIN TRY
    IF @@TRANCOUNT = 0
        PRINT 'Transaction was already rolled back (an earlier step failed) - stopping before Step 2.';
    ELSE
    BEGIN
        IF NOT EXISTS (
            SELECT 1 FROM sys.indexes
            WHERE name = 'IX_Carbynetech_DayTypeRequest_Employee_Date'
              AND object_id = OBJECT_ID('Carbynetech_DayTypeRequest')
        )
        BEGIN
            CREATE INDEX IX_Carbynetech_DayTypeRequest_Employee_Date
                ON Carbynetech_DayTypeRequest (EmployeeId, RequestDate);
            PRINT 'Created index IX_Carbynetech_DayTypeRequest_Employee_Date.';
        END
        ELSE
            PRINT 'Index already exists - skipped.';

        PRINT '';
        PRINT '--- Carbynetech_DayTypeRequest columns AFTER this script ---';
        SELECT c.name AS ColumnName, t.name AS DataType, c.max_length, c.is_nullable
        FROM sys.columns c
        JOIN sys.types t ON t.user_type_id = c.user_type_id
        WHERE c.object_id = OBJECT_ID('Carbynetech_DayTypeRequest')
        ORDER BY c.column_id;

        PRINT '';
        PRINT 'Review the columns above.';
        PRINT 'If they look right: run  COMMIT TRANSACTION AddDayTypeRequest;';
        PRINT 'If anything looks wrong: run  ROLLBACK TRANSACTION AddDayTypeRequest;';
    END
END TRY
BEGIN CATCH
    PRINT 'ERROR in Step 2 - rolling back everything.';
    PRINT ERROR_MESSAGE();
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION AddDayTypeRequest;
    THROW;
END CATCH
GO

-- ---------------------------------------------------------------------
-- Run exactly ONE of these two lines yourself, after reviewing the
-- output above. Nothing below runs automatically.
-- ---------------------------------------------------------------------
-- COMMIT TRANSACTION AddDayTypeRequest;
-- ROLLBACK TRANSACTION AddDayTypeRequest;
