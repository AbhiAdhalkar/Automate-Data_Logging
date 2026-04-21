namespace backend.Models;

public class OpcTagConfig
{
    public string MachineName { get; set; } = "";
    public string ServerName { get; set; } = "";
    public int UpdateRate { get; set; } = 500;
    public List<string> Tags { get; set; } = new();
}