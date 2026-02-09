using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using DigitalStampRally.Services;

namespace DigitalStampRally.Pages;

public class ContactModel : PageModel
{
    private readonly IEmailSender _emailSender;
    private readonly MailSettings _mailSettings;
    private readonly ILogger<ContactModel> _logger;

    public ContactModel(
        IEmailSender emailSender,
        IOptions<MailSettings> mailSettings,
        ILogger<ContactModel> logger)
    {
        _emailSender = emailSender;
        _mailSettings = mailSettings.Value;
        _logger = logger;
    }

    [BindProperty]
    public ContactInputModel Input { get; set; } = new();

    [TempData]
    public string? Success { get; set; }

    public class ContactInputModel
    {
        [Required(ErrorMessage = "氏名は必須です。")]
        [StringLength(100)]
        public string Name { get; set; } = "";

        [StringLength(200)]
        public string? Organization { get; set; }

        [Required(ErrorMessage = "メールアドレスは必須です。")]
        [EmailAddress(ErrorMessage = "メールアドレスの形式が正しくありません。")]
        [StringLength(200)]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "問い合わせ内容は必須です。")]
        [StringLength(4000, ErrorMessage = "問い合わせ内容は4000文字以内で入力してください。")]
        public string Message { get; set; } = "";
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        if (string.IsNullOrWhiteSpace(_mailSettings.AdminEmail))
        {
            ModelState.AddModelError(string.Empty, "管理者メールアドレスが設定されていません。");
            return Page();
        }

        // 1) ユーザー向け確認メール
        var userSubject = "【Digital Stamp Rally】お問い合わせを受け付けました";
        var userBody = BuildUserBody(Input);

        // 2) 管理者向け通知メール
        var adminSubject = "【問い合わせ通知】Digital Stamp Rally";
        var adminBody = BuildAdminBody(Input);

        try
        {
            await _emailSender.SendAsync(Input.Email, userSubject, userBody);
            await _emailSender.SendAsync(_mailSettings.AdminEmail, adminSubject, adminBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send contact emails.");
            ModelState.AddModelError(string.Empty, "メール送信に失敗しました。時間をおいて再度お試しください。");
            return Page();
        }

        Success = "送信しました。確認メールをお送りしました。";
        return RedirectToPage(); // 二重送信防止（PRG）
    }

    private static string BuildUserBody(ContactInputModel input)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{input.Name} 様");
        sb.AppendLine();
        sb.AppendLine("お問い合わせありがとうございます。以下の内容で受け付けました。");
        sb.AppendLine("担当より折り返しご連絡いたします。");
        sb.AppendLine();
        sb.AppendLine("----");
        sb.AppendLine($"氏名：{input.Name}");
        sb.AppendLine($"団体名：{input.Organization ?? ""}");
        sb.AppendLine($"メール：{input.Email}");
        sb.AppendLine("問い合わせ内容：");
        sb.AppendLine(input.Message);
        sb.AppendLine("----");
        sb.AppendLine();
        sb.AppendLine("※本メールは送信専用です。返信されてもお答えできません。");
        sb.AppendLine("Digital Stamp Rally");
        return sb.ToString();
    }

    private static string BuildAdminBody(ContactInputModel input)
    {
        var sb = new StringBuilder();
        sb.AppendLine("お問い合わせが届きました。");
        sb.AppendLine();
        sb.AppendLine("----");
        sb.AppendLine($"氏名：{input.Name}");
        sb.AppendLine($"団体名：{input.Organization ?? ""}");
        sb.AppendLine($"メール：{input.Email}");
        sb.AppendLine("問い合わせ内容：");
        sb.AppendLine(input.Message);
        sb.AppendLine("----");
        return sb.ToString();
    }
}
