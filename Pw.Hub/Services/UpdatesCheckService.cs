using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Pw.Hub.Services;

public class UpdatesCheckService : IUpdatesCheckService
{
    private readonly ModuleService _moduleService;
    private readonly ModulesApiClient _modulesApiClient;

    public UpdatesCheckService()
    {
        _moduleService = new ModuleService();
        _modulesApiClient = new ModulesApiClient();
    }

    public async Task<List<string>> GetUpdatesAsync()
    {
        var result = new List<string>();
        try
        {
            var locals = _moduleService.LoadModules();
            if (locals == null || locals.Count == 0)
                return result;

            var resp = await _modulesApiClient.SearchAsync(null, null, null, null, 1, 100);
            var items = resp?.Items ?? new List<ModuleDto>();

            foreach (var local in locals)
            {
                if (!Guid.TryParse(local.Id, out var id))
                    continue;
                var remote = items.FirstOrDefault(i => i.Id == id);
                if (remote == null) continue;
                if (IsUpdateAvailable(local.Version ?? "1.0.0", remote.Version ?? "1.0.0"))
                {
                    result.Add(local.Name ?? local.Id);
                }
            }
        }
        catch
        {
            // ignore and return what we have
        }
        return result;
    }

    private static bool IsUpdateAvailable(string localVersion, string remoteVersion)
    {
        try
        {
            var lv = ParseVersion(localVersion);
            var rv = ParseVersion(remoteVersion);
            for (int i = 0; i < Math.Max(lv.Length, rv.Length); i++)
            {
                var a = i < lv.Length ? lv[i] : 0;
                var b = i < rv.Length ? rv[i] : 0;
                if (a != b) return a < b;
            }
            return false;
        }
        catch
        {
            return !string.Equals(localVersion, remoteVersion, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static int[] ParseVersion(string v)
    {
        return (v ?? "0").Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s, out var n) ? n : 0)
            .ToArray();
    }
}
