-- =====================================================================
-- 07_add_project_type_and_employee_project_allocation.sql
--
-- Ports the "Project Type" feature onto THIS branch's schema, and adds
-- the brand-new "Project-wise resource allocation" feature alongside it:
--   1. Renames Carbynetech_TaskCategory -> Carbynetech_ProjectType (same
--      table, same rows, new name/PK column name only - Code/Name and all
--      existing data are untouched).
--   2. Creates Carbynetech_ProjectTypeModuleTemplate (Level-1) and
--      Carbynetech_ProjectTypeTaskTemplate (Level-2) - the template tree
--      that drives auto-generated Modules/Tasks for a Project Type.
--   3. Migrates Carbynetech_Module.TaskCategoryId -> ProjectTypeId: renamed,
--      made NULLable, and its FK changed to ON DELETE SET NULL (a Module's
--      ProjectTypeId is just lineage - "which template generated this
--      module" - not load-bearing, so deleting a Project Type must never be
--      blocked by Modules it previously generated).
--   4. Adds the new admin-requested fields to Carbynetech_Project:
--      ProjectTypeId, ProjectTech, BillingType, CustomerPO, Notes,
--      ProjectLeadEmployeeId, ProjectManagerEmployeeId,
--      DeliveryHeadEmployeeId, NeedsReview. Also corrects ProjectTech/Notes
--      to their intended widths (NVARCHAR(200)/NVARCHAR(2000)) if an
--      earlier run of a predecessor script already created them narrower.
--   5. Seeds the "Pending Classification" internal Account - where an
--      employee's "Others" quick-add Project lands until admin reviews it
--      (see MasterDataService.QuickAddProjectAsync).
--   6. Seeds 7 real Project Types with their full Level-1/Level-2 template
--      trees, taken verbatim from the "Team Task List" tab of the Meridian
--      Master Data Collection Template (Project Methodology section):
--      Agile Scrum, AMS Support, Activate, HR, Inside Sales, Marketing,
--      Presales. (Network & Technical Support is NOT included here - its
--      real task list has not been supplied yet; add it the same way once
--      it is.) These get new Codes (PT_ prefix) so they cannot collide
--      with any old placeholder codes already in Carbynetech_TaskCategory
--      on this branch, which are left exactly as they are - untouched.
--   7. Creates Carbynetech_EmployeeProjectAllocation - the new Employee<->
--      Project join table that backs the admin-facing "who's actually
--      working on which project" resource-allocation feature (checkbox
--      list on Add/Edit Employee, project resource-count report).
--
-- Idempotent: safe to re-run - every step checks whether it's already
-- been done before doing it again.
--
-- SAFE BY DEFAULT: runs inside an open transaction that spans every
-- batch below (the transaction is a connection-level construct, not a
-- batch-level one, so GO doesn't end it); review what's printed, then
-- explicitly COMMIT or ROLLBACK at the bottom.
-- =====================================================================

USE [Carbynetech_TimeSheet_Application];
GO
SET NOCOUNT ON;

BEGIN TRANSACTION AddProjTypeResAlloc;

PRINT '--- BEFORE ---';
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Carbynetech_TaskCategory')
    PRINT 'Carbynetech_TaskCategory exists (not yet renamed).';
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Carbynetech_ProjectType')
    SELECT COUNT(*) AS [ProjectType RowCount] FROM Carbynetech_ProjectType;
SELECT COUNT(*) AS [Project RowCount] FROM Carbynetech_Project;
SELECT COUNT(*) AS [Module RowCount] FROM Carbynetech_Module;
GO

-- ---------- Step 1: rename Carbynetech_TaskCategory -> Carbynetech_ProjectType ----------
BEGIN TRY
    IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Carbynetech_TaskCategory')
       AND NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Carbynetech_ProjectType')
    BEGIN
        EXEC sp_rename 'Carbynetech_TaskCategory', 'Carbynetech_ProjectType';
        EXEC sp_rename 'Carbynetech_ProjectType.TaskCategoryId', 'ProjectTypeId', 'COLUMN';
        PRINT 'Renamed Carbynetech_TaskCategory -> Carbynetech_ProjectType (and its PK column).';
    END
    ELSE IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Carbynetech_ProjectType')
        PRINT 'Carbynetech_ProjectType already exists - skipped.';
    ELSE
    BEGIN
        PRINT 'ERROR: neither Carbynetech_TaskCategory nor Carbynetech_ProjectType exists - unexpected schema state.';
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION AddProjTypeResAlloc;
        THROW 50001, 'Neither Carbynetech_TaskCategory nor Carbynetech_ProjectType found.', 1;
    END
END TRY
BEGIN CATCH
    PRINT 'ERROR in Step 1 - rolling back everything.';
    PRINT ERROR_MESSAGE();
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION AddProjTypeResAlloc;
    THROW;
END CATCH
GO

-- ---------- Step 2: create the Project Type template tables ----------
BEGIN TRY
    IF @@TRANCOUNT = 0
        PRINT 'Transaction was already rolled back (Step 1 failed) - stopping before Step 2.';
    ELSE
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Carbynetech_ProjectTypeModuleTemplate')
        BEGIN
            CREATE TABLE Carbynetech_ProjectTypeModuleTemplate (
                ProjectTypeModuleTemplateId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Carbynetech_ProjectTypeModuleTemplate PRIMARY KEY,
                ProjectTypeId               INT NOT NULL,
                Name                        NVARCHAR(150) NOT NULL,
                SortOrder                   INT NOT NULL CONSTRAINT DF_Carbynetech_ProjectTypeModuleTemplate_SortOrder DEFAULT 0,
                CONSTRAINT FK_Carbynetech_ProjectTypeModuleTemplate_ProjectType
                    FOREIGN KEY (ProjectTypeId) REFERENCES Carbynetech_ProjectType(ProjectTypeId) ON DELETE CASCADE,
                CONSTRAINT UQ_Carbynetech_ProjectTypeModuleTemplate_Type_Name UNIQUE (ProjectTypeId, Name)
            );
            PRINT 'Created Carbynetech_ProjectTypeModuleTemplate.';
        END
        ELSE
            PRINT 'Carbynetech_ProjectTypeModuleTemplate already exists - skipped.';

        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Carbynetech_ProjectTypeTaskTemplate')
        BEGIN
            CREATE TABLE Carbynetech_ProjectTypeTaskTemplate (
                ProjectTypeTaskTemplateId   INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Carbynetech_ProjectTypeTaskTemplate PRIMARY KEY,
                ProjectTypeModuleTemplateId INT NOT NULL,
                Name                        NVARCHAR(150) NOT NULL,
                SortOrder                   INT NOT NULL CONSTRAINT DF_Carbynetech_ProjectTypeTaskTemplate_SortOrder DEFAULT 0,
                CONSTRAINT FK_Carbynetech_ProjectTypeTaskTemplate_ModuleTemplate
                    FOREIGN KEY (ProjectTypeModuleTemplateId) REFERENCES Carbynetech_ProjectTypeModuleTemplate(ProjectTypeModuleTemplateId) ON DELETE CASCADE,
                CONSTRAINT UQ_Carbynetech_ProjectTypeTaskTemplate_Module_Name UNIQUE (ProjectTypeModuleTemplateId, Name)
            );
            PRINT 'Created Carbynetech_ProjectTypeTaskTemplate.';
        END
        ELSE
            PRINT 'Carbynetech_ProjectTypeTaskTemplate already exists - skipped.';
    END
END TRY
BEGIN CATCH
    PRINT 'ERROR in Step 2 - rolling back everything.';
    PRINT ERROR_MESSAGE();
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION AddProjTypeResAlloc;
    THROW;
END CATCH
GO

-- ---------- Step 3a: rename Carbynetech_Module's category column ----------
-- (kept in its own batch, deliberately not touching nullability/FK yet -
-- best practice is to never ALTER/reference a just-sp_renamed column in
-- the same batch as the rename itself.)
BEGIN TRY
    IF @@TRANCOUNT = 0
        PRINT 'Transaction was already rolled back (an earlier step failed) - stopping before Step 3a.';
    ELSE
    BEGIN
        IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Carbynetech_Module') AND name = 'TaskCategoryId')
           AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Carbynetech_Module') AND name = 'ProjectTypeId')
        BEGIN
            -- Drop whatever the existing FK on Module.TaskCategoryId is
            -- actually named (never assume a name - it depends on how this
            -- database's schema was originally created).
            DECLARE @OldFkName NVARCHAR(200);
            SELECT @OldFkName = fk.name
            FROM sys.foreign_keys fk
            JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
            JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
            WHERE fk.parent_object_id = OBJECT_ID('Carbynetech_Module')
              AND c.name = 'TaskCategoryId';

            IF @OldFkName IS NOT NULL
            BEGIN
                EXEC('ALTER TABLE Carbynetech_Module DROP CONSTRAINT [' + @OldFkName + ']');
                PRINT 'Dropped old FK ' + @OldFkName + ' on Carbynetech_Module.TaskCategoryId.';
            END
            ELSE
                PRINT 'No existing FK found on Carbynetech_Module.TaskCategoryId - continuing anyway.';

            EXEC sp_rename 'Carbynetech_Module.TaskCategoryId', 'ProjectTypeId', 'COLUMN';
            PRINT 'Renamed Carbynetech_Module.TaskCategoryId -> ProjectTypeId.';
        END
        ELSE IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Carbynetech_Module') AND name = 'ProjectTypeId')
            PRINT 'Carbynetech_Module.ProjectTypeId already exists - skipped rename.';
        ELSE
            PRINT 'Neither TaskCategoryId nor ProjectTypeId found on Carbynetech_Module - unexpected, leaving alone.';
    END
END TRY
BEGIN CATCH
    PRINT 'ERROR in Step 3a - rolling back everything.';
    PRINT ERROR_MESSAGE();
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION AddProjTypeResAlloc;
    THROW;
END CATCH
GO

-- ---------- Step 3b: make it nullable + ensure its FK is ON DELETE SET NULL ----------
-- Runs in a fresh batch (after the rename above has fully committed to
-- catalog metadata), and is written as a single check that's correct
-- whether Step 3a just ran this same execution or ran in an earlier one -
-- it only looks at the column's current state, not at what Step 3a did.
BEGIN TRY
    IF @@TRANCOUNT = 0
        PRINT 'Transaction was already rolled back (an earlier step failed) - stopping before Step 3b.';
    ELSE IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Carbynetech_Module') AND name = 'ProjectTypeId')
        PRINT 'Carbynetech_Module.ProjectTypeId does not exist (Step 3a did not run or failed) - stopping before Step 3b.';
    ELSE
    BEGIN
        IF EXISTS (
            SELECT 1 FROM sys.columns
            WHERE object_id = OBJECT_ID('Carbynetech_Module') AND name = 'ProjectTypeId' AND is_nullable = 0
        )
        BEGIN
            ALTER TABLE Carbynetech_Module ALTER COLUMN ProjectTypeId INT NULL;
            PRINT 'Made Carbynetech_Module.ProjectTypeId nullable.';
        END
        ELSE
            PRINT 'Carbynetech_Module.ProjectTypeId is already nullable - skipped.';

        DECLARE @CurrentFkName NVARCHAR(200), @CurrentDeleteAction TINYINT;
        SELECT @CurrentFkName = fk.name, @CurrentDeleteAction = fk.delete_referential_action
        FROM sys.foreign_keys fk
        JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
        JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
        WHERE fk.parent_object_id = OBJECT_ID('Carbynetech_Module')
          AND c.name = 'ProjectTypeId';

        IF @CurrentFkName IS NULL
        BEGIN
            ALTER TABLE Carbynetech_Module
                ADD CONSTRAINT FK_Carbynetech_Module_ProjectType
                    FOREIGN KEY (ProjectTypeId) REFERENCES Carbynetech_ProjectType(ProjectTypeId) ON DELETE SET NULL;
            PRINT 'Added FK_Carbynetech_Module_ProjectType (ON DELETE SET NULL).';
        END
        ELSE IF @CurrentDeleteAction <> 2 -- 2 = SET_NULL
        BEGIN
            EXEC('ALTER TABLE Carbynetech_Module DROP CONSTRAINT [' + @CurrentFkName + ']');
            ALTER TABLE Carbynetech_Module
                ADD CONSTRAINT FK_Carbynetech_Module_ProjectType
                    FOREIGN KEY (ProjectTypeId) REFERENCES Carbynetech_ProjectType(ProjectTypeId) ON DELETE SET NULL;
            PRINT 'Upgraded existing FK ' + @CurrentFkName + ' on Carbynetech_Module.ProjectTypeId to ON DELETE SET NULL.';
        END
        ELSE
            PRINT 'Carbynetech_Module.ProjectTypeId FK already ON DELETE SET NULL - skipped.';
    END
END TRY
BEGIN CATCH
    PRINT 'ERROR in Step 3b - rolling back everything.';
    PRINT ERROR_MESSAGE();
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION AddProjTypeResAlloc;
    THROW;
END CATCH
GO

-- ---------- Step 4: new columns on Carbynetech_Project ----------
BEGIN TRY
    IF @@TRANCOUNT = 0
        PRINT 'Transaction was already rolled back (an earlier step failed) - stopping before Step 4.';
    ELSE
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Carbynetech_Project') AND name = 'ProjectTypeId')
        BEGIN
            ALTER TABLE Carbynetech_Project ADD ProjectTypeId INT NULL;
            ALTER TABLE Carbynetech_Project
                ADD CONSTRAINT FK_Carbynetech_Project_ProjectType
                    FOREIGN KEY (ProjectTypeId) REFERENCES Carbynetech_ProjectType(ProjectTypeId) ON DELETE NO ACTION;
            PRINT 'Added Carbynetech_Project.ProjectTypeId.';
        END
        ELSE PRINT 'Carbynetech_Project.ProjectTypeId already exists - skipped.';

        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Carbynetech_Project') AND name = 'ProjectTech')
        BEGIN
            ALTER TABLE Carbynetech_Project ADD ProjectTech NVARCHAR(200) NULL;
            PRINT 'Added Carbynetech_Project.ProjectTech.';
        END
        ELSE PRINT 'Carbynetech_Project.ProjectTech already exists - skipped.';

        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Carbynetech_Project') AND name = 'BillingType')
        BEGIN
            ALTER TABLE Carbynetech_Project ADD BillingType NVARCHAR(30) NULL;
            PRINT 'Added Carbynetech_Project.BillingType.';
        END
        ELSE PRINT 'Carbynetech_Project.BillingType already exists - skipped.';

        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Carbynetech_Project') AND name = 'CustomerPO')
        BEGIN
            ALTER TABLE Carbynetech_Project ADD CustomerPO NVARCHAR(100) NULL;
            PRINT 'Added Carbynetech_Project.CustomerPO.';
        END
        ELSE PRINT 'Carbynetech_Project.CustomerPO already exists - skipped.';

        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Carbynetech_Project') AND name = 'Notes')
        BEGIN
            ALTER TABLE Carbynetech_Project ADD Notes NVARCHAR(2000) NULL;
            PRINT 'Added Carbynetech_Project.Notes.';
        END
        ELSE PRINT 'Carbynetech_Project.Notes already exists - skipped.';

        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Carbynetech_Project') AND name = 'NeedsReview')
        BEGIN
            ALTER TABLE Carbynetech_Project ADD NeedsReview BIT NOT NULL CONSTRAINT DF_Carbynetech_Project_NeedsReview DEFAULT 0;
            PRINT 'Added Carbynetech_Project.NeedsReview.';
        END
        ELSE PRINT 'Carbynetech_Project.NeedsReview already exists - skipped.';

        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Carbynetech_Project') AND name = 'ProjectLeadEmployeeId')
        BEGIN
            ALTER TABLE Carbynetech_Project ADD ProjectLeadEmployeeId INT NULL;
            ALTER TABLE Carbynetech_Project
                ADD CONSTRAINT FK_Carbynetech_Project_ProjectLeadEmployee
                    FOREIGN KEY (ProjectLeadEmployeeId) REFERENCES Carbynetech_Employee(EmployeeId) ON DELETE NO ACTION;
            PRINT 'Added Carbynetech_Project.ProjectLeadEmployeeId.';
        END
        ELSE PRINT 'Carbynetech_Project.ProjectLeadEmployeeId already exists - skipped.';

        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Carbynetech_Project') AND name = 'ProjectManagerEmployeeId')
        BEGIN
            ALTER TABLE Carbynetech_Project ADD ProjectManagerEmployeeId INT NULL;
            ALTER TABLE Carbynetech_Project
                ADD CONSTRAINT FK_Carbynetech_Project_ProjectManagerEmployee
                    FOREIGN KEY (ProjectManagerEmployeeId) REFERENCES Carbynetech_Employee(EmployeeId) ON DELETE NO ACTION;
            PRINT 'Added Carbynetech_Project.ProjectManagerEmployeeId.';
        END
        ELSE PRINT 'Carbynetech_Project.ProjectManagerEmployeeId already exists - skipped.';

        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Carbynetech_Project') AND name = 'DeliveryHeadEmployeeId')
        BEGIN
            ALTER TABLE Carbynetech_Project ADD DeliveryHeadEmployeeId INT NULL;
            ALTER TABLE Carbynetech_Project
                ADD CONSTRAINT FK_Carbynetech_Project_DeliveryHeadEmployee
                    FOREIGN KEY (DeliveryHeadEmployeeId) REFERENCES Carbynetech_Employee(EmployeeId) ON DELETE NO ACTION;
            PRINT 'Added Carbynetech_Project.DeliveryHeadEmployeeId.';
        END
        ELSE PRINT 'Carbynetech_Project.DeliveryHeadEmployeeId already exists - skipped.';
    END
END TRY
BEGIN CATCH
    PRINT 'ERROR in Step 4 - rolling back everything.';
    PRINT ERROR_MESSAGE();
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION AddProjTypeResAlloc;
    THROW;
END CATCH
GO

-- ---------- Step 4b: widen ProjectTech/Notes if a previous run left them narrower ----------
-- Corrective step. An earlier predecessor script (already run against this
-- database) added ProjectTech as NVARCHAR(100) and Notes as NVARCHAR(1000).
-- Step 4 above only ADDs these columns when they don't exist yet, so it
-- would silently leave them at those older, narrower widths. This step
-- widens them to what this script actually intends (200 / 2000) whenever
-- they're currently smaller - never shrinks, so it's safe to re-run and
-- safe even if Step 4 just created them fresh at the right width already.
BEGIN TRY
    IF @@TRANCOUNT = 0
        PRINT 'Transaction was already rolled back (an earlier step failed) - stopping before Step 4b.';
    ELSE
    BEGIN
        IF EXISTS (
            SELECT 1 FROM sys.columns
            WHERE object_id = OBJECT_ID('Carbynetech_Project') AND name = 'ProjectTech'
              AND max_length <> -1 AND max_length < 400 -- 200 NVARCHAR chars = 400 bytes
        )
        BEGIN
            ALTER TABLE Carbynetech_Project ALTER COLUMN ProjectTech NVARCHAR(200) NULL;
            PRINT 'Widened Carbynetech_Project.ProjectTech to NVARCHAR(200).';
        END
        ELSE
            PRINT 'Carbynetech_Project.ProjectTech is already NVARCHAR(200) or wider - skipped.';

        IF EXISTS (
            SELECT 1 FROM sys.columns
            WHERE object_id = OBJECT_ID('Carbynetech_Project') AND name = 'Notes'
              AND max_length <> -1 AND max_length < 4000 -- 2000 NVARCHAR chars = 4000 bytes
        )
        BEGIN
            ALTER TABLE Carbynetech_Project ALTER COLUMN Notes NVARCHAR(2000) NULL;
            PRINT 'Widened Carbynetech_Project.Notes to NVARCHAR(2000).';
        END
        ELSE
            PRINT 'Carbynetech_Project.Notes is already NVARCHAR(2000) or wider - skipped.';
    END
END TRY
BEGIN CATCH
    PRINT 'ERROR in Step 4b - rolling back everything.';
    PRINT ERROR_MESSAGE();
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION AddProjTypeResAlloc;
    THROW;
END CATCH
GO

-- ---------- Step 5: seed the "Pending Classification" internal Account ----------
BEGIN TRY
    IF @@TRANCOUNT = 0
        PRINT 'Transaction was already rolled back (an earlier step failed) - stopping before Step 5.';
    ELSE
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_Account WHERE Name = 'Pending Classification')
        BEGIN
            DECLARE @PendingDeptId INT;
            SELECT TOP 1 @PendingDeptId = DepartmentId FROM Carbynetech_Department
                WHERE Code IN ('MCB', 'ADMIN', 'ADMIN & IT SUPPORT', 'ADMIN&ITSUPPORT')
                ORDER BY CASE Code WHEN 'MCB' THEN 0 ELSE 1 END;
            IF @PendingDeptId IS NULL
                SELECT TOP 1 @PendingDeptId = DepartmentId FROM Carbynetech_Department ORDER BY DepartmentId;

            IF @PendingDeptId IS NULL
            BEGIN
                PRINT 'ERROR: no Carbynetech_Department rows exist at all - cannot seed Pending Classification Account.';
                IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION AddProjTypeResAlloc;
                THROW 50002, 'No department available for Pending Classification Account.', 1;
            END

            INSERT INTO Carbynetech_Account (DepartmentId, Name, AccountType, CreatedAt)
                VALUES (@PendingDeptId, 'Pending Classification', 'Internal', SYSUTCDATETIME());
            PRINT 'Seeded "Pending Classification" Account under DepartmentId ' + CAST(@PendingDeptId AS NVARCHAR(10)) + ' - verify this department looks right, reassign later on the Master Data screen if not.';
        END
        ELSE
            PRINT '"Pending Classification" Account already exists - skipped.';
    END
END TRY
BEGIN CATCH
    PRINT 'ERROR in Step 5 - rolling back everything.';
    PRINT ERROR_MESSAGE();
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION AddProjTypeResAlloc;
    THROW;
END CATCH
GO

-- ---------- Step 6: seed 7 real Project Types + their Level-1/Level-2 templates ----------
-- Source: "Team Task List" tab, "Project Methodology" section, of the
-- Meridian Master Data Collection Template (verbatim Level-1/Level-2
-- names). New Codes (PT_ prefix) so they can never collide with the old
-- placeholder codes (consult/dev/bi/support/presales/train/admin), which
-- are left completely alone by this script.
BEGIN TRY
    IF @@TRANCOUNT = 0
        PRINT 'Transaction was already rolled back (an earlier step failed) - stopping before Step 6.';
    ELSE
    BEGIN
        -- Helper table of (Code, Name) for the 7 types - inserted one at a
        -- time below so each can be skipped independently if already there.
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectType WHERE Code = 'PT_AGILE_SCRUM')
            INSERT INTO Carbynetech_ProjectType (Code, Name) VALUES ('PT_AGILE_SCRUM', 'Agile Scrum');
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectType WHERE Code = 'PT_AMS_SUPPORT')
            INSERT INTO Carbynetech_ProjectType (Code, Name) VALUES ('PT_AMS_SUPPORT', 'AMS Support');
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectType WHERE Code = 'PT_ACTIVATE')
            INSERT INTO Carbynetech_ProjectType (Code, Name) VALUES ('PT_ACTIVATE', 'Activate');
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectType WHERE Code = 'PT_HR')
            INSERT INTO Carbynetech_ProjectType (Code, Name) VALUES ('PT_HR', 'HR');
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectType WHERE Code = 'PT_INSIDE_SALES')
            INSERT INTO Carbynetech_ProjectType (Code, Name) VALUES ('PT_INSIDE_SALES', 'Inside Sales');
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectType WHERE Code = 'PT_MARKETING')
            INSERT INTO Carbynetech_ProjectType (Code, Name) VALUES ('PT_MARKETING', 'Marketing');
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectType WHERE Code = 'PT_PRESALES')
            INSERT INTO Carbynetech_ProjectType (Code, Name) VALUES ('PT_PRESALES', 'Presales');
        PRINT 'Ensured all 7 Project Type rows exist (Agile Scrum, AMS Support, Activate, HR, Inside Sales, Marketing, Presales).';
    END
