-- =====================================================================
-- 30_add_user_story.sql
--
-- New feature: User Stories. An employee creates a user story (a name +
-- one of their allocated projects) and adds tasks under it, each task with
-- its hours per day. Every task is an ordinary timesheet line in
-- Carbynetech_TimeEntry that points back to its story.
--
-- What this script does:
--   1. Creates Carbynetech_UserStory (see UserStory.cs /
--      UserStoryConfiguration in TimesheetConfigurations.cs).
--   2. Adds a NULLABLE column Carbynetech_TimeEntry.UserStoryId with a
--      foreign key and an index. Existing lines keep NULL and are not
--      touched.
--
-- Not done here (done by the app on first use): each project gets a
-- "General" module with a "General" task the first time a story task is
-- added for that project, because every timesheet line still needs a
-- Module and Task. An existing module/task with that name is reused.
--
-- Idempotent - every step checks its own pre-condition. Safe to re-run.
-- Deploy together with the backend and frontend changes for User Stories.
-- A ROLLBACK block is at the bottom (commented out).
-- =====================================================================

USE [Carbynetech_TimeSheet_Application];
GO
SET NOCOUNT ON;

-- Step 1: the user story table.
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Carbynetech_UserStory')
BEGIN
    CREATE TABLE Carbynetech_UserStory (
        UserStoryId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Carbynetech_UserStory PRIMARY KEY,
        EmployeeId  INT            NOT NULL,
        ProjectId   INT            NOT NULL,
        Name        NVARCHAR(200)  NOT NULL,
        IsActive    BIT            NOT NULL CONSTRAINT DF_Carbynetech_UserStory_IsActive DEFAULT (1),
        CreatedAt   DATETIME2      NOT NULL,
        UpdatedAt   DATETIME2      NULL,
        CONSTRAINT FK_Carbynetech_UserStory_Employee FOREIGN KEY (EmployeeId) REFERENCES Carbynetech_Employee (EmployeeId),
        CONSTRAINT FK_Carbynetech_UserStory_Project  FOREIGN KEY (ProjectId)  REFERENCES Carbynetech_Project (ProjectId)
    );
    CREATE INDEX IX_Carbynetech_UserStory_EmployeeId ON Carbynetech_UserStory (EmployeeId);
    PRINT 'Created Carbynetech_UserStory.';
END
ELSE
    PRINT 'Carbynetech_UserStory already exists - skipping.';
GO

-- Step 2: link timesheet lines to a user story (nullable).
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Carbynetech_TimeEntry') AND name = 'UserStoryId')
BEGIN
    ALTER TABLE Carbynetech_TimeEntry ADD UserStoryId INT NULL;
    PRINT 'Added Carbynetech_TimeEntry.UserStoryId (INT NULL).';
END
ELSE
    PRINT 'Carbynetech_TimeEntry.UserStoryId already exists - skipping.';
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Carbynetech_TimeEntry_UserStory')
BEGIN
    ALTER TABLE Carbynetech_TimeEntry
        ADD CONSTRAINT FK_Carbynetech_TimeEntry_UserStory FOREIGN KEY (UserStoryId) REFERENCES Carbynetech_UserStory (UserStoryId);
    PRINT 'Added FK_Carbynetech_TimeEntry_UserStory.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Carbynetech_TimeEntry_UserStoryId' AND object_id = OBJECT_ID('Carbynetech_TimeEntry'))
BEGIN
    CREATE INDEX IX_Carbynetech_TimeEntry_UserStoryId ON Carbynetech_TimeEntry (UserStoryId);
    PRINT 'Added IX_Carbynetech_TimeEntry_UserStoryId.';
END
GO

-- Check
SELECT
    (SELECT COUNT(*) FROM sys.tables  WHERE name = 'Carbynetech_UserStory') AS UserStoryTableExists,
    (SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('Carbynetech_TimeEntry') AND name = 'UserStoryId') AS TimeEntryColumnExists;
GO

-- =====================================================================
-- ROLLBACK (run only to undo this script, with the old backend deployed).
-- Story tasks stay as normal timesheet lines under "General › General";
-- only the link to the story is removed. The "General" modules/tasks the
-- app created are left in place (they may have hours logged against them).
-- =====================================================================
-- IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Carbynetech_TimeEntry_UserStoryId')
--     DROP INDEX IX_Carbynetech_TimeEntry_UserStoryId ON Carbynetech_TimeEntry;
-- IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Carbynetech_TimeEntry_UserStory')
--     ALTER TABLE Carbynetech_TimeEntry DROP CONSTRAINT FK_Carbynetech_TimeEntry_UserStory;
-- IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Carbynetech_TimeEntry') AND name = 'UserStoryId')
--     ALTER TABLE Carbynetech_TimeEntry DROP COLUMN UserStoryId;
-- IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Carbynetech_UserStory')
--     DROP TABLE Carbynetech_UserStory;
-- GO
