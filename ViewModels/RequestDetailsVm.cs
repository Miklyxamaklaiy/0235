using PlechPomoshchi.Models;

namespace PlechPomoshchi.ViewModels;

public class RequestDetailsVm
{
    public HelpRequest Request { get; set; } = new();
    public string? NewCommentText { get; set; }
}
