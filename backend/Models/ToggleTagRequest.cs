namespace backend.Models;

public class ToggleTagRequest
{
    public string TagName { get; set; } = "";
    public string Value { get; set; } = ""; // Now accepts ANY value
}