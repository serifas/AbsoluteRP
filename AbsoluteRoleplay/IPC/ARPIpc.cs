using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using AbsoluteRP.Caching;
using Networking;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;

namespace AbsoluteRP.IPC
{
    public sealed class ARPIpc : IDisposable
    {
        private const string ApiVersionId         = "AbsoluteRP.ApiVersion";
        private const string GetProfileByNameId   = "AbsoluteRP.GetProfileByName";
        private const string OpenProfileByNameId  = "AbsoluteRP.OpenProfileByName";
        private const string ProfileChangedId     = "AbsoluteRP.ProfileChanged";
        private const string GetMyAccountTagId    = "AbsoluteRP.GetMyAccountTag";
        private const string GetProfilesByTagId   = "AbsoluteRP.GetProfilesByAccountTag";
        private const string OpenProfileByTagId   = "AbsoluteRP.OpenProfileByAccountTag";
        private const string OpenProfileByIdId    = "AbsoluteRP.OpenProfileById";

        // Bump on breaking changes to the JSON wire shape or callable signatures.
        public const uint ApiMajor = 1;
        public const uint ApiMinor = 0;

        private ICallGateProvider<(uint, uint)>?            apiVersionProvider;
        private ICallGateProvider<string, string, string>?  getProfileProvider;
        private ICallGateProvider<string, string, object>?  openProfileProvider;
        // Outbound only - we publish "name@world" when a profile we know about is updated.
        private ICallGateProvider<string, object>?          profileChangedProvider;
        private ICallGateProvider<string>?                  getMyAccountTagProvider;
        private ICallGateProvider<string, string>?          getProfilesByTagProvider;
        private ICallGateProvider<string, object>?          openProfileByTagProvider;
        private ICallGateProvider<int, string, string, object>? openProfileByIdProvider;

        private readonly IDalamudPluginInterface pluginInterface;

        // Latest server-returned profile summaries per tagName.
        private static readonly ConcurrentDictionary<string, List<DataReceiver.ARPProfileSummary>> profilesByTag
            = new(StringComparer.OrdinalIgnoreCase);

        public ARPIpc(IDalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;
            DataReceiver.OnProfilesByTagReceived += OnProfilesByTagReceived;
            try
            {
                apiVersionProvider = pluginInterface.GetIpcProvider<(uint, uint)>(ApiVersionId);
                apiVersionProvider.RegisterFunc(() => (ApiMajor, ApiMinor));

                getProfileProvider = pluginInterface.GetIpcProvider<string, string, string>(GetProfileByNameId);
                getProfileProvider.RegisterFunc(GetProfileJson);

                openProfileProvider = pluginInterface.GetIpcProvider<string, string, object>(OpenProfileByNameId);
                openProfileProvider.RegisterAction(OpenProfile);

                profileChangedProvider = pluginInterface.GetIpcProvider<string, object>(ProfileChangedId);

                getMyAccountTagProvider = pluginInterface.GetIpcProvider<string>(GetMyAccountTagId);
                getMyAccountTagProvider.RegisterFunc(GetMyAccountTag);

                getProfilesByTagProvider = pluginInterface.GetIpcProvider<string, string>(GetProfilesByTagId);
                getProfilesByTagProvider.RegisterFunc(GetProfilesByTagJson);

                openProfileByTagProvider = pluginInterface.GetIpcProvider<string, object>(OpenProfileByTagId);
                openProfileByTagProvider.RegisterAction(OpenProfileByTag);

                openProfileByIdProvider = pluginInterface.GetIpcProvider<int, string, string, object>(OpenProfileByIdId);
                openProfileByIdProvider.RegisterAction(OpenProfileById);

                Plugin.PluginLog?.Information(
                    $"ARPIpc: registered IPC channels (version {ApiMajor}.{ApiMinor}).");
            }
            catch (Exception ex)
            {
                Plugin.PluginLog?.Warning($"ARPIpc: failed to register IPC channels: {ex.Message}");
            }
        }
        private static string GetProfileJson(string playerName, string playerWorld)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(playerName))
                {
                    Plugin.PluginLog?.Debug("ARPIpc.GetProfileJson: empty playerName");
                    return string.Empty;
                }

                ProfileData? p = null;
                if (!string.IsNullOrWhiteSpace(playerWorld))
                    p = ProfilesCache.LoadProfileCache(playerName, playerWorld);
                if (p == null)
                    p = ProfilesCache.LoadProfileCacheByName(playerName);

                if (p == null)
                {
                    Plugin.PluginLog?.Debug(
                        $"ARPIpc.GetProfileJson: no cache for '{playerName}'@'{playerWorld}', " +
                        $"firing background fetch.");
                    TryBackgroundFetch(playerName, playerWorld);
                    return string.Empty;
                }

