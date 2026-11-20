-- =====================================================================
-- 36_add_client_week_offs.sql
--
-- WEEK OFFS (Nov 2026), step 2: week offs are now set per CLIENT, and
-- employees are assigned to the client (Master Data -> Week Offs).
--
-- What this script does:
--   1. Creates Carbynetech_ClientWeekOff - one row per client per period:
--      week-off days (e.g. 'Fri,Sat'), From and To (To NULL = no end date).
--   2. Moves any rows already in Carbynetech_EmployeeWeekOff (from script
--      35) over: one client week off per client, using that client's
--      earliest row's days and From date.
--   3. Drops the WeekOffDays column from Carbynetech_EmployeeWeekOff - an
--      employee row is now just: employee, client, From, To.
--
-- Run AFTER 35_add_employee_week_offs.sql, together with the backend and
-- frontend changes for client week offs.
-- Select the timesheet database before running (no USE line on purpose).
-- Idempotent - every step checks its own pre-condition. Safe to re-run.
-- A ROLLBACK block is at the bottom (commented out).
-- =====================================================================

SET NOCOUNT ON;

-- Step 1: the client table.
IF OBJECT_ID('Carbynetech_ClientWeekOff') IS NULL
BEGIN
    CREATE TABLE Carbynetech_ClientWeekOff
    (
        ClientWeekOffId     INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_Carbynetech_ClientWeekOff PRIMARY KEY,
        AccountId           INT           NOT NULL
            CONSTRAINT FK_Carbynetech_ClientWeekOff_Account REFERENCES Carbynetech_Account (AccountId),
        WeekOffDays         NVARCHAR(30)  NOT NULL,
        EffectiveFrom       DATE          NOT NULL,
        EffectiveTo         DATE          NULL,
        CreatedByEmployeeId INT           NULL
            CONSTRAINT FK_Carbynetech_ClientWeekOff_CreatedBy REFERENCES Carbynetech_Employee (EmployeeId),
        CreatedAt           DATETIME2     NOT NULL
            CONSTRAINT DF_Carbynetech_ClientWeekOff_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt           DATETIME2     NULL,
        CONSTRAINT CK_Carbynetech_ClientWeekOff_Dates CHECK (EffectiveTo IS NULL OR EffectiveTo >= EffectiveFrom)
    );
    CREATE INDEX IX_Carbynetech_ClientWeekOff_AccountId ON Carbynetech_ClientWeekOff (AccountId);
    PRINT 'Created Carbynetech_ClientWeekOff.';
END
ELSE
    PRINT 'Carbynetech_ClientWeekOff already exists - skipped.';
GO

-- Step 2: move existing employee rows' days to their client (only while the old column still exists).
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Carbynetech_EmployeeWeekOff') AND name = 'WeekOffDays')
BEGIN
    EXEC('
        INSERT INTO Carbynetech_ClientWeekOff (AccountId, WeekOffDays, EffectiveFrom, EffectiveTo, CreatedByEmployeeId, CreatedAt)
        SELECT e.AccountId, e.WeekOffDays, e.EffectiveFrom, NULL, e.CreatedByEmployeeId, SYSUTCDATETIME()
        FROM Carbynetech_EmployeeWeekOff e
        WHERE e.EmployeeWeekOffId = (SELECT TOP 1 x.EmployeeWeekOffId FROM Carbynetech_EmployeeWeekOff x
                                     WHERE x.AccountId = e.AccountId ORDER BY x.EffectiveFrom, x.EmployeeWeekOffId)
          AND NOT EXISTS (SELECT 1 FROM Carbynetech_ClientWeekOff c WHERE c.AccountId = e.AccountId);
    ');
    PRINT 'Moved existing week-off days to their clients (if any).';
END
GO

-- Step 3: the employee table no longer stores days.
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Carbynetech_EmployeeWeekOff') AND name = 'WeekOffDays')
BEGIN
    ALTER TABLE Carbynetech_EmployeeWeekOff DROP COLUMN WeekOffDays;
    PRINT 'Dropped Carbynetech_EmployeeWeekOff.WeekOffDays.';
END
ELSE
    PRINT 'Carbynetech_EmployeeWeekOff.WeekOffDays already dropped - skipped.';
GO

-- Check
SELECT c.ClientWeekOffId, a.Name AS Client, c.WeekOffDays, c.EffectiveFrom, c.EffectiveTo,
       (SELECT COUNT(*) FROM Carbynetech_EmployeeWeekOff e WHERE e.AccountId = c.AccountId) AS EmployeesAssigned
FROM Carbynetech_ClientWeekOff c
JOIN Carbynetech_Account a ON a.AccountId = c.AccountId
ORDER BY a.Name, c.EffectiveFrom;
GO

-- =====================================================================
-- ROLLBACK (run only to undo this script, with the previous backend
-- deployed). Puts WeekOffDays back on employee rows (filled from their
-- client) and removes the client table.
-- =====================================================================
-- ALTER TABLE Carbynetech_EmployeeWeekOff ADD WeekOffDays NVARCHAR(30) NULL;
-- GO
-- UPDATE e SET WeekOffDays = ISNULL((SELECT TOP 1 c.WeekOffDays FROM Carbynetech_ClientWeekOff c
--                                    WHERE c.AccountId = e.AccountId ORDER BY c.EffectiveFrom), 'Sat,Sun')
-- FROM Carbynetech_EmployeeWeekOff e;
-- ALTER TABLE Carbynetech_EmployeeWeekOff ALTER COLUMN WeekOffDays NVARCHAR(30) NOT NULL;
-- DROP TABLE Carbynetech_ClientWeekOff;
