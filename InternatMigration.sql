-- ============================================================================
-- InternatMigration.sql
-- Migriert InternatBelegung von InternatZimmer auf Raum.
-- Ausführen in SSMS gegen die Schulverwaltungs-Datenbank.
-- ============================================================================

-- 1. RaumTyp "Internatszimmer" sicherstellen
IF NOT EXISTS (SELECT 1 FROM [dbo].[RaumTyp] WHERE [Code] = N'INTERNAT')
    INSERT INTO [dbo].[RaumTyp] ([Code], [Bezeichnung], [Reihenfolge], [IstInternat], [Gesperrt])
    VALUES (N'INTERNAT', N'Internatszimmer', 10, 1, 0);
GO

-- 2. Bestehende InternatZimmer als Raum-Einträge übernehmen
INSERT INTO [dbo].[Raum] ([RaumNr], [Bezeichnung], [RaumTypId], [Kapazitaet], [Gesperrt], [SperrGrund], [Notiz])
SELECT
    iz.[ZimmerNr],
    CASE WHEN iz.[Name] IS NOT NULL AND iz.[Name] != N''
         THEN iz.[ZimmerNr] + N' · ' + iz.[Name]
         ELSE iz.[ZimmerNr] END,
    (SELECT [RaumTypId] FROM [dbo].[RaumTyp] WHERE [Code] = N'INTERNAT'),
    iz.[Kapazitaet],
    iz.[Gesperrt],
    iz.[SperrGrund],
    iz.[Ausstattung]   -- Ausstattung als Notiz übernehmen
FROM [dbo].[InternatZimmer] iz
WHERE NOT EXISTS (
    SELECT 1 FROM [dbo].[Raum] r WHERE r.[RaumNr] = iz.[ZimmerNr]
);
GO

-- 3. InternatBelegung: neue RaumId-Spalte ergänzen
ALTER TABLE [dbo].[InternatBelegung] ADD [RaumId] INT NULL;
ALTER TABLE [dbo].[InternatBelegung] ADD CONSTRAINT [FK_InternatBelegung_Raum]
    FOREIGN KEY ([RaumId]) REFERENCES [dbo].[Raum] ([RaumId])
    ON DELETE NO ACTION;
GO

-- 4. RaumId befüllen anhand ZimmerId → Raum-Mapping über ZimmerNr
UPDATE ib
    SET ib.[RaumId] = r.[RaumId]
FROM [dbo].[InternatBelegung] ib
JOIN [dbo].[InternatZimmer] iz ON iz.[ZimmerId] = ib.[ZimmerId]
JOIN [dbo].[Raum] r            ON r.[RaumNr]  = iz.[ZimmerNr];
GO

-- 5. Alte FK-Constraint auf InternatZimmer droppen
ALTER TABLE [dbo].[InternatBelegung] DROP CONSTRAINT [FK_InternatBelegung_Zimmer];
GO

-- 6. Alten Index auf (ZimmerId, Von, Bis) droppen falls vorhanden
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_InternatBelegung_ZimmerZeitraum'
           AND object_id = OBJECT_ID(N'dbo.InternatBelegung'))
    DROP INDEX [IX_InternatBelegung_ZimmerZeitraum] ON [dbo].[InternatBelegung];
GO

-- 7. Alte ZimmerId-Spalte droppen
ALTER TABLE [dbo].[InternatBelegung] DROP COLUMN [ZimmerId];
GO

-- 8. RaumId NOT NULL setzen
ALTER TABLE [dbo].[InternatBelegung] ALTER COLUMN [RaumId] INT NOT NULL;
GO

-- 9. Index auf (RaumId, Von, Bis) anlegen (entspricht EF-Konfiguration)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_InternatBelegung_RaumZeitraum'
               AND object_id = OBJECT_ID(N'dbo.InternatBelegung'))
    CREATE INDEX [IX_InternatBelegung_RaumZeitraum]
        ON [dbo].[InternatBelegung] ([RaumId], [Von], [Bis]);
GO

-- 10. InternatZimmer-Tabelle droppen
DROP TABLE [dbo].[InternatZimmer];
GO

PRINT N'Migration abgeschlossen. InternatBelegung.RaumId befüllt, InternatZimmer gedroppt.';
