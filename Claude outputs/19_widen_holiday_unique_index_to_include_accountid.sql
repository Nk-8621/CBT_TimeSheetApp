-- =====================================================================
-- 19_widen_holiday_unique_index_to_include_accountid.sql
--
-- Carbynetech_Holiday's unique index was (HolidayDate, Location) only -
-- it didn't account for the optional client (AccountId) column at all.
-- That meant adding a client-specific holiday (e.g. "All India", scoped
-- to just one client via the "Specific client" dropdown on the New
-- Holiday screen) failed with a raw SQL unique-key violation - surfaced
-- to the user as a generic "An unexpected error occurred" - whenever any
-- OTHER holiday already existed on that same date/location, even a
-- completely unrelated company-wide one or one for a different client.
--
-- This widens the index to (HolidayDate, Location, AccountId), so a
-- company-wide holiday (AccountId NULL) and any number of client-specific
-- ones can coexist on the same date/location. Only an exact repeat -
-- same date, same location text, same client (including two company-wide
-- entries, since SQL Server treats two NULLs as equal for uniqueness) -
-- is still rejected, matching the friendly check now also added in
-- MasterDataService.CreateHolidayAsync/UpdateHolidayAsync.
--
-- Safe to widen: adding a column to an existing unique key can never
-- create a new duplicate among rows that were already unique under the
-- narrower key, so this never fails on account of existing data.
--
-- Idempotent: does nothing if the 3-column index already exists. Looks
-- up the current index's name dynamically rather than hardcoding it,
-- same reasoning as script 18 - conventions can differ per environment.
-- =====================================================================

USE [Carbynetech_TimeSheet_Application];
GO
SET NOCOUNT ON;

IF EXISTS (
    SELECT 1
    FROM sys.indexes i
    WHERE i.object_id = OBJECT_ID('Carbynetech_Holiday')
      AND i.is_unique = 1
      AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id) = 3
      AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND c.name = 'HolidayDate')
      AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND c.name = 'Location')
      AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND c.name = 'AccountId')
)
BEGIN
    PRINT 'Carbynetech_Holiday already has a unique index on (HolidayDate, Location, AccountId) - nothing to do.';
END
ELSE
BEGIN
    DECLARE @OldIndexName SYSNAME;
    DECLARE @OldIndexId INT;
    DECLARE @ConstraintName SYSNAME;
    DECLARE @DropSql NVARCHAR(400);

    -- Find the old 2-column (HolidayDate, Location) unique index, if present.
    SELECT TOP 1 @OldIndexName = i.name, @OldIndexId = i.index_id
    FROM sys.indexes i
    WHERE i.object_id = OBJECT_ID('Carbynetech_Holiday')
      AND i.is_unique = 1
      AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id) = 2
      AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND c.name = 'HolidayDate')
      AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND c.name = 'Location');

    IF @OldIndexName IS NOT NULL
    BEGIN
        -- This index might just be a plain unique index (droppable with
        -- DROP INDEX), or it might be the index SQL Server auto-created to
        -- back a named UNIQUE CONSTRAINT - which DROP INDEX refuses to touch
        -- directly (Msg 3723). sys.key_constraints links a constraint back
        -- to the index enforcing it, so check there first.
        SELECT @ConstraintName = kc.name
        FROM sys.key_constraints kc
        WHERE kc.parent_object_id = OBJECT_ID('Carbynetech_Holiday')
          AND kc.unique_index_id = @OldIndexId
          AND kc.type = 'UQ';

        IF @ConstraintName IS NOT NULL
        BEGIN
            SET @DropSql = 'ALTER TABLE Carbynetech_Holiday DROP CONSTRAINT ' + QUOTENAME(@ConstraintName) + ';';
            EXEC(@DropSql);
            PRINT CONCAT('Dropped unique constraint ', @ConstraintName, ' (backing index ', @OldIndexName, ') on (HolidayDate, Location).');
        END
        ELSE
        BEGIN
            SET @DropSql = 'DROP INDEX ' + QUOTENAME(@OldIndexName) + ' ON Carbynetech_Holiday;';
            EXEC(@DropSql);
            PRINT CONCAT('Dropped old unique index ', @OldIndexName, ' on (HolidayDate, Location).');
        END
    END
    ELSE
        PRINT 'No existing (HolidayDate, Location) unique index/constraint found - creating the new one from scratch.';

    CREATE UNIQUE NONCLUSTERED INDEX IX_Carbynetech_Holiday_HolidayDate_Location_AccountId
        ON Carbynetech_Holiday (HolidayDate, Location, AccountId);

    PRINT 'Created unique index IX_Carbynetech_Holiday_HolidayDate_Location_AccountId on (HolidayDate, Location, AccountId).';
END
GO
