namespace Microsoft.Extensions.DependencyInjection;

public class ShortBoxAzureOptions
{
    public required Uri BaseAddress { get; set; }
    public required string HostKey { get; set; }
}
