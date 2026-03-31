using DynamicUpdater.Host.Options;
using Microsoft.Extensions.Options;

namespace DynamicUpdater.Host.Infrastructure.Http;

public sealed class AssemblyHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly IOptionsMonitor<AssemblyClientOptions> _optionsMonitor;

    public const string ClientName = nameof(AssemblyHttpClient);

    public AssemblyHttpClient(
        HttpClient httpClient,
        IOptionsMonitor<AssemblyClientOptions> optionsMonitor)
    {
        _httpClient = httpClient;
        _optionsMonitor = optionsMonitor;
    }

    public async Task<byte[]> GetAssemblyAsync(CancellationToken cancellationToken)
    {
        var options = _optionsMonitor.CurrentValue;

        using var response = await _httpClient.GetAsync(
            options.AssemblyEndpoint,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadAsByteArrayAsync(cancellationToken);
    }
}
