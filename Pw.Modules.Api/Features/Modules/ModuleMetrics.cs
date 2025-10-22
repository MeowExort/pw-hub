using System.Diagnostics.Metrics;

namespace Pw.Modules.Api.Features.Modules;

/// <summary>
/// Centralized metrics for Modules API.
/// </summary>
public static class ModuleMetrics
{
    public const string MeterName = "Modules";
    private static readonly Meter Meter = new(MeterName, "1.0.0");

    /// <summary>
    /// Counts successful module installations.
    /// Name required by issue: modules.install
    /// </summary>
    public static readonly Counter<long> Install = Meter.CreateCounter<long>(
        name: "modules.install",
        unit: null,
        description: "Количество успешных установок модулей");

    /// <summary>
    /// Counts module search requests.
    /// Name required by issue: modules.searches
    /// </summary>
    public static readonly Counter<long> Searches = Meter.CreateCounter<long>(
        name: "modules.searches",
        unit: null,
        description: "Количество запросов поиска модулей");
}
