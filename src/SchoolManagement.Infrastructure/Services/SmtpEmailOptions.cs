namespace SchoolManagement.Infrastructure.Services;

public sealed class SmtpEmailOptions
{
    public const string SectionName = "Email:Smtp";

    public string Host { get; set; } = "";

    public int Port { get; set; } = 587;

    public bool EnableSsl { get; set; } = true;

    public string UserName { get; set; } = "";

    public string Password { get; set; } = "";

    public string FromAddress { get; set; } = "";

    public string FromName { get; set; } = "School Management System";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Host)
        && Port is > 0 and <= 65535
        && System.Net.Mail.MailAddress.TryCreate(FromAddress, out _);
}