                var dto = new ARPProfileWire
                {
                    name        = p.playerName ?? playerName,
                    world       = p.playerWorld ?? playerWorld ?? string.Empty,
                    title       = p.title ?? string.Empty,
                    description = p.OOC ?? string.Empty,
                    avatarPng   = (p.avatarBytes != null && p.avatarBytes.Length > 0)
                        ? Convert.ToBase64String(p.avatarBytes) : string.Empty,
                    bannerPng   = (p.backgroundBytes != null && p.backgroundBytes.Length > 0)
                        ? Convert.ToBase64String(p.backgroundBytes) : string.Empty,
                    isPrivate   = p.isPrivate,
                };
                return JsonSerializer.Serialize(dto, ARPProfileWire.JsonOptions);
            }
            catch (Exception ex)
            {
                Plugin.PluginLog?.Debug($"ARPIpc.GetProfileJson failed: {ex.Message}");
                return string.Empty;
            }
        }

        private static string GetMyAccountTag()
        {
            try
            {
                var inMemory = AbsoluteRP.Windows.MainPanel.MainPanel.tagName;
                if (!string.IsNullOrWhiteSpace(inMemory))
                    return inMemory;
                var persisted = Plugin.plugin?.Configuration?.account?.accountName;
                return persisted ?? string.Empty;
            }
            catch (Exception ex)
            {
                Plugin.PluginLog?.Debug($"ARPIpc.GetMyAccountTag failed: {ex.Message}");
                return string.Empty;
            }
        }

        private static string GetProfilesByTagJson(string tagName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(tagName))
                {
                    Plugin.PluginLog?.Debug("ARPIpc.GetProfilesByTag: empty tagName");
                    return string.Empty;
                }

                TryFetchProfilesByTag(tagName);

                // Return whatever the server most recently sent us for this tag.
                if (!profilesByTag.TryGetValue(tagName, out var list) || list == null || list.Count == 0)
                    return string.Empty;

                var summaries = new List<ARPProfileSummaryWire>(list.Count);
                foreach (var p in list)
                {
                    summaries.Add(new ARPProfileSummaryWire
                    {
                        profileId    = p.ProfileId,
                        profileIndex = p.ProfileIndex,
                        profileName  = p.ProfileName ?? string.Empty,
                        playerName   = p.PlayerName ?? string.Empty,
                        playerWorld  = p.PlayerWorld ?? string.Empty,
                        profileType  = p.ProfileType,
                        isTooltip    = p.IsTooltip,
                    });
                }
                return JsonSerializer.Serialize(summaries, ARPProfileWire.JsonOptions);
            }
            catch (Exception ex)
            {
                Plugin.PluginLog?.Debug($"ARPIpc.GetProfilesByTagJson failed: {ex.Message}");
                return string.Empty;
            }
        }

        private static void OpenProfileById(int profileId, string playerName, string playerWorld)
        {
            try
            {
                if (profileId <= 0 || string.IsNullOrWhiteSpace(playerName))
                {
                    Plugin.PluginLog?.Information(
                        $"ARPIpc.OpenProfileById: invalid args (profileId={profileId}, name='{playerName}'), ignoring");
                    return;
                }

                Plugin.PluginLog?.Information(
                    $"ARPIpc.OpenProfileById: profileId={profileId}, '{playerName}'@'{playerWorld}' " +
                    $"(local character set: {Plugin.character != null})");

                if (Plugin.character == null)
                {
                    Plugin.PluginLog?.Warning("ARPIpc.OpenProfileById: Plugin.character is null - ARP not logged in.");
                    return;
                }

                var w = playerWorld ?? string.Empty;
                try { Plugin.plugin?.OpenTargetWindow(); } catch { }
                try
                {
                    Windows.Profiles.ProfileTypeWindows.TargetProfileWindow.characterName = playerName;
                    Windows.Profiles.ProfileTypeWindows.TargetProfileWindow.characterWorld = w;
                    Windows.Profiles.ProfileTypeWindows.TargetProfileWindow.RequestingProfile = true;
                    Windows.Profiles.ProfileTypeWindows.TargetProfileWindow.ResetAllData();
                }
                catch (Exception rex) { Plugin.PluginLog?.Debug($"TargetProfileWindow setup threw: {rex.Message}"); }

                DataSender.FetchProfile(Plugin.character, false, -1, playerName, w, profileId);
            }
            catch (Exception ex)
            {
                Plugin.PluginLog?.Warning($"ARPIpc.OpenProfileById failed: {ex.Message}");
            }
        }

        private static void OpenProfileByTag(string tagName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(tagName))
                {
                    Plugin.PluginLog?.Information("ARPIpc.OpenProfileByTag: empty tagName, ignoring");
                    return;
                }

                Plugin.PluginLog?.Information(
                    $"ARPIpc.OpenProfileByTag: '{tagName}' (local character set: {Plugin.character != null})");

                TryFetchProfilesByTag(tagName);

                try { Plugin.plugin?.OpenTargetWindow(); }
                catch (Exception ex2)
                {
                    Plugin.PluginLog?.Warning($"ARPIpc.OpenProfileByTag: OpenTargetWindow threw: {ex2.Message}");
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog?.Warning($"ARPIpc.OpenProfileByTag failed: {ex.Message}");
            }
        }

        private static void TryFetchProfilesByTag(string tagName)
        {
            try
            {
                if (Plugin.character == null) return;
                DataSender.FetchProfilesByAccountTag(Plugin.character, tagName);
            }
            catch (Exception ex)
            {
                Plugin.PluginLog?.Debug($"ARPIpc.TryFetchProfilesByTag failed: {ex.Message}");
            }
        }
        private static void TryBackgroundFetch(string playerName, string playerWorld)
        {
            try
            {
                if (Plugin.character == null) return;
                DataSender.FetchProfile(Plugin.character, false, -1, playerName, playerWorld ?? string.Empty, -1);
            }
            catch (Exception ex)
            {
                Plugin.PluginLog?.Debug($"ARPIpc.TryBackgroundFetch failed: {ex.Message}");
            }
        }

        private static void OpenProfile(string playerName, string playerWorld)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(playerName))
                {
                    Plugin.PluginLog?.Information("ARPIpc.OpenProfile: empty playerName, ignoring");
                    return;
                }

                Plugin.PluginLog?.Information(
                    $"ARPIpc.OpenProfile: '{playerName}'@'{playerWorld}' " +
                    $"(local character set: {Plugin.character != null})");

                if (Plugin.character == null)
                {
                    Plugin.PluginLog?.Warning("ARPIpc.OpenProfile: Plugin.character is null - " +
                        "ARP not fully logged in, profile fetch skipped");
                    return;
                }

                var w = playerWorld ?? string.Empty;
                try { Plugin.plugin?.OpenTargetWindow(); }
                catch (Exception ex2) { Plugin.PluginLog?.Warning($"OpenTargetWindow threw: {ex2.Message}"); }
                try
                {
                    Windows.Profiles.ProfileTypeWindows.TargetProfileWindow.characterName = playerName;
                    Windows.Profiles.ProfileTypeWindows.TargetProfileWindow.characterWorld = w;
                    Windows.Profiles.ProfileTypeWindows.TargetProfileWindow.RequestingProfile = true;
                    Windows.Profiles.ProfileTypeWindows.TargetProfileWindow.ResetAllData();
                }
                catch (Exception rex) { Plugin.PluginLog?.Debug($"TargetProfileWindow setup threw: {rex.Message}"); }

                DataSender.FetchProfile(Plugin.character, false, -1, playerName, w, -1);
            }
            catch (Exception ex)
            {
                Plugin.PluginLog?.Warning($"ARPIpc.OpenProfile failed: {ex.Message}");
            }
        }
        public void NotifyProfileChanged(string playerName, string playerWorld)
        {
            try
            {
                var key = $"{playerName ?? string.Empty}@{playerWorld ?? string.Empty}";
                profileChangedProvider?.SendMessage(key);
            }
            catch (Exception ex)
            {
                Plugin.PluginLog?.Debug($"ARPIpc.NotifyProfileChanged failed: {ex.Message}");
            }
        }

        private void OnProfilesByTagReceived(string tagName, List<DataReceiver.ARPProfileSummary> list)
        {
            if (string.IsNullOrEmpty(tagName)) return;
            profilesByTag[tagName] = list ?? new List<DataReceiver.ARPProfileSummary>();
            try { profileChangedProvider?.SendMessage("tag:" + tagName); }
            catch { }
        }

        public void Dispose()
        {
            try { DataReceiver.OnProfilesByTagReceived -= OnProfilesByTagReceived; } catch { }
            try { apiVersionProvider?.UnregisterFunc(); } catch { }
            try { getProfileProvider?.UnregisterFunc(); } catch { }
            try { openProfileProvider?.UnregisterAction(); } catch { }
            try { getMyAccountTagProvider?.UnregisterFunc(); } catch { }
            try { getProfilesByTagProvider?.UnregisterFunc(); } catch { }
            try { openProfileByTagProvider?.UnregisterAction(); } catch { }
            try { openProfileByIdProvider?.UnregisterAction(); } catch { }
            // profileChangedProvider has nothing to unregister (it's a publish-only channel).
        }
    }

    // JSON wire shape for AbsoluteRP.GetProfilesByAccountTag.
    internal sealed class ARPProfileSummaryWire
    {
        public int    profileId    { get; set; }
        public int    profileIndex { get; set; }
        public string profileName  { get; set; } = string.Empty;
        public string playerName   { get; set; } = string.Empty;
        public string playerWorld  { get; set; } = string.Empty;
        public int    profileType  { get; set; }
        public bool   isTooltip    { get; set; }
    }

    internal sealed class ARPProfileWire
    {
        public string name { get; set; } = string.Empty;
        public string world { get; set; } = string.Empty;
        public string title { get; set; } = string.Empty;
        public string description { get; set; } = string.Empty;
        public string avatarPng { get; set; } = string.Empty;
        public string bannerPng { get; set; } = string.Empty;
        public bool isPrivate { get; set; }

        public static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = false,
            PropertyNamingPolicy = null, 
        };
    }
}
