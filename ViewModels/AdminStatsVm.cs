namespace PlechPomoshchi.ViewModels;

public class AdminStatsVm
{
    public int Users { get; set; }
    public int Organizations { get; set; }
    public int Requests { get; set; }
    public int VolunteerApplications { get; set; }
    public DateTime? LastParserRunUtc { get; set; }
}
