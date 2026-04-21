namespace backend.Models;

public class TagData
{
    public string TagName { get; set; } = "";
    public string? Value { get; set; }
    public string? Quality { get; set; }
    public string? Timestamp { get; set; }
    public string? Error { get; set; }
}