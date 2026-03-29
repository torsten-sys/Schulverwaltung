-- ============================================================================
-- Briefvorlagen-Modul (Briefkopf/Fuß für Dokumente)
-- Idempotentes Script – kann mehrfach ausgeführt werden
-- ============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='Briefvorlage')
BEGIN
    CREATE TABLE Briefvorlage (
        BriefvorlageId  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Briefvorlage PRIMARY KEY,
        Bezeichnung     NVARCHAR(100)     NOT NULL,
        KopfHtml        NVARCHAR(MAX)     NULL,
        FussHtml        NVARCHAR(MAX)     NULL,
        IstStandard     BIT               NOT NULL DEFAULT 0,
        Gesperrt        BIT               NOT NULL DEFAULT 0,
        Notiz           NVARCHAR(MAX)     NULL,
        CreatedAt       DATETIME2         NOT NULL DEFAULT SYSUTCDATETIME(),
        ModifiedAt      DATETIME2         NOT NULL DEFAULT SYSUTCDATETIME()
    )
    EXEC('CREATE TRIGGER TR_Briefvorlage_ModifiedAt ON Briefvorlage AFTER UPDATE AS BEGIN UPDATE Briefvorlage SET ModifiedAt=SYSUTCDATETIME() WHERE BriefvorlageId IN (SELECT BriefvorlageId FROM inserted) END')
    INSERT INTO Briefvorlage (Bezeichnung, IstStandard) VALUES (N'Standard', 1)
    PRINT 'Tabelle Briefvorlage erstellt.'
END
ELSE
    PRINT 'Tabelle Briefvorlage existiert bereits.'

PRINT 'BriefvorlagenSchema.sql: OK'
