using DynamicUpdater.Host.Middlewares;

namespace DynamicUpdater.Host.Extensions;

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseDynamicModuleMiddleware(
        this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<DynamicModuleMiddleware>();
    }
}
