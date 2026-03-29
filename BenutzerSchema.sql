-- ============================================================
-- BfO Schulverwaltung – Benutzerverwaltung Schema
-- Rollen: 0=Gast, 1=Sachbearbeiter, 2=Dozent, 3=Administrator
-- ============================================================

-- ── AppBenutzer ──────────────────────────────────────────────
IF OBJECT_ID('AppBenutzer') IS NULL
BEGIN
    CREATE TABLE AppBenutzer (
        BenutzerId     INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        AdBenutzername NVARCHAR(100) NOT NULL,
        DisplayName    NVARCHAR(200) NULL,
        Email          NVARCHAR(200) NULL,
        AppRolle       TINYINT       NOT NULL DEFAULT 0,
        PersonId       INT           NULL,
        Gesperrt       BIT           NOT NULL DEFAULT 0,
        SperrGrund     NVARCHAR(200) NULL,
        ErsterLogin    DATETIME2     NULL,
        LetzterLogin   DATETIME2     NULL,
        Notiz          NVARCHAR(MAX) NULL,
        CreatedAt      DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
        ModifiedAt     DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_AppBenutzer_ADName  UNIQUE (AdBenutzername),
        CONSTRAINT FK_AppBenutzer_Person
            FOREIGN KEY (PersonId) REFERENCES Person(PersonId)
            ON DELETE SET NULL,
        CONSTRAINT CK_AppBenutzer_Rolle   CHECK (AppRolle BETWEEN 0 AND 3)
    );
    PRINT 'Tabelle AppBenutzer erstellt.';
END
ELSE
BEGIN
    -- Constraint auf neue 4 Rollen (0-3) aktualisieren falls nötig
    IF EXISTS (
        SELECT 1 FROM sys.check_constraints
        WHERE name = 'CK_AppBenutzer_Rolle'
          AND parent_object_id = OBJECT_ID('AppBenutzer')
    )
    BEGIN
        ALTER TABLE AppBenutzer DROP CONSTRAINT CK_AppBenutzer_Rolle;
        ALTER TABLE AppBenutzer
            ADD CONSTRAINT CK_AppBenutzer_Rolle CHECK (AppRolle BETWEEN 0 AND 3);
        PRINT 'Constraint CK_AppBenutzer_Rolle auf 0-3 aktualisiert.';
    END
    ELSE
    BEGIN
        ALTER TABLE AppBenutzer
            ADD CONSTRAINT CK_AppBenutzer_Rolle CHECK (AppRolle BETWEEN 0 AND 3);
        PRINT 'Constraint CK_AppBenutzer_Rolle angelegt.';
    END
    PRINT 'Tabelle AppBenutzer bereits vorhanden.';
END
GO

-- ── AppBenutzerAenderungsposten ───────────────────────────────
IF OBJECT_ID('AppBenutzerAenderungsposten') IS NULL
BEGIN
    CREATE TABLE AppBenutzerAenderungsposten (
        PostenId        INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        BelegNr         NVARCHAR(20)  NOT NULL,
        BenutzerId      INT           NOT NULL,
        AdBenutzername  NVARCHAR(100) NOT NULL,
        DisplayName     NVARCHAR(200) NULL,
        Ereignis        NVARCHAR(50)  NOT NULL,
        Tabelle         NVARCHAR(100) NULL,
        Feld            NVARCHAR(100) NULL,
        AlterWert       NVARCHAR(MAX) NULL,
        NeuerWert       NVARCHAR(MAX) NULL,
        Zeitstempel     DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
        AusfuehrendUser NVARCHAR(100) NOT NULL,
        Notiz           NVARCHAR(MAX) NULL
    );
    PRINT 'Tabelle AppBenutzerAenderungsposten erstellt.';
END
ELSE
    PRINT 'Tabelle AppBenutzerAenderungsposten bereits vorhanden.';
GO

