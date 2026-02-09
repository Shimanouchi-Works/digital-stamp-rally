namespace DigitalStampRally.Services;


public class MailSettings
{
    public string SmtpHost { get; set; } = "";
    public int SmtpPort { get; set; } = 587;
    public string SmtpUser { get; set; } = "";
    public string SmtpPass { get; set; } = "";

    public string FromEmail { get; set; } = "";
    public string FromName { get; set; } = "Digital Stamp Rally";

    public string AdminEmail { get; set; } = "";
}
