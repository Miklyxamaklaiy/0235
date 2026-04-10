namespace PlechPomoshchi.Models;

public class ParserState
{
    public int Id { get; set; }
    public string Key { get; set; } = ""; // e.g. "org_parser"
    public DateTime LastRunUtc { get; set; } = DateTime.MinValue;
    public int LastOrgCount { get; set; } = 0;
}
