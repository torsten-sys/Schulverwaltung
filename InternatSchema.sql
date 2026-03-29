-- =============================================================================
-- InternatSchema.sql
-- Migration: Internat-Modul (InternatZimmer, InternatBelegung,
--            InternatAenderungsposten + Trigger)
-- Erstellt: 2026-03-06
-- =============================================================================

-- -----------------------------------------------------------------------------
-- 1. TABELLE: InternatZimmer
-- -----------------------------------------------------------------------------
CREATE TABLE [dbo].[InternatZimmer] (
    [ZimmerId]    INT            NOT NULL IDENTITY(1,1),
    [ZimmerNr]    NVARCHAR(20)   NOT NULL,
    [ZimmerTyp]   TINYINT        NOT NULL CONSTRAINT [DF_InternatZimmer_Typ]        DEFAULT 0,
    [Kapazitaet]  INT            NOT NULL CONSTRAINT [DF_InternatZimmer_Kap]        DEFAULT 1,
    [Ausstattung] NVARCHAR(MAX)  NULL,
    [Gesperrt]    BIT            NOT NULL CONSTRAINT [DF_InternatZimmer_Gesperrt]   DEFAULT 0,
    [SperrGrund]  NVARCHAR(200)  NULL,
    [Notiz]       NVARCHAR(MAX)  NULL,
    [CreatedAt]   DATETIME2      NOT NULL CONSTRAINT [DF_InternatZimmer_CreatedAt]  DEFAULT SYSUTCDATETIME(),
    [ModifiedAt]  DATETIME2      NOT NULL CONSTRAINT [DF_InternatZimmer_ModifiedAt] DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_InternatZimmer]    PRIMARY KEY CLUSTERED ([ZimmerId]),
    CONSTRAINT [UQ_InternatZimmer_Nr] UNIQUE ([ZimmerNr]),
    CONSTRAINT [CK_InternatZimmer_Typ] CHECK ([ZimmerTyp] IN (0,1,2,3)),
    CONSTRAINT [CK_InternatZimmer_Kap] CHECK ([Kapazitaet] > 0)
);
GO

-- -----------------------------------------------------------------------------
-- 2. TABELLE: InternatBelegung
-- -----------------------------------------------------------------------------
CREATE TABLE [dbo].[InternatBelegung] (
    [BelegungId]          INT            NOT NULL IDENTITY(1,1),
    [ZimmerId]            INT            NOT NULL,
    [PersonId]            INT            NOT NULL,
    [PersonNr]            NVARCHAR(20)   NOT NULL,
    [PersonName]          NVARCHAR(200)  NOT NULL,
    [LehrgangId]          INT            NULL,
    [LehrgangNr]          NVARCHAR(20)   NULL,
    [LehrgangBezeichnung] NVARCHAR(200)  NULL,
    [BelegungsTyp]        TINYINT        NOT NULL CONSTRAINT [DF_InternatBelegung_Typ]       DEFAULT 0,
    [Von]                 DATE           NOT NULL,
    [Bis]                 DATE           NOT NULL,
    [KostenArt]           TINYINT        NOT NULL CONSTRAINT [DF_InternatBelegung_KostenArt] DEFAULT 1,
    [Kosten]              DECIMAL(10,2)  NULL,
    [Bezahlt]             BIT            NOT NULL CONSTRAINT [DF_InternatBelegung_Bezahlt]   DEFAULT 0,
    [BezahltAm]           DATE           NULL,
    [Notiz]               NVARCHAR(MAX)  NULL,
    [CreatedBy]           NVARCHAR(100)  NULL,
    [CreatedAt]           DATETIME2      NOT NULL CONSTRAINT [DF_InternatBelegung_CreatedAt]  DEFAULT SYSUTCDATETIME(),
    [ModifiedAt]          DATETIME2      NOT NULL CONSTRAINT [DF_InternatBelegung_ModifiedAt] DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_InternatBelegung] PRIMARY KEY CLUSTERED ([BelegungId]),
    CONSTRAINT [FK_InternatBelegung_Zimmer]
        FOREIGN KEY ([ZimmerId]) REFERENCES [dbo].[InternatZimmer] ([ZimmerId]),
    CONSTRAINT [FK_InternatBelegung_Lehrgang]
        FOREIGN KEY ([LehrgangId]) REFERENCES [dbo].[Lehrgang] ([LehrgangId])
        ON DELETE SET NULL,
    CONSTRAINT [CK_InternatBelegung_Datum]    CHECK ([Von] <= [Bis]),
    CONSTRAINT [CK_InternatBelegung_Typ]      CHECK ([BelegungsTyp] IN (0,1,2)),
    CONSTRAINT [CK_InternatBelegung_KostenArt] CHECK ([KostenArt] IN (0,1,2))
);
GO

