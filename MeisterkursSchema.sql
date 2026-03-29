-- =============================================================================
-- MeisterkursSchema.sql
-- Meisterkurs-Modul: alle CREATE TABLE Statements, Trigger, NoSerie-Insert
-- Erstellt: 2026-03-06
-- Voraussetzung: LehrgangGrundstruktur.sql bereits ausgeführt
-- =============================================================================

-- -----------------------------------------------------------------------------
-- 0. NoSerie MEISTERBUCHUNG
-- -----------------------------------------------------------------------------
INSERT INTO [dbo].[NoSerie] ([NoSerieCode], [Bezeichnung], [Standardmaessig], [Datumsgeb])
VALUES ('MEISTERBUCHUNG', 'Meisterkurs Buchungsposten', 1, 0);

INSERT INTO [dbo].[NoSerieZeile] (
    [NoSerieCode], [StartingNo], [LastNoUsed], [Prefix],
    [NummerLaenge], [IncrementBy], [Offen])
VALUES ('MEISTERBUCHUNG', 'MB-000001', NULL, 'MB-', 6, 1, 1);
GO

-- -----------------------------------------------------------------------------
-- 1. MeisterAbschnitt
-- -----------------------------------------------------------------------------
CREATE TABLE [dbo].[MeisterAbschnitt] (
    [AbschnittId]   INT            NOT NULL IDENTITY(1,1),
    [LehrgangId]    INT            NOT NULL,
    [Nummer]        INT            NOT NULL,
    [Bezeichnung]   NVARCHAR(200)  NOT NULL,
    [AbschnittTyp]  TINYINT        NOT NULL CONSTRAINT [DF_MeisterAbschnitt_AbschnittTyp] DEFAULT 0,
    [Beschreibung]  NVARCHAR(MAX)  NULL,
    [Reihenfolge]   INT            NOT NULL CONSTRAINT [DF_MeisterAbschnitt_Reihenfolge] DEFAULT 0,
    [Status]        TINYINT        NOT NULL CONSTRAINT [DF_MeisterAbschnitt_Status] DEFAULT 0,
    [CreatedAt]     DATETIME2      NOT NULL CONSTRAINT [DF_MeisterAbschnitt_CreatedAt]  DEFAULT SYSUTCDATETIME(),
    [ModifiedAt]    DATETIME2      NOT NULL CONSTRAINT [DF_MeisterAbschnitt_ModifiedAt] DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_MeisterAbschnitt]       PRIMARY KEY CLUSTERED ([AbschnittId]),
    CONSTRAINT [CK_MeisterAbschnitt_Nummer]      CHECK ([Nummer] BETWEEN 1 AND 10),
    CONSTRAINT [CK_MeisterAbschnitt_AbschnittTyp] CHECK ([AbschnittTyp] IN (0,1,2)),
    CONSTRAINT [UQ_MeisterAbschnitt_LehrgangNummer] UNIQUE ([LehrgangId], [Nummer]),
    CONSTRAINT [FK_MeisterAbschnitt_Lehrgang]
        FOREIGN KEY ([LehrgangId]) REFERENCES [dbo].[Lehrgang] ([LehrgangId])
        ON DELETE CASCADE
);
GO

-- -----------------------------------------------------------------------------
-- 2. MeisterFach
-- -----------------------------------------------------------------------------
CREATE TABLE [dbo].[MeisterFach] (
    [FachId]        INT            NOT NULL IDENTITY(1,1),
    [LehrgangId]    INT            NOT NULL,
    [Bezeichnung]   NVARCHAR(200)  NOT NULL,
    [Gewichtung]    DECIMAL(5,2)   NOT NULL CONSTRAINT [DF_MeisterFach_Gewichtung] DEFAULT 1.00,
    [Reihenfolge]   INT            NOT NULL CONSTRAINT [DF_MeisterFach_Reihenfolge] DEFAULT 0,
    [CreatedAt]     DATETIME2      NOT NULL CONSTRAINT [DF_MeisterFach_CreatedAt]  DEFAULT SYSUTCDATETIME(),
    [ModifiedAt]    DATETIME2      NOT NULL CONSTRAINT [DF_MeisterFach_ModifiedAt] DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_MeisterFach] PRIMARY KEY CLUSTERED ([FachId]),
    CONSTRAINT [UQ_MeisterFach_LehrgangBezeichnung] UNIQUE ([LehrgangId], [Bezeichnung]),
    CONSTRAINT [FK_MeisterFach_Lehrgang]
        FOREIGN KEY ([LehrgangId]) REFERENCES [dbo].[Lehrgang] ([LehrgangId])
        ON DELETE CASCADE
);
GO

