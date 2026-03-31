namespace DynamicUpdater.Host.Contracts;

public interface IDynamicCore
{
    void ConfigureServices(IServiceCollection services);

    Task Start();

    Task Stop();
}
