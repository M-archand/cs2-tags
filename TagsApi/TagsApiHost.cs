using CounterStrikeSharp.API.Core.Capabilities;

namespace TagsApi;

/// <summary>
/// Process-wide holder for the live <see cref="ITagApi"/> implementation.
/// </summary>
public static class TagsApiHost
{
    private static bool _registered;

    /// <summary>The currently loaded implementation, or <c>null</c> while cs2-tags is unloaded.</summary>
    public static ITagApi? Current { get; private set; }

    /// <summary>Publishes <paramref name="api"/> as the live implementation. Call from <c>Load</c>.</summary>
    public static void Attach(ITagApi api)
    {
        Current = api;

        if (_registered)
            return;

        Capabilities.RegisterPluginCapability(ITagApi.Capability, () => Current!);
        _registered = true;
    }

    /// <summary>Clears <see cref="Current"/> if it still is <paramref name="api"/>. Call from <c>Unload</c>.</summary>
    public static void Detach(ITagApi api)
    {
        if (ReferenceEquals(Current, api))
            Current = null;
    }
}