-- -----------------------------------------------------------------------------
-- 3. MeisterNote
-- -----------------------------------------------------------------------------
CREATE TABLE [dbo].[MeisterNote] (
    [NoteId]                    INT            NOT NULL IDENTITY(1,1),
    [LehrgangId]                INT            NOT NULL,
    [FachId]                    INT            NOT NULL,
    [PersonId]                  INT            NOT NULL,    -- Snapshot, kein FK
    [PersonNr]                  NVARCHAR(20)   NOT NULL,
    [PersonName]                NVARCHAR(200)  NOT NULL,
    [Note]                      TINYINT        NULL,        -- 1–6, null = noch nicht bewertet
    [BewertendeDozentPersonId]  INT            NULL,        -- Snapshot, kein FK
    [BewertendeDozentName]      NVARCHAR(200)  NULL,
    [BewertungsDatum]           DATE           NULL,
    [Notiz]                     NVARCHAR(MAX)  NULL,
    [CreatedBy]                 NVARCHAR(100)  NULL,
    [CreatedAt]                 DATETIME2      NOT NULL CONSTRAINT [DF_MeisterNote_CreatedAt]  DEFAULT SYSUTCDATETIME(),
    [ModifiedAt]                DATETIME2      NOT NULL CONSTRAINT [DF_MeisterNote_ModifiedAt] DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_MeisterNote] PRIMARY KEY CLUSTERED ([NoteId]),
    CONSTRAINT [CK_MeisterNote_Note] CHECK ([Note] BETWEEN 1 AND 6),
    CONSTRAINT [UQ_MeisterNote_Lehrgang_Fach_Person] UNIQUE ([LehrgangId], [FachId], [PersonId]),
    CONSTRAINT [FK_MeisterNote_Fach]
        FOREIGN KEY ([FachId]) REFERENCES [dbo].[MeisterFach] ([FachId])
        ON DELETE CASCADE,
    CONSTRAINT [FK_MeisterNote_Lehrgang]
        FOREIGN KEY ([LehrgangId]) REFERENCES [dbo].[Lehrgang] ([LehrgangId])
        ON DELETE NO ACTION
);
GO

-- -----------------------------------------------------------------------------
-- 4. MeisterNoteAenderungsposten  (Snapshot – unveränderlich)
-- -----------------------------------------------------------------------------
CREATE TABLE [dbo].[MeisterNoteAenderungsposten] (
    [PostenId]             INT            NOT NULL IDENTITY(1,1),
    [BelegNr]              NVARCHAR(20)   NOT NULL,
    [NoteId]               INT            NOT NULL,    -- Snapshot, kein FK
    [LehrgangId]           INT            NOT NULL,
    [LehrgangNr]           NVARCHAR(20)   NOT NULL,
    [FachBezeichnung]      NVARCHAR(200)  NOT NULL,
    [PersonNr]             NVARCHAR(20)   NOT NULL,
    [PersonName]           NVARCHAR(200)  NOT NULL,
    [AlteNote]             TINYINT        NULL,
    [NeueNote]             TINYINT        NOT NULL,
    [BewertendeDozentName] NVARCHAR(200)  NULL,
    [Zeitstempel]          DATETIME2      NOT NULL CONSTRAINT [DF_MeisterNoteAend_Zeitstempel] DEFAULT SYSUTCDATETIME(),
    [AusfuehrendUser]      NVARCHAR(100)  NOT NULL,
    CONSTRAINT [PK_MeisterNoteAenderungsposten] PRIMARY KEY CLUSTERED ([PostenId])
);
GO

