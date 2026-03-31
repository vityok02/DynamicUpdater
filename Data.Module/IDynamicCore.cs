using Microsoft.Extensions.DependencyInjection;

namespace Data.Module;

public interface IDynamicCore
{
    void ConfigureServices(IServiceCollection services);

    Task Start();

    Task Stop();
}
