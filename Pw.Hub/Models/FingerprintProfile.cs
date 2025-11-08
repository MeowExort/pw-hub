using System.Collections.Generic;

namespace Pw.Hub.Models;

/// <summary>
/// Профиль «отпечатка» браузера для анти‑детекта.
/// Используется для подмены свойств <c>navigator</c>/<c>screen</c>, WebGL и Canvas на уровне JS,
/// а также для установки <c>User-Agent</c> в настройках WebView2. Может сохраняться на аккаунт.
/// </summary>
public class FingerprintProfile
{
    /// <summary>
    /// Строка <c>User-Agent</c>, выставляется как в HTTP (когда возможно), так и в <c>navigator.userAgent</c> (через JS).
    /// </summary>
    public string UserAgent { get; set; }

    /// <summary>
    /// Значение <c>navigator.platform</c> (например, <c>Win32</c> или <c>Windows</c>).
    /// </summary>
    public string Platform { get; set; }

    /// <summary>
    /// Значение <c>navigator.vendor</c> (типично: <c>Google Inc.</c> для Chromium).
    /// </summary>
    public string Vendor { get; set; }

    /// <summary>
    /// Значение <c>navigator.vendorSub</c>. В Chromium почти всегда пустая строка.
    /// </summary>
    public string VendorSub { get; set; } = string.Empty;

    /// <summary>
    /// Значение <c>navigator.product</c>. Для совместимости у многих браузеров остаётся <c>"Gecko"</c>.
    /// </summary>
    public string Product { get; set; } = "Gecko";

    /// <summary>
    /// Значение <c>navigator.productSub</c>. Обычно <c>"20030107"</c>.
    /// </summary>
    public string ProductSub { get; set; } = "20030107";

    /// <summary>
    /// Массив предпочтительных языков (<c>navigator.languages</c>), например: <c>["ru-RU","ru","en-US","en"]</c>.
    /// Первый элемент также используется для <c>navigator.language</c>.
    /// </summary>
    public List<string> Languages { get; set; } = new();

    /// <summary>
    /// Значение для HTTP‑заголовка <c>Accept-Language</c> (через запятую), например <c>"ru-RU,ru,en-US,en"</c>.
    /// Примечание: напрямую в WebView2 заголовок не подменяется — это поле хранится «на будущее»
    /// (для прокси/перехвата запросов), а также для консистентности с <see cref="Languages"/>.
    /// </summary>
    public string AcceptLanguageHeader { get; set; }

    /// <summary>
    /// Смещение часового пояса в минутах, возвращаемое <c>Date.prototype.getTimezoneOffset()</c>.
    /// Например, для Москвы (UTC+3) — <c>-180</c>.
    /// </summary>
    public int TimezoneOffsetMinutes { get; set; }

    /// <summary>
    /// <c>navigator.hardwareConcurrency</c> — количество аппаратных потоков CPU.
    /// </summary>
    public int HardwareConcurrency { get; set; } = 8;

    /// <summary>
    /// <c>navigator.deviceMemory</c> — объём памяти устройства (в ГБ, может быть дробным).
    /// </summary>
    public double DeviceMemory { get; set; } = 8;

    /// <summary>
    /// <c>screen.width</c> — ширина экрана в пикселях.
    /// </summary>
    public int ScreenWidth { get; set; } = 1920;

    /// <summary>
    /// <c>screen.height</c> — высота экрана в пикселях.
    /// </summary>
    public int ScreenHeight { get; set; } = 1080;

    /// <summary>
    /// <c>screen.colorDepth</c> — глубина цвета (бит на пиксель), обычно 24 или 30.
    /// </summary>
    public int ColorDepth { get; set; } = 24;

    /// <summary>
    /// Включён ли WebGL в профиле. Сейчас используется как флаг для будущих решений;
    /// спуфинг реализован через подмену параметров WebGL.
    /// </summary>
    public bool WebglEnabled { get; set; } = true;

    /// <summary>
    /// Подставляемый в WebGL параметр <c>UNMASKED_VENDOR_WEBGL</c>.
    /// </summary>
    public string WebglVendor { get; set; }

    /// <summary>
    /// Подставляемый в WebGL параметр <c>UNMASKED_RENDERER_WEBGL</c>.
    /// </summary>
    public string WebglRenderer { get; set; }

    /// <summary>
    /// Уровень добавляемого шума для Canvas‑отпечатка (диапазон 0..1 условно).
    /// Небольшие значения дают лёгкую рандомизацию без визуальных артефактов.
    /// </summary>
    public double CanvasNoise { get; set; } = 0.33;
}