-- -----------------------------------------------------------------------------
-- 5. MeisterFunktion
-- -----------------------------------------------------------------------------
CREATE TABLE [dbo].[MeisterFunktion] (
    [FunktionId]  INT            NOT NULL IDENTITY(1,1),
    [LehrgangId]  INT            NOT NULL,
    [PersonId]    INT            NOT NULL,    -- Snapshot, kein FK
    [PersonNr]    NVARCHAR(20)   NOT NULL,
    [PersonName]  NVARCHAR(200)  NOT NULL,
    [Funktion]    TINYINT        NOT NULL,
    [GueltigAb]   DATE           NOT NULL,
    [GueltigBis]  DATE           NULL,
    [Notiz]       NVARCHAR(MAX)  NULL,
    [CreatedBy]   NVARCHAR(100)  NULL,
    [CreatedAt]   DATETIME2      NOT NULL CONSTRAINT [DF_MeisterFunktion_CreatedAt] DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_MeisterFunktion] PRIMARY KEY CLUSTERED ([FunktionId]),
    CONSTRAINT [CK_MeisterFunktion_Funktion] CHECK ([Funktion] BETWEEN 0 AND 9),
    CONSTRAINT [FK_MeisterFunktion_Lehrgang]
        FOREIGN KEY ([LehrgangId]) REFERENCES [dbo].[Lehrgang] ([LehrgangId])
        ON DELETE CASCADE
);
GO

CREATE INDEX [IX_MeisterFunktion_Aktiv]
    ON [dbo].[MeisterFunktion] ([LehrgangId], [Funktion])
    WHERE [GueltigBis] IS NULL;
GO

-- -----------------------------------------------------------------------------
-- 6. MeisterPatientenZuordnung
-- -----------------------------------------------------------------------------
CREATE TABLE [dbo].[MeisterPatientenZuordnung] (
    [ZuordnungId]                    INT            NOT NULL IDENTITY(1,1),
    [LehrgangId]                     INT            NOT NULL,
    [AbschnittId]                    INT            NOT NULL,
    -- Patient (Snapshot)
    [PatientPersonId]                INT            NOT NULL,
    [PatientPersonNr]                NVARCHAR(20)   NOT NULL,
    [PatientName]                    NVARCHAR(200)  NOT NULL,
    -- MS1 (Snapshot)
    [Meisterschueler1PersonId]       INT            NOT NULL,
    [Meisterschueler1Nr]             NVARCHAR(20)   NOT NULL,
    [Meisterschueler1Name]           NVARCHAR(200)  NOT NULL,
    -- MS2 (Snapshot, optional)
    [Meisterschueler2PersonId]       INT            NULL,
    [Meisterschueler2Nr]             NVARCHAR(20)   NULL,
    [Meisterschueler2Name]           NVARCHAR(200)  NULL,
    [IstErsatzpatient]               BIT            NOT NULL CONSTRAINT [DF_MeisterPZ_Ersatz] DEFAULT 0,
    [PruefungskommissionZugelassen]  BIT            NULL,
    [ZuordnungsStatus]               TINYINT        NOT NULL CONSTRAINT [DF_MeisterPZ_ZStatus] DEFAULT 0,
    [BuchungsStatus]                 TINYINT        NOT NULL CONSTRAINT [DF_MeisterPZ_BStatus] DEFAULT 0,
    [Notiz]                          NVARCHAR(MAX)  NULL,
    [CreatedBy]                      NVARCHAR(100)  NULL,
    [CreatedAt]     DATETIME2 NOT NULL CONSTRAINT [DF_MeisterPZ_CreatedAt]  DEFAULT SYSUTCDATETIME(),
    [ModifiedAt]    DATETIME2 NOT NULL CONSTRAINT [DF_MeisterPZ_ModifiedAt] DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_MeisterPatientenZuordnung] PRIMARY KEY CLUSTERED ([ZuordnungId]),
    CONSTRAINT [CK_MeisterPZ_ZuordnungsStatus] CHECK ([ZuordnungsStatus] IN (0,1,2,3,4)),
    CONSTRAINT [CK_MeisterPZ_BuchungsStatus]   CHECK ([BuchungsStatus]   IN (0,1,2)),
    CONSTRAINT [FK_MeisterPZ_Abschnitt]
        FOREIGN KEY ([AbschnittId]) REFERENCES [dbo].[MeisterAbschnitt] ([AbschnittId])
        ON DELETE CASCADE,
    CONSTRAINT [FK_MeisterPZ_Lehrgang]
        FOREIGN KEY ([LehrgangId]) REFERENCES [dbo].[Lehrgang] ([LehrgangId])
        ON DELETE NO ACTION
);
GO

