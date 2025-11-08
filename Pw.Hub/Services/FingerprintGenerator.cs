using System;
using System.Collections.Generic;
using Pw.Hub.Models;

namespace Pw.Hub.Services;

public static class FingerprintGenerator
{
    private static readonly string[] UaPool = new[]
    {
        // A few realistic Chromium-based UAs (Windows 10/11)
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/127.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36",
    };

    private static readonly string[] Platforms = { "Win32", "Windows" };
    private static readonly string[] Vendors = { "Google Inc.", "Google LLC" };
    private static readonly (string vendor, string renderer)[] GpuPairs = new[]
    {
        ("Google Inc. (Intel)", "ANGLE (Intel, Intel(R) UHD Graphics 620 Direct3D11 vs_5_0 ps_5_0)"),
        ("Google Inc. (NVIDIA)", "ANGLE (NVIDIA, NVIDIA GeForce GTX 1660 Direct3D11 vs_5_0 ps_5_0)"),
        ("Google Inc. (AMD)", "ANGLE (AMD, Radeon RX 570 Series Direct3D11 vs_5_0 ps_5_0)"),
        ("Google Inc. (Microsoft)", "ANGLE (Microsoft, Basic Render Driver Direct3D11 vs_5_0 ps_5_0)"),
    };

    private static readonly string[][] LanguageSets = new[]
    {
        new [] { "ru-RU", "ru", "en-US", "en" },
        new [] { "ru", "ru-RU", "en-US" },
        new [] { "en-US", "en", "ru-RU" },
    };

    private static readonly int[] ColorDepths = { 24, 30 }; // typical

    private static readonly Random Rng = new Random();

    public static FingerprintProfile Generate()
    {
        var ua = Pick(UaPool);
        var platform = Pick(Platforms);
        var vendor = Pick(Vendors);
        var (glVendor, glRenderer) = Pick(GpuPairs);
        var langs = new List<string>(Pick(LanguageSets));

        // Timezone offset in minutes: pick from common RU/EU zones
        var tzOptions = new[] { 180, 120, 60, 180, 180, 180, 180, 180, 180, 180 }; // skew to 180 (UTC+3)
        var tz = Pick(tzOptions);

        var screenPresets = new (int w, int h)[]
        {
            (1920, 1080), (1366, 768), (1536, 864), (1600, 900), (2560, 1440)
        };
        var (w,h) = Pick(screenPresets);

        var hwcOptions = new[] { 4, 6, 8, 12, 16 };
        var dmOptions = new[] { 4.0, 6.0, 8.0, 12.0, 16.0 };

        return new FingerprintProfile
        {
            UserAgent = ua,
            Platform = platform,
            Vendor = vendor,
            Languages = langs,
            AcceptLanguageHeader = string.Join(",", langs),
            TimezoneOffsetMinutes = -tz, // JS getTimezoneOffset returns negative for UTC+X? Actually returns minutes behind UTC (e.g., Moscow UTC+3 => -180)
            HardwareConcurrency = Pick(hwcOptions),
            DeviceMemory = Pick(dmOptions),
            ScreenWidth = w,
            ScreenHeight = h,
            ColorDepth = Pick(ColorDepths),
            WebglEnabled = true,
            WebglVendor = glVendor,
            WebglRenderer = glRenderer,
            CanvasNoise = Math.Round(Rng.NextDouble() * 0.6 + 0.2, 2),
        };
    }

    private static T Pick<T>(IList<T> arr) => arr[Rng.Next(arr.Count)];
}