using FGate.Models;      

namespace FGate.Services
{
    public interface ITokenService
    {
        Task<(bool IsValid, TokenDefinition? TokenDetails)> ValidateTokenAsync(string tokenValue, string requiredEndpointGroup);
    }
}