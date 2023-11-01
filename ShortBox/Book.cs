namespace ShortBox.Api.Data; 

public record Series(string Name);

public class Book {
    public int Id { get; set; }
    public string FileName { get; set; }
    public string? Series { get; set; }
    public string? Number { get; set; }
    public int? PageCount { get; set; }
}
