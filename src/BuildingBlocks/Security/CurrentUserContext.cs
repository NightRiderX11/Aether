namespace Aether.BuildingBlocks.Security;

public interface ICurrentUserContext
{
    string UserId { get; }
    IReadOnlyCollection<string> Roles { get; }
}

public sealed class CurrentUserContext : ICurrentUserContext
{
    public required string UserId { get; init; }
    public required IReadOnlyCollection<string> Roles { get; init; }
}
