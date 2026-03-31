namespace DynamicUpdater.Host.Exceptions;

public sealed class DynamicConfigurationException(string message)
    : InvalidOperationException(message);
