-- =====================================================================
-- 43_techeff_create_dry_run_user_stories.sql
--
-- TECHEFF DRY RUN (Nov 2026): creates 10 user stories and gives them to
-- every active employee allocated to the two TechEff projects.
--
-- How it works (same shape the app itself uses):
--   1. The 10 story names below are added to their project as manager-
--      defined "Project Stories" (Carbynetech_ProjectStoryName), created
--      by CBT1267 - 5 for each project. They then also appear on the
--      Project Stories tab.
--   2. Every active TechEff employee allocated to that project gets one
--      user story for each of its 5 names (Carbynetech_UserStory, linked
--      to the name), so each story is used by all of that project's
--      employees. Someone on both projects gets all 10.
--   3. Story dates = @StartDate..@EndDate, trimmed to the project's own
--      start/end date (the app refuses stories outside project dates).
--
-- Skipped automatically: a name that already exists on the project, an
-- employee who already has a story with that name on that project,
-- senior leadership (no manager / VP / Chief - they don't log time), and
-- projects whose dates don't overlap the window.
--
-- Edit the story names and dates in the SETTINGS block if you like.
-- SAFE BY DEFAULT: @Apply = 0 only previews. Set @Apply = 1 to create.
-- ROLLBACK: every row created is recorded in Carbynetech_DryRun_Created;
-- run 43_ROLLBACK_techeff_remove_dry_run_user_stories.sql to remove them.
-- Select the Carbynetech_Timesheet_App database before running.
-- =====================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

-- ============================ SETTINGS ===============================
DECLARE @Apply BIT = 1;                       -- 0 = preview only, 1 = create
DECLARE @CreatedByCode NVARCHAR(20) = N'CBT1267';

-- Story window: by default from last Monday for 6 weeks, so both
-- dry-run weeks (last week and this week) are covered.
-- (1900-01-01 was a Monday, so this ignores SET DATEFIRST.)
DECLARE @Today     DATE = CAST(GETDATE() AS DATE);
DECLARE @StartDate DATE = DATEADD(DAY, -(DATEDIFF(DAY, '19000101', @Today) % 7) - 7, @Today);
DECLARE @EndDate   DATE = DATEADD(DAY, 41, @StartDate);   -- 6 full weeks, ends on a Sunday

DECLARE @Stories TABLE (ProjectName NVARCHAR(200), StoryName NVARCHAR(200), SortOrder INT);
INSERT INTO @Stories (ProjectName, StoryName, SortOrder) VALUES
    (N'Techeff for Europe - Chevigny', N'Chevigny - Requirement Study & Gap Analysis',     1),
    (N'Techeff for Europe - Chevigny', N'Chevigny - Data Model & Source Integration',      2),
    (N'Techeff for Europe - Chevigny', N'Chevigny - Report & Dashboard Development',       3),
    (N'Techeff for Europe - Chevigny', N'Chevigny - Testing & UAT Support',                4),
    (N'Techeff for Europe - Chevigny', N'Chevigny - Deployment & Hypercare',               5),
    (N'TechEff for India Business',    N'India Business - Requirement Study & Gap Analysis', 1),
    (N'TechEff for India Business',    N'India Business - Data Model & Source Integration',  2),
    (N'TechEff for India Business',    N'India Business - Report & Dashboard Development',   3),
    (N'TechEff for India Business',    N'India Business - Testing & UAT Support',            4),
    (N'TechEff for India Business',    N'India Business - Deployment & Hypercare',           5);
-- =====================================================================

DECLARE @CreatedById INT = (SELECT EmployeeId FROM Carbynetech_Employee WHERE EmployeeCode = @CreatedByCode);
IF @CreatedById IS NULL
BEGIN
    RAISERROR('Employee %s not found - nothing changed.', 16, 1, @CreatedByCode);
    RETURN;
END
IF EXISTS (SELECT 1 FROM @Stories WHERE LEN(StoryName) > 200)
BEGIN
    RAISERROR('A story name is longer than 200 characters - nothing changed.', 16, 1);
    RETURN;
END

-- A. Projects and the date window each one gets
DECLARE @Proj TABLE (ProjectName NVARCHAR(200) PRIMARY KEY, ProjectId INT, IsActive BIT,
                     StoryStart DATE, StoryEnd DATE, Problem NVARCHAR(200));
INSERT INTO @Proj (ProjectName, ProjectId, IsActive, StoryStart, StoryEnd)
SELECT s.ProjectName, p.ProjectId, p.IsActive,
       CASE WHEN p.StartDate IS NOT NULL AND p.StartDate > @StartDate THEN p.StartDate ELSE @StartDate END,
       CASE WHEN p.EndDate   IS NOT NULL AND p.EndDate   < @EndDate   THEN p.EndDate   ELSE @EndDate   END
FROM (SELECT DISTINCT ProjectName FROM @Stories) s
LEFT JOIN Carbynetech_Project p ON p.Name = s.ProjectName;

UPDATE @Proj SET Problem = CASE
    WHEN ProjectId IS NULL THEN '*** PROJECT NOT FOUND - check the name ***'
    WHEN IsActive = 0 THEN 'Project is INACTIVE - skipped'
    WHEN StoryEnd < StoryStart THEN 'Project dates do not overlap the story window - skipped'
    END;

SELECT 'A. Projects' AS Section, ProjectName, ProjectId, StoryStart, StoryEnd, ISNULL(Problem, 'OK') AS Result
FROM @Proj;

IF EXISTS (SELECT 1 FROM @Proj WHERE ProjectId IS NULL)
BEGIN
    RAISERROR('One or more projects were not found (see section A) - nothing changed.', 16, 1);
    RETURN;
END

-- B. Story names to add to each project
DECLARE @Names TABLE (ProjectId INT, ProjectName NVARCHAR(200), StoryName NVARCHAR(200), ExistingId INT, Action NVARCHAR(100));
INSERT INTO @Names (ProjectId, ProjectName, StoryName, ExistingId, Action)
SELECT p.ProjectId, p.ProjectName, s.StoryName, n.ProjectStoryNameId,
       CASE WHEN p.Problem IS NOT NULL THEN 'SKIP - project not usable'
            WHEN n.ProjectStoryNameId IS NOT NULL THEN 'EXISTS - reuse it'
            ELSE 'CREATE' END
FROM @Stories s
JOIN @Proj p ON p.ProjectName = s.ProjectName
LEFT JOIN Carbynetech_ProjectStoryName n ON n.ProjectId = p.ProjectId AND n.Name = s.StoryName;

SELECT 'B. Project story names' AS Section, ProjectName, StoryName, Action FROM @Names ORDER BY ProjectName, StoryName;

-- C. Employees and the user stories each will get
DECLARE @Plan TABLE (EmployeeId INT, EmployeeCode NVARCHAR(20), FullName NVARCHAR(200),
                     ProjectId INT, ProjectName NVARCHAR(200), StoryName NVARCHAR(200),
                     StartDate DATE, EndDate DATE, Action NVARCHAR(100));
INSERT INTO @Plan
SELECT e.EmployeeId, e.EmployeeCode, e.FullName, nm.ProjectId, nm.ProjectName, nm.StoryName,
       p.StoryStart, p.StoryEnd,
       CASE WHEN nm.Action LIKE 'SKIP%' THEN 'SKIP - project not usable'
            WHEN e.ManagerEmployeeId IS NULL OR e.Designation LIKE '%Vice President%' OR e.Designation LIKE '%Chief%'
                 THEN 'SKIP - senior leadership (does not log time)'
            WHEN EXISTS (SELECT 1 FROM Carbynetech_UserStory u
                         WHERE u.EmployeeId = e.EmployeeId AND u.ProjectId = nm.ProjectId AND u.Name = nm.StoryName)
                 THEN 'SKIP - already has this story'
            ELSE 'CREATE' END
FROM @Names nm
JOIN @Proj p ON p.ProjectId = nm.ProjectId
JOIN Carbynetech_EmployeeProjectAllocation a ON a.ProjectId = nm.ProjectId
JOIN Carbynetech_Employee e ON e.EmployeeId = a.EmployeeId AND e.IsActive = 1;

SELECT 'C. User stories per employee' AS Section, EmployeeCode, FullName, ProjectName,
       SUM(CASE WHEN Action = 'CREATE' THEN 1 ELSE 0 END) AS StoriesToCreate,
       MIN(StartDate) AS StoryStart, MAX(EndDate) AS StoryEnd,
       ISNULL(STRING_AGG(CASE WHEN Action <> 'CREATE' THEN CONCAT(StoryName, ': ', Action) END, ' | '), '') AS Skipped
FROM @Plan
GROUP BY EmployeeCode, FullName, ProjectName
ORDER BY FullName, ProjectName;

SELECT 'D. Totals' AS Section,
       (SELECT COUNT(*) FROM @Names WHERE Action = 'CREATE')             AS StoryNamesToCreate,
       (SELECT COUNT(*) FROM @Plan  WHERE Action = 'CREATE')             AS UserStoriesToCreate,
       (SELECT COUNT(DISTINCT EmployeeId) FROM @Plan WHERE Action = 'CREATE') AS EmployeesGettingStories;

IF @Apply = 0
BEGIN
    SELECT 'PREVIEW ONLY' AS Result,
           'Nothing was changed. Review sections A-D, then set @Apply = 1 and run again.' AS NextStep;
    RETURN;
END

-- ---------------------------------------------------------------------
-- Apply
-- ---------------------------------------------------------------------
IF OBJECT_ID('Carbynetech_DryRun_Created') IS NULL
    CREATE TABLE Carbynetech_DryRun_Created (
        DryRunCreatedId INT IDENTITY(1,1) PRIMARY KEY,
        TableName       NVARCHAR(100) NOT NULL,
        RowId           INT           NOT NULL,
        Script          NVARCHAR(100) NOT NULL,
        CreatedAt       DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME()
    );

BEGIN TRAN;

    DECLARE @NewNames TABLE (ProjectStoryNameId INT, ProjectId INT, Name NVARCHAR(200));

    INSERT INTO Carbynetech_ProjectStoryName (ProjectId, Name, IsActive, CreatedByEmployeeId, CreatedAt)
    OUTPUT inserted.ProjectStoryNameId, inserted.ProjectId, inserted.Name INTO @NewNames
    SELECT ProjectId, StoryName, 1, @CreatedById, SYSUTCDATETIME()
    FROM @Names WHERE Action = 'CREATE';

    INSERT INTO Carbynetech_DryRun_Created (TableName, RowId, Script)
    SELECT 'Carbynetech_ProjectStoryName', ProjectStoryNameId, N'43_techeff_create_dry_run_user_stories'
    FROM @NewNames;

    DECLARE @NewStories TABLE (UserStoryId INT);

    INSERT INTO Carbynetech_UserStory (EmployeeId, ProjectId, Name, StartDate, EndDate, IsActive, CreatedAt, ProjectStoryNameId)
    OUTPUT inserted.UserStoryId INTO @NewStories
    SELECT pl.EmployeeId, pl.ProjectId, pl.StoryName, pl.StartDate, pl.EndDate, 1, SYSUTCDATETIME(), n.ProjectStoryNameId
    FROM @Plan pl
    JOIN Carbynetech_ProjectStoryName n ON n.ProjectId = pl.ProjectId AND n.Name = pl.StoryName
    WHERE pl.Action = 'CREATE';

    INSERT INTO Carbynetech_DryRun_Created (TableName, RowId, Script)
    SELECT 'Carbynetech_UserStory', UserStoryId, N'43_techeff_create_dry_run_user_stories'
    FROM @NewStories;

COMMIT;

SELECT 'APPLIED' AS Result,
       (SELECT COUNT(*) FROM @NewNames)   AS StoryNamesCreated,
       (SELECT COUNT(*) FROM @NewStories) AS UserStoriesCreated,
       'Recorded in Carbynetech_DryRun_Created. Use 43_ROLLBACK to remove.' AS Note;
