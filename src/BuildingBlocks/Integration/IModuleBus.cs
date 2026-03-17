namespace Aether.BuildingBlocks.Integration;

public interface IModuleBus
{
    Task<TResponse> QueryAsync<TResponse>(string contract, object payload, CancellationToken cancellationToken = default);
}

public sealed class InMemoryModuleBus : IModuleBus
{
    private readonly Dictionary<string, Func<object, CancellationToken, Task<object>>> _handlers = new();

    public void Register<TPayload, TResponse>(string contract, Func<TPayload, CancellationToken, Task<TResponse>> handler)
    {
        _handlers[contract] = async (payload, ct) => await handler((TPayload)payload, ct);
    }

    public async Task<TResponse> QueryAsync<TResponse>(string contract, object payload, CancellationToken cancellationToken = default)
    {
        if (!_handlers.TryGetValue(contract, out var handler))
        {
            throw new InvalidOperationException($"Contract '{contract}' not found.");
        }

        return (TResponse)await handler(payload, cancellationToken);
    }
}
