namespace DynamicUpdater.Host.Infrastructure.DynamicManagement;

public interface IDynamicContainer
{
    DynamicModule? CurrentModule { get; }

    Task UpdateModuleAsync(
        DynamicModule newModule,
        CancellationToken cancellationToken);
}
