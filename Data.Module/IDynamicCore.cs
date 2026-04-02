using Microsoft.Extensions.DependencyInjection;

namespace Module.Api;

public interface IDynamicCore
{
    void ConfigureServices(IServiceCollection services);

    Task Start();

    Task Stop();
}