END TRY
BEGIN CATCH
    PRINT 'ERROR in Step 6 (Project Type rows) - rolling back everything.';
    PRINT ERROR_MESSAGE();
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION AddProjTypeResAlloc;
    THROW;
END CATCH
GO

-- ---------- Step 6b: seed the Level-1/Level-2 template rows for each of the 7 types ----------
BEGIN TRY
    IF @@TRANCOUNT = 0
        PRINT 'Transaction was already rolled back (an earlier step failed) - stopping before Step 6b.';
    ELSE
    BEGIN
        DECLARE @mtid INT;

        -- ==== PT_AGILE_SCRUM ====
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_AGILE_SCRUM' AND mt.Name = N'Design')
            INSERT INTO Carbynetech_ProjectTypeModuleTemplate (ProjectTypeId, Name, SortOrder)
                SELECT ProjectTypeId, N'Design', 1 FROM Carbynetech_ProjectType WHERE Code = 'PT_AGILE_SCRUM';
        SELECT @mtid = mt.ProjectTypeModuleTemplateId FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_AGILE_SCRUM' AND mt.Name = N'Design';
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'User Stories')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'User Stories', 1);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Wireframes')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Wireframes', 2);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Design Docs - BRD , HLD, LLD, FS, TS, Architecture')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Design Docs - BRD , HLD, LLD, FS, TS, Architecture', 3);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Design - Infra')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Design - Infra', 4);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Design Review-Internal')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Design Review-Internal', 5);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Design Validation - Customer')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Design Validation - Customer', 6);

        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_AGILE_SCRUM' AND mt.Name = N'Build')
            INSERT INTO Carbynetech_ProjectTypeModuleTemplate (ProjectTypeId, Name, SortOrder)
                SELECT ProjectTypeId, N'Build', 2 FROM Carbynetech_ProjectType WHERE Code = 'PT_AGILE_SCRUM';
        SELECT @mtid = mt.ProjectTypeModuleTemplateId FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_AGILE_SCRUM' AND mt.Name = N'Build';
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Code Build')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Code Build', 1);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Code Build using AI')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Code Build using AI', 2);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Code Review')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Code Review', 3);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Infra Build')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Infra Build', 4);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'DevOps')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'DevOps', 5);

        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_AGILE_SCRUM' AND mt.Name = N'Testing')
            INSERT INTO Carbynetech_ProjectTypeModuleTemplate (ProjectTypeId, Name, SortOrder)
                SELECT ProjectTypeId, N'Testing', 3 FROM Carbynetech_ProjectType WHERE Code = 'PT_AGILE_SCRUM';
        SELECT @mtid = mt.ProjectTypeModuleTemplateId FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_AGILE_SCRUM' AND mt.Name = N'Testing';
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Unit Testing')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Unit Testing', 1);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Functional Testing - Internal')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Functional Testing - Internal', 2);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'UAT- Customer')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'UAT- Customer', 3);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Regression Testing')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Regression Testing', 4);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Other Test Scenarios')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Other Test Scenarios', 5);

        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_AGILE_SCRUM' AND mt.Name = N'Documentation')
            INSERT INTO Carbynetech_ProjectTypeModuleTemplate (ProjectTypeId, Name, SortOrder)
                SELECT ProjectTypeId, N'Documentation', 4 FROM Carbynetech_ProjectType WHERE Code = 'PT_AGILE_SCRUM';
        SELECT @mtid = mt.ProjectTypeModuleTemplateId FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_AGILE_SCRUM' AND mt.Name = N'Documentation';
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Design')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Design', 1);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Test Documentation')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Test Documentation', 2);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Documentation Review')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Documentation Review', 3);

        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_AGILE_SCRUM' AND mt.Name = N'Meetings')
            INSERT INTO Carbynetech_ProjectTypeModuleTemplate (ProjectTypeId, Name, SortOrder)
                SELECT ProjectTypeId, N'Meetings', 5 FROM Carbynetech_ProjectType WHERE Code = 'PT_AGILE_SCRUM';
        SELECT @mtid = mt.ProjectTypeModuleTemplateId FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_AGILE_SCRUM' AND mt.Name = N'Meetings';
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Customer Meetings')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Customer Meetings', 1);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Sprint - Internal Meeting')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Sprint - Internal Meeting', 2);

        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_AGILE_SCRUM' AND mt.Name = N'Knowledge Share')
            INSERT INTO Carbynetech_ProjectTypeModuleTemplate (ProjectTypeId, Name, SortOrder)
                SELECT ProjectTypeId, N'Knowledge Share', 6 FROM Carbynetech_ProjectType WHERE Code = 'PT_AGILE_SCRUM';
        SELECT @mtid = mt.ProjectTypeModuleTemplateId FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_AGILE_SCRUM' AND mt.Name = N'Knowledge Share';
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Customer - KT')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Customer - KT', 1);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Internal-KT')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Internal-KT', 2);

        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_AGILE_SCRUM' AND mt.Name = N'Training')
            INSERT INTO Carbynetech_ProjectTypeModuleTemplate (ProjectTypeId, Name, SortOrder)
                SELECT ProjectTypeId, N'Training', 7 FROM Carbynetech_ProjectType WHERE Code = 'PT_AGILE_SCRUM';
        SELECT @mtid = mt.ProjectTypeModuleTemplateId FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_AGILE_SCRUM' AND mt.Name = N'Training';
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Customer - Training')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Customer - Training', 1);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Internal-Training')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Internal-Training', 2);

        -- ==== PT_AMS_SUPPORT ====
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_AMS_SUPPORT' AND mt.Name = N'Issue Resolution')
            INSERT INTO Carbynetech_ProjectTypeModuleTemplate (ProjectTypeId, Name, SortOrder)
                SELECT ProjectTypeId, N'Issue Resolution', 1 FROM Carbynetech_ProjectType WHERE Code = 'PT_AMS_SUPPORT';
        SELECT @mtid = mt.ProjectTypeModuleTemplateId FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_AMS_SUPPORT' AND mt.Name = N'Issue Resolution';
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Issue Research')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Issue Research', 1);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Issue Fix')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Issue Fix', 2);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Testing')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Testing', 3);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Release, Transport, Documentation')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Release, Transport, Documentation', 4);

        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_AMS_SUPPORT' AND mt.Name = N'Enhancement')
            INSERT INTO Carbynetech_ProjectTypeModuleTemplate (ProjectTypeId, Name, SortOrder)
                SELECT ProjectTypeId, N'Enhancement', 2 FROM Carbynetech_ProjectType WHERE Code = 'PT_AMS_SUPPORT';
        SELECT @mtid = mt.ProjectTypeModuleTemplateId FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_AMS_SUPPORT' AND mt.Name = N'Enhancement';
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Requirements')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Requirements', 1);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Build')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Build', 2);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Unit Testing')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Unit Testing', 3);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Functional Testing - Internal')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Functional Testing - Internal', 4);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'UAT- Customer')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'UAT- Customer', 5);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Regression Testing')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Regression Testing', 6);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Other Test Scenarios')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Other Test Scenarios', 7);

        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_AMS_SUPPORT' AND mt.Name = N'Documentation')
            INSERT INTO Carbynetech_ProjectTypeModuleTemplate (ProjectTypeId, Name, SortOrder)
                SELECT ProjectTypeId, N'Documentation', 3 FROM Carbynetech_ProjectType WHERE Code = 'PT_AMS_SUPPORT';
        SELECT @mtid = mt.ProjectTypeModuleTemplateId FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_AMS_SUPPORT' AND mt.Name = N'Documentation';
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Design')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Design', 1);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Test Documentation')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Test Documentation', 2);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Documentation Review')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Documentation Review', 3);

        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_AMS_SUPPORT' AND mt.Name = N'Meetings')
            INSERT INTO Carbynetech_ProjectTypeModuleTemplate (ProjectTypeId, Name, SortOrder)
                SELECT ProjectTypeId, N'Meetings', 4 FROM Carbynetech_ProjectType WHERE Code = 'PT_AMS_SUPPORT';
        SELECT @mtid = mt.ProjectTypeModuleTemplateId FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_AMS_SUPPORT' AND mt.Name = N'Meetings';
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Customer Meetings')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Customer Meetings', 1);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Sprint - Internal Meeting')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Sprint - Internal Meeting', 2);

        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_AMS_SUPPORT' AND mt.Name = N'Knowledge Share')
            INSERT INTO Carbynetech_ProjectTypeModuleTemplate (ProjectTypeId, Name, SortOrder)
                SELECT ProjectTypeId, N'Knowledge Share', 5 FROM Carbynetech_ProjectType WHERE Code = 'PT_AMS_SUPPORT';
        SELECT @mtid = mt.ProjectTypeModuleTemplateId FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_AMS_SUPPORT' AND mt.Name = N'Knowledge Share';
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Customer - KT')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Customer - KT', 1);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Internal-KT')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Internal-KT', 2);

        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_AMS_SUPPORT' AND mt.Name = N'Training')
            INSERT INTO Carbynetech_ProjectTypeModuleTemplate (ProjectTypeId, Name, SortOrder)
                SELECT ProjectTypeId, N'Training', 6 FROM Carbynetech_ProjectType WHERE Code = 'PT_AMS_SUPPORT';
        SELECT @mtid = mt.ProjectTypeModuleTemplateId FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_AMS_SUPPORT' AND mt.Name = N'Training';
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Customer - Training')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Customer - Training', 1);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Internal-Training')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Internal-Training', 2);

        -- ==== PT_HR ====
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_HR' AND mt.Name = N'Recruit')
            INSERT INTO Carbynetech_ProjectTypeModuleTemplate (ProjectTypeId, Name, SortOrder)
                SELECT ProjectTypeId, N'Recruit', 1 FROM Carbynetech_ProjectType WHERE Code = 'PT_HR';
        SELECT @mtid = mt.ProjectTypeModuleTemplateId FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_HR' AND mt.Name = N'Recruit';
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Internal')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Internal', 1);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'External')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'External', 2);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Junior Talent')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Junior Talent', 3);

        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_HR' AND mt.Name = N'L&D')
            INSERT INTO Carbynetech_ProjectTypeModuleTemplate (ProjectTypeId, Name, SortOrder)
                SELECT ProjectTypeId, N'L&D', 2 FROM Carbynetech_ProjectType WHERE Code = 'PT_HR';
        SELECT @mtid = mt.ProjectTypeModuleTemplateId FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_HR' AND mt.Name = N'L&D';
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Plan and Execute Sessions')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Plan and Execute Sessions', 1);

        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_HR' AND mt.Name = N'Others')
            INSERT INTO Carbynetech_ProjectTypeModuleTemplate (ProjectTypeId, Name, SortOrder)
                SELECT ProjectTypeId, N'Others', 3 FROM Carbynetech_ProjectType WHERE Code = 'PT_HR';
        SELECT @mtid = mt.ProjectTypeModuleTemplateId FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_HR' AND mt.Name = N'Others';
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Resource Onboarding/Exits')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Resource Onboarding/Exits', 1);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Resource Meetings/Advisory/Coach')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Resource Meetings/Advisory/Coach', 2);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Fun&Celebrations')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Fun&Celebrations', 3);

        -- ==== PT_INSIDE_SALES ====
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_INSIDE_SALES' AND mt.Name = N'Lead Generation')
            INSERT INTO Carbynetech_ProjectTypeModuleTemplate (ProjectTypeId, Name, SortOrder)
                SELECT ProjectTypeId, N'Lead Generation', 1 FROM Carbynetech_ProjectType WHERE Code = 'PT_INSIDE_SALES';
        SELECT @mtid = mt.ProjectTypeModuleTemplateId FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_INSIDE_SALES' AND mt.Name = N'Lead Generation';
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Outbound Calls')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Outbound Calls', 1);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Email & Campaigns')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Email & Campaigns', 2);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Field Visit - F2F')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Field Visit - F2F', 3);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Prospect Meetings - Remote, Requirements')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Prospect Meetings - Remote, Requirements', 4);

        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_INSIDE_SALES' AND mt.Name = N'Others')
            INSERT INTO Carbynetech_ProjectTypeModuleTemplate (ProjectTypeId, Name, SortOrder)
                SELECT ProjectTypeId, N'Others', 2 FROM Carbynetech_ProjectType WHERE Code = 'PT_INSIDE_SALES';
        SELECT @mtid = mt.ProjectTypeModuleTemplateId FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_INSIDE_SALES' AND mt.Name = N'Others';
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Review Meetings')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Review Meetings', 1);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Campaign Build - Content')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Campaign Build - Content', 2);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Trainings')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Trainings', 3);

        -- ==== PT_MARKETING ====
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_MARKETING' AND mt.Name = N'Content')
            INSERT INTO Carbynetech_ProjectTypeModuleTemplate (ProjectTypeId, Name, SortOrder)
                SELECT ProjectTypeId, N'Content', 1 FROM Carbynetech_ProjectType WHERE Code = 'PT_MARKETING';
        SELECT @mtid = mt.ProjectTypeModuleTemplateId FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_MARKETING' AND mt.Name = N'Content';
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Content Design')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Content Design', 1);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Content Buid -UI/UX,Flyers, Posters')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Content Buid -UI/UX,Flyers, Posters', 2);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Content Publish')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Content Publish', 3);

        -- ==== PT_PRESALES ====
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_PRESALES' AND mt.Name = N'Ideation')
            INSERT INTO Carbynetech_ProjectTypeModuleTemplate (ProjectTypeId, Name, SortOrder)
                SELECT ProjectTypeId, N'Ideation', 1 FROM Carbynetech_ProjectType WHERE Code = 'PT_PRESALES';
        SELECT @mtid = mt.ProjectTypeModuleTemplateId FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_PRESALES' AND mt.Name = N'Ideation';
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Requirements')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Requirements', 1);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Design - Wireframes')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Design - Wireframes', 2);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Deployment')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Deployment', 3);

        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_PRESALES' AND mt.Name = N'Proposal')
            INSERT INTO Carbynetech_ProjectTypeModuleTemplate (ProjectTypeId, Name, SortOrder)
                SELECT ProjectTypeId, N'Proposal', 2 FROM Carbynetech_ProjectType WHERE Code = 'PT_PRESALES';
        SELECT @mtid = mt.ProjectTypeModuleTemplateId FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_PRESALES' AND mt.Name = N'Proposal';
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Requirements')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Requirements', 1);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Estimates - Technical')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Estimates - Technical', 2);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Client Discovery &Validation Meetings')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Client Discovery &Validation Meetings', 3);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Proposal Response')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Proposal Response', 4);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Solution Build -Technical POC''s')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Solution Build -Technical POC''s', 5);

        -- ==== PT_ACTIVATE ====
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_ACTIVATE' AND mt.Name = N'Discover')
            INSERT INTO Carbynetech_ProjectTypeModuleTemplate (ProjectTypeId, Name, SortOrder)
                SELECT ProjectTypeId, N'Discover', 1 FROM Carbynetech_ProjectType WHERE Code = 'PT_ACTIVATE';
        SELECT @mtid = mt.ProjectTypeModuleTemplateId FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_ACTIVATE' AND mt.Name = N'Discover';
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Discover')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Discover', 1);

        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_ACTIVATE' AND mt.Name = N'Prepare')
            INSERT INTO Carbynetech_ProjectTypeModuleTemplate (ProjectTypeId, Name, SortOrder)
                SELECT ProjectTypeId, N'Prepare', 2 FROM Carbynetech_ProjectType WHERE Code = 'PT_ACTIVATE';
        SELECT @mtid = mt.ProjectTypeModuleTemplateId FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_ACTIVATE' AND mt.Name = N'Prepare';
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Prepare')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Prepare', 1);

        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_ACTIVATE' AND mt.Name = N'Explore')
            INSERT INTO Carbynetech_ProjectTypeModuleTemplate (ProjectTypeId, Name, SortOrder)
                SELECT ProjectTypeId, N'Explore', 3 FROM Carbynetech_ProjectType WHERE Code = 'PT_ACTIVATE';
        SELECT @mtid = mt.ProjectTypeModuleTemplateId FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_ACTIVATE' AND mt.Name = N'Explore';
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'User Stories')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'User Stories', 1);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Wireframes')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Wireframes', 2);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Design Review')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Design Review', 3);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Design Validation')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Design Validation', 4);

        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_ACTIVATE' AND mt.Name = N'Realize')
            INSERT INTO Carbynetech_ProjectTypeModuleTemplate (ProjectTypeId, Name, SortOrder)
                SELECT ProjectTypeId, N'Realize', 4 FROM Carbynetech_ProjectType WHERE Code = 'PT_ACTIVATE';
        SELECT @mtid = mt.ProjectTypeModuleTemplateId FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_ACTIVATE' AND mt.Name = N'Realize';
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Code Build')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Code Build', 1);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Code Build using AI')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Code Build using AI', 2);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Code Review')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Code Review', 3);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Unit Testing')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Unit Testing', 4);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Functional Testing - Internal')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Functional Testing - Internal', 5);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'UAT- Customer')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'UAT- Customer', 6);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Regression Testing')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Regression Testing', 7);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Other Test Scenarios')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Other Test Scenarios', 8);

        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_ACTIVATE' AND mt.Name = N'Deploy')
            INSERT INTO Carbynetech_ProjectTypeModuleTemplate (ProjectTypeId, Name, SortOrder)
                SELECT ProjectTypeId, N'Deploy', 5 FROM Carbynetech_ProjectType WHERE Code = 'PT_ACTIVATE';
        SELECT @mtid = mt.ProjectTypeModuleTemplateId FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_ACTIVATE' AND mt.Name = N'Deploy';
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Deploy')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Deploy', 1);

        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_ACTIVATE' AND mt.Name = N'Run')
            INSERT INTO Carbynetech_ProjectTypeModuleTemplate (ProjectTypeId, Name, SortOrder)
                SELECT ProjectTypeId, N'Run', 6 FROM Carbynetech_ProjectType WHERE Code = 'PT_ACTIVATE';
        SELECT @mtid = mt.ProjectTypeModuleTemplateId FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_ACTIVATE' AND mt.Name = N'Run';
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Run')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Run', 1);

        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_ACTIVATE' AND mt.Name = N'Documentation')
            INSERT INTO Carbynetech_ProjectTypeModuleTemplate (ProjectTypeId, Name, SortOrder)
                SELECT ProjectTypeId, N'Documentation', 7 FROM Carbynetech_ProjectType WHERE Code = 'PT_ACTIVATE';
        SELECT @mtid = mt.ProjectTypeModuleTemplateId FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_ACTIVATE' AND mt.Name = N'Documentation';
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Design')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Design', 1);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Test Documentation')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Test Documentation', 2);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Documentation Review')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Documentation Review', 3);

        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_ACTIVATE' AND mt.Name = N'Meetings')
            INSERT INTO Carbynetech_ProjectTypeModuleTemplate (ProjectTypeId, Name, SortOrder)
                SELECT ProjectTypeId, N'Meetings', 8 FROM Carbynetech_ProjectType WHERE Code = 'PT_ACTIVATE';
        SELECT @mtid = mt.ProjectTypeModuleTemplateId FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_ACTIVATE' AND mt.Name = N'Meetings';
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Customer Meetings')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Customer Meetings', 1);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Sprint - Internal Meeting')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Sprint - Internal Meeting', 2);

        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_ACTIVATE' AND mt.Name = N'Knowledge Share')
            INSERT INTO Carbynetech_ProjectTypeModuleTemplate (ProjectTypeId, Name, SortOrder)
                SELECT ProjectTypeId, N'Knowledge Share', 9 FROM Carbynetech_ProjectType WHERE Code = 'PT_ACTIVATE';
        SELECT @mtid = mt.ProjectTypeModuleTemplateId FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_ACTIVATE' AND mt.Name = N'Knowledge Share';
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Customer - KT')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Customer - KT', 1);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Internal-KT')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Internal-KT', 2);

        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_ACTIVATE' AND mt.Name = N'Training')
            INSERT INTO Carbynetech_ProjectTypeModuleTemplate (ProjectTypeId, Name, SortOrder)
                SELECT ProjectTypeId, N'Training', 10 FROM Carbynetech_ProjectType WHERE Code = 'PT_ACTIVATE';
        SELECT @mtid = mt.ProjectTypeModuleTemplateId FROM Carbynetech_ProjectTypeModuleTemplate mt JOIN Carbynetech_ProjectType t ON t.ProjectTypeId = mt.ProjectTypeId WHERE t.Code = 'PT_ACTIVATE' AND mt.Name = N'Training';
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Customer - Training')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Customer - Training', 1);
        IF NOT EXISTS (SELECT 1 FROM Carbynetech_ProjectTypeTaskTemplate WHERE ProjectTypeModuleTemplateId = @mtid AND Name = N'Internal-Training')
            INSERT INTO Carbynetech_ProjectTypeTaskTemplate (ProjectTypeModuleTemplateId, Name, SortOrder) VALUES (@mtid, N'Internal-Training', 2);

        PRINT 'Ensured template rows for all 7 Project Types (31 modules, 95 tasks total).';
    END
