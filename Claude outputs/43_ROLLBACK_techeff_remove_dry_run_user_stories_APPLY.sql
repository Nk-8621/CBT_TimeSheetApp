-- =====================================================================
-- 43_ROLLBACK_techeff_remove_dry_run_user_stories.sql
--
-- Undoes 43_techeff_create_dry_run_user_stories.sql: removes the user
-- stories and project story names it created (tracked in
-- Carbynetech_DryRun_Created). Nothing created by people is touched.
--
-- Time logged during the dry run points at these stories, so a story
-- with time on it can't simply be deleted. @RemoveDryRunTimesheets:
--   0 (default) - stories that have time logged are KEPT and listed;
--                 everything else is removed.
--   1           - also deletes the task lines logged on these stories,
--                 and then any week record (with its approval trail)
--                 that has no task lines left. Use this to wipe the
--                 dry run completely.
--
-- SAFE BY DEFAULT: @Apply = 0 only previews. Set @Apply = 1 to remove.
-- Select the Carbynetech_Timesheet_App database before running.
-- =====================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Apply BIT = 1;                    -- 0 = preview only, 1 = remove
DECLARE @RemoveDryRunTimesheets BIT = 1;   -- see header

IF OBJECT_ID('Carbynetech_DryRun_Created') IS NULL
BEGIN
    RAISERROR('Carbynetech_DryRun_Created does not exist - script 43 was never applied. Nothing to roll back.', 16, 1);
    RETURN;
END

DECLARE @Stories TABLE (UserStoryId INT PRIMARY KEY);
INSERT INTO @Stories
SELECT c.RowId FROM Carbynetech_DryRun_Created c
JOIN Carbynetech_UserStory u ON u.UserStoryId = c.RowId
WHERE c.TableName = 'Carbynetech_UserStory';

DECLARE @Names TABLE (ProjectStoryNameId INT PRIMARY KEY);
INSERT INTO @Names
SELECT c.RowId FROM Carbynetech_DryRun_Created c
JOIN Carbynetech_ProjectStoryName n ON n.ProjectStoryNameId = c.RowId
WHERE c.TableName = 'Carbynetech_ProjectStoryName';

-- A. Stories and the time logged on them
SELECT 'A. Dry-run user stories' AS Section, e.EmployeeCode, e.FullName, u.Name AS Story,
       COUNT(t.TimeEntryId) AS TaskLines,
       ISNULL(SUM(t.MondayHours + t.TuesdayHours + t.WednesdayHours + t.ThursdayHours + t.FridayHours + t.SaturdayHours + t.SundayHours), 0) AS Hours,
       CASE WHEN COUNT(t.TimeEntryId) = 0 THEN 'REMOVE'
            WHEN @RemoveDryRunTimesheets = 1 THEN 'REMOVE (with its task lines)'
            ELSE 'KEEP - has time logged (set @RemoveDryRunTimesheets = 1 to remove)' END AS Action
FROM @Stories s
JOIN Carbynetech_UserStory u ON u.UserStoryId = s.UserStoryId
JOIN Carbynetech_Employee e  ON e.EmployeeId = u.EmployeeId
LEFT JOIN Carbynetech_TimeEntry t ON t.UserStoryId = u.UserStoryId
GROUP BY e.EmployeeCode, e.FullName, u.Name
ORDER BY e.FullName, u.Name;

-- B. Weeks touched by the dry run (status at the moment)
SELECT 'B. Weeks with dry-run time' AS Section, e.EmployeeCode, e.FullName, w.WeekStartDate, w.Status,
       (SELECT COUNT(*) FROM Carbynetech_TimeEntry x WHERE x.EmployeeId = w.EmployeeId AND x.WeekStartDate = w.WeekStartDate
          AND (x.UserStoryId IS NULL OR x.UserStoryId NOT IN (SELECT UserStoryId FROM @Stories))) AS OtherTaskLinesInWeek
FROM Carbynetech_WeekRecord w
JOIN Carbynetech_Employee e ON e.EmployeeId = w.EmployeeId
WHERE EXISTS (SELECT 1 FROM Carbynetech_TimeEntry t
              WHERE t.EmployeeId = w.EmployeeId AND t.WeekStartDate = w.WeekStartDate
                AND t.UserStoryId IN (SELECT UserStoryId FROM @Stories))
