using System.IO.Compression;
using Microsoft.Extensions.Logging.Abstractions;
using ShortBox.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ShortBox.Acquisition.Test;

[TestClass]
public class LibraryIngestorTests
{
    [TestMethod]
    public async Task AddsNewArchivesWithMetadataAndCover()
    {
        var env = new Env();
        env.Archives.Files["Darkhawk 004 (2026).cbz"] = Cbz(series: "Darkhawk", number: "4", pages: 3);

        var result = await env.Ingestor.IngestAsync(default);

        CollectionAssert.AreEqual(new[] { "Darkhawk 004 (2026).cbz" }, result.Added.ToArray());
        var book = env.Catalog.Added.Single();
        Assert.AreEqual("Darkhawk 004 (2026).cbz", book.FileName);
        Assert.AreEqual("Darkhawk", book.Series);
        Assert.AreEqual("4", book.Number);
        Assert.AreEqual(3, book.PageCount);
        CollectionAssert.AreEqual(new[] { "Darkhawk 004 (2026).cbz" }, env.Archives.Covers);
        Assert.AreEqual(1, env.PullList.LinkedBooks.Count);
    }

    [TestMethod]
    public async Task FallsBackToTheFileNameWhenThereIsNoComicInfo()
    {
        var env = new Env();
        env.Archives.Files["Daredevil & Echo 001 (2023) (Digital).cbz"] = Cbz(series: null, number: null, pages: 2);

        await env.Ingestor.IngestAsync(default);

        var book = env.Catalog.Added.Single();
        Assert.AreEqual("Daredevil & Echo", book.Series);
        Assert.AreEqual("001", book.Number);
    }

    [TestMethod]
    public async Task SkipsKnownFilesAndNonArchives()
    {
        var env = new Env("known.cbz");
        env.Archives.Files["known.cbz"] = Cbz("Known", "1", 2);
        env.Archives.Files["notes.txt"] = [1, 2, 3];
        env.Archives.Files["new.cbz"] = Cbz("New", "1", 2);

        var result = await env.Ingestor.IngestAsync(default);

        CollectionAssert.AreEqual(new[] { "new.cbz" }, result.Added.ToArray());
        CollectionAssert.AreEqual(new[] { "new.cbz" }, env.Archives.Downloaded);
    }

    [TestMethod]
    public async Task OneBrokenArchiveDoesNotStopTheRest()
    {
        var env = new Env();
        env.Archives.Files["a-broken.cbz"] = [0, 1, 2, 3];
        env.Archives.Files["b-good.cbz"] = Cbz("Good", "1", 2);

        var result = await env.Ingestor.IngestAsync(default);

        CollectionAssert.AreEqual(new[] { "b-good.cbz" }, result.Added.ToArray());
        CollectionAssert.AreEqual(new[] { "a-broken.cbz" }, result.Failed.ToArray());
    }

    [TestMethod]
    public async Task LimitsTheNumberOfFilesPerRun()
    {
        var env = new Env();
        for (var i = 0; i < LibraryIngestor.MaxFilesPerRun + 3; i++)
        {
            env.Archives.Files[$"issue-{i:000}.cbz"] = Cbz("S", i.ToString(), 1);
        }

        var result = await env.Ingestor.IngestAsync(default);

        Assert.AreEqual(LibraryIngestor.MaxFilesPerRun, result.Added.Count);
        Assert.AreEqual(3, result.Remaining);
    }

    [TestMethod]
    public async Task NothingToDoLeavesThePullListAlone()
    {
        var env = new Env();

        var result = await env.Ingestor.IngestAsync(default);

        Assert.AreEqual(0, result.Added.Count);
        Assert.AreEqual(0, env.PullList.LinkedBooks.Count);
    }

    private class Env
    {
        public Env(params string[] known)
        {
            Catalog = new(known);
            Ingestor = new LibraryIngestor(
                Archives,
                Catalog,
                PullList,
                new BookFactory(new ComicInfoReader(), new ComicFileNameParser()),
                new RarReader(),
                new ZipReader(),
                new CoverBusiness(),
                NullLogger<LibraryIngestor>.Instance);
        }

        public FakeArchiveLibrary Archives { get; } = new();
        public FakeBookCatalog Catalog { get; }
        public FakePullListStore PullList { get; } = new();
        public LibraryIngestor Ingestor { get; }
    }

    private static byte[] Cbz(string? series, string? number, int pages)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            for (var i = 0; i < pages; i++)
            {
                using var image = new Image<Rgba32>(40, 60);
                using var entry = zip.CreateEntry($"page-{i:00}.jpg").Open();
                image.SaveAsJpeg(entry);
            }
            if (series is not null)
            {
                using var writer = new StreamWriter(zip.CreateEntry("ComicInfo.xml").Open());
                writer.Write($"<ComicInfo><Series>{series}</Series><Number>{number}</Number><PageCount>{pages}</PageCount></ComicInfo>");
            }
        }
        return buffer.ToArray();
    }
}
