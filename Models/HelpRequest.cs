using System.ComponentModel.DataAnnotations;

namespace PlechPomoshchi.Models;

public class HelpRequest
{
    public int Id { get; set; }
    public int UserId { get; set; }

    [MaxLength(120)]
    public string Category { get; set; } = "Материальная";

    public string Description { get; set; } = "";

    // Новая / В работе / Выполнена / Отклонена
    [MaxLength(30)]
    public string Status { get; set; } = "Новая";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(500)]
    public string? FilePath { get; set; }

    public AppUser? User { get; set; }
    public List<RequestComment> Comments { get; set; } = new();
}
