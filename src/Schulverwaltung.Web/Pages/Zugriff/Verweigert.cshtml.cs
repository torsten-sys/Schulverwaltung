using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Schulverwaltung.Web.Pages.Zugriff;

[AllowAnonymous]
public class VerweigertModel : PageModel
{
    public string AdBenutzername { get; set; } = "";

    public void OnGet()
    {
        AdBenutzername = User?.Identity?.Name ?? "";
    }
}
