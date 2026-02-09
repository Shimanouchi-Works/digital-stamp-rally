using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace DigitalStampRally.Services;

public class SmtpEmailSender : IEmailSender
{
    private readonly MailSettings _settings;

    public SmtpEmailSender(IOptions<MailSettings> settings)
    {
        _settings = settings.Value;
    }

    public async Task SendAsync(string toEmail, string subject, string bodyText)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;

        message.Body = new TextPart("plain")
        {
            Text = bodyText
        };

        using var client = new SmtpClient();

        // 587ならStartTls、465ならSslOnConnectが多いです
        await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(_settings.SmtpUser, _settings.SmtpPass);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}
