-- 1. AnzahlRaten field on Lehrgang
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Lehrgang') AND name = 'AnzahlRaten')
    ALTER TABLE Lehrgang ADD AnzahlRaten INT NULL;

-- 2. EinladungsVorlage (singleton ID=1)
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('EinladungsVorlage') AND type = 'U')
BEGIN
    CREATE TABLE EinladungsVorlage (
        EinladungsVorlageId INT          NOT NULL PRIMARY KEY,
        Anschreiben         NVARCHAR(MAX) NULL,
        ZahlungsplanText    NVARCHAR(MAX) NULL,
        InternatAbschnitt   NVARCHAR(MAX) NULL,
        RatenplanText       NVARCHAR(MAX) NULL,
        Schlusstext         NVARCHAR(MAX) NULL
    );
    INSERT INTO EinladungsVorlage (EinladungsVorlageId, Anschreiben, ZahlungsplanText, InternatAbschnitt, RatenplanText, Schlusstext)
    VALUES (1,
        N'<p>{{Anrede}} {{Nachname}},</p>
<p>wir freuen uns, Sie hiermit zum Meisterlehrgang <strong>{{LehrgangNr}} – {{LehrgangBezeichnung}}</strong> einzuladen.</p>
<p>Der Lehrgang findet vom <strong>{{LehrgangStart}}</strong> bis zum <strong>{{LehrgangEnde}}</strong> statt.</p>',
        N'<p><strong>Lehrgangsgebühr: {{KostenLehrgang}} €</strong></p>
<p>Wir bitten um Überweisung einer Grundzahlung in Höhe von <strong>{{GrundzahlungBetrag}} €</strong> bis spätestens <strong>{{GrundzahlungTermin}}</strong>.</p>
<p>Der Restbetrag von <strong>{{RestbetragNachGrundzahlung}} €</strong> wird in {{AnzahlRaten}} monatlichen Raten à <strong>{{Monatsrate}} €</strong> ab dem <strong>{{BeginnAbbuchung}}</strong> abgebucht.</p>',
        N'<p><strong>Internatsunterbringung ({{InternatZimmerTyp}}):</strong> Zimmer {{InternatZimmerNr}}</p>
<p>Einzug: <strong>{{InternatEinzug}}</strong> &ndash; Auszug: <strong>{{InternatAuszug}}</strong></p>
<p>Kosten Unterkunft: <strong>{{InternatKosten}} €</strong></p>',
        N'<p>Nachfolgend Ihr persönlicher Zahlungsplan:</p>
{{Ratenplan}}',
        N'<p>Bitte senden Sie uns die unterzeichnete Anmeldung bis zum <strong>{{GrundzahlungTermin}}</strong> zurück.</p>
<p>Bei Fragen stehen wir Ihnen gerne zur Verfügung.</p>
<p>Mit freundlichen Grüßen</p>');
END

-- 3. LehrgangEinladung
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('LehrgangEinladung') AND type = 'U')
BEGIN
    CREATE TABLE LehrgangEinladung (
        LehrgangEinladungId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        LehrgangId          INT      NOT NULL REFERENCES Lehrgang(LehrgangId) ON DELETE CASCADE,
        PersonId            INT      NOT NULL,
        ErstelltAm          DATETIME NOT NULL DEFAULT GETDATE(),
        GesendetAm          DATETIME NULL,
        Status              TINYINT  NOT NULL DEFAULT 0
    );
    CREATE UNIQUE INDEX UX_LehrgangEinladung ON LehrgangEinladung(LehrgangId, PersonId);
END
