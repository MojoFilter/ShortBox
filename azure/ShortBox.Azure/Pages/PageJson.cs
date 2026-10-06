using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShortBox.Azure;

/// <summary>The body of the prepare and status endpoints.</summary>
internal sealed record StatusBody(string? Status, int? PageCount, string? Error);

/// <summary>Source generated, so reading the body needs no reflection (trimming and AOT safe).</summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(StatusBody))]
internal sealed partial class PageJsonContext : JsonSerializerContext;
