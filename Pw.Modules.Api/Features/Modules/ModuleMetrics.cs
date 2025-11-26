using System.Diagnostics.Metrics;

namespace Pw.Modules.Api.Features.Modules;

/// <summary>
/// Centralized metrics for Modules API.
/// </summary>
public static class ModuleMetrics
{
    public const string MeterName = "Modules";
    private static readonly Meter Meter = new(MeterName, "1.0.0");

    // --- Common tag keys ---
    public const string TagResult = "result"; // success | failure
    public const string TagReason = "reason"; // reason for failure (invalid_input, unauthorized, not_found, etc.)
    public const string TagModuleId = "module.id";
    public const string TagModuleName = "module.name";
    public const string TagUsername = "username";

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

    // --- Auth: login ---
    public static readonly Counter<long> AuthLoginAttempts = Meter.CreateCounter<long>(
        name: "auth.login.attempts",
        unit: null,
        description: "Попытки входа (login)");

    public static readonly Counter<long> AuthLoginSuccess = Meter.CreateCounter<long>(
        name: "auth.login.success",
        unit: null,
        description: "Успешные входы");

    public static readonly Counter<long> AuthLoginFailure = Meter.CreateCounter<long>(
        name: "auth.login.failure",
        unit: null,
        description: "Неуспешные входы");

    public static readonly Histogram<double> AuthLoginDurationMs = Meter.CreateHistogram<double>(
        name: "auth.login.duration",
        unit: "ms",
        description: "Длительность обработки входа, мс");

    // --- Auth: register ---
    public static readonly Counter<long> AuthRegisterAttempts = Meter.CreateCounter<long>(
        name: "auth.register.attempts",
        unit: null,
        description: "Попытки регистрации");

    public static readonly Counter<long> AuthRegisterSuccess = Meter.CreateCounter<long>(
        name: "auth.register.success",
        unit: null,
        description: "Успешные регистрации");

    public static readonly Counter<long> AuthRegisterFailure = Meter.CreateCounter<long>(
        name: "auth.register.failure",
        unit: null,
        description: "Неуспешные регистрации");

    public static readonly Histogram<double> AuthRegisterDurationMs = Meter.CreateHistogram<double>(
        name: "auth.register.duration",
        unit: "ms",
        description: "Длительность обработки регистрации, мс");

    // --- Auth: check (me) ---
    public static readonly Counter<long> AuthCheckAttempts = Meter.CreateCounter<long>(
        name: "auth.check.attempts",
        unit: null,
        description: "Попытки проверки авторизации (GET /me)");

    public static readonly Counter<long> AuthCheckSuccess = Meter.CreateCounter<long>(
        name: "auth.check.success",
        unit: null,
        description: "Успешные проверки авторизации");

    public static readonly Counter<long> AuthCheckFailure = Meter.CreateCounter<long>(
        name: "auth.check.failure",
        unit: null,
        description: "Неуспешные проверки авторизации");

    public static readonly Histogram<double> AuthCheckDurationMs = Meter.CreateHistogram<double>(
        name: "auth.check.duration",
        unit: "ms",
        description: "Длительность проверки авторизации, мс");

    // --- Modules: start (run) ---
    public static readonly Counter<long> ModulesStartAttempts = Meter.CreateCounter<long>(
        name: "modules.start.attempts",
        unit: null,
        description: "Попытки запуска модулей");

    public static readonly Counter<long> ModulesStartSuccess = Meter.CreateCounter<long>(
        name: "modules.start.success",
        unit: null,
        description: "Успешные запуски модулей");

    public static readonly Counter<long> ModulesStartFailure = Meter.CreateCounter<long>(
        name: "modules.start.failure",
        unit: null,
        description: "Неуспешные запуски модулей");

    public static readonly Histogram<double> ModulesStartDurationMs = Meter.CreateHistogram<double>(
        name: "modules.start.duration",
        unit: "ms",
        description: "Длительность операций запуска модуля, мс");
}
