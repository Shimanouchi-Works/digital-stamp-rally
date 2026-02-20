using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using DigitalStampRally.Services;
using DigitalStampRally.Database;

namespace DigitalStampRally.Pages;

public class IndexModel : PageModel
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
            IWebHostEnvironment env,
            ILogger<IndexModel> logger)
    {
        _env = env;
        _logger = logger;
    }


    public void OnGet()
    {
    }

    public IActionResult OnGetDownloadSample()
    {
        _logger.LogInformation($"Get downloading a sample request.");

        var dir = Path.Combine(_env.ContentRootPath, "samples");

        if (!Directory.Exists(dir))
            return NotFound();

        var filePath = Directory.EnumerateFiles(dir, "*_sample.zip")
            .OrderByDescending(f => System.IO.File.GetLastWriteTimeUtc(f))
            .FirstOrDefault();

        if (filePath == null)
            return NotFound();

        return PhysicalFile(filePath, "application/zip", Path.GetFileName(filePath));
    }

}
