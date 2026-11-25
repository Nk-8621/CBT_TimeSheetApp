-- =====================================================================
-- 12_add_new_employees_reporting_to_cbt1017.sql
--
-- Inserts the 16 new employees Babu supplied on 2026-09-15, all
-- reporting to CBT1017 (Sunil Adhi, EmployeeId 120 per the reference
-- row Babu included). This replaces script 11 for these 16 people -
-- they didn't exist in Carbynetech_Employee yet, which is why that
-- UPDATE-based script left ManagerEmployeeId showing NULL (there was
-- no row to update).
--
-- Idempotent: matched by EmployeeCode: only inserts rows that don't
-- already exist, so it's safe to re-run.
--
-- ASSUMPTIONS - please confirm/correct before running:
--
--   1. "CBT1299." (trailing period in your paste) is treated as a typo
--      for "CBT1299" - matches the code you used in the original
--      manager-assignment request.
--
--   2. Initials were blank in your paste, so per your confirmation
--      they're generated as first-letter-of-first-name +
--      first-letter-of-last-word (matching CBT1017's own "Sunil Adhi"
--      -> "SA" pattern) - e.g. "Abdul Gaffur Shaik" -> "AS". Two pairs
--      of employees land on the same 2-letter initials (Ramya Bandaram
--      / Rohith Belladige -> both "RB"; Guddanti Thirumala Subbarao /
--      Ganesh Varun Sai -> both "GS") - that's fine, Initials isn't a
--      unique column, but flagging it in case you'd rather
--      disambiguate those four manually afterward.
--
--   3. CreatedAt and LoginAccessGrantedAt were truncated fragments in
--      your paste ("58:25.2" / "09:11.5") - per your confirmation,
--      both are set to the actual time this script runs instead, and
--      LoginAccessGrantedByEmployeeId is set to CBT1017 (120), since
--      he appears to be the one provisioning these accounts.
--
--   4. Everything else copied as given: DepartmentId 11, LocationId 1,
--      IsActive 1, the shared PasswordHash + MustChangePassword 1
--      (matches CBT1017's own row - looks like your org's standard
--      "must change on first login" default, not a paste error),
--      IsExternal 0, EntraObjectId/PrimaryAccountId/DeactivatedAt/
--      DeactivatedByEmployeeId all NULL, UpdatedAt NULL.
-- =====================================================================

USE [Carbynetech_TimeSheet_Application];
GO
SET NOCOUNT ON;

DECLARE @Now datetime2 = SYSUTCDATETIME();
DECLARE @ManagerId int = 120; -- CBT1017 / Sunil Adhi, per the reference row supplied

IF NOT EXISTS (SELECT 1 FROM Carbynetech_Employee WHERE EmployeeId = @ManagerId AND EmployeeCode = 'CBT1017')
    PRINT 'WARNING: EmployeeId 120 is not CBT1017 in this database - double check @ManagerId before trusting this run.';

IF NOT EXISTS (SELECT 1 FROM Carbynetech_Department WHERE DepartmentId = 11)
    PRINT 'WARNING: DepartmentId 11 does not exist - inserts below will fail on the FK constraint.';

IF NOT EXISTS (SELECT 1 FROM Carbynetech_Location WHERE LocationId = 1)
    PRINT 'WARNING: LocationId 1 does not exist - inserts below will fail on the FK constraint.';

