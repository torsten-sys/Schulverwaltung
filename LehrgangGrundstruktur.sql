-- =============================================================================
-- LehrgangGrundstruktur.sql
-- Migration: Lehrgang Grundstruktur (LehrgangArt, LehrgangEinheit,
--            LehrgangAenderungsposten, Lehrgang-Erweiterungen)
-- Erstellt: 2026-03-06
-- =============================================================================

-- -----------------------------------------------------------------------------
-- 1. TABELLE: LehrgangArt
-- -----------------------------------------------------------------------------
CREATE TABLE [dbo].[LehrgangArt] (
    [ArtId]         INT            NOT NULL IDENTITY(1,1),
    [Code]          NVARCHAR(20)   NOT NULL,
    [Bezeichnung]   NVARCHAR(100)  NOT NULL,
    [Reihenfolge]   INT            NOT NULL CONSTRAINT [DF_LehrgangArt_Reihenfolge] DEFAULT 0,
    [Notiz]         NVARCHAR(500)  NULL,
    [CreatedAt]     DATETIME2      NOT NULL CONSTRAINT [DF_LehrgangArt_CreatedAt]  DEFAULT SYSUTCDATETIME(),
    [ModifiedAt]    DATETIME2      NOT NULL CONSTRAINT [DF_LehrgangArt_ModifiedAt] DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_LehrgangArt] PRIMARY KEY CLUSTERED ([ArtId]),
    CONSTRAINT [UQ_LehrgangArt_Code] UNIQUE ([Code])
);
GO

-- -----------------------------------------------------------------------------
-- 2. SEED: LehrgangArt (4 Standardwerte)
-- -----------------------------------------------------------------------------
INSERT INTO [dbo].[LehrgangArt] ([Code], [Bezeichnung], [Reihenfolge])
VALUES
    ('INTERN',    'Intern',           10),
    ('EXTERN',    'Extern',           20),
    ('GEFOERD',   'Gefördert',        30),
    ('KOSTENPFL', 'Kostenpflichtig',  40);
GO

-- -----------------------------------------------------------------------------
-- 3. ALTER TABLE Lehrgang – neue Spalten
-- -----------------------------------------------------------------------------
ALTER TABLE [dbo].[Lehrgang]
    ADD [LehrgangTyp]     TINYINT        NOT NULL CONSTRAINT [DF_Lehrgang_LehrgangTyp]    DEFAULT 0,
        [ArtId]           INT            NULL,
        [BezeichnungLang] NVARCHAR(500)  NULL,
        [MinTeilnehmer]   INT            NOT NULL CONSTRAINT [DF_Lehrgang_MinTeilnehmer]  DEFAULT 0,
        [Gebuehren]       DECIMAL(10,2)  NULL;
GO

-- Foreign Key: Lehrgang → LehrgangArt (SET NULL on delete)
ALTER TABLE [dbo].[Lehrgang]
    ADD CONSTRAINT [FK_Lehrgang_Art]
        FOREIGN KEY ([ArtId]) REFERENCES [dbo].[LehrgangArt] ([ArtId])
        ON DELETE SET NULL;
GO

-- -----------------------------------------------------------------------------
-- 4. TABELLE: LehrgangEinheit
-- -----------------------------------------------------------------------------
CREATE TABLE [dbo].[LehrgangEinheit] (
    [EinheitId]        INT            NOT NULL IDENTITY(1,1),
    [LehrgangId]       INT            NOT NULL,
    [Datum]            DATE           NOT NULL,
    [UhrzeitVon]       TIME(0)        NULL,
    [UhrzeitBis]       TIME(0)        NULL,
    [Thema]            NVARCHAR(200)  NOT NULL,
    [Inhalt]           NVARCHAR(2000) NULL,
    [DozentPersonId]   INT            NULL,
    [RaumBezeichnung]  NVARCHAR(100)  NULL,
    [EinheitTyp]       TINYINT        NOT NULL CONSTRAINT [DF_LehrgangEinheit_EinheitTyp] DEFAULT 0,
    [Reihenfolge]      INT            NOT NULL CONSTRAINT [DF_LehrgangEinheit_Reihenfolge] DEFAULT 0,
    [Notiz]            NVARCHAR(1000) NULL,
    [CreatedAt]        DATETIME2      NOT NULL CONSTRAINT [DF_LehrgangEinheit_CreatedAt]  DEFAULT SYSUTCDATETIME(),
    [ModifiedAt]       DATETIME2      NOT NULL CONSTRAINT [DF_LehrgangEinheit_ModifiedAt] DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_LehrgangEinheit] PRIMARY KEY CLUSTERED ([EinheitId]),
    CONSTRAINT [CK_LehrgangEinheit_EinheitTyp] CHECK ([EinheitTyp] IN (0, 1, 2, 3)),
    CONSTRAINT [FK_LehrgangEinheit_Lehrgang]
        FOREIGN KEY ([LehrgangId]) REFERENCES [dbo].[Lehrgang] ([LehrgangId])
        ON DELETE CASCADE,
    CONSTRAINT [FK_LehrgangEinheit_Dozent]
        FOREIGN KEY ([DozentPersonId]) REFERENCES [dbo].[Person] ([PersonId])
        ON DELETE SET NULL
);
GO

