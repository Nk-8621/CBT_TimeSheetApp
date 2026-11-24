-- =====================================================================
-- 38_add_client_default_week_off.sql
--
-- CLIENT WEEK OFF (Nov 2026): every client (Customer / Internal) gets a
-- default week off, set in Master Data -> Customers & Internal.
--
-- What this script does:
--   Adds Carbynetech_Account.WeekOffDays (e.g. 'Sat,Sun').
--   Every existing client is filled with 'Sat,Sun' (the company default),
--   so nothing changes for anyone until HR edits a client.
--
-- How week offs work after this:
--   1. Employee in a custom week off (Master Data -> Custom WeekOffs)
--      -> those days.
--   2. Otherwise -> their primary client's default week off.
--   3. No primary client -> Sat, Sun.
--
-- Run together with the backend and frontend changes for client week off.
-- Select the timesheet database before running (no USE line on purpose).
-- Idempotent - skips the change if the column already exists.
-- A ROLLBACK block is at the bottom (commented out).
-- =====================================================================

SET NOCOUNT ON;

IF COL_LENGTH('Carbynetech_Account', 'WeekOffDays') IS NULL
BEGIN
    ALTER TABLE Carbynetech_Account
        ADD WeekOffDays NVARCHAR(30) NOT NULL
            CONSTRAINT DF_Carbynetech_Account_WeekOffDays DEFAULT 'Sat,Sun';
    PRINT 'Added Carbynetech_Account.WeekOffDays (existing clients set to Sat,Sun).';
END
ELSE
    PRINT 'Carbynetech_Account.WeekOffDays already exists - skipped.';
GO

-- Check
SELECT AccountId, Name, AccountType, WeekOffDays
FROM Carbynetech_Account
ORDER BY Name;
GO

-- =====================================================================
-- ROLLBACK (run only to undo this script, with the previous backend
-- deployed). Removes the column and every client's default week off.
-- =====================================================================
-- ALTER TABLE Carbynetech_Account DROP CONSTRAINT DF_Carbynetech_Account_WeekOffDays;
-- ALTER TABLE Carbynetech_Account DROP COLUMN WeekOffDays;
