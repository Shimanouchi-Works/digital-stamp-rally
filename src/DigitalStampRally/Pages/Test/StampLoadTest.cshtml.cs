using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using DigitalStampRally.Database; // ←あなたのDbContext名前空間に合わせて
// using DigitalStampRally.Models; // Entityの名前空間に合わせて

namespace DigitalStampRally.Pages.Test;

public class StampLoadTestModel : PageModel
{
    private readonly DigitalStampRallyContext _db;

    public StampLoadTestModel(DigitalStampRallyContext db)
    {
        _db = db;
    }

    // GET: ?handler=Meta&eventId=123
    public async Task<IActionResult> OnGetMetaAsync(long eventId)
    {
        // ★テストページなので、本番公開しない/認証する/環境制限するのが安全
        // if (!Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT").Equals("Development")) return NotFound();

        // ▼ここはあなたのテーブル/カラム名に合わせてください
        var ev = await _db.Events
            .AsNoTracking()
            .Where(x => x.Id == eventId)
            .Select(x => new { x.Id, x.StartsAt, x.EndsAt })
            .FirstOrDefaultAsync();

        if (ev == null) return NotFound("Event not found");

        var spotIds = await _db.EventSpots
            .AsNoTracking()
            .Where(x => x.EventsId == eventId)
            .OrderBy(x => x.SortOrder)          // Order列が無ければ削除OK
            .Select(x => new {x.Id})
            .ToListAsync();

        return new JsonResult(new
        {
            startsAt = ev.StartsAt!.Value.ToString("o"), // ISO 8601
            endsAt = ev.EndsAt!.Value.ToString("o"),
            spots = spotIds
        });
    }
}
