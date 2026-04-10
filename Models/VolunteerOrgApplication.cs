using System.ComponentModel.DataAnnotations;

namespace PlechPomoshchi.Models;

public class VolunteerOrgApplication
{
    public int Id { get; set; }

    [MaxLength(250)]
    public string OrgName { get; set; } = "";

    [MaxLength(100)]
    public string Category { get; set; } = "";

    [MaxLength(300)]
    public string Address { get; set; } = "";

    [MaxLength(250)]
    public string Contact { get; set; } = "";

    public string Message { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
