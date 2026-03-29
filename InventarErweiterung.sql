-- ============================================================================
-- Inventar-Modul-Erweiterung
-- Idempotentes Script – kann mehrfach ausgeführt werden
-- ============================================================================

-- Inventar: neue Felder
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Inventar') AND name='Typ')
    ALTER TABLE Inventar ADD Typ NVARCHAR(100) NULL

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Inventar') AND name='WartungNaechstesDatum')
    ALTER TABLE Inventar ADD WartungNaechstesDatum DATE NULL

-- Organisationseinheit (vor OrgEinheitId FK in Inventar)
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='Organisationseinheit')
BEGIN
    CREATE TABLE Organisationseinheit (
        OrgEinheitId  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Organisationseinheit PRIMARY KEY,
        Code          NVARCHAR(20) NOT NULL,
        Bezeichnung   NVARCHAR(100) NOT NULL,
        Reihenfolge   INT NOT NULL DEFAULT 0,
        Gesperrt      BIT NOT NULL DEFAULT 0,
        CreatedAt     DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        ModifiedAt    DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_Organisationseinheit_Code UNIQUE (Code)
    )
    EXEC('CREATE TRIGGER TR_Organisationseinheit_ModifiedAt ON Organisationseinheit AFTER UPDATE AS BEGIN UPDATE Organisationseinheit SET ModifiedAt=SYSUTCDATETIME() WHERE OrgEinheitId IN (SELECT OrgEinheitId FROM inserted) END')
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Inventar') AND name='OrgEinheitId')
    ALTER TABLE Inventar ADD OrgEinheitId INT NULL CONSTRAINT FK_Inventar_OrgEinheit FOREIGN KEY REFERENCES Organisationseinheit(OrgEinheitId) ON DELETE SET NULL

-- InventarKomponente
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='InventarKomponente')
    CREATE TABLE InventarKomponente (
        KomponenteId  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_InventarKomponente PRIMARY KEY,
        InventarId    INT NOT NULL,
        Bezeichnung   NVARCHAR(200) NOT NULL,
        Menge         DECIMAL(10,2) NOT NULL DEFAULT 1,
        Seriennummer  NVARCHAR(100) NULL,
        Notiz         NVARCHAR(MAX) NULL,
        Reihenfolge   INT NOT NULL DEFAULT 0,
        CreatedAt     DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_InventarKomponente_Inventar FOREIGN KEY (InventarId) REFERENCES Inventar(InventarId) ON DELETE CASCADE
    )

-- InventarWartung
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='InventarWartung')
BEGIN
    CREATE TABLE InventarWartung (
        WartungId       INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_InventarWartung PRIMARY KEY,
        InventarId      INT NOT NULL,
        WartungsDatum   DATE NOT NULL,
        IstExtern       BIT NOT NULL DEFAULT 0,
        BetriebId       INT NULL,
        BetriebName     NVARCHAR(200) NULL,
        Anmerkungen     NVARCHAR(MAX) NULL,
        AusfuehrendUser NVARCHAR(100) NOT NULL,
        ErstelltAm      DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_InventarWartung_Inventar FOREIGN KEY (InventarId) REFERENCES Inventar(InventarId) ON DELETE CASCADE,
        CONSTRAINT FK_InventarWartung_Betrieb  FOREIGN KEY (BetriebId)  REFERENCES Betrieb(BetriebId)  ON DELETE SET NULL
    )
    EXEC('CREATE TRIGGER TR_InventarWartung_Protect ON InventarWartung AFTER UPDATE, DELETE AS BEGIN RAISERROR(''InventarWartung-Eintraege sind unveraenderlich.'',16,1) ROLLBACK TRANSACTION END')
END

-- LehrgangArt: Gesperrt (falls noch nicht vorhanden)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('LehrgangArt') AND name='Gesperrt')
    ALTER TABLE LehrgangArt ADD Gesperrt BIT NOT NULL DEFAULT 0

PRINT 'InventarErweiterung.sql erfolgreich ausgeführt.'
