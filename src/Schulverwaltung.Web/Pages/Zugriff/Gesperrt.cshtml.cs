using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Schulverwaltung.Web.Pages.Zugriff;

[AllowAnonymous]
public class GesperrtModel : PageModel
{
    public void OnGet() { }
}
