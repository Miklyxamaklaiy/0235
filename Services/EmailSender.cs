using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using PlechPomoshchi.Models;

namespace PlechPomoshchi.Services;

public class EmailSender
{
    private readonly IConfiguration _cfg;
    private readonly ILogger<EmailSender> _log;

    public EmailSender(IConfiguration cfg, ILogger<EmailSender> log)
    {
        _cfg = cfg;
        _log = log;
    }

    public async Task TrySendVolunteerOrgApplicationAsync(VolunteerOrgApplication a)
    {
        var host = _cfg["Smtp:Host"] ?? "";
        var to = _cfg["Smtp:To"] ?? "";
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(to))
        {
            _log.LogInformation("SMTP is not configured. Skipping email send.");
            return;
        }

        var port = int.TryParse(_cfg["Smtp:Port"], out var p) ? p : 587;
        var user = _cfg["Smtp:User"] ?? "";
        var pass = _cfg["Smtp:Password"] ?? "";
        var from = _cfg["Smtp:From"] ?? "noreply@plecho.local";

        var msg = new MimeMessage();
        msg.From.Add(MailboxAddress.Parse(from));
        msg.To.Add(MailboxAddress.Parse(to));
        msg.Subject = $"Заявка организации: {a.OrgName}";

        msg.Body = new TextPart("plain")
        {
            Text =
$@"Новая заявка:

Организация: {a.OrgName}
Категория: {a.Category}
Адрес: {a.Address}
Контакт: {a.Contact}

Комментарий:
{a.Message}

Дата: {a.CreatedAt:u}"
        };

        using var smtp = new SmtpClient();
        await smtp.ConnectAsync(host, port, SecureSocketOptions.StartTlsWhenAvailable);

        if (!string.IsNullOrWhiteSpace(user))
            await smtp.AuthenticateAsync(user, pass);

        await smtp.SendAsync(msg);
        await smtp.DisconnectAsync(true);
    }
}
