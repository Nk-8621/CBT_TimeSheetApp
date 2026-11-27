-- =====================================================================
-- 44_techeff_cleanup_dry_run_leftovers.sql
--
-- Removes the two things the TechEff dry run (27 Nov 2026) created by hand
-- in the app that scripts 42/43 do not track:
--   1. TEST001's free-text user story "Dry run - date check"
--      (TechEff for India Business, 23-29 Nov) - only if no time is on it.
--   2. The closed project story name
--      "India Business - Dry run temp name (renamed)" - only if no user
--      story uses it.
-- Run AFTER 43_ROLLBACK. Nothing else is touched.
--
-- SAFE BY DEFAULT: @Apply = 0 only previews. Set @Apply = 1 to remove.
-- Select the Carbynetech_Timesheet_App database before running.
-- =====================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Apply BIT = 1;   -- 0 = preview only, 1 = remove

DECLARE @Story TABLE (UserStoryId INT PRIMARY KEY);
INSERT INTO @Story
SELECT u.UserStoryId
FROM Carbynetech_UserStory u
JOIN Carbynetech_Employee e ON e.EmployeeId = u.EmployeeId
WHERE e.EmployeeCode = N'TEST001' AND u.Name = N'Dry run - date check';

DECLARE @Name TABLE (ProjectStoryNameId INT PRIMARY KEY);
INSERT INTO @Name
SELECT n.ProjectStoryNameId
FROM Carbynetech_ProjectStoryName n
WHERE n.Name = N'India Business - Dry run temp name (renamed)';

SELECT 'A. User story' AS Section, u.UserStoryId, u.Name,
       (SELECT COUNT(*) FROM Carbynetech_TimeEntry t WHERE t.UserStoryId = u.UserStoryId) AS TaskLines,
       CASE WHEN EXISTS (SELECT 1 FROM Carbynetech_TimeEntry t WHERE t.UserStoryId = u.UserStoryId)
            THEN 'KEEP - has time logged' ELSE 'REMOVE' END AS Action
FROM Carbynetech_UserStory u WHERE u.UserStoryId IN (SELECT UserStoryId FROM @Story);

SELECT 'B. Story name' AS Section, n.ProjectStoryNameId, n.Name,
       (SELECT COUNT(*) FROM Carbynetech_UserStory u WHERE u.ProjectStoryNameId = n.ProjectStoryNameId) AS UsedBy,
       CASE WHEN EXISTS (SELECT 1 FROM Carbynetech_UserStory u WHERE u.ProjectStoryNameId = n.ProjectStoryNameId)
            THEN 'KEEP - in use' ELSE 'REMOVE' END AS Action
FROM Carbynetech_ProjectStoryName n WHERE n.ProjectStoryNameId IN (SELECT ProjectStoryNameId FROM @Name);

IF @Apply = 0
BEGIN
    SELECT 'PREVIEW ONLY' AS Result, 'Nothing was changed. Set @Apply = 1 and run again.' AS NextStep;
    RETURN;
END

BEGIN TRAN;
    DELETE u FROM Carbynetech_UserStory u
    WHERE u.UserStoryId IN (SELECT UserStoryId FROM @Story)
      AND NOT EXISTS (SELECT 1 FROM Carbynetech_TimeEntry t WHERE t.UserStoryId = u.UserStoryId);
    DECLARE @S INT = @@ROWCOUNT;

    DELETE n FROM Carbynetech_ProjectStoryName n
    WHERE n.ProjectStoryNameId IN (SELECT ProjectStoryNameId FROM @Name)
      AND NOT EXISTS (SELECT 1 FROM Carbynetech_UserStory u WHERE u.ProjectStoryNameId = n.ProjectStoryNameId);
    DECLARE @N INT = @@ROWCOUNT;
COMMIT;

SELECT 'REMOVED' AS Result, @S AS UserStoriesRemoved, @N AS StoryNamesRemoved;
