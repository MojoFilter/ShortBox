namespace ShortBox.Api.Data;

public record Series(string Name);

public abstract record IntId(int Value);

public sealed record BookId(int Value) : IntId(Value)
{
    public static bool TryParse(string input, IFormatProvider formatProvider, out BookId id)
    {
        if (int.TryParse(input, formatProvider, out int value))
        {
            id = new(value);
            return true;
        }
        id = new(0);
        return false;
    }

    public static BookId Empty { get; } = new(0);
}

public class Book {
    
    public BookId? Id { get; set; }
    public DateTime Added { get; set; } = DateTime.Now;
    public DateTime Modified { get; set; } = DateTime.Now;
    public required string FileName { get; set; }
    public string? Series { get; set; }
    public string? Number { get; set; }
    public int? PageCount { get; set; }
    public int CurrentPage { get; set; }
}