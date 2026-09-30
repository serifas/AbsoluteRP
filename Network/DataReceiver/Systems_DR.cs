using AbsoluteRP;
using AbsoluteRP.Defines;
using AbsoluteRP.Helpers;
using AbsoluteRP.RsUI.Pages;
using AbsoluteRP.Windows.Ect;
using AbsoluteRP.Windows.Listings;
using AbsoluteRP.Windows;
using AbsoluteRP.Windows.Moderator;
using AbsoluteRP.Windows.Profiles;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes;
using AbsoluteRP.Windows.Social.Views;
using AbsoluteRP.Windows.Social.Views.Groups;
using AbsoluteRP.Windows.Social.Views.SubViews;
using Dalamud.Interface.Textures.TextureWraps;
using FFXIVClientStructs.FFXIV.Common.Math;
using Serilog;
using System.Linq;
using System.Xml.Linq;
using Networking;

namespace AbsoluteRP.Network
{
    // RP systems, sheets and roster packets. Split out of DataReceiver.
    internal class Systems_DR
    {
        // RP Systems handlers. These handle responses for the custom RP systems feature (stats, skills, combat, sheets).

        // Confirmation that a new RP system was created
        public static void HandleSystemCreated(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    buffer.ReadInt(); // packet ID
                    int systemId = buffer.ReadInt();
                    string shareCode = buffer.ReadString();
                    string name = buffer.ReadString();

                    Plugin.PluginLog.Info($"[Systems] Created system id={systemId} code={shareCode} name={name}");

                    // Update the local system with the server-assigned ID
                    var system = AbsoluteRP.Windows.Listings.SystemsWindow.currentSystem;
                    if (system != null && system.id <= 0)
                    {
                        system.id = systemId;
                        system.shareCode = shareCode;
                    }
                }
            }
            catch (Exception ex) { Plugin.PluginLog.Error($"HandleSystemCreated Error: {ex.Message}"); }
        }

        public static void HandleSystemDeleted(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    buffer.ReadInt();
                    int systemId = buffer.ReadInt();
                    Plugin.PluginLog.Info($"[Systems] Deleted system id={systemId}");
                    AbsoluteRP.Windows.Listings.SystemsWindow.systemData.RemoveAll(s => s.id == systemId);
                    if (AbsoluteRP.Windows.Listings.SystemsWindow.currentSystem?.id == systemId)
                    {
                        AbsoluteRP.Windows.Listings.SystemsWindow.currentSystem = null;
                        AbsoluteRP.Windows.Listings.SystemsWindow.currentSystemIndex = -1;
                    }
                }
            }
            catch (Exception ex) { Plugin.PluginLog.Error($"HandleSystemDeleted Error: {ex.Message}"); }
        }

        public static void HandleMySystems(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    buffer.ReadInt();
                    int count = buffer.ReadInt();
                    Plugin.PluginLog.Info($"[Systems] Received {count} systems");

                    AbsoluteRP.Windows.Listings.SystemsWindow.systemData.Clear();
                    for (int i = 0; i < count; i++)
                    {
                        int id = buffer.ReadInt();
                        string name = buffer.ReadString();
                        string code = buffer.ReadString();
                        int bpa = buffer.ReadInt();
                        bool ra = buffer.ReadBool();
                        AbsoluteRP.Windows.Listings.SystemsWindow.systemData.Add(new SystemData
                        {
                            id = id,
                            name = name,
                            shareCode = code,
                            basePointsAvailable = bpa,
                            requireApproval = ra,
                        });
                    }

                    // Also add to View Systems available list
                    foreach (var sys in AbsoluteRP.Windows.Listings.SystemsWindow.systemData)
                    {
                        if (sys.id > 0 && !AbsoluteRP.Windows.Systems.ViewSystems.ViewSystems.availableSystems.Exists(s => s.id == sys.id))
                            AbsoluteRP.Windows.Systems.ViewSystems.ViewSystems.availableSystems.Add(sys);
                    }

                    if (AbsoluteRP.Windows.Listings.SystemsWindow.systemData.Count > 0)
                    {
                        AbsoluteRP.Windows.Listings.SystemsWindow.currentSystemIndex = 0;
                        AbsoluteRP.Windows.Listings.SystemsWindow.currentSystem =
                            AbsoluteRP.Windows.Listings.SystemsWindow.systemData[0];

                        // Auto-fetch full data for all owned systems
                        if (Plugin.character != null)
                        {
                            foreach (var sys in AbsoluteRP.Windows.Listings.SystemsWindow.systemData)
                            {
                                if (sys.id > 0)
                                    AbsoluteRP.Network.Systems_DS.FetchSystem(Plugin.character, sys.id);
                            }
                        }
                    }

                    // Always fetch joined systems (imported via share code) - even if user has no owned systems
                    if (Plugin.character != null)
                        AbsoluteRP.Network.Systems_DS.FetchJoinedSystems(Plugin.character);
                }
            }
            catch (Exception ex) { Plugin.PluginLog.Error($"HandleMySystems Error: {ex.Message}"); }
        }

        // Receives the full data for an RP system: stats, skills, skill classes, combat config, character sheet template, and all associated images. This is the largest single handler because it loads the entire system definition.
        public static void HandleSystemData(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    buffer.ReadInt(); // packet id

                    int systemId = buffer.ReadInt();
                    string name = buffer.ReadString();
                    string shareCode = buffer.ReadString();
                    int basePointsAvailable = buffer.ReadInt();
                    bool requireApproval = buffer.ReadBool();
                    string rules = buffer.ReadString();
                    int ownerUserId = buffer.ReadInt();
                    bool restrictResourceModification = buffer.ReadBool();

                    // Banner
                    byte[] bannerBytes = null;
                    int bannerLen = buffer.ReadInt();
                    if (bannerLen > 0) bannerBytes = buffer.ReadBytes(bannerLen);

                    // Logo
                    byte[] logoBytes = null;
                    int logoLen = buffer.ReadInt();
                    if (logoLen > 0) logoBytes = buffer.ReadBytes(logoLen);

                    Plugin.PluginLog.Info($"[Systems] Received full data for system {systemId} ({name})");

                    // Find matching system in the list
                    var systemsList = AbsoluteRP.Windows.Listings.SystemsWindow.systemData;
                    SystemData target = null;
                    int targetIndex = -1;
                    for (int i = 0; i < systemsList.Count; i++)
                    {
                        if (systemsList[i].id == systemId)
                        {
                            target = systemsList[i];
                            targetIndex = i;
                            break;
                        }
                    }
                    bool isImport = false;
                    if (target == null)
                    {
                        // System not in managed list - this is an import via share code
                        target = new SystemData { id = systemId };
                        isImport = true;
                    }

                    target.name = name;
                    target.shareCode = shareCode;
                    target.basePointsAvailable = basePointsAvailable;
                    target.requireApproval = requireApproval;
                    target.rules = rules;
                    target.ownerUserId = ownerUserId;
                    target.restrictResourceModification = restrictResourceModification;
                    target.bannerBytes = bannerBytes;
                    target.logoBytes = logoBytes;

                    // Load banner/logo textures
                    if (bannerBytes != null && bannerBytes.Length > 0)
                    {
                        var capturedTarget = target;
                        var capturedBytes = bannerBytes;
                        _ = System.Threading.Tasks.Task.Run(async () =>
                        { try { capturedTarget.bannerTexture = await Plugin.TextureProvider.CreateFromImageAsync(capturedBytes); } catch { } });
                    }
                    if (logoBytes != null && logoBytes.Length > 0)
                    {
                        var capturedTarget = target;
                        var capturedBytes = logoBytes;
                        _ = System.Threading.Tasks.Task.Run(async () =>
                        { try { capturedTarget.logoTexture = await Plugin.TextureProvider.CreateFromImageAsync(capturedBytes); } catch { } });
                    }

                    // Stats
                    int statCount = buffer.ReadInt();
                    target.StatsData.Clear();
                    for (int i = 0; i < statCount; i++)
                    {
                        int sid = buffer.ReadInt();
                        int sortOrder = buffer.ReadInt();
                        string sname = buffer.ReadString();
                        string sdesc = buffer.ReadString();
                        float cr = buffer.ReadFloat();
                        float cg = buffer.ReadFloat();
                        float cb = buffer.ReadFloat();
                        float ca = buffer.ReadFloat();
                        int bmin = buffer.ReadInt();
                        int bmax = buffer.ReadInt();
                        bool canAdd = buffer.ReadBool();
                        bool canRem = buffer.ReadBool();
                        bool canNeg = buffer.ReadBool();
                        bool negGives = buffer.ReadBool();

                        target.StatsData[sortOrder] = new StatData()
                        {
                            id = sid,
                            name = sname,
                            description = sdesc,
                            color = new System.Numerics.Vector4(cr, cg, cb, ca),
                            baseMin = bmin,
                            baseMax = bmax,
                            canAddPoints = canAdd,
                            canRemovePoints = canRem,
                            canGoNegative = canNeg,
                            negativeGivesPoint = negGives,
                        };
                    }

                    // Combat config
                    target.CombatConfig = new CombatConfigData()
                    {
                        healthEnabled = buffer.ReadBool(),
                        healthBase = buffer.ReadInt(),
                        healthMax = buffer.ReadInt(),
                        healthLinkedStatId = buffer.ReadInt(),
                        healthStatMultiplier = buffer.ReadFloat(),
                        healthRegenAmount = buffer.ReadInt(),
                        healthRegenEveryNTurns = buffer.ReadInt(),
                        turnCount = buffer.ReadInt(),
                        diceType = buffer.ReadInt(),
                        diceCount = buffer.ReadInt(),
                        diceModifier = buffer.ReadInt(),
                    };

                    // Resources
                    int resCount = buffer.ReadInt();
                    target.Resources.Clear();
                    for (int i = 0; i < resCount; i++)
                    {
                        target.Resources.Add(new ResourceData()
                        {
                            id = buffer.ReadInt(),
                            name = buffer.ReadString(),
                            description = buffer.ReadString(),
                            baseValue = buffer.ReadInt(),
                            maxValue = buffer.ReadInt(),
                            color = new System.Numerics.Vector4(buffer.ReadFloat(), buffer.ReadFloat(), buffer.ReadFloat(), buffer.ReadFloat()),
                            linkedStatId = buffer.ReadInt(),
                            statMultiplier = buffer.ReadFloat(),
                            regenAmount = buffer.ReadInt(),
                            regenEveryNTurns = buffer.ReadInt(),
                        });
                    }

                    // Skill classes
                    int classCount = buffer.ReadInt();
                    target.SkillClasses.Clear();
                    for (int i = 0; i < classCount; i++)
                    {
                        target.SkillClasses.Add(new SkillClassData()
                        {
                            id = buffer.ReadInt(),
                            name = buffer.ReadString(),
                            description = buffer.ReadString(),
                            sortOrder = buffer.ReadInt(),
                            allowCustomSkills = buffer.ReadBool(),
                            iconId = buffer.ReadInt(),
                            initialSkillPoints = buffer.ReadInt(),
                        });
                    }

                    // Skill trees - assign to their parent classes
                    int treeCount = buffer.ReadInt();
                    // Clear all class trees first
                    foreach (var cls in target.SkillClasses)
                        cls.SkillTrees.Clear();
                    for (int i = 0; i < treeCount; i++)
                    {
                        int treeClassId = buffer.ReadInt();
                        string treeName = buffer.ReadString();
                        int treeSortOrder = buffer.ReadInt();
                        var parentClass = target.SkillClasses.FirstOrDefault(c => c.id == treeClassId);
                        if (parentClass != null)
                        {
                            parentClass.SkillTrees.Add(new SkillTreeData
                            {
                                name = treeName,
                                sortOrder = treeSortOrder,
                            });
                        }
                    }
                    // Ensure every class has at least one tree
                    foreach (var cls in target.SkillClasses)
                    {
                        if (cls.SkillTrees.Count == 0)
                            cls.SkillTrees.Add(new SkillTreeData { name = "Main Tree", sortOrder = 0 });
                    }

                    // Skills
                    int skillCount = buffer.ReadInt();
                    target.Skills.Clear();
                    for (int i = 0; i < skillCount; i++)
                    {
                        target.Skills.Add(new SkillData()
                        {
                            id = buffer.ReadInt(),
                            classId = buffer.ReadInt(),
                            treeIndex = buffer.ReadInt(),
                            name = buffer.ReadString(),
                            description = buffer.ReadString(),
                            iconId = buffer.ReadInt(),
                            gridX = buffer.ReadInt(),
                            gridY = buffer.ReadInt(),
                            isCastable = buffer.ReadBool(),
                            cooldownTurns = buffer.ReadInt(),
                            resourceId = buffer.ReadInt(),
                            resourceCost = buffer.ReadInt(),
                            maxTiers = buffer.ReadInt(),
                        });
                    }

                    // Skill connections
                    int connCount = buffer.ReadInt();
                    target.SkillConnections.Clear();
                    for (int i = 0; i < connCount; i++)
                    {
                        target.SkillConnections.Add(new SkillConnectionData()
                        {
                            fromSkillId = buffer.ReadInt(),
                            toSkillId = buffer.ReadInt(),
                            requiredPoints = buffer.ReadInt(),
                        });
                    }
                    // Trailing: roll-for settings (older servers don't send them).
                    if (buffer.Length() >= 2)
                    {
                        target.rollForStats = buffer.ReadBool();
                        target.rollForSkills = buffer.ReadBool();
                    }

                    // If this is the currently selected system, refresh the UI references
                    if (AbsoluteRP.Windows.Listings.SystemsWindow.currentSystem != null
                        && AbsoluteRP.Windows.Listings.SystemsWindow.currentSystem.id == systemId)
                    {
                        AbsoluteRP.Windows.Listings.SystemsWindow.currentSystem = target;
                        AbsoluteRP.Windows.Listings.SystemsWindow.drawStatLayout = true;
                    }

                    // Always update View Systems available list
                    AbsoluteRP.Windows.Systems.ViewSystems.ViewSystems.OnPublicSystemReceived(target);

                    // Load icon textures for classes and skills
                    _ = LoadSystemIconsAsync(target);

                    Plugin.PluginLog.Info($"[Systems] Loaded system {systemId}: {statCount} stats, {resCount} resources, {classCount} classes, {skillCount} skills, {connCount} connections");
                }
            }
            catch (Exception ex) { Plugin.PluginLog.Error($"HandleSystemData Error: {ex.Message}"); }
        }

        internal static async System.Threading.Tasks.Task LoadSystemIconsAsync(SystemData system)
        {
            try
            {
                // Load class icons (these are game icons from the icon picker, not status effects)
                foreach (var cls in system.SkillClasses)
                {
                    if (cls.iconId > 0 && (cls.iconTexture == null || cls.iconTexture.Handle == IntPtr.Zero))
                    {
                        var tex = await LoadGameIconAsync((uint)cls.iconId);
                        if (tex != null) cls.iconTexture = tex;
                    }
                }
                // Load skill icons
                foreach (var skill in system.Skills)
                {
                    if (skill.iconId > 0 && (skill.iconTexture == null || skill.iconTexture.Handle == IntPtr.Zero))
                    {
                        var tex = await LoadGameIconAsync((uint)skill.iconId);
                        if (tex != null) skill.iconTexture = tex;
                    }
                }
            }
            catch (Exception ex) { Plugin.PluginLog.Error($"LoadSystemIconsAsync Error: {ex.Message}"); }
        }

        internal static async System.Threading.Tasks.Task<Dalamud.Interface.Textures.TextureWraps.IDalamudTextureWrap> LoadGameIconAsync(uint iconId)
        {
            return await WindowOperations.LoadGameIconAsync(iconId);
        }

        public static void HandleStatsSaved(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    buffer.ReadInt();
                    int systemId = buffer.ReadInt();
                    bool success = buffer.ReadBool();
                    Plugin.PluginLog.Info($"[Systems] Stats saved for system {systemId}: {success}");
                }
            }
            catch (Exception ex) { Plugin.PluginLog.Error($"HandleStatsSaved Error: {ex.Message}"); }
        }

        public static void HandleCombatConfigSaved(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    buffer.ReadInt();
                    int systemId = buffer.ReadInt();
                    bool success = buffer.ReadBool();
                    Plugin.PluginLog.Info($"[Systems] Combat config saved for system {systemId}: {success}");
                }
            }
            catch (Exception ex) { Plugin.PluginLog.Error($"HandleCombatConfigSaved Error: {ex.Message}"); }
        }

        public static void HandleSkillClassesSaved(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    buffer.ReadInt();
                    int systemId = buffer.ReadInt();
                    bool success = buffer.ReadBool();
                    Plugin.PluginLog.Info($"[Systems] Skill classes saved for system {systemId}: {success}");
                }
            }
            catch (Exception ex) { Plugin.PluginLog.Error($"HandleSkillClassesSaved Error: {ex.Message}"); }
        }

        public static void HandleSkillsSaved(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    buffer.ReadInt();
                    int systemId = buffer.ReadInt();
                    bool success = buffer.ReadBool();
                    Plugin.PluginLog.Info($"[Systems] Skills saved for system {systemId}: {success}");

                    // Re-fetch full system data to get server-assigned IDs for classes/skills
                    if (success && Plugin.character != null && systemId > 0)
                    {
                        AbsoluteRP.Network.Systems_DS.FetchSystem(Plugin.character, systemId);
                        // Refresh roster and clear stale assignment state
                        AbsoluteRP.Windows.Systems.Roster.Roster.ResetForSystem();
                        AbsoluteRP.Windows.Systems.ViewSystems.ViewSystems.ClearAssignmentState();
                        AbsoluteRP.Network.Systems_DS.FetchSystemRoster(Plugin.character, systemId);
                    }
                }
            }
            catch (Exception ex) { Plugin.PluginLog.Error($"HandleSkillsSaved Error: {ex.Message}"); }
        }

        public static void HandleSystemError(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    buffer.ReadInt();
                    string message = buffer.ReadString();
                    Plugin.PluginLog.Error($"[Systems] Server error: {message}");
                }
            }
            catch (Exception ex) { Plugin.PluginLog.Error($"HandleSystemError Error: {ex.Message}"); }
        }

        public static void HandleSubmitSheetResult(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    buffer.ReadInt();
                    bool success = buffer.ReadBool();
                    int sheetId = buffer.ReadInt();
                    string message = buffer.ReadString();
                    Plugin.PluginLog.Info($"[Systems] Sheet submit result: {success} - {message}");
                    AbsoluteRP.Windows.Systems.ViewSystems.ViewSystems.OnSubmitResult(success, message);
                }
            }
            catch (Exception ex) { Plugin.PluginLog.Error($"HandleSubmitSheetResult Error: {ex.Message}"); }
        }

        public static void HandleSystemRoster(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    buffer.ReadInt();
                    int systemId = buffer.ReadInt();
                    int count = buffer.ReadInt();
                    var sheets = new List<CharacterSheetData>();
                    for (int i = 0; i < count; i++)
                    {
                        var sheet = new CharacterSheetData
                        {
                            id = buffer.ReadInt(),
                            classId = buffer.ReadInt(),
                            characterName = buffer.ReadString(),
                            characterWorld = buffer.ReadString(),
                            systemId = systemId,
                        };
                        // Parse stat values JSON
                        string statJson = buffer.ReadString();
                        try
                        {
                            sheet.statValues = System.Text.Json.JsonSerializer.Deserialize<Dictionary<int, int>>(statJson) ?? new Dictionary<int, int>();
                        }
                        catch { sheet.statValues = new Dictionary<int, int>(); }

                        // Parse learned skills JSON
                        string skillJson = buffer.ReadString();
                        try
                        {
                            sheet.learnedSkills = System.Text.Json.JsonSerializer.Deserialize<List<int>>(skillJson) ?? new List<int>();
                        }
                        catch { sheet.learnedSkills = new List<int>(); }

                        sheet.status = buffer.ReadInt();
                        sheet.revisionReason = buffer.ReadString();
                        sheet.createdAt = buffer.ReadLong();
                        sheet.level = buffer.ReadInt();
                        sheet.bonusSkillPoints = buffer.ReadInt();
                        sheet.profileId = buffer.ReadInt();
                        sheet.bonusStatPoints = buffer.ReadInt();
                        sheet.currentHealth = buffer.ReadInt();
                        string resJson = buffer.ReadString();
                        try
                        {
                            sheet.resourceValues = System.Text.Json.JsonSerializer.Deserialize<Dictionary<int, int>>(resJson) ?? new Dictionary<int, int>();
                        }
                        catch { sheet.resourceValues = new Dictionary<int, int>(); }
                        sheet.profileName = buffer.ReadString();

                        // Avatar bytes
                        int avatarLen = buffer.ReadInt();
                        if (avatarLen > 0)
                        {
                            byte[] avatarBytes = buffer.ReadBytes(avatarLen);
                            if (avatarBytes != null && avatarBytes.Length > 0)
                            {
                                var capturedSheet = sheet;
                                var capturedBytes = avatarBytes;
                                _ = System.Threading.Tasks.Task.Run(async () =>
                                {
                                    try { capturedSheet.profileAvatar = await Plugin.TextureProvider.CreateFromImageAsync(capturedBytes); }
                                    catch { }
                                });
                            }
                        }

                        sheets.Add(sheet);
                    }
                    Plugin.PluginLog.Info($"[Systems] Received roster for system {systemId}: {count} sheets");
                    AbsoluteRP.Windows.Systems.Roster.Roster.OnRosterReceived(sheets);
                }
            }
            catch (Exception ex) { Plugin.PluginLog.Error($"HandleSystemRoster Error: {ex.Message}"); }
        }

        public static void HandleSheetResponse(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    buffer.ReadInt();
                    int sheetId = buffer.ReadInt();
                    int newStatus = buffer.ReadInt();
                    bool success = buffer.ReadBool();
                    Plugin.PluginLog.Info($"[Systems] Sheet {sheetId} response: status={newStatus}, success={success}");
                    if (success)
                        AbsoluteRP.Windows.Systems.Roster.Roster.OnSheetResponseReceived(sheetId, newStatus);
                }
            }
            catch (Exception ex) { Plugin.PluginLog.Error($"HandleSheetResponse Error: {ex.Message}"); }
        }

        // Pending joined system IDs that need to be fetched when Plugin.character is available
        public static List<int> pendingJoinedSystemIds = new List<int>();

        public static void HandleJoinedSystems(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    buffer.ReadInt();
                    int count = buffer.ReadInt();
                    Plugin.PluginLog.Info($"[Systems] Received {count} joined system IDs");
                    for (int i = 0; i < count; i++)
                    {
                        int systemId = buffer.ReadInt();
                        if (Plugin.character != null)
                        {
                            AbsoluteRP.Network.Systems_DS.FetchSystem(Plugin.character, systemId);
                        }
                        else
                        {
                            // Character not resolved yet - queue for later
                            if (!pendingJoinedSystemIds.Contains(systemId))
                                pendingJoinedSystemIds.Add(systemId);
                        }
                    }
                }
            }
            catch (Exception ex) { Plugin.PluginLog.Error($"HandleJoinedSystems Error: {ex.Message}"); }
        }

        /// Called from the main thread when Plugin.character becomes available. Fetches any pending joined systems that arrived before character was resolved.
        public static void ProcessPendingJoinedSystems()
        {
            if (pendingJoinedSystemIds.Count == 0 || Plugin.character == null) return;
            Plugin.PluginLog.Info($"[Systems] Processing {pendingJoinedSystemIds.Count} pending joined systems");
            foreach (int systemId in pendingJoinedSystemIds)
                AbsoluteRP.Network.Systems_DS.FetchSystem(Plugin.character, systemId);
            pendingJoinedSystemIds.Clear();
        }

        public static void HandleSystemBans(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    buffer.ReadInt();
                    int systemId = buffer.ReadInt();
                    int count = buffer.ReadInt();
                    var bans = new List<(int id, int userId, string name, string world, string reason, long bannedAt)>();
                    for (int i = 0; i < count; i++)
                    {
                        bans.Add((buffer.ReadInt(), buffer.ReadInt(), buffer.ReadString(),
                            buffer.ReadString(), buffer.ReadString(), buffer.ReadLong()));
                    }
                    AbsoluteRP.Windows.Systems.Roster.Roster.OnBansReceived(bans);
                }
            }
            catch (Exception ex) { Plugin.PluginLog.Error($"HandleSystemBans Error: {ex.Message}"); }
        }


        // ==============================
        // Account Profile (display name / avatar / gender / age / RP prefs)
        // ==============================

        // Per-userID cache of account profiles received from the server. UI code can subscribe to AccountProfileReceived to react to each incoming record.
     

        // Response to FetchPostsByAuthor - stores the list in. SocialFeed.AuthorPostsCache and fires AuthorPostsUpdated so the user profile popup can rebuild its cached view.
    }
}
