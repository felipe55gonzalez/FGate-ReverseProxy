using FGate.Models;   

namespace FGate.Services
{
    public interface IEndpointCategorizer
    {
        EndpointCategorizationResult? GetEndpointGroupForPath(string path);
    }
}