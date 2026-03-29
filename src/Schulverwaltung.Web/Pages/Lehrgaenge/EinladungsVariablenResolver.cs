using Schulverwaltung.Domain.Entities;
using System.Text;

namespace Schulverwaltung.Web.Pages.Lehrgaenge;

public static class EinladungsVariablenResolver
{
    public static Dictionary<string, string> Erstelle(
        Lehrgang lehrgang, Person person, InternatBelegung? belegung)
    {
        var v = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Person
        v["Anrede"]     = person.Anrede ?? "";
        v["Vorname"]    = person.Vorname;
        v["Nachname"]   = person.Nachname;
        v["VollerName"] = person.VollerName;
        v["Strasse"]    = person.Strasse ?? "";
        v["PLZ"]        = person.PLZ ?? "";
        v["Ort"]        = person.Ort ?? "";
        v["Email"]      = person.Email ?? "";

        // Lehrgang
        v["LehrgangNr"]          = lehrgang.LehrgangNr;
        v["LehrgangBezeichnung"] = lehrgang.Bezeichnung;
        v["LehrgangStart"]       = lehrgang.StartDatum.ToString("dd.MM.yyyy");
        v["LehrgangEnde"]        = lehrgang.EndDatum?.ToString("dd.MM.yyyy") ?? "–";

        // Kosten
        v["KostenLehrgang"]       = Fmt(lehrgang.KostenLehrgang);
        v["GrundzahlungBetrag"]   = Fmt(lehrgang.GrundzahlungBetrag);
        v["GrundzahlungTermin"]   = lehrgang.GrundzahlungTermin?.ToString("dd.MM.yyyy") ?? "–";
        v["BeginnAbbuchung"]      = lehrgang.BeginnAbbuchung?.ToString("dd.MM.yyyy") ?? "–";
        v["KautionWerkstatt"]     = Fmt(lehrgang.KautionWerkstatt);
        v["KautionInternat"]      = Fmt(lehrgang.KautionInternat);
        v["Verwaltungspauschale"] = Fmt(lehrgang.Verwaltungspauschale);

        // Berechnungen
        var kosten       = lehrgang.KostenLehrgang ?? 0m;
        var grundz       = lehrgang.GrundzahlungBetrag ?? 0m;
        var restbetrag   = kosten - grundz;
        var anzahlRaten  = lehrgang.AnzahlRaten ?? 6;
        var monatsrate   = anzahlRaten > 0 ? Math.Round(restbetrag / anzahlRaten, 2) : 0m;

        v["RestbetragNachGrundzahlung"] = Fmt(restbetrag);
        v["AnzahlRaten"]                = anzahlRaten.ToString();
        v["Monatsrate"]                 = Fmt(monatsrate);
        v["Ratenplan"]                  = BaueRatenplanHtml(lehrgang, restbetrag, anzahlRaten, monatsrate);

        // Internat
        if (belegung != null)
        {
            var typBez = belegung.Raum?.RaumTyp?.Bezeichnung ?? "";
            var istEZ  = typBez.Contains("Einzel", StringComparison.OrdinalIgnoreCase);
            v["InternatZimmerNr"]  = belegung.Raum?.RaumNr ?? "–";
            v["InternatZimmerTyp"] = istEZ ? "Einzelzimmer" : "Doppelzimmer";
            v["InternatKosten"]    = Fmt(istEZ ? lehrgang.KostenInternatEZ : lehrgang.KostenInternatDZ);
            v["InternatEinzug"]    = belegung.Von.ToString("dd.MM.yyyy");
            v["InternatAuszug"]    = belegung.Bis.ToString("dd.MM.yyyy");
        }
        else
        {
            v["InternatZimmerNr"]  = "–";
            v["InternatZimmerTyp"] = "–";
            v["InternatKosten"]    = "–";
            v["InternatEinzug"]    = "–";
            v["InternatAuszug"]    = "–";
        }

        return v;
    }

    public static string Ersetze(string? template, Dictionary<string, string> vars)
    {
        if (string.IsNullOrEmpty(template)) return "";
        foreach (var kv in vars)
            template = template.Replace("{{" + kv.Key + "}}", kv.Value,
                StringComparison.OrdinalIgnoreCase);
        return template;
    }

    private static string BaueRatenplanHtml(
        Lehrgang lg, decimal restbetrag, int anzahl, decimal rate)
    {
        if (lg.BeginnAbbuchung == null || anzahl <= 0)
            return "<em>– kein Ratenplan konfiguriert –</em>";

        var sb = new StringBuilder();
        sb.Append("<table style=\"border-collapse:collapse;width:auto;min-width:400px;font-size:13px;margin:8px 0\">");
        sb.Append("<thead><tr style=\"background:#1F3864;color:#fff\">");
        sb.Append("<th style=\"padding:5px 12px;text-align:left\">Rate</th>");
        sb.Append("<th style=\"padding:5px 12px;text-align:left\">Fälligkeitsdatum</th>");
        sb.Append("<th style=\"padding:5px 12px;text-align:right\">Betrag</th>");
        sb.Append("</tr></thead><tbody>");

        var startDatum = lg.BeginnAbbuchung.Value;
        for (int i = 1; i <= anzahl; i++)
        {
            // last rate absorbs rounding difference
            var betrag = (i == anzahl) ? (restbetrag - rate * (anzahl - 1)) : rate;
            var bg     = i % 2 == 0 ? "background:#f3f2f1" : "";
            var datum  = startDatum.AddMonths(i - 1);
            sb.Append($"<tr style=\"{bg}\">");
            sb.Append($"<td style=\"padding:4px 12px\">{i}. Rate</td>");
            sb.Append($"<td style=\"padding:4px 12px\">{datum:dd.MM.yyyy}</td>");
            sb.Append($"<td style=\"padding:4px 12px;text-align:right\">{betrag:N2}&nbsp;€</td>");
            sb.Append("</tr>");
        }

        sb.Append("<tr style=\"border-top:2px solid #1F3864;font-weight:600\">");
        sb.Append("<td colspan=\"2\" style=\"padding:5px 12px\">Gesamt Ratenzahlung</td>");
        sb.Append($"<td style=\"padding:5px 12px;text-align:right\">{restbetrag:N2}&nbsp;€</td>");
        sb.Append("</tr></tbody></table>");
        return sb.ToString();
    }

    private static string Fmt(decimal? v)
        => v.HasValue ? v.Value.ToString("N2") : "–";
}
