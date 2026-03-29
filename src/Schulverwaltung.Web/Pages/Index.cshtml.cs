using Microsoft.AspNetCore.Mvc.RazorPages;
using Schulverwaltung.Domain.Entities;

namespace Schulverwaltung.Web.Pages;

public class IndexModel : PageModel
{
    public string? DisplayName    { get; set; }
    public string  AdBenutzername { get; set; } = "";
    public byte    AppRolle       { get; set; } = 0;

    public void OnGet()
    {
        var benutzer = HttpContext.Items["AppBenutzer"] as AppBenutzer;
        if (benutzer != null)
        {
            DisplayName    = benutzer.DisplayName;
            AdBenutzername = benutzer.AdBenutzername;
            AppRolle       = benutzer.AppRolle;
        }
        else
        {
            AdBenutzername = User?.Identity?.Name ?? "";
        }
    }
}
