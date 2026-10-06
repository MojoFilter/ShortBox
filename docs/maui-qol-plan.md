# ShortBoxMobile quality-of-life plan

Working plan, carried out over several sessions, one chunk at a time. Tick chunks off as they land and
record decisions in the **Decision log** at the bottom.

**Targets:** Android is primary. Windows should also work. MacCatalyst is not a concern.
**Branch flow:** each chunk starts from a clean `azure` branch (or a feature branch off it) and is committed on its own.

Findings below come from a read-only code scan (2026-10-06). Verify on a device before relying on them.

## Goals

1. Series screens show covers and open books correctly.
2. Page loading no longer depends on one HTTP call finishing before the Image control times out.
3. Upcoming pages are preloaded, enabling optional continuous scroll and page animations.
4. Zoom is stable.
5. Panels and reading order (Azure Custom Vision) drive animation between panels on a page.
6. A book can be marked read without paging through it.

## Dependencies

```
1,2,3,4  independent, any order
5 -> 6 -> 7 -> 8 -> 9 -> 11 -> 12
10 (zoom control) -> 11, 15
13 (Custom Vision spike) can happen any time; 14 needs 5-7; 15 needs 10, 11, 14
```

## Phase 1: Quick wins

- [x] **1. Series covers and pages** (verified on device)
  - `SeriesPage.xaml:27` binds `<UriImageSource Uri="{Binding}">` straight to a `Book` with no converter.
    `MainPage.xaml:29` uses `BookCoverUriConverter`. Declare the converter in `SeriesPage.xaml` and use it.
  - Confirm on device that opening a book from a series loads pages. If it still fails, suspects in order:
    - `SeriesPage.xaml.cs` view-model wiring (`booksView.BindingContext`, `OnBindingContextChanged` does not call base).
    - Unescaped series name in REST paths (`ShortBoxAzureClient.cs:60,63`); empty-name series (`BookStore.cs:93`).
    - `GetSomeAsync` swallowing exceptions (`HttpClientExtensions.cs`), so failures look like empty series.
  - Fix duplicate series after flyout Refresh (`AppShell.xaml.cs:19-34` never clears `seriesContainer.Items`).
- [x] **2. Mark as read / unread** (merged into `azure`; Functions to be published)
  - New Function endpoint plus a method on `IShortBoxReaderClient` and `ShortBoxAzureClient`, and `IBookStore`.
  - Unread rule is `CurrentPage/PageCount < 0.91` (`EfExtensions.cs`), and a null `PageCount` is always unread.
    Mark-read sets `CurrentPage = PageCount` and must handle a null `PageCount`.
  - UI: long-press or context action on grid items (main page and series page) and a button in the reader.
    Include mark unread.
- [ ] **3. Zoom crash hotfix**
  - `Math.Clamp(1.0, Scale * e.Scale, 3.0)` has its arguments in the wrong order (`BookPage.xaml.cs:64`) and throws
    once scale passes 3x.
  - Also: respect `GestureStatus`, stop assigning `ScaleOrigin` to translation, clamp panning, raise `IsZoomed` after pinch.
  - Patch only. The real fix is chunk 10.
- [ ] **4. Harden `MarkPage`**
  - Called from an `async void` `OnPropertyChanged` with no try/catch (`BookPage.xaml.cs:163-195`), so a network blip on a page turn can crash the app.
  - Wrap in try/catch, check the response status, debounce page-turn updates.

## Phase 2: Server page pipeline (Azure Functions)

Today the first page request for an uncached book downloads the whole archive from Drive and uploads every page
to blob storage before returning (`AzureStoragePageCache.cs:19-93`). A concurrent request sees a partially
extracted folder (`Count > 0`) and returns the wrong page or throws. A failed extraction leaves a partial folder
treated as complete until the nightly decache.

- [ ] **5. Reliable extraction**
  - Manifest plus "ready" marker so a partial extraction is never treated as complete.
  - Page count from real image entries (`ZipReader.cs:13` uses entry count minus one).
  - Keep full entry paths in blob names (`e.Name` collides across subfolders). Use stable ordering instead of blob listing order.
  - Correct content type for png/webp/gif (`GetPage.cs` always returns `image/jpeg`).
  - Honor the request cancellation token (`GetPage.cs:14` uses `CancellationToken.None`).
  - Dispose the `ZipArchive` in `ZipExtractor`.
- [ ] **6. Prepare and status endpoints**
  - `POST book/{id}/prepare` kicks off background extraction (queue-triggered function).
  - `GetPage` returns the page when ready and a retryable `202` plus `Retry-After` when not.
  - Decision: background full extraction (recommended, works for zip and solid RAR) versus on-demand single entry (zip only).
- [ ] **7. Page metadata**
  - `GET book/{id}/pages` returns the page list with dimensions, content type and status. Generated from the manifest in chunk 5.
  - Used by the reader (page count, aspect ratios for layout) and by panel detection.

## Phase 3: Client page loading

- [ ] **8. `PageProvider` service**
  - Fetch with `HttpClient`, with retry and 202 handling. Disk cache in `FileSystem.CacheDirectory`.
  - Exposes `Task<ImageSource>` per page. Loading and error (with retry) states in the UI.
  - Replaces the `UriImageSource` multi-binding in `BookPage.xaml` and the bogus first request to `api/book/0/0`.
  - Fix `ShortBoxAzureClient.WithClient` disposing the `HttpClient` before the returned stream is read.
