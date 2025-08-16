using System.Net;   
using System.Threading.Tasks;

namespace FGate.Services
{
    public interface IIpBlockingService
    {
        Task<bool> IsIpBlockedAsync(IPAddress ipAddress);
    }
}