-- ── Trigger: Änderungsposten unveränderlich ───────────────────
IF OBJECT_ID('TR_AppBenutzerAenderungsposten_Protect') IS NOT NULL
    DROP TRIGGER TR_AppBenutzerAenderungsposten_Protect;
GO
CREATE TRIGGER TR_AppBenutzerAenderungsposten_Protect
ON AppBenutzerAenderungsposten AFTER UPDATE, DELETE
AS
BEGIN
    RAISERROR('AppBenutzerAenderungsposten sind unveraenderlich.', 16, 1);
    ROLLBACK TRANSACTION;
END
GO

-- ── Trigger: ModifiedAt automatisch setzen ────────────────────
IF OBJECT_ID('TR_AppBenutzer_ModifiedAt') IS NOT NULL
    DROP TRIGGER TR_AppBenutzer_ModifiedAt;
GO
CREATE TRIGGER TR_AppBenutzer_ModifiedAt
ON AppBenutzer AFTER UPDATE
AS
BEGIN
    UPDATE AppBenutzer
       SET ModifiedAt = SYSUTCDATETIME()
     WHERE BenutzerId IN (SELECT BenutzerId FROM inserted);
END
GO

-- ── NoSerie für Änderungsposten ───────────────────────────────
-- Tabellen: NoSerie (PK=NoSerieCode) / NoSerieZeile (PK=NoSerieCode+StartingNo)
IF NOT EXISTS (SELECT 1 FROM NoSerie WHERE NoSerieCode = 'AENDERUNG')
BEGIN
    INSERT INTO NoSerie (NoSerieCode, Bezeichnung, Standardmaessig, Datumsgeb,
                         CreatedAt, ModifiedAt)
    VALUES ('AENDERUNG', 'Änderungsposten', 1, 0,
            SYSUTCDATETIME(), SYSUTCDATETIME());
    PRINT 'NoSerie AENDERUNG angelegt.';
END

IF NOT EXISTS (
    SELECT 1 FROM NoSerieZeile
    WHERE NoSerieCode = 'AENDERUNG' AND Offen = 1
)
BEGIN
    INSERT INTO NoSerieZeile
        (NoSerieCode, StartingNo, Prefix, NummerLaenge, IncrementBy, AllowGaps, Offen,
         CreatedAt, ModifiedAt)
    VALUES
        ('AENDERUNG', 'AE000001', 'AE', 6, 1, 0, 1,
         SYSUTCDATETIME(), SYSUTCDATETIME());
    PRINT 'NoSerieZeile AENDERUNG angelegt.';
END
GO

-- ── Ersten Administrator anlegen / aktualisieren ──────────────
-- WICHTIG: Wird nur angelegt wenn noch nicht vorhanden!
IF NOT EXISTS (
    SELECT 1 FROM AppBenutzer WHERE AdBenutzername = 'BFO-HANNOVER\treadmin'
)
BEGIN
    INSERT INTO AppBenutzer
        (AdBenutzername, DisplayName, AppRolle, ErsterLogin, CreatedAt, ModifiedAt)
    VALUES
        ('BFO-HANNOVER\treadmin', 'Administrator', 3,
         SYSUTCDATETIME(), SYSUTCDATETIME(), SYSUTCDATETIME());
    PRINT 'Administrator BFO-HANNOVER\treadmin angelegt (AppRolle=3).';
END
ELSE
BEGIN
    -- Sicherstellen dass treadmin Admin-Rolle hat
    UPDATE AppBenutzer
       SET AppRolle = 3, Gesperrt = 0
     WHERE AdBenutzername = 'BFO-HANNOVER\treadmin'
       AND (AppRolle < 3 OR Gesperrt = 1);
    PRINT 'AppBenutzer BFO-HANNOVER\treadmin bereits vorhanden – Rolle auf Administrator geprüft.';
END
GO

PRINT '=== BenutzerSchema.sql abgeschlossen ===';
GO
