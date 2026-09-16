-- =====================================================================
-- 13_remove_dev_qa_prod_module_templates.sql
--
-- Removes the "DEV", "QA", "PROD" Project Type Module Templates (test
-- data, per Babu) and their child Task Templates (e.g. "Data") -
-- Carbynetech_ProjectTypeModuleTemplate / Carbynetech_ProjectTypeTaskTemplate.
--
-- These are the reusable TEMPLATES under Master Data > Project Types >
-- Manage Templates - not live Modules/Tasks sitting on an actual
-- Project. Deleting a template has no effect on projects that were
-- already generated from it: Carbynetech_Module only remembers which
-- ProjectType it came from (Module.ProjectTypeId), never which specific
-- template row, so nothing on an existing project's timesheet grid
-- changes. This only affects what gets auto-created for projects going
-- forward.
--
-- Task Templates cascade-delete automatically with their parent Module
-- Template (Carbynetech_ProjectTypeTaskTemplate has ON DELETE CASCADE
-- to Carbynetech_ProjectTypeModuleTemplate) - so removing "DEV" removes
-- every task under it, not just one named "Data". That matches what was
-- asked (remove the module and its task together); flagging in case any
-- of DEV/QA/PROD has an extra task beyond "Data" you didn't mean to lose.
--
-- Also checks (read-only - does NOT delete) whether any LIVE Modules or
-- Tasks with these names exist on a real Project, just so nothing is
-- hiding there unexpectedly.
-- =====================================================================

USE [Carbynetech_TimeSheet_Application];
GO
SET NOCOUNT ON;

PRINT '=== Module Templates matching DEV / QA / PROD (about to be deleted) ===';
SELECT mt.ProjectTypeModuleTemplateId, mt.Name AS ModuleTemplateName, pt.Code AS ProjectTypeCode, pt.Name AS ProjectTypeName
FROM Carbynetech_ProjectTypeModuleTemplate mt
JOIN Carbynetech_ProjectType pt ON pt.ProjectTypeId = mt.ProjectTypeId
WHERE mt.Name IN ('DEV', 'QA', 'PROD');

PRINT '';
PRINT '=== Their Task Templates (will cascade-delete with the module template above) ===';
SELECT tt.ProjectTypeTaskTemplateId, tt.Name AS TaskTemplateName, mt.Name AS ModuleTemplateName
FROM Carbynetech_ProjectTypeTaskTemplate tt
JOIN Carbynetech_ProjectTypeModuleTemplate mt ON mt.ProjectTypeModuleTemplateId = tt.ProjectTypeModuleTemplateId
WHERE mt.Name IN ('DEV', 'QA', 'PROD');

DELETE FROM Carbynetech_ProjectTypeModuleTemplate WHERE Name IN ('DEV', 'QA', 'PROD');
PRINT '';
PRINT CONCAT(@@ROWCOUNT, ' Module Template row(s) deleted (their Task Templates went with them via cascade).');

PRINT '';
PRINT '=== Informational only - LIVE Modules/Tasks on real Projects with these names (NOT touched by this script) ===';
SELECT m.ModuleId, m.Name AS ModuleName, p.Code AS ProjectCode, p.Name AS ProjectName
FROM Carbynetech_Module m
JOIN Carbynetech_Project p ON p.ProjectId = m.ProjectId
WHERE m.Name IN ('DEV', 'QA', 'PROD');

SELECT t.TaskId, t.Name AS TaskName, m.Name AS ModuleName, p.Code AS ProjectCode
FROM Carbynetech_Task t
JOIN Carbynetech_Module m ON m.ModuleId = t.ModuleId
JOIN Carbynetech_Project p ON p.ProjectId = m.ProjectId
WHERE t.Name = 'Data';

PRINT '';
PRINT 'If either of the two result sets above is non-empty, that''s live project data with the same names - let me know and I''ll write a separate script for those (they''re governed by a stricter delete rule since real timesheet hours may point at them).';
GO
