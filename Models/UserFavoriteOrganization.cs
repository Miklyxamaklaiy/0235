namespace PlechPomoshchi.Models;

public class UserFavoriteOrganization
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int OrganizationId { get; set; }

    public AppUser? User { get; set; }
    public Organization? Organization { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
