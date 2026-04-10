using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PlechPomoshchi.Data;
using PlechPomoshchi.Models;

namespace PlechPomoshchi.Services;

public class Seeder
{
    private readonly ApplicationDbContext _db;
    private readonly OrgParser _parser;
    private readonly ILogger<Seeder> _log;

    public Seeder(ApplicationDbContext db, OrgParser parser, IConfiguration cfg, ILogger<Seeder> log)
    {
        _db = db;
        _parser = parser;
        _log = log;
    }

    public async Task SeedAsync()
    {
        await _db.Database.EnsureCreatedAsync();

        await SeedUsersAsync();
        await SeedRequestsAsync();
        await SeedOrganizationsAsync();
    }

    private async Task SeedUsersAsync()
    {
        if (await _db.Users.AnyAsync()) return;

        var hasher = new PasswordHasher<AppUser>();

        var admin = new AppUser
        {
            Email = "admin@plecho.local",
            FullName = "Администратор",
            Role = "Admin",
            CreatedAt = DateTime.UtcNow
        };
        admin.PasswordHash = hasher.HashPassword(admin, "admin12345");

        var volunteer = new AppUser
        {
            Email = "coordinator@plecho.local",
            FullName = "Координатор",
            Role = "Volunteer",
            CreatedAt = DateTime.UtcNow
        };
        volunteer.PasswordHash = hasher.HashPassword(volunteer, "volunteer123");

        var requester = new AppUser
        {
            Email = "user@plecho.local",
            FullName = "Пользователь",
            Role = "Requester",
            CreatedAt = DateTime.UtcNow
        };
        requester.PasswordHash = hasher.HashPassword(requester, "requester123");

        _db.Users.AddRange(admin, volunteer, requester);
        await _db.SaveChangesAsync();
    }

    private async Task SeedRequestsAsync()
    {
        if (await _db.Requests.AnyAsync()) return;

        var requester = await _db.Users.FirstAsync(x => x.Role == "Requester");
        _db.Requests.Add(new HelpRequest
        {
            UserId = requester.Id,
            Category = "Медицинская",
            Description = "Нужна консультация по вопросам реабилитации после лечения.",
            Status = "Новая",
            CreatedAt = DateTime.UtcNow
        });

        _db.Requests.Add(new HelpRequest
        {
            UserId = requester.Id,
            Category = "Юридическая",
            Description = "Требуется помощь с оформлением документов и мерами поддержки.",
            Status = "В работе",
            CreatedAt = DateTime.UtcNow.AddHours(-6)
        });

        await _db.SaveChangesAsync();
    }

    private async Task SeedOrganizationsAsync()
    {
        if (await _db.Organizations.AnyAsync()) return;

        _log.LogInformation("Initial organization sync started.");
        await _parser.RunAsync(force: true);
    }
}
