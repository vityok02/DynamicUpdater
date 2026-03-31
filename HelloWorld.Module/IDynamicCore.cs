using Microsoft.Extensions.DependencyInjection;

namespace HelloWorld.Module;

public interface IDynamicCore
{
    void ConfigureServices(IServiceCollection services);

    Task Start();

    Task Stop();
}