ORDER BY e.FullName, w.WeekStartDate;

IF @Apply = 0
BEGIN
    SELECT 'PREVIEW ONLY' AS Result,
           (SELECT COUNT(*) FROM @Stories) AS DryRunUserStories,
           (SELECT COUNT(*) FROM @Names)   AS DryRunStoryNames,
           'Nothing was changed. Review sections A-B, then set @Apply = 1 and run again.' AS NextStep;
    RETURN;
END

BEGIN TRAN;

    DECLARE @AffectedWeeks TABLE (EmployeeId INT, WeekStartDate DATE, PRIMARY KEY (EmployeeId, WeekStartDate));
    DECLARE @LinesDeleted INT = 0, @WeeksDeleted INT = 0;

    IF @RemoveDryRunTimesheets = 1
    BEGIN
        INSERT INTO @AffectedWeeks
        SELECT DISTINCT EmployeeId, WeekStartDate FROM Carbynetech_TimeEntry
        WHERE UserStoryId IN (SELECT UserStoryId FROM @Stories);

        DELETE FROM Carbynetech_TimeEntry WHERE UserStoryId IN (SELECT UserStoryId FROM @Stories);
        SET @LinesDeleted = @@ROWCOUNT;

        -- Week records left with no task lines at all: remove their trail and the week.
        DECLARE @EmptyWeeks TABLE (WeekRecordId INT PRIMARY KEY);
        INSERT INTO @EmptyWeeks
        SELECT w.WeekRecordId FROM Carbynetech_WeekRecord w
        JOIN @AffectedWeeks a ON a.EmployeeId = w.EmployeeId AND a.WeekStartDate = w.WeekStartDate
        WHERE NOT EXISTS (SELECT 1 FROM Carbynetech_TimeEntry t WHERE t.EmployeeId = w.EmployeeId AND t.WeekStartDate = w.WeekStartDate);

        DELETE FROM Carbynetech_ApprovalEvent WHERE WeekRecordId IN (SELECT WeekRecordId FROM @EmptyWeeks);
        DELETE FROM Carbynetech_WeekRecord    WHERE WeekRecordId IN (SELECT WeekRecordId FROM @EmptyWeeks);
        SET @WeeksDeleted = @@ROWCOUNT;
    END

    -- Stories with no time left on them
    DECLARE @StoriesDeleted TABLE (UserStoryId INT);
    DELETE u
    OUTPUT deleted.UserStoryId INTO @StoriesDeleted
    FROM Carbynetech_UserStory u
    WHERE u.UserStoryId IN (SELECT UserStoryId FROM @Stories)
      AND NOT EXISTS (SELECT 1 FROM Carbynetech_TimeEntry t WHERE t.UserStoryId = u.UserStoryId);

    -- Story names no user story uses any more (including stories people made from them)
    DECLARE @NamesDeleted TABLE (ProjectStoryNameId INT);
    DELETE n
    OUTPUT deleted.ProjectStoryNameId INTO @NamesDeleted
    FROM Carbynetech_ProjectStoryName n
    WHERE n.ProjectStoryNameId IN (SELECT ProjectStoryNameId FROM @Names)
      AND NOT EXISTS (SELECT 1 FROM Carbynetech_UserStory u WHERE u.ProjectStoryNameId = n.ProjectStoryNameId);

    DELETE FROM Carbynetech_DryRun_Created
    WHERE (TableName = 'Carbynetech_UserStory'        AND RowId IN (SELECT UserStoryId FROM @StoriesDeleted))
       OR (TableName = 'Carbynetech_ProjectStoryName' AND RowId IN (SELECT ProjectStoryNameId FROM @NamesDeleted));

COMMIT;

SELECT 'ROLLED BACK' AS Result,
       (SELECT COUNT(*) FROM @StoriesDeleted) AS UserStoriesRemoved,
       (SELECT COUNT(*) FROM @NamesDeleted)   AS StoryNamesRemoved,
       @LinesDeleted                          AS TaskLinesRemoved,
       @WeeksDeleted                          AS WeekRecordsRemoved,
       (SELECT COUNT(*) FROM Carbynetech_DryRun_Created) AS StillTracked;

IF NOT EXISTS (SELECT 1 FROM Carbynetech_DryRun_Created)
    DROP TABLE Carbynetech_DryRun_Created;
