using System.Diagnostics;

namespace ShortBoxMobile;

internal static class BookReadActions
{
    /// <summary>
    /// Offers to flip the book's read state. Returns true when the server state changed.
    /// </summary>
    public static async Task<bool> PromptToggleReadAsync(Book book, IShortBoxReaderClient client)
    {
        var page = Shell.Current.CurrentPage;
        var markRead = !book.IsRead;
        var action = markRead ? "Mark as read" : "Mark as unread";
        var choice = await page.DisplayActionSheet($"{book.Series} #{book.Number}", "Cancel", null, action);
        return choice == action && await TryMarkReadAsync(book.Id.Value, markRead, client);
    }

    public static async Task<bool> TryMarkReadAsync(int bookId, bool read, IShortBoxReaderClient client)
    {
        try
        {
            await client.MarkReadAsync(bookId, read, default);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            await Shell.Current.CurrentPage.DisplayAlert(
                "Couldn't update the book",
                $"{(read ? "It could not be marked as read." : "It could not be marked as unread.")}\n\n{ex.Message}",
                "OK");
            return false;
        }
    }
}
