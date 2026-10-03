using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using static TagsApi.Tags;

namespace TagsApi;

public interface ITagApi
{
    public static readonly PluginCapability<ITagApi> Capability = new("tags:api");

    /// <summary>Raised on the main thread; subscribe on the main thread only.</summary>
    public event Func<MessageProcess, HookResult>? OnMessageProcessPre;
    /// <summary>Raised on the main thread; subscribe on the main thread only.</summary>
    public event Func<MessageProcess, HookResult>? OnMessageProcess;
    /// <summary>Raised on the main thread; subscribe on the main thread only.</summary>
    public event Action<MessageProcess>? OnMessageProcessPost;
    /// <summary>Raised on the main thread; subscribe on the main thread only.</summary>
    public event Action<CCSPlayerController, Tag>? OnTagsUpdatedPre;
    /// <summary>Raised on the main thread; subscribe on the main thread only.</summary>
    public event Action<CCSPlayerController, Tag>? OnTagsUpdatedPost;

    /// <summary>Main thread only.</summary>
    public void AddAttribute(CCSPlayerController player, TagType types, TagPrePost prePost, string newValue);
    /// <summary>Main thread only.</summary>
    public void SetAttribute(CCSPlayerController player, TagType types, string newValue);
    /// <summary>Main thread only.</summary>
    public string? GetAttribute(CCSPlayerController player, TagType type);
    /// <summary>Main thread only.</summary>
    public void ResetAttribute(CCSPlayerController player, TagType types);
    /// <summary>Main thread only.</summary>
    public bool GetPlayerChatSound(CCSPlayerController player);
    /// <summary>Main thread only.</summary>
    public void SetPlayerChatSound(CCSPlayerController controller, bool value);
    /// <summary>Main thread only.</summary>
    public bool GetPlayerVisibility(CCSPlayerController player);
    /// <summary>Main thread only.</summary>
    public void SetPlayerVisibility(CCSPlayerController player, bool value);
    /// <summary>Main thread only.</summary>
    public void ReloadTags();
}