END TRY
BEGIN CATCH
    PRINT 'ERROR in Step 6b (template rows) - rolling back everything.';
    PRINT ERROR_MESSAGE();
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION AddProjTypeResAlloc;
    THROW;
END CATCH
GO

-- ---------- Step 7: create Carbynetech_EmployeeProjectAllocation ----------
-- The new Employee<->Project join table backing the resource-allocation
-- feature - one row per (EmployeeId, ProjectId) pair, ticked/unticked from
-- the Add/Edit Employee form. Both FKs cascade: removing an Employee or a
-- Project also removes their allocation rows (there's nothing else useful
-- to do with an allocation row pointing at a deleted parent).
BEGIN TRY
    IF @@TRANCOUNT = 0
        PRINT 'Transaction was already rolled back (an earlier step failed) - stopping before Step 7.';
    ELSE
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Carbynetech_EmployeeProjectAllocation')
        BEGIN
            CREATE TABLE Carbynetech_EmployeeProjectAllocation (
                EmployeeId  INT NOT NULL,
                ProjectId   INT NOT NULL,
                AssignedAt  DATETIME2 NOT NULL CONSTRAINT DF_Carbynetech_EmployeeProjectAllocation_AssignedAt DEFAULT SYSUTCDATETIME(),
                CONSTRAINT PK_Carbynetech_EmployeeProjectAllocation PRIMARY KEY (EmployeeId, ProjectId),
                CONSTRAINT FK_Carbynetech_EmployeeProjectAllocation_Employee
                    FOREIGN KEY (EmployeeId) REFERENCES Carbynetech_Employee(EmployeeId) ON DELETE CASCADE,
                CONSTRAINT FK_Carbynetech_EmployeeProjectAllocation_Project
                    FOREIGN KEY (ProjectId) REFERENCES Carbynetech_Project(ProjectId) ON DELETE CASCADE
            );
            PRINT 'Created Carbynetech_EmployeeProjectAllocation.';
        END
        ELSE
            PRINT 'Carbynetech_EmployeeProjectAllocation already exists - skipped.';
    END
