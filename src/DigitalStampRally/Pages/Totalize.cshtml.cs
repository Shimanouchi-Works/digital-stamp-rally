using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DigitalStampRally.Services;
using DigitalStampRally.Models;

namespace DigitalStampRally.Pages;

[IgnoreAntiforgeryToken]
public class TotalizeModel : PageModel
{
    private readonly IProjectStore _projectStore;
    private readonly IWebHostEnvironment _env;

    public TotalizeModel(IProjectStore projectStore, IWebHostEnvironment env)
    {
        _projectStore = projectStore;
        _env = env;
    }

    // 入力（クエリ）
    public string EventId { get; private set; } = "";
    public string Token { get; private set; } = "";

    // 表示
    public bool IsAuthorized { get; private set; }
    public string EventTitle { get; private set; } = "";
    public DateTime ValidFrom { get; private set; }
    public DateTime ValidTo { get; private set; }

    public string? ErrorMessage { get; private set; }
    public string? AuthErrorMessage { get; private set; }

    public List<SpotView> Spots { get; private set; } = new();

    // 集計結果
    public int GoalTotal { get; private set; }
    public int TotalStampReads { get; private set; }

    public Dictionary<string, int> TotalBySpot { get; private set; } = new();
    public Dictionary<string, List<HourCountRow>> HourlyBySpot { get; private set; } = new();
    public List<HourCountRow> GoalsByHour { get; private set; } = new();

    // GET: パスワード入力画面
    public IActionResult OnGet(string? e, string? t)
    {
        if (string.IsNullOrWhiteSpace(e) || string.IsNullOrWhiteSpace(t))
        {
            ErrorMessage = "URLの情報が不足しています。";
            return Page();
        }

        EventId = e;
        Token = t;

        if (!_projectStore.TryGet(e, out var project))
        {
            ErrorMessage = "イベントが見つかりませんでした。";
            return Page();
        }

        // トークン検証
        if (!string.Equals(project.TotalizeToken, t, StringComparison.Ordinal))
        {
            ErrorMessage = "集計画面トークンが無効です。";
            return Page();
        }

        // 表示用
        EventTitle = project.EventTitle;
        ValidFrom = project.ValidFrom;
        ValidTo = project.ValidTo;

        return Page();
    }

    // POST: パスワード認証して集計表示
    public IActionResult OnPostAuth(string? e, string? t, string? password)
    {
        if (string.IsNullOrWhiteSpace(e) || string.IsNullOrWhiteSpace(t))
        {
            ErrorMessage = "URLの情報が不足しています。";
            return Page();
        }

        EventId = e;
        Token = t;

        if (!_projectStore.TryGet(e, out var project))
        {
            ErrorMessage = "イベントが見つかりませんでした。";
            return Page();
        }

        if (!string.Equals(project.TotalizeToken, t, StringComparison.Ordinal))
        {
            ErrorMessage = "集計画面トークンが無効です。";
            return Page();
        }

        EventTitle = project.EventTitle;
        ValidFrom = project.ValidFrom;
        ValidTo = project.ValidTo;

        if (string.IsNullOrWhiteSpace(password) || !string.Equals(password, project.TotalizePassword, StringComparison.Ordinal))
        {
            AuthErrorMessage = "パスワードが違います。";
            return Page();
        }

        IsAuthorized = true;

        Spots = project.Spots
            .Select(s => new SpotView { SpotId = s.SpotId, SpotName = s.SpotName, IsRequired = s.IsRequired })
            .ToList();

        // ---- 集計実行 ----
        var stampLogs = ReadStampLogs(e);
        var goals = ReadGoals(e);

        // 読み取り回数：押印相当（IsDuplicate=falseのみ）
        var effectiveLogs = stampLogs.Where(x => x.IsDuplicate == false).ToList();

        TotalStampReads = effectiveLogs.Count;

        // 合計（spot別）
        TotalBySpot = effectiveLogs
            .GroupBy(x => x.SpotId)
            .ToDictionary(g => g.Key, g => g.Count());

        // 毎時（spot別）
        HourlyBySpot = effectiveLogs
            .GroupBy(x => x.SpotId)
            .ToDictionary(
                g => g.Key,
                g => g.GroupBy(x => TruncToHour(x.At))
                      .OrderBy(x => x.Key)
                      .Select(x => new HourCountRow { Hour = x.Key, Count = x.Count() })
                      .ToList()
            );

        // ゴール人数（合計・毎時）
        GoalTotal = goals.Count;

        GoalsByHour = goals
            .GroupBy(x => TruncToHour(x.GoaledAt))
            .OrderBy(x => x.Key)
            .Select(x => new HourCountRow { Hour = x.Key, Count = x.Count() })
            .ToList();

        return Page();
    }

    // --------------------
    // ファイル読み出し（MVP）
    // --------------------
    private List<StampLogLine> ReadStampLogs(string eventId)
    {
        var dir = Path.Combine(_env.ContentRootPath, "App_Data", "logs");
        var path = Path.Combine(dir, $"{eventId}_stamp.log");
        if (!System.IO.File.Exists(path)) return new List<StampLogLine>();

        var result = new List<StampLogLine>();
        foreach (var line in System.IO.File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                var log = JsonSerializer.Deserialize<StampLogLine>(line);
                if (log != null) result.Add(log);
            }
            catch
            {
                // 壊れた行はスキップ
            }
        }
        return result;
    }

    private List<GoalRecord> ReadGoals(string eventId)
    {
        var dir = Path.Combine(_env.ContentRootPath, "App_Data", "goals");
        var path = Path.Combine(dir, $"{eventId}.json");
        if (!System.IO.File.Exists(path)) return new List<GoalRecord>();

        try
        {
            var json = System.IO.File.ReadAllText(path);
            var map = JsonSerializer.Deserialize<Dictionary<string, GoalRecord>>(json) ?? new();
            return map.Values.ToList();
        }
        catch
        {
            return new List<GoalRecord>();
        }
    }

    private static DateTime TruncToHour(DateTime dt)
        => new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, 0, 0);

    // --------------------
    // 表示用モデル
    // --------------------
    public class SpotView
    {
        public string SpotId { get; set; } = "";
        public string SpotName { get; set; } = "";
        public bool IsRequired { get; set; }
    }

    public class HourCountRow
    {
        public DateTime Hour { get; set; }
        public int Count { get; set; }
        public string HourLabel => Hour.ToString("yyyy/MM/dd HH:00");
    }

    // ReadStampのログ（FileStampLogStore が吐くJSONに合わせる）
    public class StampLogLine
    {
        public DateTime At { get; set; }
        public string EventId { get; set; } = "";
        public string SpotId { get; set; } = "";
        public string VisitorId { get; set; } = "";
        public bool IsDuplicate { get; set; }
        public string UserAgent { get; set; } = "";
    }
}