CREATE INDEX [IX_LehrgangEinheit_LehrgangId_Datum]
    ON [dbo].[LehrgangEinheit] ([LehrgangId], [Datum], [Reihenfolge]);
GO

-- -----------------------------------------------------------------------------
-- 5. TABELLE: LehrgangAenderungsposten
-- -----------------------------------------------------------------------------
CREATE TABLE [dbo].[LehrgangAenderungsposten] (
    [PostenId]             INT            NOT NULL IDENTITY(1,1),
    [BelegNr]              NVARCHAR(20)   NOT NULL,
    [LehrgangId]           INT            NOT NULL,    -- Snapshot, kein FK
    [LehrgangNr]           NVARCHAR(20)   NOT NULL,
    [LehrgangBezeichnung]  NVARCHAR(200)  NOT NULL,
    [Ereignis]             NVARCHAR(100)  NOT NULL,
    [Tabelle]              NVARCHAR(100)  NULL,
    [Feld]                 NVARCHAR(100)  NULL,
    [AlterWert]            NVARCHAR(500)  NULL,
    [NeuerWert]            NVARCHAR(500)  NULL,
    [Zeitstempel]          DATETIME2      NOT NULL CONSTRAINT [DF_LehrgangAend_Zeitstempel] DEFAULT SYSUTCDATETIME(),
    [AusfuehrendUser]      NVARCHAR(100)  NOT NULL,
    [Notiz]                NVARCHAR(1000) NULL,
    CONSTRAINT [PK_LehrgangAenderungsposten] PRIMARY KEY CLUSTERED ([PostenId])
);
GO

-- -----------------------------------------------------------------------------
-- 6. TRIGGER: LehrgangAenderungsposten schützen (kein UPDATE / DELETE)
-- -----------------------------------------------------------------------------
CREATE OR ALTER TRIGGER [dbo].[TR_LehrgangAenderungsposten_Protect]
ON [dbo].[LehrgangAenderungsposten]
AFTER UPDATE, DELETE
AS
BEGIN
    RAISERROR('Änderungsposten dürfen nicht geändert oder gelöscht werden.', 16, 1);
    ROLLBACK TRANSACTION;
END;
GO

-- -----------------------------------------------------------------------------
-- 7. TRIGGER: LehrgangArt – ModifiedAt automatisch setzen
-- -----------------------------------------------------------------------------
CREATE OR ALTER TRIGGER [dbo].[TR_LehrgangArt_ModifiedAt]
ON [dbo].[LehrgangArt]
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE [dbo].[LehrgangArt]
    SET    [ModifiedAt] = SYSUTCDATETIME()
    WHERE  [ArtId] IN (SELECT [ArtId] FROM inserted);
END;
GO

-- -----------------------------------------------------------------------------
-- 8. TRIGGER: LehrgangEinheit – ModifiedAt automatisch setzen
-- -----------------------------------------------------------------------------
CREATE OR ALTER TRIGGER [dbo].[TR_LehrgangEinheit_ModifiedAt]
ON [dbo].[LehrgangEinheit]
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE [dbo].[LehrgangEinheit]
    SET    [ModifiedAt] = SYSUTCDATETIME()
    WHERE  [EinheitId] IN (SELECT [EinheitId] FROM inserted);
END;
GO

-- -----------------------------------------------------------------------------
-- HINWEIS: LehrgangPerson.BetriebId / BetriebName
-- Diese Spalten (Snapshot bei Person-Hinzufügen) existieren bereits in der
-- Tabelle LehrgangPerson aus der vorherigen Migration.
-- Kein ALTER TABLE erforderlich.
-- -----------------------------------------------------------------------------

-- =============================================================================
-- FERTIG
-- =============================================================================
