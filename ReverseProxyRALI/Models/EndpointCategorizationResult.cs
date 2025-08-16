namespace FGate.Models
{
    public record EndpointCategorizationResult(
        string GroupName,
        bool RequiresToken,
        string MatchedPathPattern 
    );
}
