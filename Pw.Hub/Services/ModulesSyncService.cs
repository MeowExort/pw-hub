using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Pw.Hub.Models;

namespace Pw.Hub.Services;

/// <summary>
/// Реализация сервиса синхронизации модулей с сервером и управления локальными установками.
/// Выделяет общую бизнес-логику из окон/представлений.
/// </summary>
public class ModulesSyncService : IModulesSyncService
{
    private readonly ModuleService _moduleService;
    private readonly ModulesApiClient _modulesApi;

    public ModulesSyncService()
    {
        _moduleService = new ModuleService();
        _modulesApi = new ModulesApiClient();
    }

    /// <inheritdoc />
    public async Task<bool> SyncInstalledAsync()
    {
        try
        {
            // Ensure we know current user
            if (_modulesApi.CurrentUser == null)
            {
                try { await _modulesApi.MeAsync(); } catch { }
            }
            var userId = _modulesApi.CurrentUser?.UserId;
            if (string.IsNullOrWhiteSpace(userId))
                return false; // not authenticated — nothing to sync

            // Server list
            var serverInstalled = await _modulesApi.GetInstalledAsync(userId);
            var serverIds = new HashSet<Guid>(serverInstalled.Select(m => m.Id));

            // Local list
            var locals = _moduleService.LoadModules();
            var localIds = new HashSet<Guid>(locals
                .Select(m => Guid.TryParse(m.Id, out var g) ? g : Guid.Empty)
                .Where(g => g != Guid.Empty));

            var toInstall = serverIds.Except(localIds).ToList();
            var toRemove = localIds.Except(serverIds).ToList();

            var changed = false;

            // Install missing
            if (toInstall.Count > 0)
            {
                foreach (var id in toInstall)
                {
                    var dto = serverInstalled.FirstOrDefault(x => x.Id == id);
                    if (dto != null)
                    {
                        try { InstallModuleLocally(dto); changed = true; } catch { }
                    }
                }
            }

            // Remove extras
            if (toRemove.Count > 0)
            {
                foreach (var id in toRemove)
                {
                    try { RemoveModuleLocally(id); changed = true; } catch { }
                }
            }

            return changed;
        }
        catch
        {
            return false; // swallow errors
        }
    }

    /// <inheritdoc />
    public void InstallModuleLocally(ModuleDto module)
    {
        try
        {
            var baseDir = AppContext.BaseDirectory;
            var scriptsDir = System.IO.Path.Combine(baseDir, "Scripts");
            System.IO.Directory.CreateDirectory(scriptsDir);
            var fileName = module.Id.ToString() + ".lua";
            var scriptPath = System.IO.Path.Combine(scriptsDir, fileName);
            System.IO.File.WriteAllText(scriptPath, module.Script ?? string.Empty);

            var def = new ModuleDefinition
            {
                Id = module.Id.ToString(),
                Name = module.Name,
                Version = string.IsNullOrWhiteSpace(module.Version) ? "1.0.0" : module.Version,
                Description = module.Description ?? string.Empty,
                Script = fileName,
                Inputs = module.Inputs?.Select(i => new ModuleInput
                {
                    Name = i.Name,
                    Label = string.IsNullOrWhiteSpace(i.Label) ? i.Name : i.Label,
                    Type = string.IsNullOrWhiteSpace(i.Type) ? "string" : i.Type,
                    Default = string.IsNullOrWhiteSpace(i.Default) ? null : i.Default,
                    Required = i.Required
                }).ToList() ?? new List<ModuleInput>()
            };

            _moduleService.AddOrUpdateModule(def);
        }
        catch
        {
        }
    }

    /// <inheritdoc />
    public void RemoveModuleLocally(Guid id)
    {
        try
        {
            var list = _moduleService.LoadModules();
            var existing = list.FirstOrDefault(x => string.Equals(x.Id, id.ToString(), StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                // Try delete script file if under Scripts
                try
                {
                    var baseDir = AppContext.BaseDirectory;
                    var candidate1 = System.IO.Path.Combine(baseDir, existing.Script);
                    var candidate2 = System.IO.Path.Combine(baseDir, "Scripts", existing.Script);
                    if (System.IO.File.Exists(candidate1)) System.IO.File.Delete(candidate1);
                    else if (System.IO.File.Exists(candidate2)) System.IO.File.Delete(candidate2);
                }
                catch { }

                list.Remove(existing);
                _moduleService.SaveModules(list);
            }
        }
        catch
        {
        }
    }
}
