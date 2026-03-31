namespace DynamicUpdater.Host.Infrastructure.AssemblyProvider;

public interface IAssemblyProvider
{
    Task<byte[]> GetAssemblyBytesAsync(CancellationToken cancellationToken);
}
