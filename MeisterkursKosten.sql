-- Meisterkurs Kosten-Felder auf Lehrgang-Tabelle
-- Idempotent: nur hinzufügen wenn noch nicht vorhanden

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Lehrgang') AND name = 'KostenLehrgang')
BEGIN
    ALTER TABLE Lehrgang ADD KostenLehrgang      DECIMAL(10,2) NULL;
    ALTER TABLE Lehrgang ADD KostenInternatDZ    DECIMAL(10,2) NULL;
    ALTER TABLE Lehrgang ADD KostenInternatEZ    DECIMAL(10,2) NULL;
    ALTER TABLE Lehrgang ADD GrundzahlungBetrag  DECIMAL(10,2) NULL;
    ALTER TABLE Lehrgang ADD GrundzahlungTermin  DATE NULL;
    ALTER TABLE Lehrgang ADD BeginnAbbuchung     DATE NULL;
    ALTER TABLE Lehrgang ADD KautionWerkstatt    DECIMAL(10,2) NULL;
    ALTER TABLE Lehrgang ADD KautionInternat     DECIMAL(10,2) NULL;
    ALTER TABLE Lehrgang ADD Verwaltungspauschale DECIMAL(10,2) NULL;
    PRINT 'Meisterkurs Kosten-Felder hinzugefügt.';
END
ELSE
BEGIN
    PRINT 'Meisterkurs Kosten-Felder bereits vorhanden – keine Änderung.';
END
