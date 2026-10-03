using System.Text.RegularExpressions;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Extensions;
using CounterStrikeSharp.API.Modules.Utils;
using static Tags.Tags;
using static TagsApi.Tags;

namespace Tags;

public static partial class TagExtensions
{
    [GeneratedRegex(@"\{.*?\}|\p{C}")]
    private static partial Regex MyRegex();

    public static string Name(this CsTeam team)
    {
        return Instance.Config.Settings.TeamNames[team];
    }

    public static string RemoveCurlyBraceContent(this string message)
    {
        return MyRegex().Replace(message, string.Empty);
    }

    [GeneratedRegex(@"[{}\p{Cc}\u2028\u2029]")]
    private static partial Regex NameRegex();

    // Drops brace, control and line-break characters
    public static string SanitizeName(this string name)
    {
        return NameRegex().Replace(name, string.Empty);
    }

    public static string ReplaceTags(this string message, CsTeam team)
    {
        return message.ReplaceColorTags()
                      .Replace("{TeamColor}", ChatColors.ForTeam(team).ToString());
    }

    public static string FormatMessage(CsTeam team, params string[] args)
    {
        return ReplaceTags(string.Concat(args), team);
    }

    // Bots and non-authed players report SteamID 0
    public static bool HasTagIdentity(this CCSPlayerController player)
    {
        return player.IsValid && !player.IsBot && player.SteamID != 0;
    }

    public static Tag GetOrCreatePlayerTag(CCSPlayerController player, bool force)
    {
        if (!player.HasTagIdentity())
            return Instance.Config.Default.Clone();

        if (!force)
            return PlayerTagsList.GetOrAdd(player.SteamID, static (_, p) => p.GetTag(), player);

        Tag tag = player.GetTag();
        PlayerTagsList[player.SteamID] = tag;
        return tag;
    }


    public static Tag GetTag(this CCSPlayerController player)
    {
        Config config = Instance.Config;
        string steamId = player.SteamID.ToString();

        if (config.SteamIdTags.TryGetValue(steamId, out Tag? steamIdTag))
            return steamIdTag.Clone();

        SteamID steamID = new(player.SteamID);

        foreach (Tag tag in config.GroupTags)
        {
            if (AdminManager.PlayerInGroup(steamID, tag.Role!))
                return tag.Clone();
        }

        foreach (Tag tag in config.PermissionTags)
        {
            if (AdminManager.PlayerHasPermissions(steamID, tag.Role!))
                return tag.Clone();
        }

        return config.Default.Clone();
    }

    public static bool HasVisibilityPermission(this CCSPlayerController player)
    {
        if (!player.HasTagIdentity())
            return false;

        List<string> perms = Instance.Config.Settings.VisibilityPermissions;
        if (perms is not { Count: > 0 })
            return true;

        SteamID steamID = new(player.SteamID);
        foreach (string perm in perms)
        {
            if (perm.Length == 0)
                continue;

            bool allowed = perm[0] == '#'
                ? AdminManager.PlayerInGroup(steamID, perm)
                : AdminManager.PlayerHasPermissions(steamID, perm);

            if (allowed)
                return true;
        }

        return false;
    }

    public static string GetPrePostValue(TagPrePost prePost, string? oldValue, string newValue)
    {
        return prePost switch
        {
            TagPrePost.Pre => newValue + oldValue,
            TagPrePost.Post => oldValue + newValue,
            _ => newValue
        };
    }

    public static void AddAttribute(this CCSPlayerController player, TagType types, TagPrePost prePost, string newValue)
    {
        if (!player.HasTagIdentity())
            return;

        Tag tag = GetOrCreatePlayerTag(player, false);

        Tags.Api.TagsUpdatedPre(player, tag);

        if ((types & TagType.ScoreTag) != 0)
        {
            string value = GetPrePostValue(prePost, tag.ScoreTag, newValue);
            tag.ScoreTag = value;
            player.SetScoreTag(value);
        }
        if ((types & TagType.ChatTag) != 0)
            tag.ChatTag = GetPrePostValue(prePost, tag.ChatTag, newValue);
        if ((types & TagType.NameColor) != 0)
            tag.NameColor = GetPrePostValue(prePost, tag.NameColor, newValue);
        if ((types & TagType.ChatColor) != 0)
            tag.ChatColor = GetPrePostValue(prePost, tag.ChatColor, newValue);

        PlayerTagOverrides.AddOrUpdate(player.SteamID, types, (_, current) => current | types);
        Tags.Api.TagsUpdatedPost(player, tag);
    }

    public static void SetAttribute(this CCSPlayerController player, TagType types, string newValue)
    {
        if (!player.HasTagIdentity())
            return;

        Tag tag = GetOrCreatePlayerTag(player, false);

        Tags.Api.TagsUpdatedPre(player, tag);

        if ((types & TagType.ScoreTag) != 0)
        {
            tag.ScoreTag = newValue;
            player.SetScoreTag(newValue);
        }
        if ((types & TagType.ChatTag) != 0)
            tag.ChatTag = newValue;
        if ((types & TagType.NameColor) != 0)
            tag.NameColor = newValue;
        if ((types & TagType.ChatColor) != 0)
            tag.ChatColor = newValue;

        PlayerTagOverrides.AddOrUpdate(player.SteamID, types, (_, current) => current | types);
        Tags.Api.TagsUpdatedPost(player, tag);
    }

