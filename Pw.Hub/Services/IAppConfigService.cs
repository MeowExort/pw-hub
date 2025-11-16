using System.Threading.Tasks;

namespace Pw.Hub.Services;

public interface IAppConfigService
{
    bool TryGetString(string key, out string? value);
    void SetString(string key, string? value);
    Task SaveAsync();
}
