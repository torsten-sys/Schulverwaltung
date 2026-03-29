using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Schulverwaltung.Domain.Entities;
using Schulverwaltung.Domain.Enums;
using Schulverwaltung.Infrastructure.Persistence;

namespace Schulverwaltung.Web.Pages.Lehrgaenge;

public class IndexModel : PageModel
{
    private readonly SchulverwaltungDbContext _db;
    public IndexModel(SchulverwaltungDbContext db) => _db = db;

    public bool DarfBearbeiten =>
        HttpContext.Items["AppBenutzer"] is AppBenutzer b && b.AppRolle >= 1 && !b.Gesperrt;

    [BindProperty(SupportsGet = true)] public string? Suche        { get; set; }
    [BindProperty(SupportsGet = true)] public string? TypFilter    { get; set; }
    [BindProperty(SupportsGet = true)] public string? StatusFilter { get; set; }
    [BindProperty(SupportsGet = true)] public int?    ArtFilter    { get; set; }

    public IReadOnlyList<LehrgangZeile> Lehrgaenge  { get; set; } = [];
    public IReadOnlyList<LehrgangArt>   LehrgangArten { get; set; } = [];

    public async Task OnGetAsync()
    {
        LehrgangArten = await _db.LehrgangArten.OrderBy(a => a.Reihenfolge).ToListAsync();

        var query = _db.Lehrgaenge.Include(l => l.Art).AsQueryable();

        if (!string.IsNullOrWhiteSpace(TypFilter) && byte.TryParse(TypFilter, out var tb))
            query = query.Where(l => (byte)l.LehrgangTyp == tb);

        if (!string.IsNullOrWhiteSpace(StatusFilter) && byte.TryParse(StatusFilter, out var sb))
            query = query.Where(l => (byte)l.Status == sb);

        if (ArtFilter.HasValue)
            query = query.Where(l => l.ArtId == ArtFilter.Value);

        if (!string.IsNullOrWhiteSpace(Suche))
        {
            var s = Suche.Trim().ToLower();
            query = query.Where(l => l.LehrgangNr.ToLower().Contains(s) ||
                                     l.Bezeichnung.ToLower().Contains(s));
        }

        var lehrgaenge = await query.OrderByDescending(l => l.StartDatum).ToListAsync();

        var ids = lehrgaenge.Select(l => l.LehrgangId).ToList();
        var counts = await _db.LehrgangPersonen
            .Where(lp => ids.Contains(lp.LehrgangId))
            .GroupBy(lp => new { lp.LehrgangId, lp.Rolle })
            .Select(g => new { g.Key.LehrgangId, g.Key.Rolle, Anzahl = g.Count() })
            .ToListAsync();

        Lehrgaenge = lehrgaenge.Select(l => new LehrgangZeile(
            l.LehrgangId, l.LehrgangNr, (byte)l.LehrgangTyp, l.Bezeichnung,
            l.Art?.Bezeichnung,
            l.StartDatum, l.EndDatum,
            l.MinTeilnehmer, l.MaxTeilnehmer,
            l.Gebuehren,
            (byte)l.Status,
            l.Status switch {
                LehrgangStatus.Planung        => "Planung",
                LehrgangStatus.AnmeldungOffen => "Anmeldung offen",
                LehrgangStatus.Aktiv          => "Aktiv",
                LehrgangStatus.Abgeschlossen  => "Abgeschlossen",
                LehrgangStatus.Storniert      => "Storniert",
                _ => ""
            },
            counts.Where(c => c.LehrgangId == l.LehrgangId && c.Rolle == LehrgangPersonRolle.Teilnehmer).Sum(c => c.Anzahl),
            l.MaxTeilnehmer,
            counts.Where(c => c.LehrgangId == l.LehrgangId && c.Rolle == LehrgangPersonRolle.Dozent).Sum(c => c.Anzahl)
        )).ToList();
    }

    public record LehrgangZeile(
        int LehrgangId, string LehrgangNr, byte LehrgangTyp, string Bezeichnung,
        string? ArtBezeichnung,
        DateOnly StartDatum, DateOnly? EndDatum,
        int MinTeilnehmer, int MaxTeilnehmer,
        decimal? Gebuehren,
        byte Status, string StatusText,
        int AnzahlTeilnehmer, int MaxTN, int AnzahlDozenten);
}
