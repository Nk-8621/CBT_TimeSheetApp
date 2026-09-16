-- =====================================================================
-- 10_add_berco_projects_and_rename_sap_support.sql
--
-- Three independent changes Babu asked for on 2026-09-15:
--
--   A) Adds 3 new Berco Analytics projects (Italy Phase1, Germany, AM).
--   B) Renames the 3 "SAP Support - AMS" projects (added in script 09)
--      so each one shows its customer in the name, since they all
--      looked identical in the DB before this.
--   C) Renames 2 pre-existing projects by their Project Code (given
--      by Babu directly, from the live Carbynetech_Project table):
--        UL-TECHEFF    "TechEff - HUL"  -> "TechEff for India Business"
--        UL - Chevigny "UL- Chevigny"   -> "Techeff for Europe - Chevigny"
--
-- Idempotent for part A (matched by Code, only inserts if missing).
-- Parts B and C are plain UPDATEs by Code - safe to re-run (same end
-- name each time). Part B depends on script 09 having already run.
--
-- ASSUMPTIONS:
--   - New Berco projects: DefaultBillable=1, IsActive=1 (from "Yes"/
--     "Active" in your table), CustomerPO left NULL (same reasoning as
--     script 09 - "Yes" isn't an actual PO number), ProjectTech/Lead/
--     Manager/DeliveryHead left NULL/blank, Notes copied as given.
--   - Codes generated as SAP_BERCO_ITALY1 / SAP_BERCO_GERMANY /
--     SAP_BERCO_AM, per your choice.
--   - Renamed to "SAP Support - AMS/<Customer>" using the customer
--     spelling already stored on the Account (Ethihad, TVS Eurpgrip,
--     TVS Air Springs) rather than the "Ethihd" variant, so the project
--     name and the account name it points to stay consistent.
-- =====================================================================

USE [Carbynetech_TimeSheet_Application];
GO
SET NOCOUNT ON;

DECLARE @Now datetime2 = SYSUTCDATETIME();

-- ---------------------------------------------------------------------
-- Part A: 3 new Berco Analytics projects
-- ---------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM Carbynetech_Account WHERE Name = 'Berco')
    PRINT 'WARNING: Account "Berco" not found - Part A will insert 0 rows. Run script 09 first.';

DECLARE @NewProjects TABLE (
    Code nvarchar(20), Name nvarchar(200), BillingType nvarchar(30),
    DefaultBillable bit, IsActive bit, Notes nvarchar(2000)
);
INSERT INTO @NewProjects (Code, Name, BillingType, DefaultBillable, IsActive, Notes) VALUES
    ('SAP_BERCO_ITALY1',  'Berco Analytics (Italy Phase1)', 'Fixed', 1, 1, 'Started May 2026'),
    ('SAP_BERCO_GERMANY', 'Berco Analytics (Germany)',      'Fixed', 1, 1, 'Started Jul 2026'),
    ('SAP_BERCO_AM',      'Berco Analytics (AM)',           'Fixed', 1, 1, 'Started Sept 2026');

MERGE Carbynetech_Project AS target
USING (
    SELECT np.Code, np.Name, a.AccountId, np.BillingType, np.DefaultBillable, np.IsActive, np.Notes
    FROM @NewProjects np
    JOIN Carbynetech_Account a ON a.Name = 'Berco'
) AS src
    ON target.Code = src.Code
WHEN NOT MATCHED BY TARGET THEN
    INSERT (AccountId, Code, Name, DefaultBillable, IsActive, BillingType, CustomerPO, Notes, CreatedAt)
    VALUES (src.AccountId, src.Code, src.Name, src.DefaultBillable, src.IsActive, src.BillingType, NULL, src.Notes, @Now);

PRINT CONCAT(@@ROWCOUNT, ' new Berco project row(s) inserted (0 means Berco account missing, or these already exist).');

-- ---------------------------------------------------------------------
-- Part B: rename the 3 "SAP Support - AMS" projects to include customer
-- ---------------------------------------------------------------------
UPDATE Carbynetech_Project SET Name = 'SAP Support - AMS/Ethihad',         UpdatedAt = @Now WHERE Code = 'SAP_ETHIHAD';
PRINT CONCAT(@@ROWCOUNT, ' row(s) updated for SAP_ETHIHAD (expect 1 - 0 means script 09 hasn''t run yet).');

UPDATE Carbynetech_Project SET Name = 'SAP Support - AMS/TVS Eurpgrip',    UpdatedAt = @Now WHERE Code = 'SAP_TVSEUROGRIP';
PRINT CONCAT(@@ROWCOUNT, ' row(s) updated for SAP_TVSEUROGRIP (expect 1).');

UPDATE Carbynetech_Project SET Name = 'SAP Support - AMS/TVS Air Springs', UpdatedAt = @Now WHERE Code = 'SAP_TVSAIRSPRINGS';
PRINT CONCAT(@@ROWCOUNT, ' row(s) updated for SAP_TVSAIRSPRINGS (expect 1).');

-- ---------------------------------------------------------------------
-- Part C: rename 2 pre-existing projects, matched by their exact Code
-- ---------------------------------------------------------------------
UPDATE Carbynetech_Project SET Name = 'TechEff for India Business',        UpdatedAt = @Now WHERE Code = 'UL-TECHEFF';
PRINT CONCAT(@@ROWCOUNT, ' row(s) updated for UL-TECHEFF (expect 1).');

UPDATE Carbynetech_Project SET Name = 'Techeff for Europe - Chevigny',     UpdatedAt = @Now WHERE Code = 'UL - Chevigny';
PRINT CONCAT(@@ROWCOUNT, ' row(s) updated for UL - Chevigny (expect 1).');

PRINT '';
PRINT 'Done. All three parts applied.';
GO
