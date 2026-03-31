using System.ComponentModel.DataAnnotations;

namespace DynamicUpdater.Host.Options;

public sealed class TimerOptions
{
    public const string SectionName = "TimerSettings";

    [Range(5, int.MaxValue)]
    public int IntervalInSeconds { get; init; } = 300;
}
