namespace PlechPomoshchi.Models;

public class RequestComment
{
    public int Id { get; set; }
    public int RequestId { get; set; }
    public int UserId { get; set; }

    public string Text { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public HelpRequest? Request { get; set; }
    public AppUser? User { get; set; }
}
