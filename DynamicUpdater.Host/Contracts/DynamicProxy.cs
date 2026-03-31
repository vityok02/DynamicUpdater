using System.Reflection;

namespace DynamicUpdater.Host.Contracts;

public sealed class DynamicProxy : IDynamicCore
{
    private object? _core;
    private MethodInfo? _configureServices;
    private MethodInfo? _start;
    private MethodInfo? _stop;

    public DynamicProxy(object core)
    {
        _core = core;
        var type = core.GetType();
        _configureServices = type.GetMethod(nameof(ConfigureServices))!;
        _start = type.GetMethod(nameof(Start))!;
        _stop = type.GetMethod(nameof(Stop))!;
    }

    public void ConfigureServices(IServiceCollection services)
        => _configureServices!.Invoke(_core, [services]);

    public Task Start()
        => (Task)_start!.Invoke(_core, [])!;

    public Task Stop()
    {
        var task = (Task)_stop!.Invoke(_core, [])!;

        _core = null;
        _configureServices = null;
        _start = null;
        _stop = null;

        return task;
    }
}