-- -----------------------------------------------------------------------------
-- 7. MeisterPatientenTermin
-- -----------------------------------------------------------------------------
CREATE TABLE [dbo].[MeisterPatientenTermin] (
    [TerminId]                INT            NOT NULL IDENTITY(1,1),
    [ZuordnungId]             INT            NOT NULL,
    [TerminTyp]               TINYINT        NOT NULL CONSTRAINT [DF_MeisterTermin_Typ]    DEFAULT 0,
    [Datum]                   DATE           NULL,
    [Uhrzeit]                 TIME(0)        NULL,
    [Status]                  TINYINT        NOT NULL CONSTRAINT [DF_MeisterTermin_Status] DEFAULT 0,
    [HilfsmittelUebergeben]   BIT            NULL,
    [NichtUebergebenGrund]    NVARCHAR(500)  NULL,
    [Notiz]                   NVARCHAR(MAX)  NULL,
    [CreatedAt]  DATETIME2 NOT NULL CONSTRAINT [DF_MeisterTermin_CreatedAt]  DEFAULT SYSUTCDATETIME(),
    [ModifiedAt] DATETIME2 NOT NULL CONSTRAINT [DF_MeisterTermin_ModifiedAt] DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_MeisterPatientenTermin]   PRIMARY KEY CLUSTERED ([TerminId]),
    CONSTRAINT [CK_MeisterTermin_Typ]        CHECK ([TerminTyp] IN (0,1,2)),
    CONSTRAINT [CK_MeisterTermin_Status]     CHECK ([Status]    IN (0,1,2,3)),
    CONSTRAINT [UQ_MeisterTermin_Zuordnung_Typ] UNIQUE ([ZuordnungId], [TerminTyp]),
    CONSTRAINT [FK_MeisterTermin_Zuordnung]
        FOREIGN KEY ([ZuordnungId]) REFERENCES [dbo].[MeisterPatientenZuordnung] ([ZuordnungId])
        ON DELETE CASCADE
);
GO

-- -----------------------------------------------------------------------------
-- 8. MeisterPatientenBuchungsposten  (Snapshot – unveränderlich)
-- -----------------------------------------------------------------------------
CREATE TABLE [dbo].[MeisterPatientenBuchungsposten] (
    [PostenId]                       INT            NOT NULL IDENTITY(1,1),
    [BelegNr]                        NVARCHAR(20)   NOT NULL,
    -- Lehrgang-Snapshot
    [LehrgangId]                     INT            NOT NULL,
    [LehrgangNr]                     NVARCHAR(20)   NOT NULL,
    -- Abschnitt-Snapshot
    [AbschnittNummer]                INT            NOT NULL,
    [AbschnittBezeichnung]           NVARCHAR(200)  NOT NULL,
    [BuchungsDatum]                  DATETIME2      NOT NULL CONSTRAINT [DF_MeisterBP_BuchDatum] DEFAULT SYSUTCDATETIME(),
    -- Patient-Snapshot
    [PatientPersonId]                INT            NOT NULL,
    [PatientNr]                      NVARCHAR(20)   NOT NULL,
    [PatientName]                    NVARCHAR(200)  NOT NULL,
    -- MS1-Snapshot
    [Meisterschueler1PersonId]       INT            NOT NULL,
    [MS1Nr]                          NVARCHAR(20)   NOT NULL,
    [MS1Name]                        NVARCHAR(200)  NOT NULL,
    -- MS2-Snapshot
    [Meisterschueler2PersonId]       INT            NULL,
    [MS2Nr]                          NVARCHAR(20)   NULL,
    [MS2Name]                        NVARCHAR(200)  NULL,
    [IstErsatzpatient]               BIT            NOT NULL,
    [PruefungskommissionZugelassen]  BIT            NULL,
    -- Termin-Snapshots
    [Termin1Datum]                   DATE           NULL,
    [Termin1Status]                  TINYINT        NULL,
    [Termin2Datum]                   DATE           NULL,
    [Termin2Status]                  TINYINT        NULL,
    [Termin3Datum]                   DATE           NULL,
    [Termin3Status]                  TINYINT        NULL,
    [HilfsmittelUebergeben]          BIT            NULL,
    [NichtUebergebenGrund]           NVARCHAR(500)  NULL,
    [GebuchtvonUser]                 NVARCHAR(100)  NOT NULL,
    [GebuchtAm]                      DATETIME2      NOT NULL CONSTRAINT [DF_MeisterBP_GebuchtAm] DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_MeisterPatientenBuchungsposten] PRIMARY KEY CLUSTERED ([PostenId])
    -- Kein FK (Snapshot-Prinzip)
);
GO

