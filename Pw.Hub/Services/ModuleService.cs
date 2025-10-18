using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pw.Hub.Models;

namespace Pw.Hub.Services;

public class ModuleService
{
    public string ManifestPath { get; }

    public ModuleService()
    {
        var baseDir = AppContext.BaseDirectory;
        ManifestPath = Path.Combine(baseDir, "modules.json");
    }

    public List<ModuleDefinition> LoadModules()
    {
        try
        {
            if (!File.Exists(ManifestPath))
                return new List<ModuleDefinition>();

            var json = File.ReadAllText(ManifestPath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            if (json.TrimStart().StartsWith("["))
            {
                var list = JsonSerializer.Deserialize<List<ModuleDefinition>>(json, options) ?? new();
                return Normalize(list);
            }
            else
            {
                var manifest = JsonSerializer.Deserialize<ModuleManifest>(json, options) ?? new ModuleManifest();
                return Normalize(manifest.Modules);
            }
        }
        catch
        {
            return new List<ModuleDefinition>();
        }
    }

    public void SaveModules(List<ModuleDefinition> modules)
    {
        try
        {
            var manifest = new ModuleManifest { Modules = Normalize(modules) };
            var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
            File.WriteAllText(ManifestPath, json);
        }
        catch
        {
            // ignore
        }
    }

    public void AddOrUpdateModule(ModuleDefinition module)
    {
        var list = LoadModules();
        var existing = list.FirstOrDefault(m => string.Equals(m.Id, module.Id, StringComparison.OrdinalIgnoreCase));
        if (existing == null)
        {
            list.Add(module);
        }
        else
        {
            // replace properties
            existing.Name = module.Name;
            existing.Description = module.Description;
            existing.Script = module.Script;
            existing.Inputs = module.Inputs ?? new List<ModuleInput>();
        }
        SaveModules(list);
    }

    private List<ModuleDefinition> Normalize(List<ModuleDefinition> modules)
    {
        foreach (var m in modules)
        {
            m.Id = string.IsNullOrWhiteSpace(m.Id) ? m.Name?.Replace(' ', '_') ?? Guid.NewGuid().ToString("N") : m.Id;
            m.Inputs ??= new List<ModuleInput>();
            foreach (var i in m.Inputs)
            {
                if (string.IsNullOrWhiteSpace(i.Label)) i.Label = i.Name;
                if (string.IsNullOrWhiteSpace(i.Type)) i.Type = "string";
            }
        }
        return modules.OrderBy(m => m.Name).ToList();
    }
}