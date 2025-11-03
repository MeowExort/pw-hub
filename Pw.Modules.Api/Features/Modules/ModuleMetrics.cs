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

    // --- Telegram metrics ---
    /// <summary>
    /// Counts successful Telegram links (user accounts linked to Telegram).
    /// </summary>
    public static readonly Counter<long> TelegramLinked = Meter.CreateCounter<long>(
        name: "telegram.linked",
        unit: null,
        description: "Количество успешных привязок Telegram");

    /// <summary>
    /// Counts successful Telegram unlinks.
    /// </summary>
    public static readonly Counter<long> TelegramUnlinked = Meter.CreateCounter<long>(
        name: "telegram.unlinked",
        unit: null,
        description: "Количество успешных отвязок Telegram");

    /// <summary>
    /// Counts successfully sent Telegram messages via API sender.
    /// </summary>
    public static readonly Counter<long> TelegramMessagesSent = Meter.CreateCounter<long>(
        name: "telegram.messages.sent",
        unit: null,
        description: "Количество успешно отправленных сообщений в Telegram");
}
