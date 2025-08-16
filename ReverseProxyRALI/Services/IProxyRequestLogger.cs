using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;   

namespace FGate.Services
{
    public interface IProxyRequestLogger
    {
        Task LogRequestAsync(HttpContext context, Func<Task> next);
    }
}