- [ ] **9. Preloading**
  - Sliding window: next 2-3 pages and previous 1. Cancel on jump, evict outside the window.

## Phase 4: Reader features

- [ ] **10. Reusable `ZoomPanView` control**
  - Replaces the overlay gesture views in `BookPage.xaml`. Reusable control, to match how we share controls across products.
  - Pinch around the focal point, pan bounds, double-tap to zoom at the tap point, no collision between single-tap
    navigation and double-tap.
  - Windows: mouse wheel to zoom, drag to pan, keyboard navigation (no touch pinch).
  - Programmatic `ZoomToRect` (normalized rect) is the API chunk 15 will use.
- [ ] **11. Pager and page animations**
  - Pager built on `PageProvider` and `ZoomPanView`. Spike `CarouselView` against a custom approach first.
  - Page-turn animation, with a setting to turn it off.
- [ ] **12. Optional continuous scroll**
  - Vertical mode on the same provider and prefetch window, selectable in settings.

## Phase 5: Panel animation (Azure Custom Vision)

- [ ] **13. Spike: call the Custom Vision prediction endpoint**
  - Send one page image. Record: response shape, latency, per-call cost, accuracy, input limits (size cap on the prediction API).
  - Expect an object-detection response with tag, probability and a normalized bounding box (left, top, width, height).
  - Open question: how reading order is expressed. Options: encoded in tags, or derived from box positions
    (row clustering, left-to-right or right-to-left per book). Confirm against the trained project.
  - Prediction key stays server side. Never ship it in the app.
- [ ] **14. Server-side panel detection and cache**
  - Run lazily on first request or at ingest. Store panel JSON per page next to the page blobs.
  - Probability threshold and ordering applied server side.
  - Endpoint: `GET book/{id}/{page}/panels`.
- [ ] **15. Panel navigator in the reader**
  - Animate scale and translation panel to panel via `ZoomPanView.ZoomToRect`, falling back to the full page when none are detected.
  - Panel mode toggle. Tap advances through panels, then to the next page.

## Parking lot (not on the list, fit in where convenient)

- `DeleteCoversAsync` deletes `{FileName}.jpg`, but covers are stored under the book id (`AzureStoragePageCache.cs:58,103,145`). Nightly cover cleanup matches nothing.
- Function auth levels are mixed (Admin vs Function). The app uses one host key for everything.
- `GetBookCover` ignores the `height` parameter and reads the stream into a `byte[]`.
- `CoverView.xaml(.cs)` is dead and broken. Remove or finish it.
- `ShortBoxMobile.csproj` has stale per-TFM property groups (net9 ios/maccatalyst/windows), an extra `net9.0-windows10.0.22621.0` target
  and a duplicate `EmbedAssembliesIntoApk` group at the end. Clean up while we care about Windows.
- `BookPage.xaml` mixes compiled bindings (`x:DataType`) with `Source={x:Reference page}` bindings. Revisit during chunks 8-11.
- No tests for `BookStore`, `AzureStoragePageCache`, the Functions, or the MAUI view models and converters.
  Chunks 5-7 are easy to cover (`ShortBox.Test` is the place).

## Decision log

| Date | Decision |
|---|---|
| 2026-10-06 | Android primary, Windows nice to have, MacCatalyst out of scope. |
| 2026-10-06 | Panel detection uses an Azure Custom Vision project (not a Google service). |
| 2026-10-06 | Pending csproj/manifest changes committed first (`9d0fbf1`) so each chunk starts clean. |
| 2026-10-06 | Chunk 1 root cause for series books not opening: `SeriesPageViewModel.OpenBook` put `book.Id` in the query string, which is a record and serialises as `BookId { Value = n }`. Now uses `book.Id.Value` like `MainPage`. |
| 2026-10-06 | Chunk 1 verified on device and merged into `azure`. |
| 2026-10-06 | Chunk 2 implemented, then squash merged into `azure` at the user's request; the new `MarkRead` Function is published separately. Long-press on Android still to be confirmed on device. `PUT api/book/{id}/read/{true\|false}` (Admin auth, like `MarkPage`) -> `IBookStore.MarkReadAsync`. Read sets `CurrentPage = PageCount`; unread sets `0`; both bump `Modified`. Null `PageCount` on mark-read returns `409` instead of guessing. `Book.IsRead` (JSON-ignored) now owns the 91% threshold; `EfExtensions` uses `Book.ReadThreshold`. |
| 2026-10-06 | Chunk 2 UI: MAUI has no long-press gesture and Toolkit 7.0.1 has no `TouchBehavior`, so `LongPressBehavior` hooks the native view (Android `LongClick`, Windows `RightTapped`). Long-press a grid item for an action sheet (read/unread). Reader gets a "Mark read" toolbar item that marks read and leaves the book. Reader now clamps the opening page to `PageCount - 1` and skips the page-mark on load, so reopening a read book does not rewrite its state. The legacy folder API (`ShortBox.Api`) has no endpoint, and `ShortBoxApiClient` throws `NotSupportedException`. |
