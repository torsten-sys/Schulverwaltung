-- ============================================================
-- PersonenSchema.sql  (Prompt 2 – aktualisierte Version)
-- Migriert das alte Teilnehmer/Dozent-Modell auf Person/PersonRolle
-- Ausführen auf der Schulverwaltung-Datenbank (einmalig, idempotent)
-- ============================================================

-- ────────────────────────────────────────────────────────────
-- 0. Alte Tabellen DROP (falls vorhanden)
-- ────────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.AnmeldungBuchungsposten', 'U') IS NOT NULL DROP TABLE dbo.AnmeldungBuchungsposten;
IF OBJECT_ID('dbo.DozentBuchungsposten',    'U') IS NOT NULL DROP TABLE dbo.DozentBuchungsposten;
IF OBJECT_ID('dbo.AnmeldungBuchblatt',      'U') IS NOT NULL DROP TABLE dbo.AnmeldungBuchblatt;
IF OBJECT_ID('dbo.DozentBuchblatt',         'U') IS NOT NULL DROP TABLE dbo.DozentBuchblatt;
IF OBJECT_ID('dbo.LehrgangTeilnehmer',      'U') IS NOT NULL DROP TABLE dbo.LehrgangTeilnehmer;
IF OBJECT_ID('dbo.LehrgangDozent',          'U') IS NOT NULL DROP TABLE dbo.LehrgangDozent;
-- LehrgangPerson (Stub) neu erstellen – DROP falls Stub existiert
IF OBJECT_ID('dbo.LehrgangPerson',          'U') IS NOT NULL DROP TABLE dbo.LehrgangPerson;
IF OBJECT_ID('dbo.Teilnehmer',              'U') IS NOT NULL DROP TABLE dbo.Teilnehmer;
IF OBJECT_ID('dbo.Dozent',                  'U') IS NOT NULL DROP TABLE dbo.Dozent;
GO

-- ────────────────────────────────────────────────────────────
-- 1. Person
-- ────────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.Person', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Person
    (
        PersonId        INT            NOT NULL IDENTITY(1,1) PRIMARY KEY,
        PersonNr        NVARCHAR(20)   NOT NULL,
        Anrede          NVARCHAR(20)   NULL,
        Titel           NVARCHAR(50)   NULL,
        Vorname         NVARCHAR(100)  NOT NULL,
        Nachname        NVARCHAR(100)  NOT NULL,
        Namenszusatz    NVARCHAR(50)   NULL,
        Geburtsdatum    DATE           NULL,
        Geschlecht      TINYINT        NULL,          -- 1=M 2=W 3=D
        Strasse         NVARCHAR(200)  NULL,
        PLZ             NVARCHAR(10)   NULL,
        Ort             NVARCHAR(100)  NULL,
        Land            NVARCHAR(100)  NOT NULL CONSTRAINT DF_Person_Land DEFAULT N'Deutschland',
        Email           NVARCHAR(200)  NULL,
        Telefon         NVARCHAR(50)   NULL,
        Mobil           NVARCHAR(50)   NULL,
        Gesperrt        BIT            NOT NULL CONSTRAINT DF_Person_Gesperrt DEFAULT 0,
        Notiz           NVARCHAR(MAX)  NULL,
        CreatedAt       DATETIME2      NOT NULL CONSTRAINT DF_Person_CreatedAt DEFAULT SYSUTCDATETIME(),
        ModifiedAt      DATETIME2      NOT NULL CONSTRAINT DF_Person_ModifiedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_Person_PersonNr UNIQUE (PersonNr)
    );
    PRINT 'Tabelle Person erstellt.';
END;
GO

