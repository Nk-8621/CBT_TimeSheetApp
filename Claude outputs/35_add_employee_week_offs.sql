-- =====================================================================
-- 35_add_employee_week_offs.sql
--
-- WEEK OFFS (Nov 2026): employee-specific week offs for people working
-- for a client whose week offs differ from the company default (Sat + Sun).
-- Admins manage them from Master Data -> Week Offs.
--
-- What this script does:
--   1. Creates Carbynetech_EmployeeWeekOff - one row per employee per
--      period: client, week-off days (e.g. 'Fri,Sat'), From and To dates
--      (To NULL = no end date).
--   2. Gives the ADMIN role to ONE employee (the HR person who will
--      maintain week offs) - set @AdminEmployeeCode below first.
--      Leave it as 'SET_ME' to skip this step.
--
-- Employees with no row keep the company default (Sat + Sun) - nothing
-- changes for anyone until an Admin adds a record.
--
-- Select the timesheet database before running (no USE line on purpose).
-- Deploy together with the backend and frontend changes for Week Offs.
-- Idempotent - every step checks its own pre-condition. Safe to re-run.
-- A ROLLBACK block is at the bottom (commented out).
-- =====================================================================

SET NOCOUNT ON;

-- Step 1: the table.
IF OBJECT_ID('Carbynetech_EmployeeWeekOff') IS NULL
BEGIN
    CREATE TABLE Carbynetech_EmployeeWeekOff
    (
        EmployeeWeekOffId   INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_Carbynetech_EmployeeWeekOff PRIMARY KEY,
        EmployeeId          INT           NOT NULL
            CONSTRAINT FK_Carbynetech_EmployeeWeekOff_Employee REFERENCES Carbynetech_Employee (EmployeeId),
        AccountId           INT           NOT NULL
            CONSTRAINT FK_Carbynetech_EmployeeWeekOff_Account REFERENCES Carbynetech_Account (AccountId),
        WeekOffDays         NVARCHAR(30)  NOT NULL,
        EffectiveFrom       DATE          NOT NULL,
        EffectiveTo         DATE          NULL,
        CreatedByEmployeeId INT           NULL
            CONSTRAINT FK_Carbynetech_EmployeeWeekOff_CreatedBy REFERENCES Carbynetech_Employee (EmployeeId),
        CreatedAt           DATETIME2     NOT NULL
            CONSTRAINT DF_Carbynetech_EmployeeWeekOff_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt           DATETIME2     NULL,
        CONSTRAINT CK_Carbynetech_EmployeeWeekOff_Dates CHECK (EffectiveTo IS NULL OR EffectiveTo >= EffectiveFrom)
    );
    CREATE INDEX IX_Carbynetech_EmployeeWeekOff_EmployeeId ON Carbynetech_EmployeeWeekOff (EmployeeId);
    PRINT 'Created Carbynetech_EmployeeWeekOff.';
END
ELSE
    PRINT 'Carbynetech_EmployeeWeekOff already exists - skipped.';
GO

-- Step 2: give the ADMIN role to one employee. Put their employee code here.
DECLARE @AdminEmployeeCode NVARCHAR(15) = 'SET_ME';

IF @AdminEmployeeCode = 'SET_ME'
    PRINT 'Step 2 skipped - set @AdminEmployeeCode to the employee code and re-run.';
ELSE
BEGIN
    DECLARE @EmployeeId INT = (SELECT EmployeeId FROM Carbynetech_Employee WHERE EmployeeCode = @AdminEmployeeCode);
    DECLARE @RoleId INT = (SELECT RoleId FROM Carbynetech_Role WHERE Code = 'ADMIN');

    IF @EmployeeId IS NULL
        PRINT CONCAT('ERROR: no employee with code ', @AdminEmployeeCode, ' - nothing assigned.');
    ELSE IF @RoleId IS NULL
        PRINT 'ERROR: the ADMIN role was not found - nothing assigned.';
    ELSE IF EXISTS (SELECT 1 FROM Carbynetech_EmployeeRole WHERE EmployeeId = @EmployeeId AND RoleId = @RoleId)
        PRINT CONCAT(@AdminEmployeeCode, ' already has the ADMIN role - skipped.');
    ELSE
    BEGIN
        INSERT INTO Carbynetech_EmployeeRole (EmployeeId, RoleId, AssignedAt)
        VALUES (@EmployeeId, @RoleId, SYSUTCDATETIME());
        PRINT CONCAT('Gave the ADMIN role to ', @AdminEmployeeCode, '.');
    END
END
GO

-- Check
SELECT e.EmployeeCode, e.FullName, r.Code AS RoleCode
FROM Carbynetech_EmployeeRole er
JOIN Carbynetech_Role r ON r.RoleId = er.RoleId
JOIN Carbynetech_Employee e ON e.EmployeeId = er.EmployeeId
WHERE r.Code = 'ADMIN';
GO

-- =====================================================================
-- ROLLBACK (run only to undo this script, with the previous backend
-- deployed). Removes the table, including every week-off record in it.
-- An ADMIN role given in step 2 is NOT removed - take it away by hand
-- if needed.
-- =====================================================================
-- IF OBJECT_ID('Carbynetech_EmployeeWeekOff') IS NOT NULL
--     DROP TABLE Carbynetech_EmployeeWeekOff;
