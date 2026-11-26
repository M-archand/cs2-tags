# Changelog

## [1.3.0](https://github.com/M-archand/cs2-tags/compare/v1.2.0...v1.3.0) (unreleased)


### Features

* tags are refreshed when a player is authorized and when `css_groups_reload` runs, so a player whose admin group or permission loads after connect gets the matching tag without a map change; new `ITagApi.ReloadTags(player)` refreshes one player after a plugin changes their groups or permissions ([fac2568](https://github.com/M-archand/cs2-tags/commit/fac2568a9cafdd7b34be9b9e26971404986cde0b))


### Bug Fixes

* the `tags:api` capability is registered once through the shared `TagsApiHost`, so after a cs2-tags hot reload `ITagApi.Capability.Get()` returns the live instance instead of the unloaded one; unload also drops every event subscriber and clears the player tag cache. Upgrading from v1.2.0 needs a full server restart ([224a191](https://github.com/M-archand/cs2-tags/commit/224a19106242e449b68e45923b54f2d022a836a8))
* the `css_admins_reload` listener only reloads tags when run from the console or by a player with `@css/generic`; command-triggered reloads run at most once every 2 seconds, and `css_tags_reload` replies when it is on cooldown ([377e0ab](https://github.com/M-archand/cs2-tags/commit/377e0abc51bd1008f3b0c3c46654c9174ba657d9))
* the native `nextlevel_changed` event created for every score tag update is freed after it is sent, fixing a memory leak ([7b672a7](https://github.com/M-archand/cs2-tags/commit/7b672a7e0f22fa2fda24e2e8559b280de72e331d))
* the example config is shipped as `cs2-tags.example.toml`, so CounterStrikeSharp copies it to `cs2-tags.toml` on first load; a warning is logged when the config has no `[[Tags]]` entries ([f7d5eff](https://github.com/M-archand/cs2-tags/commit/f7d5eff1aeef5cea21d662bd5ebcdc8adf5d985c))
* bots and players without a SteamID (not yet authorized) no longer share one cached tag under SteamID 0; every command, event handler and `ITagApi` call skips them ([68794dd](https://github.com/M-archand/cs2-tags/commit/68794dd8d118c74b1ccbf4de0c7555f377d7c413))
* chat commands are detected with the CounterStrikeSharp `PublicChatTrigger` / `SilentChatTrigger` values from `core.json` instead of a hardcoded `!`, `/` and `.`, so custom triggers are respected and messages that start with `.` get tags; `Tag.Clone()` now copies `Role` ([4fde5c2](https://github.com/M-archand/cs2-tags/commit/4fde5c22090249d97c69c7b4f344ba2342a7e640))
* attributes set through `SetAttribute` / `AddAttribute` and the visibility and chat sound state are kept across tag reloads and map changes; a map change no longer resets players to their config tag ([c374af7](https://github.com/M-archand/cs2-tags/commit/c374af7d8e46dbe7e80951be085d43d0c4576d69))
* braces and control characters are removed from player names, so a name can no longer inject color tokens or line breaks into chat ([f111890](https://github.com/M-archand/cs2-tags/commit/f111890d58105dc48a731ccee56502955501045e))
* chat lines no longer show two spaces after the colon when a `ChatColor` is set ([9fc520f](https://github.com/M-archand/cs2-tags/commit/9fc520f6b6fff175d5a98ee47aab755e36adf8cb))
* the `{TeamColor}` token is matched in any letter case, like the other color tokens ([d394c96](https://github.com/M-archand/cs2-tags/commit/d394c962b7d275bc623748f3ae4be5796d192e16))
* the dead marker is shown only for dead T and CT players; spectators and unassigned players no longer get it ([ba68c88](https://github.com/M-archand/cs2-tags/commit/ba68c886de44a3debcd1b75495478bd67f268660))
* a consumer plugin that throws inside an `ITagApi` event handler is logged (type, method, event) and skipped; the other subscribers still run and the chat line or tag update continues ([0b7d115](https://github.com/M-archand/cs2-tags/commit/0b7d115c452a82e5908b369d64b8df697e931a94))


### Code Refactoring

* the player tag cache is filled with `GetOrAdd`, and the `ITagApi` XML docs state that every member must be called from the game thread ([29cbc0c](https://github.com/M-archand/cs2-tags/commit/29cbc0cc1e0cd30f10b0d4167a2adfc5d8e40309))
* `Tags.PlayerTagsList`, `Tags.Api` and `Tags.Instance` in `cs2-tags.dll` are now `internal`. Breaking only for plugins that referenced `cs2-tags.dll` directly instead of `TagsApi.dll`; none are known ([4fde5c2](https://github.com/M-archand/cs2-tags/commit/4fde5c22090249d97c69c7b4f344ba2342a7e640))


### Config Changes

**Renamed**

* `cs2-tags-example.toml` -> `cs2-tags.example.toml` - an existing `cs2-tags.toml` is not touched; the old example file can be deleted ([f7d5eff](https://github.com/M-archand/cs2-tags/commit/f7d5eff1aeef5cea21d662bd5ebcdc8adf5d985c))

**Changed defaults**

* keys left out of `[Settings]` and `[Commands]` now fall back to the shipped example values instead of empty strings and empty lists; before, a config without `[Commands]` registered no commands. A missing `[Default]` section falls back to `{Grey}[Player]` with `{White}` chat and `{TeamColor}` names ([f7d5eff](https://github.com/M-archand/cs2-tags/commit/f7d5eff1aeef5cea21d662bd5ebcdc8adf5d985c))


### Miscellaneous

* **deps:** bump CounterStrikeSharp.API to 1.0.381 ([59babeb](https://github.com/M-archand/cs2-tags/commit/59babeb14700ceb7290ca95301ab46538c7b9742))
* declare `MinimumApiVersion(313)` ([991c1de](https://github.com/M-archand/cs2-tags/commit/991c1de244dc606782684b986f6e65fc4d05a572))
* **docs:** expand the README (requirements, config, commands, API reference and examples), document that `ChatSound` has no effect, and add `CHANGELOG.md`

## [1.2.0](https://github.com/M-archand/cs2-tags/compare/421f0112411ccec735854eb1315ce15a94f7a5ae...v1.2.0) (2026-06-08)


### Features

* new `VisibilityPermissions` setting decides who may use the visibility toggle command (`#group` or permission flag, empty list = everyone); before, the command always needed `@css/admin` ([52611fa](https://github.com/M-archand/cs2-tags/commit/52611fa1f16382910e0f31069c1590b529f153bf))
* every player's tag is resolved again on each map start ([53b037c](https://github.com/M-archand/cs2-tags/commit/53b037c86ab7116e0b8bb7807a9e1e5944812542))


### Bug Fixes

* chat is read from the `say` / `say_team` commands instead of hooking the `SayText2` user message; cs2-tags sends its own formatted line and blocks the stock one. Messages that start with `!`, `/` or `.` are left to CounterStrikeSharp. Since this change `ChatSound` has no effect ([9a9b2ed](https://github.com/M-archand/cs2-tags/commit/9a9b2ed021c3b0fe7e6fdfcfc63524b81c348121))
* the score tag is sent again on every spawn, so it survives map changes; score tag updates and every `ITagApi` call skip invalid players ([b8d1c32](https://github.com/M-archand/cs2-tags/commit/b8d1c32cca3f246aca1dff22347b6a35876e65e7))
* hiding tags clears the score tag instead of showing the `[Default]` score tag; connections with SteamID 0 are ignored ([bcfb77a](https://github.com/M-archand/cs2-tags/commit/bcfb77ac10ed41eb3f4fcc63528012a8ea5e13b0))
* chat from an invalid player controller is left to the stock chat ([008acb9](https://github.com/M-archand/cs2-tags/commit/008acb9419d7c0e560996999a1d9fa29addb9c2d))
* `Tags.Instance` throws a clear `InvalidOperationException` when used before `Load()` instead of returning a dummy or null instance ([b2db90d](https://github.com/M-archand/cs2-tags/commit/b2db90d7235ea6855a49ed7ac115f2aad441fdd7))
* a player who hid their tags no longer gets the score tag back on spawn, and `css_tags_reload` / `css_admins_reload` keep each player's visibility and chat sound state ([0e7b35d](https://github.com/M-archand/cs2-tags/commit/0e7b35d590b606fa737fb3a7fd13641dec4eb7c9))
* with several `OnMessageProcessPre` / `OnMessageProcess` subscribers, the highest `HookResult` wins instead of the result of the last subscriber, so one plugin can no longer undo another plugin's block ([3164402](https://github.com/M-archand/cs2-tags/commit/31644021eecf0455812caed378f84d52d9dbb79e))


### Performance Improvements

* `[[Tags]]` entries are indexed by SteamID64, group and permission when the config loads, instead of scanning the full list for every player lookup ([53b037c](https://github.com/M-archand/cs2-tags/commit/53b037c86ab7116e0b8bb7807a9e1e5944812542))


### Code Refactoring

* the reload and visibility commands are removed explicitly on unload, the dummy `Tags.Instance` default is gone, and the `css_tags_reload` handler is an instance method ([bcfb77a](https://github.com/M-archand/cs2-tags/commit/bcfb77ac10ed41eb3f4fcc63528012a8ea5e13b0))
* the player tag cache is a `ConcurrentDictionary`, and the team label map is replaced in one step on reload instead of being updated key by key ([bcfb77a](https://github.com/M-archand/cs2-tags/commit/bcfb77ac10ed41eb3f4fcc63528012a8ea5e13b0))
* the team chat loop checks validity and team inline instead of through a LINQ filter ([008acb9](https://github.com/M-archand/cs2-tags/commit/008acb9419d7c0e560996999a1d9fa29addb9c2d))
* remove the `cs2-tags-api-test` example project and `cs2-tags.sln` ([8971f29](https://github.com/M-archand/cs2-tags/commit/8971f29cc84bb2b09bb94bac8ada95637ba34e36))


### Config Changes

**New keys**

* `[Settings] VisibilityPermissions` - list of `#group` names or permission flags that may use the visibility toggle; empty list = no permission needed. Shipped example: `[ "@css/admin" ]`. Without the key: `[ "@css/admin", "@css/root" ]` ([52611fa](https://github.com/M-archand/cs2-tags/commit/52611fa1f16382910e0f31069c1590b529f153bf))


### Miscellaneous

* **deps:** target .NET 10 and CounterStrikeSharp.API 1.0.369 (from 1.0.333 through 1.0.362); **CounterStrikeSharp v1.0.369 or newer is required** ([01f567e](https://github.com/M-archand/cs2-tags/commit/01f567ea109bdd46cac7e9f4b2af3ccfcf5e6dfa)) ([8971f29](https://github.com/M-archand/cs2-tags/commit/8971f29cc84bb2b09bb94bac8ada95637ba34e36))
* version numbering restarts at 1.2.0 (the previous upstream release was 1.15), and `ModuleAuthor` is now `schwarper, Marchand` ([b2db90d](https://github.com/M-archand/cs2-tags/commit/b2db90d7235ea6855a49ed7ac115f2aad441fdd7))
* **ci:** releases are started by hand (`workflow_dispatch`) instead of on every push to `main`; the version comes from `ModuleVersion`, the build uses .NET 10, and the release zip is `cs2-tags-v<version>.zip` with the CounterStrikeSharp folder layout ([d662ae0](https://github.com/M-archand/cs2-tags/commit/d662ae0b07931fc6a294cd9172e113606d47cbeb))
