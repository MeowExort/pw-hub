using System;
using System.IO;
using System.Text.Json;

namespace Pw.Hub.Services;

public static class AuthState
{
    private static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Pw.Hub");
    private static readonly string FilePath = Path.Combine(Folder, "auth.json");

    public static string? Token { get; private set; }
    public static UserDto? CurrentUser { get; private set; }

    public static void Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return;
            var json = File.ReadAllText(FilePath);
            var dto = JsonSerializer.Deserialize<AuthPersisted>(json);
            Token = dto?.Token;
            CurrentUser = dto?.User;
        }
        catch { }
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            var dto = new AuthPersisted { Token = Token, User = CurrentUser };
            var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
        catch { }
    }

    public static void Set(string? token, UserDto? user)
    {
        Token = token;
        CurrentUser = user;
        Save();
    }

    private class AuthPersisted
    {
        public string? Token { get; set; }
        public UserDto? User { get; set; }
    }
}