-- =============================================================================
-- TRIGGER
-- =============================================================================

-- Trigger: MeisterNoteAenderungsposten schützen
CREATE OR ALTER TRIGGER [dbo].[TR_MeisterNoteAenderungsposten_Protect]
ON [dbo].[MeisterNoteAenderungsposten]
AFTER UPDATE, DELETE
AS
BEGIN
    RAISERROR('MeisterNoteAenderungsposten dürfen nicht geändert oder gelöscht werden.', 16, 1);
    ROLLBACK TRANSACTION;
END;
GO

-- Trigger: MeisterPatientenBuchungsposten schützen
CREATE OR ALTER TRIGGER [dbo].[TR_MeisterPatientenBuchungsposten_Protect]
ON [dbo].[MeisterPatientenBuchungsposten]
AFTER UPDATE, DELETE
AS
BEGIN
    RAISERROR('MeisterPatientenBuchungsposten dürfen nicht geändert oder gelöscht werden.', 16, 1);
    ROLLBACK TRANSACTION;
END;
GO

-- ModifiedAt Trigger
CREATE OR ALTER TRIGGER [dbo].[TR_MeisterAbschnitt_ModifiedAt]
ON [dbo].[MeisterAbschnitt] AFTER UPDATE AS BEGIN
    SET NOCOUNT ON;
    UPDATE [dbo].[MeisterAbschnitt] SET [ModifiedAt] = SYSUTCDATETIME()
    WHERE [AbschnittId] IN (SELECT [AbschnittId] FROM inserted);
END;
GO

CREATE OR ALTER TRIGGER [dbo].[TR_MeisterFach_ModifiedAt]
ON [dbo].[MeisterFach] AFTER UPDATE AS BEGIN
    SET NOCOUNT ON;
    UPDATE [dbo].[MeisterFach] SET [ModifiedAt] = SYSUTCDATETIME()
    WHERE [FachId] IN (SELECT [FachId] FROM inserted);
END;
GO

CREATE OR ALTER TRIGGER [dbo].[TR_MeisterNote_ModifiedAt]
ON [dbo].[MeisterNote] AFTER UPDATE AS BEGIN
    SET NOCOUNT ON;
    UPDATE [dbo].[MeisterNote] SET [ModifiedAt] = SYSUTCDATETIME()
    WHERE [NoteId] IN (SELECT [NoteId] FROM inserted);
END;
GO

CREATE OR ALTER TRIGGER [dbo].[TR_MeisterPatientenZuordnung_ModifiedAt]
ON [dbo].[MeisterPatientenZuordnung] AFTER UPDATE AS BEGIN
    SET NOCOUNT ON;
    UPDATE [dbo].[MeisterPatientenZuordnung] SET [ModifiedAt] = SYSUTCDATETIME()
    WHERE [ZuordnungId] IN (SELECT [ZuordnungId] FROM inserted);
END;
GO

CREATE OR ALTER TRIGGER [dbo].[TR_MeisterPatientenTermin_ModifiedAt]
ON [dbo].[MeisterPatientenTermin] AFTER UPDATE AS BEGIN
    SET NOCOUNT ON;
    UPDATE [dbo].[MeisterPatientenTermin] SET [ModifiedAt] = SYSUTCDATETIME()
    WHERE [TerminId] IN (SELECT [TerminId] FROM inserted);
END;
GO

-- =============================================================================
-- HINWEIS
-- =============================================================================
-- SQL in SSMS ausführen, dann update.bat starten.
-- =============================================================================
