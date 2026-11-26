-- =====================================================================
-- 40_add_project_dates_and_project_stories.sql
--
-- PROJECT DATES + PROJECT STORIES (Nov 2026)
--
-- What this script does:
--   1. Adds StartDate and EndDate to Carbynetech_Project (both NULL for
--      existing projects - fill them in from Master Data -> Project List).
--      New projects require a Start Date (checked by the app).
--   2. Creates Carbynetech_ProjectStoryName - user story names a Level 1
--      manager (or an Admin) adds for a project, offered to everyone
--      allocated to that project.
--   3. Adds ProjectStoryNameId (NULL) to Carbynetech_UserStory - set when
--      an employee picks a manager-added name.
--
-- Run together with the backend and frontend changes for this feature.
-- Select the timesheet database before running (no USE line on purpose).
-- Idempotent - every step checks its own pre-condition. Safe to re-run.
-- A ROLLBACK block is at the bottom (commented out).
-- =====================================================================

SET NOCOUNT ON;

-- Step 1: project dates.
IF COL_LENGTH('Carbynetech_Project', 'StartDate') IS NULL
BEGIN
    ALTER TABLE Carbynetech_Project ADD StartDate DATE NULL;
    PRINT 'Added Carbynetech_Project.StartDate.';
END
ELSE
    PRINT 'Carbynetech_Project.StartDate already exists - skipped.';

IF COL_LENGTH('Carbynetech_Project', 'EndDate') IS NULL
BEGIN
    ALTER TABLE Carbynetech_Project ADD EndDate DATE NULL;
    PRINT 'Added Carbynetech_Project.EndDate.';
END
ELSE
    PRINT 'Carbynetech_Project.EndDate already exists - skipped.';
GO

-- Step 2: project story names.
IF OBJECT_ID('Carbynetech_ProjectStoryName') IS NULL
BEGIN
    CREATE TABLE Carbynetech_ProjectStoryName
    (
        ProjectStoryNameId   INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_Carbynetech_ProjectStoryName PRIMARY KEY,
        ProjectId            INT            NOT NULL
            CONSTRAINT FK_Carbynetech_ProjectStoryName_Project REFERENCES Carbynetech_Project (ProjectId),
        Name                 NVARCHAR(200)  NOT NULL,
        IsActive             BIT            NOT NULL CONSTRAINT DF_Carbynetech_ProjectStoryName_IsActive DEFAULT (1),
        CreatedByEmployeeId  INT            NULL
            CONSTRAINT FK_Carbynetech_ProjectStoryName_CreatedBy REFERENCES Carbynetech_Employee (EmployeeId),
        CreatedAt            DATETIME2      NOT NULL CONSTRAINT DF_Carbynetech_ProjectStoryName_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAt            DATETIME2      NULL
    );
    CREATE INDEX IX_Carbynetech_ProjectStoryName_ProjectId ON Carbynetech_ProjectStoryName (ProjectId);
    PRINT 'Created Carbynetech_ProjectStoryName.';
END
ELSE
    PRINT 'Carbynetech_ProjectStoryName already exists - skipped.';
GO

-- Step 3: link from an employee's user story to the name it came from.
IF COL_LENGTH('Carbynetech_UserStory', 'ProjectStoryNameId') IS NULL
BEGIN
    ALTER TABLE Carbynetech_UserStory ADD ProjectStoryNameId INT NULL
        CONSTRAINT FK_Carbynetech_UserStory_ProjectStoryName REFERENCES Carbynetech_ProjectStoryName (ProjectStoryNameId);
    PRINT 'Added Carbynetech_UserStory.ProjectStoryNameId.';
END
ELSE
    PRINT 'Carbynetech_UserStory.ProjectStoryNameId already exists - skipped.';
GO

-- Check: active projects still without a Start Date (fill them in from Master Data).
SELECT p.Code, p.Name, a.Name AS Client, p.StartDate, p.EndDate
FROM Carbynetech_Project p
JOIN Carbynetech_Account a ON a.AccountId = p.AccountId
WHERE p.IsActive = 1 AND p.StartDate IS NULL
ORDER BY a.Name, p.Name;
GO

-- =====================================================================
-- ROLLBACK (run only to undo this script, with the previous backend
-- deployed). Removes every project story name and the project dates.
-- =====================================================================
-- ALTER TABLE Carbynetech_UserStory DROP CONSTRAINT FK_Carbynetech_UserStory_ProjectStoryName;
-- ALTER TABLE Carbynetech_UserStory DROP COLUMN ProjectStoryNameId;
-- DROP TABLE Carbynetech_ProjectStoryName;
-- ALTER TABLE Carbynetech_Project DROP COLUMN EndDate;
-- ALTER TABLE Carbynetech_Project DROP COLUMN StartDate;
