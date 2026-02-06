using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DigitalStampRally.Services;

namespace DigitalStampRally.Pages;

public class IndexModel : PageModel
{
    private readonly IProjectDraftStore _draftStore;

    public IndexModel(IProjectDraftStore draftStore)
    {
        _draftStore = draftStore;
    }

    [BindProperty]
    public string? ErrorMessage { get; set; }

    public string? LoadedToken { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostLoadAsync(IFormFile? projectFile)
    {
        // 基本バリデーション
        if (projectFile == null || projectFile.Length == 0)
        {
            ErrorMessage = "JSONファイルが選択されていません。";
            return Page();
        }

        // 初期リリース想定：1MB制限（必要なら後で調整）
        const long maxBytes = 1 * 1024 * 1024;
        if (projectFile.Length > maxBytes)
        {
            ErrorMessage = "ファイルサイズが大きすぎます（最大 1MB）。";
            return Page();
        }

        // 拡張子チェック（最低限）
        var ext = Path.GetExtension(projectFile.FileName);
        if (!string.Equals(ext, ".json", StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = "拡張子が .json のファイルを選択してください。";
            return Page();
        }

        string json;
        try
        {
            using var sr = new StreamReader(projectFile.OpenReadStream());
            json = await sr.ReadToEndAsync();
        }
        catch
        {
            ErrorMessage = "ファイルの読み取りに失敗しました。";
            return Page();
        }

        // “JSONっぽい”最低限の検査（厳密なスキーマ検証はCreateNew側で行う）
        if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith("{"))
        {
            ErrorMessage = "JSON形式として読み取れませんでした。";
            return Page();
        }

        // サーバー側に短時間だけ保持して、CreateNewへトークンで渡す
        var token = _draftStore.Save(json);

        // Indexに残さず、作成画面へリダイレクト
        return Redirect($"/CreateNew?load={Uri.EscapeDataString(token)}");
    }
}
