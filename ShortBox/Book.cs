namespace ShortBox.Api.Data; 

public record Series(string Name);

public class Book {
    public int Id { get; set; }
    public DateTime Added { get; set; } = DateTime.Now;
    public DateTime Modified { get; set; } = DateTime.Now;
    public required string FileName { get; set; }
    public string? Series { get; set; }
    public string? Number { get; set; }
    public int? PageCount { get; set; }
    public int CurrentPage { get; set; }
}
