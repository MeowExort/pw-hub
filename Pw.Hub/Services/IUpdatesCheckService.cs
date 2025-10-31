using System.Collections.Generic;
using System.Threading.Tasks;

namespace Pw.Hub.Services;

public interface IUpdatesCheckService
{
    Task<List<string>> GetUpdatesAsync();
}