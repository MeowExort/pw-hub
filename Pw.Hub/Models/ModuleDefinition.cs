namespace Pw.Hub.Models;

public class ModuleManifest
{
    public List<ModuleDefinition> Modules { get; set; } = new();
}

public class ModuleDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public string Description { get; set; } = string.Empty;
    public string Script { get; set; } = string.Empty; // relative to Scripts folder or absolute
    public List<ModuleInput> Inputs { get; set; } = new();
    // Stores last used launch arguments (as strings) by input name
    public Dictionary<string, string> LastArgs { get; set; } = new();
}

public class ModuleInput
{
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Type { get; set; } = "string"; // string|number|bool|password|squad|squads|account|accounts
    public string Default { get; set; }
    public bool Required { get; set; } = false;
}