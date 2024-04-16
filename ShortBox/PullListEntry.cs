namespace ShortBox.Api.Data;

public sealed record PullListEntryId(int Value) : IntId(Value);

public class PullListEntry
{
    public required PullListEntryId Id { get; set; }
    public required string Title { get; set; }
    public required double IssueNumber { get; set; }
    public string Description { get; set; } = string.Empty;
    public required Uri ThumbnailUri { get; set; }
    public DateTime Added { get; set; } = DateTime.Now;
    public BookId? BookId { get; set; }
}
