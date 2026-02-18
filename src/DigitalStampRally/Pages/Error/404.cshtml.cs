using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DigitalStampRally.Pages.Error;

public class _404Model : PageModel
{
    public string RequestedPath { get; private set; } = "";
    public string? Referer { get; private set; }

    public void OnGet()
    {
        RequestedPath = $"{Request.Path}{Request.QueryString}";
        Referer = Request.Headers.Referer.ToString();
    }
}
