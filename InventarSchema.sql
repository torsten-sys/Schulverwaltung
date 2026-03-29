-- =============================================================================
--  InventarSchema.sql
--  Idempotentes SQL-Skript für das Inventar/Raum-Modul der Schulverwaltung
--  Erstellt: 2026-03-12
-- =============================================================================

SET NOCOUNT ON;
GO

-- =============================================================================
--  1. TABELLE: RaumTyp
-- =============================================================================
IF OBJECT_ID('dbo.RaumTyp', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.RaumTyp (
        RaumTypId   INT           NOT NULL IDENTITY(1,1),
        Code        NVARCHAR(20)  NOT NULL,
        Bezeichnung NVARCHAR(100) NOT NULL,
        Reihenfolge INT           NOT NULL DEFAULT 0,
        IstInternat BIT           NOT NULL DEFAULT 0,
        Gesperrt    BIT           NOT NULL DEFAULT 0,
        CreatedAt   DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
        ModifiedAt  DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_RaumTyp        PRIMARY KEY (RaumTypId),
        CONSTRAINT UQ_RaumTyp_Code   UNIQUE      (Code)
    );
    PRINT 'Tabelle RaumTyp erstellt.';
END
ELSE
    PRINT 'Tabelle RaumTyp bereits vorhanden – übersprungen.';
GO

-- Trigger: ModifiedAt aktualisieren
IF OBJECT_ID('dbo.TR_RaumTyp_ModifiedAt', 'TR') IS NULL
BEGIN
    EXEC('
    CREATE TRIGGER dbo.TR_RaumTyp_ModifiedAt
    ON dbo.RaumTyp
    AFTER UPDATE
    AS
    BEGIN
        SET NOCOUNT ON;
        UPDATE dbo.RaumTyp
        SET    ModifiedAt = SYSUTCDATETIME()
        FROM   dbo.RaumTyp rt
        INNER JOIN inserted i ON rt.RaumTypId = i.RaumTypId;
    END
    ');
    PRINT 'Trigger TR_RaumTyp_ModifiedAt erstellt.';
END
GO

-- =============================================================================
--  2. TABELLE: Raum
-- =============================================================================
IF OBJECT_ID('dbo.Raum', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Raum (
        RaumId      INT           NOT NULL IDENTITY(1,1),
        RaumNr      NVARCHAR(20)  NOT NULL,
        Bezeichnung NVARCHAR(100) NOT NULL,
        RaumTypId   INT           NOT NULL,
        Kapazitaet  INT           NULL,
        Gesperrt    BIT           NOT NULL DEFAULT 0,
        SperrGrund  NVARCHAR(200) NULL,
        Notiz       NVARCHAR(MAX) NULL,
        CreatedAt   DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
        ModifiedAt  DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_Raum          PRIMARY KEY (RaumId),
        CONSTRAINT FK_Raum_RaumTyp  FOREIGN KEY (RaumTypId)
            REFERENCES dbo.RaumTyp (RaumTypId)
            ON DELETE NO ACTION
    );
    PRINT 'Tabelle Raum erstellt.';
END
ELSE
    PRINT 'Tabelle Raum bereits vorhanden – übersprungen.';
GO

-- Trigger: ModifiedAt aktualisieren
IF OBJECT_ID('dbo.TR_Raum_ModifiedAt', 'TR') IS NULL
BEGIN
    EXEC('
    CREATE TRIGGER dbo.TR_Raum_ModifiedAt
    ON dbo.Raum
    AFTER UPDATE
    AS
    BEGIN
        SET NOCOUNT ON;
        UPDATE dbo.Raum
        SET    ModifiedAt = SYSUTCDATETIME()
        FROM   dbo.Raum r
        INNER JOIN inserted i ON r.RaumId = i.RaumId;
    END
    ');
    PRINT 'Trigger TR_Raum_ModifiedAt erstellt.';
END
GO

-- =============================================================================
--  3. TABELLE: InventarKategorie
-- =============================================================================
IF OBJECT_ID('dbo.InventarKategorie', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.InventarKategorie (
        KategorieId INT           NOT NULL IDENTITY(1,1),
        Code        NVARCHAR(20)  NOT NULL,
        Bezeichnung NVARCHAR(100) NOT NULL,
        Reihenfolge INT           NOT NULL DEFAULT 0,
        Gesperrt    BIT           NOT NULL DEFAULT 0,
        CreatedAt   DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
        ModifiedAt  DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_InventarKategorie      PRIMARY KEY (KategorieId),
        CONSTRAINT UQ_InventarKategorie_Code UNIQUE      (Code)
    );
    PRINT 'Tabelle InventarKategorie erstellt.';
END
ELSE
    PRINT 'Tabelle InventarKategorie bereits vorhanden – übersprungen.';
GO

-- Trigger: ModifiedAt aktualisieren
IF OBJECT_ID('dbo.TR_InventarKategorie_ModifiedAt', 'TR') IS NULL
BEGIN
    EXEC('
    CREATE TRIGGER dbo.TR_InventarKategorie_ModifiedAt
    ON dbo.InventarKategorie
    AFTER UPDATE
    AS
    BEGIN
        SET NOCOUNT ON;
        UPDATE dbo.InventarKategorie
        SET    ModifiedAt = SYSUTCDATETIME()
        FROM   dbo.InventarKategorie ik
        INNER JOIN inserted i ON ik.KategorieId = i.KategorieId;
    END
    ');
    PRINT 'Trigger TR_InventarKategorie_ModifiedAt erstellt.';
END
GO

-- =============================================================================
--  4. TABELLE: Inventar
-- =============================================================================
IF OBJECT_ID('dbo.Inventar', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Inventar (
        InventarId             INT             NOT NULL IDENTITY(1,1),
        InventarNr             NVARCHAR(20)    NOT NULL,
        Bezeichnung            NVARCHAR(200)   NOT NULL,
        KategorieId            INT             NOT NULL,
        Seriennummer           NVARCHAR(100)   NULL,
        Anschaffungsdatum      DATE            NULL,
        Anschaffungskosten     DECIMAL(10,2)   NULL,
        RaumId                 INT             NULL,
        PersonId               INT             NULL,
        Zustand                TINYINT         NOT NULL DEFAULT 0,
        WartungStartdatum      DATE            NULL,
        WartungIntervallMonate INT             NULL,
        WartungLetztesDatum    DATE            NULL,
        Gesperrt               BIT             NOT NULL DEFAULT 0,
        SperrGrund             NVARCHAR(200)   NULL,
        Notiz                  NVARCHAR(MAX)   NULL,
        CreatedAt              DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
        ModifiedAt             DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_Inventar              PRIMARY KEY (InventarId),
        CONSTRAINT UQ_Inventar_Nr           UNIQUE      (InventarNr),
        CONSTRAINT CK_Inventar_Zustand      CHECK       ([Zustand] BETWEEN 0 AND 3),
        CONSTRAINT FK_Inventar_Kategorie    FOREIGN KEY (KategorieId)
            REFERENCES dbo.InventarKategorie (KategorieId)
            ON DELETE NO ACTION,
        CONSTRAINT FK_Inventar_Raum         FOREIGN KEY (RaumId)
            REFERENCES dbo.Raum (RaumId)
            ON DELETE SET NULL,
        CONSTRAINT FK_Inventar_Person       FOREIGN KEY (PersonId)
            REFERENCES dbo.Person (PersonId)
            ON DELETE SET NULL
    );
    PRINT 'Tabelle Inventar erstellt.';
END
ELSE
    PRINT 'Tabelle Inventar bereits vorhanden – übersprungen.';
GO

-- Trigger: ModifiedAt aktualisieren
IF OBJECT_ID('dbo.TR_Inventar_ModifiedAt', 'TR') IS NULL
BEGIN
    EXEC('
    CREATE TRIGGER dbo.TR_Inventar_ModifiedAt
    ON dbo.Inventar
    AFTER UPDATE
    AS
    BEGIN
        SET NOCOUNT ON;
        UPDATE dbo.Inventar
        SET    ModifiedAt = SYSUTCDATETIME()
        FROM   dbo.Inventar inv
        INNER JOIN inserted i ON inv.InventarId = i.InventarId;
    END
    ');
    PRINT 'Trigger TR_Inventar_ModifiedAt erstellt.';
END
GO

-- =============================================================================
--  5. TABELLE: InventarAenderungsposten
-- =============================================================================
IF OBJECT_ID('dbo.InventarAenderungsposten', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.InventarAenderungsposten (
        PostenId        INT           NOT NULL IDENTITY(1,1),
        BelegNr         NVARCHAR(20)  NOT NULL,
        InventarId      INT           NOT NULL,   -- Snapshot – kein FK
        InventarNr      NVARCHAR(20)  NOT NULL,
        Bezeichnung     NVARCHAR(200) NOT NULL,
        Ereignis        NVARCHAR(50)  NOT NULL,
        Feld            NVARCHAR(100) NULL,
        AlterWert       NVARCHAR(MAX) NULL,
        NeuerWert       NVARCHAR(MAX) NULL,
        Zeitstempel     DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
        AusfuehrendUser NVARCHAR(100) NOT NULL,
        CreatedAt       DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
        ModifiedAt      DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_InventarAenderungsposten PRIMARY KEY (PostenId)
        -- Kein FK auf Inventar – Snapshot-Prinzip
    );
    PRINT 'Tabelle InventarAenderungsposten erstellt.';
END
ELSE
    PRINT 'Tabelle InventarAenderungsposten bereits vorhanden – übersprungen.';
GO

-- Trigger: Schutz vor nachträglicher Änderung (kein UPDATE erlaubt)
IF OBJECT_ID('dbo.TR_InventarAenderungsposten_Protect', 'TR') IS NULL
BEGIN
    EXEC('
    CREATE TRIGGER dbo.TR_InventarAenderungsposten_Protect
    ON dbo.InventarAenderungsposten
    AFTER UPDATE, DELETE
    AS
    BEGIN
        SET NOCOUNT ON;
        RAISERROR(''Aenderungsposten duerfen nicht veraendert oder geloescht werden.'', 16, 1);
        ROLLBACK TRANSACTION;
    END
    ');
    PRINT 'Trigger TR_InventarAenderungsposten_Protect erstellt.';
END
GO

-- =============================================================================
--  6. ALTER TABLE AppBenutzer – Spalte DarfInventarVerwalten
-- =============================================================================
IF NOT EXISTS (
    SELECT 1
    FROM   sys.columns
    WHERE  object_id = OBJECT_ID('dbo.AppBenutzer')
    AND    name      = 'DarfInventarVerwalten'
)
BEGIN
    ALTER TABLE dbo.AppBenutzer
        ADD DarfInventarVerwalten BIT NOT NULL DEFAULT 0;
    PRINT 'Spalte DarfInventarVerwalten zu AppBenutzer hinzugefügt.';
END
ELSE
    PRINT 'Spalte DarfInventarVerwalten bereits vorhanden – übersprungen.';
GO

-- =============================================================================
--  7. MIGRATION: RaumTyp-Stammdaten einfügen
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM dbo.RaumTyp WHERE Code = 'INTERNAT')
BEGIN
    INSERT INTO dbo.RaumTyp (Code, Bezeichnung, Reihenfolge, IstInternat, Gesperrt)
    VALUES ('INTERNAT', 'Internat (Zimmer)', 10, 1, 0);
    PRINT 'RaumTyp INTERNAT eingefügt.';
END

IF NOT EXISTS (SELECT 1 FROM dbo.RaumTyp WHERE Code = 'BUERO')
BEGIN
    INSERT INTO dbo.RaumTyp (Code, Bezeichnung, Reihenfolge, IstInternat, Gesperrt)
    VALUES ('BUERO', 'Büro', 20, 0, 0);
    PRINT 'RaumTyp BUERO eingefügt.';
END

IF NOT EXISTS (SELECT 1 FROM dbo.RaumTyp WHERE Code = 'WERKSTATT')
BEGIN
    INSERT INTO dbo.RaumTyp (Code, Bezeichnung, Reihenfolge, IstInternat, Gesperrt)
    VALUES ('WERKSTATT', 'Werkstatt', 30, 0, 0);
    PRINT 'RaumTyp WERKSTATT eingefügt.';
END

IF NOT EXISTS (SELECT 1 FROM dbo.RaumTyp WHERE Code = 'KUECHE')
BEGIN
    INSERT INTO dbo.RaumTyp (Code, Bezeichnung, Reihenfolge, IstInternat, Gesperrt)
    VALUES ('KUECHE', 'Küche', 40, 0, 0);
    PRINT 'RaumTyp KUECHE eingefügt.';
END

IF NOT EXISTS (SELECT 1 FROM dbo.RaumTyp WHERE Code = 'SCHULUNG')
BEGIN
    INSERT INTO dbo.RaumTyp (Code, Bezeichnung, Reihenfolge, IstInternat, Gesperrt)
    VALUES ('SCHULUNG', 'Schulungsraum', 50, 0, 0);
    PRINT 'RaumTyp SCHULUNG eingefügt.';
END

IF NOT EXISTS (SELECT 1 FROM dbo.RaumTyp WHERE Code = 'SONSTIGES')
BEGIN
    INSERT INTO dbo.RaumTyp (Code, Bezeichnung, Reihenfolge, IstInternat, Gesperrt)
    VALUES ('SONSTIGES', 'Sonstiges', 99, 0, 0);
    PRINT 'RaumTyp SONSTIGES eingefügt.';
END
GO

-- =============================================================================
--  8. MIGRATION: InternatBelegung – Spalte RaumId hinzufügen
-- =============================================================================
IF NOT EXISTS (
    SELECT 1
    FROM   sys.columns
    WHERE  object_id = OBJECT_ID('dbo.InternatBelegung')
    AND    name      = 'RaumId'
)
BEGIN
    ALTER TABLE dbo.InternatBelegung
        ADD RaumId INT NULL;
    PRINT 'Spalte RaumId zu InternatBelegung hinzugefügt.';
END
ELSE
    PRINT 'Spalte RaumId in InternatBelegung bereits vorhanden – übersprungen.';
GO

-- FK InternatBelegung → Raum (nur anlegen wenn noch nicht vorhanden)
IF OBJECT_ID('dbo.FK_InternatBelegung_Raum', 'F') IS NULL
    AND OBJECT_ID('dbo.Raum', 'U') IS NOT NULL
BEGIN
    ALTER TABLE dbo.InternatBelegung
        ADD CONSTRAINT FK_InternatBelegung_Raum
            FOREIGN KEY (RaumId)
            REFERENCES dbo.Raum (RaumId)
            ON DELETE NO ACTION;
    PRINT 'FK FK_InternatBelegung_Raum erstellt.';
END
GO

-- =============================================================================
--  HINWEIS: Datenmigration InternatZimmer → Raum
-- =============================================================================
-- Nach dem Befüllen der Raum-Tabelle mit den Daten aus InternatZimmer
-- muss RaumId in InternatBelegung manuell oder per Script befüllt werden:
--
-- SCHRITT 1: Räume aus InternatZimmer migrieren (Beispiel):
-- INSERT INTO dbo.Raum (RaumNr, Bezeichnung, RaumTypId, Kapazitaet, Gesperrt, SperrGrund, Notiz)
-- SELECT iz.ZimmerNr,
--        ISNULL(iz.Name, iz.ZimmerNr),
--        rt.RaumTypId,  -- RaumTypId des INTERNAT-Typs
--        iz.Kapazitaet,
--        iz.Gesperrt,
--        iz.SperrGrund,
--        iz.Notiz
-- FROM   dbo.InternatZimmer iz
-- CROSS JOIN (SELECT RaumTypId FROM dbo.RaumTyp WHERE Code = 'INTERNAT') rt
-- WHERE  NOT EXISTS (SELECT 1 FROM dbo.Raum WHERE RaumNr = iz.ZimmerNr);
--
-- SCHRITT 2: RaumId in InternatBelegung befüllen:
-- UPDATE ib
-- SET    ib.RaumId = r.RaumId
-- FROM   dbo.InternatBelegung ib
-- INNER JOIN dbo.InternatZimmer iz ON ib.ZimmerId = iz.ZimmerId
-- INNER JOIN dbo.Raum           r  ON r.RaumNr    = iz.ZimmerNr;
--
-- SCHRITT 3: Erst nach Prüfung – alten FK und Spalte entfernen:
-- ALTER TABLE dbo.InternatBelegung DROP CONSTRAINT FK_InternatBelegung_InternatZimmer; -- ggf. anderen Namen prüfen
-- ALTER TABLE dbo.InternatBelegung DROP COLUMN ZimmerId;
-- =============================================================================

-- =============================================================================
--  9. NUMMERNSERIE: NoSerie-Einträge für INVENTAR, RAUM, AENDERUNG
-- =============================================================================

-- NoSerie Kopf: INVENTAR
IF NOT EXISTS (SELECT 1 FROM dbo.NoSerie WHERE NoSerieCode = 'INVENTAR')
BEGIN
    INSERT INTO dbo.NoSerie (NoSerieCode, Bezeichnung, Standardmaessig, Datumsgeb)
    VALUES ('INVENTAR', 'Inventar-Nummern', 1, 0);
    PRINT 'NoSerie INVENTAR eingefügt.';
END

-- NoSerie Zeile: INVENTAR
IF NOT EXISTS (SELECT 1 FROM dbo.NoSerieZeile WHERE NoSerieCode = 'INVENTAR' AND StartingNo = 'INV00001')
BEGIN
    INSERT INTO dbo.NoSerieZeile (NoSerieCode, StartingNo, EndingNo, LastNoUsed, IncrementBy, AllowGaps, Offen, Prefix, NummerLaenge)
    VALUES ('INVENTAR', 'INV00001', 'INV99999', NULL, 1, 0, 1, 'INV', 5);
    PRINT 'NoSerieZeile für INVENTAR eingefügt.';
END

-- NoSerie Kopf: RAUM
IF NOT EXISTS (SELECT 1 FROM dbo.NoSerie WHERE NoSerieCode = 'RAUM')
BEGIN
    INSERT INTO dbo.NoSerie (NoSerieCode, Bezeichnung, Standardmaessig, Datumsgeb)
    VALUES ('RAUM', 'Raum-Nummern', 1, 0);
    PRINT 'NoSerie RAUM eingefügt.';
END

-- NoSerie Zeile: RAUM
IF NOT EXISTS (SELECT 1 FROM dbo.NoSerieZeile WHERE NoSerieCode = 'RAUM' AND StartingNo = 'RAUM0001')
BEGIN
    INSERT INTO dbo.NoSerieZeile (NoSerieCode, StartingNo, EndingNo, LastNoUsed, IncrementBy, AllowGaps, Offen, Prefix, NummerLaenge)
    VALUES ('RAUM', 'RAUM0001', 'RAUM9999', NULL, 1, 0, 1, 'RAUM', 4);
    PRINT 'NoSerieZeile für RAUM eingefügt.';
END

-- NoSerie Kopf: AENDERUNG (gemeinsam für alle Änderungsposten-Belegnummern)
IF NOT EXISTS (SELECT 1 FROM dbo.NoSerie WHERE NoSerieCode = 'AENDERUNG')
BEGIN
    INSERT INTO dbo.NoSerie (NoSerieCode, Bezeichnung, Standardmaessig, Datumsgeb)
    VALUES ('AENDERUNG', 'Änderungsposten-Belegnummern', 1, 0);
    PRINT 'NoSerie AENDERUNG eingefügt.';
END

-- NoSerie Zeile: AENDERUNG
IF NOT EXISTS (SELECT 1 FROM dbo.NoSerieZeile WHERE NoSerieCode = 'AENDERUNG' AND StartingNo = 'AEP00001')
BEGIN
    INSERT INTO dbo.NoSerieZeile (NoSerieCode, StartingNo, EndingNo, LastNoUsed, IncrementBy, AllowGaps, Offen, Prefix, NummerLaenge)
    VALUES ('AENDERUNG', 'AEP00001', 'AEP99999', NULL, 1, 0, 1, 'AEP', 5);
    PRINT 'NoSerieZeile für AENDERUNG eingefügt.';
END
GO

PRINT '=== InventarSchema.sql abgeschlossen. ===';
GO
