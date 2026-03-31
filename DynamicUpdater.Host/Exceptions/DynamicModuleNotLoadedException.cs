namespace DynamicUpdater.Host.Exceptions;

public sealed class DynamicModuleNotLoadedException(string message)
    : InvalidOperationException(message);
