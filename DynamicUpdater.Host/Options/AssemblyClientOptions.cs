using System.ComponentModel.DataAnnotations;

namespace DynamicUpdater.Host.Options;

public sealed class AssemblyClientOptions
{
    public const string SectionName = "AssemblyClientSettings";

    [Url]
    [Required]
    public string BaseAddress { get; init; } = string.Empty;

    [Required]
    public string AssemblyEndpoint { get; init; } = string.Empty;
}