    public static string? GetAttribute(this CCSPlayerController player, TagType type)
    {
        if (!player.HasTagIdentity())
            return null;

        Tag tag = GetOrCreatePlayerTag(player, false);

        return type switch
        {
            TagType.ScoreTag => tag.ScoreTag,
            TagType.ChatTag => tag.ChatTag,
            TagType.NameColor => tag.NameColor,
            TagType.ChatColor => tag.ChatColor,
            _ => null
        };
    }

    public static void ResetAttribute(this CCSPlayerController player, TagType types)
    {
        if (!player.HasTagIdentity())
            return;

        Tag tag = GetOrCreatePlayerTag(player, false);
        Tag defaultTag = player.GetTag();

        Tags.Api.TagsUpdatedPre(player, tag);

        if ((types & TagType.ScoreTag) != 0)
        {
            tag.ScoreTag = defaultTag.ScoreTag;
            player.SetScoreTag(defaultTag.ScoreTag ?? string.Empty);
        }
        if ((types & TagType.ChatTag) != 0)
            tag.ChatTag = defaultTag.ChatTag;
        if ((types & TagType.NameColor) != 0)
            tag.NameColor = defaultTag.NameColor;
        if ((types & TagType.ChatColor) != 0)
            tag.ChatColor = defaultTag.ChatColor;

        PlayerTagOverrides.AddOrUpdate(player.SteamID, TagType.None, (_, current) => current & ~types);
        Tags.Api.TagsUpdatedPost(player, tag);
    }

    public static bool GetChatSound(this CCSPlayerController player)
    {
        if (!player.HasTagIdentity())
            return false;

        return GetOrCreatePlayerTag(player, false).ChatSound;
    }

    public static void SetChatSound(this CCSPlayerController player, bool value)
    {
        if (!player.HasTagIdentity())
            return;

        Tag tag = GetOrCreatePlayerTag(player, false);

        Tags.Api.TagsUpdatedPre(player, tag);
        tag.ChatSound = value;
        Tags.Api.TagsUpdatedPost(player, tag);
    }

    public static bool GetVisibility(this CCSPlayerController player)
    {
        if (!player.HasTagIdentity())
            return false;

        return GetOrCreatePlayerTag(player, false).Visibility;
    }

    public static void SetVisibility(this CCSPlayerController player, bool value)
    {
        if (!player.HasTagIdentity())
            return;

        Tag tag = GetOrCreatePlayerTag(player, false);

        Tags.Api.TagsUpdatedPre(player, tag);
        tag.Visibility = value;
        player.SetScoreTag(value ? (player.GetAttribute(TagType.ScoreTag) ?? string.Empty) : string.Empty);
        Tags.Api.TagsUpdatedPost(player, tag);
    }

    public static void SetScoreTag(this CCSPlayerController player, string? tag, bool force = false)
    {
        if (tag == null || !player.IsValid)
            return;

        if (!force && player.Clan == tag)
            return;

        player.Clan = tag;
        Utilities.SetStateChanged(player, "CCSPlayerController", "m_szClan");

        // nextlevel_changed makes the client re-read m_szClan. FireEventToClient never frees, so free it here.
        EventNextlevelChanged @event = new(force: true);
        try
        {
            @event.FireEventToClient(player);
        }
        finally
        {
            @event.Free();
        }
    }

    public static void ReloadConfig()
    {
        Instance.Config.Reload();
        Instance.Config.Settings.Init();
        Instance.Config.BuildIndex();
    }

    // Re-resolves the config tag. Keeps Visibility, ChatSound and API-set attributes of the cached tag
    public static Tag RefreshPlayerTag(CCSPlayerController player)
    {
        if (!player.HasTagIdentity())
            return Instance.Config.Default.Clone();

        PlayerTagsList.TryGetValue(player.SteamID, out Tag? old);
        Tag tag = GetOrCreatePlayerTag(player, true);
        if (old is null)
            return tag;

        tag.Visibility = old.Visibility;
        tag.ChatSound = old.ChatSound;

        PlayerTagOverrides.TryGetValue(player.SteamID, out TagType overrides);
        if ((overrides & TagType.ScoreTag) != 0)
            tag.ScoreTag = old.ScoreTag;
        if ((overrides & TagType.ChatTag) != 0)
            tag.ChatTag = old.ChatTag;
        if ((overrides & TagType.NameColor) != 0)
            tag.NameColor = old.NameColor;
        if ((overrides & TagType.ChatColor) != 0)
            tag.ChatColor = old.ChatColor;

        return tag;
    }

    public static void ReloadTags()
    {
        List<CCSPlayerController> players = Utilities.GetPlayers();
        foreach (CCSPlayerController player in players)
        {
            if (!player.HasTagIdentity())
                continue;

            Tag tag = RefreshPlayerTag(player);
            player.SetScoreTag(tag.Visibility ? (tag.ScoreTag ?? string.Empty) : string.Empty);
        }
    }
}
