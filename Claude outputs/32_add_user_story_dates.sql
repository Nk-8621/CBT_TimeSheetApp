-- =====================================================================
-- 32_add_user_story_dates.sql
--
-- User stories get a Start date and an End date (both required). A story
-- shows only in weeks that overlap its dates, and hours can only be added
-- on days inside them (see UserStory.cs / TimesheetService.cs).
--
-- What this script does:
--   1. Adds StartDate and EndDate (DATE) to Carbynetech_UserStory, nullable
--      at first.
--   2. Fills them for stories created before this change: StartDate = the
--      Monday and EndDate = the Sunday of the week the story was created.
--      Users can edit the dates afterwards.
--   3. Makes both columns NOT NULL and adds a CHECK that EndDate is not
--      before StartDate.
--
-- Run AFTER 30_add_user_story.sql, and deploy together with the backend and
-- frontend changes for story dates.
-- Idempotent - every step checks its own pre-condition. Safe to re-run.
-- A ROLLBACK block is at the bottom (commented out).
-- =====================================================================

USE [Carbynetech_Timesheet_App];
GO
SET NOCOUNT ON;

-- Step 1: add the columns (nullable for now).
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Carbynetech_UserStory') AND name = 'StartDate')
BEGIN
    ALTER TABLE Carbynetech_UserStory ADD StartDate DATE NULL;
    PRINT 'Added Carbynetech_UserStory.StartDate.';
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Carbynetech_UserStory') AND name = 'EndDate')
BEGIN
    ALTER TABLE Carbynetech_UserStory ADD EndDate DATE NULL;
    PRINT 'Added Carbynetech_UserStory.EndDate.';
END
GO

-- Step 2: existing stories get the Monday-Sunday week they were created in.
-- 1900-01-01 was a Monday, so this works whatever DATEFIRST is set to.
UPDATE Carbynetech_UserStory
SET StartDate = DATEADD(DAY, -(DATEDIFF(DAY, '19000101', CAST(CreatedAt AS DATE)) % 7), CAST(CreatedAt AS DATE))
WHERE StartDate IS NULL;

UPDATE Carbynetech_UserStory
SET EndDate = DATEADD(DAY, 6, StartDate)
WHERE EndDate IS NULL;
PRINT 'Filled dates for existing stories (if any).';
GO

-- Step 3: required from now on.
ALTER TABLE Carbynetech_UserStory ALTER COLUMN StartDate DATE NOT NULL;
ALTER TABLE Carbynetech_UserStory ALTER COLUMN EndDate DATE NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Carbynetech_UserStory_Dates')
BEGIN
    ALTER TABLE Carbynetech_UserStory
        ADD CONSTRAINT CK_Carbynetech_UserStory_Dates CHECK (EndDate >= StartDate);
    PRINT 'Added CK_Carbynetech_UserStory_Dates.';
END
GO

-- Check
SELECT UserStoryId, EmployeeId, Name, StartDate, EndDate, CreatedAt
FROM Carbynetech_UserStory
ORDER BY UserStoryId;
GO

-- =====================================================================
-- ROLLBACK (run only to undo this script, with the previous backend
-- deployed). Removes the two date columns; the stories themselves stay.
-- =====================================================================
-- IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Carbynetech_UserStory_Dates')
--     ALTER TABLE Carbynetech_UserStory DROP CONSTRAINT CK_Carbynetech_UserStory_Dates;
-- IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Carbynetech_UserStory') AND name = 'EndDate')
--     ALTER TABLE Carbynetech_UserStory DROP COLUMN EndDate;
-- IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Carbynetech_UserStory') AND name = 'StartDate')
--     ALTER TABLE Carbynetech_UserStory DROP COLUMN StartDate;
-- GO
