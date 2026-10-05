namespace ShortBox.Metron;

public class MetronOptions
{
    public string BaseAddress { get; set; } = "https://metron.cloud/api/";
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Publisher { get; set; } = "marvel";
}