CREATE INDEX [IX_InternatBelegung_ZimmerZeitraum]
    ON [dbo].[InternatBelegung] ([ZimmerId], [Von], [Bis]);
GO

-- -----------------------------------------------------------------------------
-- 3. TABELLE: InternatAenderungsposten
-- -----------------------------------------------------------------------------
CREATE TABLE [dbo].[InternatAenderungsposten] (
    [PostenId]          INT            NOT NULL IDENTITY(1,1),
    [BelegNr]           NVARCHAR(20)   NOT NULL,
    [ZimmerId]          INT            NOT NULL,
    [ZimmerNr]          NVARCHAR(20)   NOT NULL,
    [ZimmerBezeichnung] NVARCHAR(100)  NOT NULL,
    [BelegungId]        INT            NULL,
    [PersonNr]          NVARCHAR(20)   NULL,
    [PersonName]        NVARCHAR(200)  NULL,
    [Ereignis]          NVARCHAR(50)   NOT NULL,
    [Tabelle]           NVARCHAR(100)  NULL,
    [Feld]              NVARCHAR(100)  NULL,
    [AlterWert]         NVARCHAR(MAX)  NULL,
    [NeuerWert]         NVARCHAR(MAX)  NULL,
    [Zeitstempel]       DATETIME2      NOT NULL CONSTRAINT [DF_InternatAend_Zeit] DEFAULT SYSUTCDATETIME(),
    [AusfuehrendUser]   NVARCHAR(100)  NOT NULL,
    [Notiz]             NVARCHAR(MAX)  NULL,
    CONSTRAINT [PK_InternatAenderungsposten] PRIMARY KEY CLUSTERED ([PostenId])
    -- Kein FK – reines Snapshot-Prinzip
);
GO

-- -----------------------------------------------------------------------------
-- 4. TRIGGER: InternatAenderungsposten schützen (unveränderlich)
-- -----------------------------------------------------------------------------
CREATE TRIGGER [dbo].[TR_InternatAenderungsposten_Protect]
ON [dbo].[InternatAenderungsposten]
AFTER UPDATE, DELETE
AS
BEGIN
    RAISERROR('InternatAenderungsposten sind unveraenderlich.', 16, 1);
    ROLLBACK TRANSACTION;
END;
GO

-- -----------------------------------------------------------------------------
-- 5. TRIGGER: ModifiedAt für InternatZimmer
-- -----------------------------------------------------------------------------
CREATE TRIGGER [dbo].[TR_InternatZimmer_ModifiedAt]
ON [dbo].[InternatZimmer]
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE [dbo].[InternatZimmer]
       SET [ModifiedAt] = SYSUTCDATETIME()
     WHERE [ZimmerId] IN (SELECT [ZimmerId] FROM inserted);
END;
GO

-- -----------------------------------------------------------------------------
-- 6. TRIGGER: ModifiedAt für InternatBelegung
-- -----------------------------------------------------------------------------
CREATE TRIGGER [dbo].[TR_InternatBelegung_ModifiedAt]
ON [dbo].[InternatBelegung]
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE [dbo].[InternatBelegung]
       SET [ModifiedAt] = SYSUTCDATETIME()
     WHERE [BelegungId] IN (SELECT [BelegungId] FROM inserted);
END;
GO

-- =============================================================================
-- HINWEIS: Dieses Skript in SSMS auf der Schulverwaltung-Datenbank ausführen,
--          dann deploy.ps1 starten (oder update.bat).
-- =============================================================================
