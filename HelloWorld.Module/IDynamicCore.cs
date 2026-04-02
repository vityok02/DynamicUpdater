using Microsoft.Extensions.DependencyInjection;

namespace Module.Worker;

public interface IDynamicCore
{
    void ConfigureServices(IServiceCollection services);

    Task Start();

    Task Stop();
}