-- ────────────────────────────────────────────────────────────
-- 2. PersonRolle
-- ────────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.PersonRolle', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PersonRolle
    (
        PersonRolleId   INT           NOT NULL IDENTITY(1,1) PRIMARY KEY,
        PersonId        INT           NOT NULL,
        RolleTyp        TINYINT       NOT NULL,  -- 0=TN 1=DO 2=Patient 3=Ansprechp. 4=Prüfer 5=Betreuer
        Status          TINYINT       NOT NULL CONSTRAINT DF_PersonRolle_Status DEFAULT 0,
        GueltigAb       DATE          NOT NULL CONSTRAINT DF_PersonRolle_GueltigAb DEFAULT CAST(SYSUTCDATETIME() AS DATE),
        GueltigBis      DATE          NULL,
        BetriebId       INT           NULL,
        Notiz           NVARCHAR(MAX) NULL,
        CreatedAt       DATETIME2     NOT NULL CONSTRAINT DF_PersonRolle_CreatedAt DEFAULT SYSUTCDATETIME(),
        ModifiedAt      DATETIME2     NOT NULL CONSTRAINT DF_PersonRolle_ModifiedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_PersonRolle_Person  FOREIGN KEY (PersonId)  REFERENCES dbo.Person  (PersonId) ON DELETE CASCADE,
        CONSTRAINT FK_PersonRolle_Betrieb FOREIGN KEY (BetriebId) REFERENCES dbo.Betrieb (BetriebId) ON DELETE SET NULL
    );

    -- Gefilterter Unique-Index: pro Person nur eine aktive Rolle des gleichen Typs
    CREATE UNIQUE INDEX UX_PersonRolle_PersonRolleTyp_Aktiv
        ON dbo.PersonRolle (PersonId, RolleTyp)
        WHERE Status = 0;

    PRINT 'Tabelle PersonRolle erstellt.';
END;
GO

-- ────────────────────────────────────────────────────────────
-- 3. PersonDozentProfil
-- ────────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.PersonDozentProfil', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PersonDozentProfil
    (
        PersonId            INT            NOT NULL PRIMARY KEY,
        Kuerzel             NVARCHAR(10)   NULL,
        Intern              BIT            NOT NULL CONSTRAINT DF_DozentProfil_Intern DEFAULT 1,
        Qualifikation       NVARCHAR(500)  NULL,
        MaxStundenProWoche  DECIMAL(5,2)   NULL,
        CreatedAt           DATETIME2      NOT NULL CONSTRAINT DF_DozentProfil_CreatedAt DEFAULT SYSUTCDATETIME(),
        ModifiedAt          DATETIME2      NOT NULL CONSTRAINT DF_DozentProfil_ModifiedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_PersonDozentProfil_Person FOREIGN KEY (PersonId) REFERENCES dbo.Person (PersonId) ON DELETE CASCADE
    );
    PRINT 'Tabelle PersonDozentProfil erstellt.';
END;
GO