END TRY
BEGIN CATCH
    PRINT 'ERROR in Step 7 - rolling back everything.';
    PRINT ERROR_MESSAGE();
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION AddProjTypeResAlloc;
    THROW;
END CATCH
GO

-- ---------- Final: summary for review ----------
BEGIN TRY
    IF @@TRANCOUNT = 0
        PRINT 'Transaction was already rolled back (an earlier step failed) - nothing to summarize.';
    ELSE
    BEGIN
        PRINT '';
        PRINT '--- Carbynetech_ProjectType rows AFTER this script ---';
        SELECT ProjectTypeId, Code, Name FROM Carbynetech_ProjectType ORDER BY ProjectTypeId;

        PRINT '';
        PRINT '--- Carbynetech_ProjectTypeModuleTemplate / TaskTemplate counts per type AFTER this script ---';
        SELECT t.Code, t.Name,
               (SELECT COUNT(*) FROM Carbynetech_ProjectTypeModuleTemplate mt WHERE mt.ProjectTypeId = t.ProjectTypeId) AS ModuleTemplateCount,
               (SELECT COUNT(*) FROM Carbynetech_ProjectTypeTaskTemplate tt
                    JOIN Carbynetech_ProjectTypeModuleTemplate mt ON mt.ProjectTypeModuleTemplateId = tt.ProjectTypeModuleTemplateId
                    WHERE mt.ProjectTypeId = t.ProjectTypeId) AS TaskTemplateCount
        FROM Carbynetech_ProjectType t
        ORDER BY t.ProjectTypeId;

        PRINT '';
        PRINT '--- Carbynetech_Project columns AFTER this script ---';
        SELECT c.name AS ColumnName, ty.name AS DataType, c.max_length, c.is_nullable
        FROM sys.columns c
        JOIN sys.types ty ON ty.user_type_id = c.user_type_id
        WHERE c.object_id = OBJECT_ID('Carbynetech_Project')
        ORDER BY c.column_id;

        PRINT '';
        PRINT '--- Carbynetech_Module columns AFTER this script ---';
        SELECT c.name AS ColumnName, ty.name AS DataType, c.is_nullable
        FROM sys.columns c
        JOIN sys.types ty ON ty.user_type_id = c.user_type_id
        WHERE c.object_id = OBJECT_ID('Carbynetech_Module')
        ORDER BY c.column_id;

        PRINT '';
        PRINT '--- "Pending Classification" Account ---';
        SELECT AccountId, DepartmentId, Name, AccountType FROM Carbynetech_Account WHERE Name = 'Pending Classification';

        PRINT '';
        PRINT '--- Carbynetech_EmployeeProjectAllocation ---';
        SELECT COUNT(*) AS [EmployeeProjectAllocation RowCount] FROM Carbynetech_EmployeeProjectAllocation;

        PRINT '';
        PRINT 'Review everything above.';
        PRINT 'If it looks right: run  COMMIT TRANSACTION AddProjTypeResAlloc;';
        PRINT 'If anything looks wrong: run  ROLLBACK TRANSACTION AddProjTypeResAlloc;';
    END
END TRY
BEGIN CATCH
    PRINT 'ERROR while summarizing - rolling back everything.';
    PRINT ERROR_MESSAGE();
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION AddProjTypeResAlloc;
    THROW;
END CATCH
GO

-- ---------------------------------------------------------------------
-- Run exactly ONE of these two lines yourself, after reviewing the
-- output above. Nothing below runs automatically.
-- ---------------------------------------------------------------------
-- COMMIT TRANSACTION AddProjTypeResAlloc;
-- ROLLBACK TRANSACTION AddProjTypeResAlloc;