DECLARE @NewEmployees TABLE (
    EmployeeCode nvarchar(15), FullName nvarchar(150), Initials nvarchar(5),
    Designation nvarchar(150), Grade nvarchar(5), JobTitleRaw nvarchar(200), Email nvarchar(256)
);
INSERT INTO @NewEmployees (EmployeeCode, FullName, Initials, Designation, Grade, JobTitleRaw, Email) VALUES
    ('CBT1143', 'Abdul Gaffur Shaik',              'AS', 'Software Eng Analyst (SE)-E2',    'E2', 'Software Eng Analyst (SE)-E2',    'abdul.shaik@carbynetech.com'),
    ('CBT1323', 'Mahaboob Basha Shaik',            'MS', 'Trainee Software Associate - E0', 'E0', 'Trainee Software Associate - E0', 'basha.shaik@carbynetech.com'),
    ('CBT1299', 'Dhanunjaya Rao Konchada',         'DK', 'Trainee Software Associate - E0', 'E0', 'Trainee Software Associate - E0', 'dhanunjaya.k@carbynetech.com'),
    ('CBT1245', 'Hidayatulla Shaik',               'HS', 'Associate Software Engineer - E1','E1', 'Associate Software Engineer - E1','hidayatulla.s@carbynetech.com'),
    ('CBT1252', 'Karthyayani Golam',               'KG', 'Trainee Software Associate - E0', 'E0', 'Trainee Software Associate - E0', 'Karthyayani.g@carbynetech.com'),
    ('CBT1290', 'Mounisha Duraisamy',              'MD', 'Trainee Software Associate - E0', 'E0', 'Trainee Software Associate - E0', 'mounisha.d@carbynetech.com'),
    ('CBT1324', 'Yasaswani Namratha sri Bheemuni', 'YB', 'Trainee Software Associate - E0', 'E0', 'Trainee Software Associate - E0', 'namratha.b@carbynetech.com'),
    ('CBT1144', 'Sangapuram Navarathan Reddy',     'SR', 'Software Eng Analyst (SE)-E2',    'E2', 'Software Eng Analyst (SE)-E2',    'navarathanreddy.s@carbynetech.com'),
    ('CBT1259', 'Sara Pragna',                     'SP', 'Trainee Software Associate - E0', 'E0', 'Trainee Software Associate - E0', 'pragna.s@carbynetech.com'),
    ('CBT1346', 'Rakesh Pedapudi',                 'RP', 'Trainee Software Associate - E0', 'E0', 'Trainee Software Associate - E0', 'Rakesh.p@carbynetech.com'),
    ('CBT1262', 'Ramya Bandaram',                  'RB', 'Trainee Software Associate - E0', 'E0', 'Trainee Software Associate - E0', 'ramya.b@carbynetech.com'),
    ('CBT1336', 'Rohith Belladige',                'RB', 'Trainee Software Associate - E0', 'E0', 'Trainee Software Associate - E0', 'Rohith.b@carbynetech.com'),
    ('CBT1263', 'Guddanti Thirumala Subbarao',     'GS', 'Trainee Software Associate - E0', 'E0', 'Trainee Software Associate - E0', 'thirumala.g@carbynetech.com'),
    ('CBT1291', 'Ganesh Varun Sai',                'GS', 'Trainee Software Associate - E0', 'E0', 'Trainee Software Associate - E0', 'varunsai.g@carbynetech.com'),
    ('CBT1185', 'Vijay Shankar Polavarapu',        'VP', 'Senior Software Eng (SSE) - E3',  'E3', 'Senior Software Eng (SSE) - E3',  'vijayshankar.p@carbynetech.com'),
    ('CBT1235', 'Yuvaraju Kanala',                 'YK', 'Associate Software Engineer - E1','E1', 'Associate Software Engineer - E1','yuvaraju.k@carbynetech.com');

INSERT INTO Carbynetech_Employee (
    EmployeeCode, FullName, Initials, DepartmentId, LocationId, Designation, Grade, JobTitleRaw,
    ManagerEmployeeId, Email, EntraObjectId, IsActive, CreatedAt, UpdatedAt,
    PasswordHash, MustChangePassword, LoginAccessGrantedAt, LoginAccessGrantedByEmployeeId,
    IsExternal, PrimaryAccountId, DeactivatedAt, DeactivatedByEmployeeId
)
SELECT
    ne.EmployeeCode, ne.FullName, ne.Initials, 11, 1, ne.Designation, ne.Grade, ne.JobTitleRaw,
    @ManagerId, ne.Email, NULL, 1, @Now, NULL,
    '210000.Y30VuSIlrsxbgcRnL578JQ==.7xE72kHB8+VE7fFBcC4AslaZQRqUm8gSB4LH3K2NLvc=', 1, @Now, @ManagerId,
    0, NULL, NULL, NULL
FROM @NewEmployees ne
WHERE NOT EXISTS (SELECT 1 FROM Carbynetech_Employee e WHERE e.EmployeeCode = ne.EmployeeCode);

PRINT CONCAT(@@ROWCOUNT, ' new employee row(s) inserted (expect 16 - fewer means some already existed, which is fine on a re-run).');
PRINT '';
PRINT 'Done. All 16 now report to CBT1017 directly via ManagerEmployeeId - no separate script needed.';
GO