-- ────────────────────────────────────────────────────────────
-- 4. PersonAenderungsposten
-- ────────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.PersonAenderungsposten', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PersonAenderungsposten
    (
        PostenId            INT            NOT NULL IDENTITY(1,1) PRIMARY KEY,
        BelegNr             NVARCHAR(20)   NOT NULL,
        PersonId            INT            NOT NULL,   -- kein FK (Snapshot-Prinzip)
        PersonNr            NVARCHAR(20)   NOT NULL,
        PersonName          NVARCHAR(200)  NOT NULL,
        Ereignis            NVARCHAR(100)  NOT NULL,
        Tabelle             NVARCHAR(100)  NOT NULL,
        Feld                NVARCHAR(100)  NULL,
        AlterWert           NVARCHAR(MAX)  NULL,
        NeuerWert           NVARCHAR(MAX)  NULL,
        RolleTyp            TINYINT        NULL,
        Zeitstempel         DATETIME2      NOT NULL CONSTRAINT DF_Aenderung_Zeitstempel DEFAULT SYSUTCDATETIME(),
        AusfuehrendUser     NVARCHAR(200)  NOT NULL,
        Notiz               NVARCHAR(MAX)  NULL
    );
    PRINT 'Tabelle PersonAenderungsposten erstellt.';

    -- Sicherheits-Trigger: kein UPDATE/DELETE (Snapshot-Prinzip)
    EXEC('
    CREATE TRIGGER dbo.TR_PersonAenderungsposten_ReadOnly
    ON dbo.PersonAenderungsposten
    AFTER UPDATE, DELETE
    AS
    BEGIN
        SET NOCOUNT ON;
        RAISERROR(''PersonAenderungsposten darf nicht geändert oder gelöscht werden.'', 16, 1);
        ROLLBACK TRANSACTION;
    END;
    ');
END;
GO

-- ────────────────────────────────────────────────────────────
-- 5. LehrgangPerson (vollständig – PK: LehrgangId + PersonId + Rolle)
-- ────────────────────────────────────────────────────────────
CREATE TABLE dbo.LehrgangPerson
(
    LehrgangId      INT           NOT NULL,
    PersonId        INT           NOT NULL,
    Rolle           TINYINT       NOT NULL,  -- 0=TN 1=DO 2=Assistent 3=Gast
    -- Status: 0=Warteliste 1=Angemeldet 2=Abgemeldet 3=Bestanden
    Status          TINYINT       NOT NULL CONSTRAINT DF_LP_Status DEFAULT 1,
    AnmeldungsDatum DATE          NOT NULL CONSTRAINT DF_LP_AnmDatum DEFAULT CAST(SYSUTCDATETIME() AS DATE),
    GeplanteStunden DECIMAL(6,2)  NULL,
    Notiz           NVARCHAR(MAX) NULL,
    CreatedAt       DATETIME2     NOT NULL CONSTRAINT DF_LP_CreatedAt DEFAULT SYSUTCDATETIME(),
    ModifiedAt      DATETIME2     NOT NULL CONSTRAINT DF_LP_ModifiedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_LehrgangPerson PRIMARY KEY (LehrgangId, PersonId, Rolle),
    CONSTRAINT FK_LP_Lehrgang FOREIGN KEY (LehrgangId) REFERENCES dbo.Lehrgang (LehrgangId) ON DELETE CASCADE,
    CONSTRAINT FK_LP_Person   FOREIGN KEY (PersonId)   REFERENCES dbo.Person   (PersonId)   ON DELETE NO ACTION
);
PRINT 'Tabelle LehrgangPerson erstellt.';
GO

-- ────────────────────────────────────────────────────────────
-- 6. ModifiedAt-Trigger für Person
-- ────────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.TR_Person_ModifiedAt', 'TR') IS NOT NULL DROP TRIGGER dbo.TR_Person_ModifiedAt;
GO
CREATE TRIGGER dbo.TR_Person_ModifiedAt
ON dbo.Person
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT UPDATE(ModifiedAt)
        UPDATE dbo.Person
           SET ModifiedAt = SYSUTCDATETIME()
          FROM dbo.Person p
         INNER JOIN inserted i ON p.PersonId = i.PersonId;
END;
GO

-- ────────────────────────────────────────────────────────────
-- 7. ModifiedAt-Trigger für PersonRolle
-- ────────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.TR_PersonRolle_ModifiedAt', 'TR') IS NOT NULL DROP TRIGGER dbo.TR_PersonRolle_ModifiedAt;
GO
CREATE TRIGGER dbo.TR_PersonRolle_ModifiedAt
ON dbo.PersonRolle
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT UPDATE(ModifiedAt)
        UPDATE dbo.PersonRolle
           SET ModifiedAt = SYSUTCDATETIME()
          FROM dbo.PersonRolle r
         INNER JOIN inserted i ON r.PersonRolleId = i.PersonRolleId;
END;
GO

-- ────────────────────────────────────────────────────────────
-- 8. NoSerie-Einträge
-- ────────────────────────────────────────────────────────────

-- PERSON-Nummernserie
IF NOT EXISTS (SELECT 1 FROM dbo.NoSerie WHERE NoSerieCode = N'PERSON')
BEGIN
    INSERT INTO dbo.NoSerie  (NoSerieCode, Bezeichnung, Standardmaessig, Datumsgeb)
    VALUES (N'PERSON', N'Personen-Nummernkreis', 1, 0);

    INSERT INTO dbo.NoSerieZeile (NoSerieCode, StartingNo, Prefix, NummerLaenge, IncrementBy, Offen)
    VALUES (N'PERSON', N'PN-000001', N'PN-', 6, 1, 1);
    PRINT 'NoSerie PERSON angelegt.';
END;

-- AENDERUNG-Nummernserie (für PersonAenderungsposten BelegNr)
IF NOT EXISTS (SELECT 1 FROM dbo.NoSerie WHERE NoSerieCode = N'AENDERUNG')
BEGIN
    INSERT INTO dbo.NoSerie  (NoSerieCode, Bezeichnung, Standardmaessig, Datumsgeb)
    VALUES (N'AENDERUNG', N'Änderungsprotokoll-Nummernkreis', 1, 0);

    INSERT INTO dbo.NoSerieZeile (NoSerieCode, StartingNo, Prefix, NummerLaenge, IncrementBy, Offen)
    VALUES (N'AENDERUNG', N'AE-000001', N'AE-', 6, 1, 1);
    PRINT 'NoSerie AENDERUNG angelegt.';
END;
GO

PRINT '>>> PersonenSchema.sql erfolgreich ausgeführt. <<<';
