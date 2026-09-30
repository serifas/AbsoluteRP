using AbsoluteRP;
using AbsoluteRP.Defines;
using AbsoluteRP.Helpers;
using AbsoluteRP.RsUI.Pages;
using AbsoluteRP.Windows.Moderator;
using AbsoluteRP.Windows.Profiles;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using AbsoluteRP.Windows.Social.Views;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Hooking;
using Dalamud.Interface.Textures.TextureWraps;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Numerics;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using System.Xml.Linq;
using static Lumina.Data.Parsing.Layer.LayerCommon;
using Networking;

namespace AbsoluteRP.Network
{
    // RP systems requests. Split out of DataSender.
    internal class Systems_DS
    {
        // RP Systems Methods. These methods handle custom RP systems: stats, skills, combat configs, character sheets, and system rosters.

        // Creates a new RP system with a name and description
        public static async Task CreateSystem(Character character, string name, string description)
        {
            if (!ClientTCP.IsConnected()) return;
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteInt((int)ClientPackets.CCreateSystem);
                    buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                    buffer.WriteString(character.characterKey);
                    buffer.WriteString(name);
                    buffer.WriteString(description ?? "");
                    await ClientTCP.SendDataAsync(buffer.ToArray());
                }
            }
            catch (Exception ex) { Plugin.PluginLog.Debug($"CreateSystem error: {ex.Message}"); }
        }

        public static async Task FetchMySystems(Character character)
        {
            if (!ClientTCP.IsConnected()) return;
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteInt((int)ClientPackets.CFetchMySystems);
                    buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                    buffer.WriteString(character.characterKey);
                    await ClientTCP.SendDataAsync(buffer.ToArray());
                }
            }
            catch (Exception ex) { Plugin.PluginLog.Debug($"FetchMySystems error: {ex.Message}"); }
        }

        public static async Task FetchSystem(Character character, int systemId)
        {
            if (!ClientTCP.IsConnected()) return;
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteInt((int)ClientPackets.CFetchSystem);
                    buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                    buffer.WriteString(character.characterKey);
                    buffer.WriteInt(systemId);
                    await ClientTCP.SendDataAsync(buffer.ToArray());
                }
            }
            catch (Exception ex) { Plugin.PluginLog.Debug($"FetchSystem error: {ex.Message}"); }
        }

        public static async Task DeleteSystem(Character character, int systemId)
        {
            if (!ClientTCP.IsConnected()) return;
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteInt((int)ClientPackets.CDeleteSystem);
                    buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                    buffer.WriteString(character.characterKey);
                    buffer.WriteInt(systemId);
                    await ClientTCP.SendDataAsync(buffer.ToArray());
                }
            }
            catch (Exception ex) { Plugin.PluginLog.Debug($"DeleteSystem error: {ex.Message}"); }
        }

        public static async Task UpdateSystemSettings(Character character, int systemId, string name, int basePointsAvailable, bool requireApproval, string rules, bool restrictResourceModification, bool rollForStats = false, bool rollForSkills = false)
        {
            if (!ClientTCP.IsConnected()) return;
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteInt((int)ClientPackets.CUpdateSystem);
                    buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                    buffer.WriteString(character.characterKey);
                    buffer.WriteInt(systemId);
                    buffer.WriteString(name);
                    buffer.WriteInt(basePointsAvailable);
                    buffer.WriteBool(requireApproval);
                    buffer.WriteString(rules ?? "");
                    buffer.WriteBool(restrictResourceModification);
                    buffer.WriteBool(rollForStats);
                    buffer.WriteBool(rollForSkills);
                    await ClientTCP.SendDataAsync(buffer.ToArray());
                }
            }
            catch (Exception ex) { Plugin.PluginLog.Debug($"UpdateSystemSettings error: {ex.Message}"); }
        }

        public static async Task ImportSystemByCode(Character character, string shareCode)
        {
            if (!ClientTCP.IsConnected()) return;
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteInt((int)ClientPackets.CImportSystemByCode);
                    buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                    buffer.WriteString(character.characterKey);
                    buffer.WriteString(shareCode);
                    await ClientTCP.SendDataAsync(buffer.ToArray());
                }
            }
            catch (Exception ex) { Plugin.PluginLog.Debug($"ImportSystemByCode error: {ex.Message}"); }
        }

        public static async Task SaveSystemStats(Character character, int systemId, SortedList<int, StatData> stats)
        {
            if (!ClientTCP.IsConnected()) return;
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteInt((int)ClientPackets.CSaveSystemStats);
                    buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                    buffer.WriteString(character.characterKey);
                    buffer.WriteInt(systemId);
                    buffer.WriteInt(stats.Count);
                    for (int i = 0; i < stats.Count; i++)
                    {
                        var s = stats.Values[i];
                        buffer.WriteInt(s.id);
                        buffer.WriteInt(i); // sort order
                        buffer.WriteString(s.name ?? "");
                        buffer.WriteString(s.description ?? "");
                        buffer.WriteFloat(s.color.X);
                        buffer.WriteFloat(s.color.Y);
                        buffer.WriteFloat(s.color.Z);
                        buffer.WriteFloat(s.color.W);
                        buffer.WriteInt(s.baseMin);
                        buffer.WriteInt(s.baseMax);
                        buffer.WriteBool(s.canAddPoints);
                        buffer.WriteBool(s.canRemovePoints);
                        buffer.WriteBool(s.canGoNegative);
                        buffer.WriteBool(s.negativeGivesPoint);
                    }
                    await ClientTCP.SendDataAsync(buffer.ToArray());
                }
            }
            catch (Exception ex) { Plugin.PluginLog.Debug($"SaveSystemStats error: {ex.Message}"); }
        }

        public static async Task SaveCombatConfig(Character character, int systemId, CombatConfigData config, List<ResourceData> resources)
        {
            if (!ClientTCP.IsConnected()) return;
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteInt((int)ClientPackets.CSaveCombatConfig);
                    buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                    buffer.WriteString(character.characterKey);
                    buffer.WriteInt(systemId);
                    // Combat config
                    buffer.WriteBool(config.healthEnabled);
                    buffer.WriteInt(config.healthBase);
                    buffer.WriteInt(config.healthMax);
                    buffer.WriteInt(config.healthLinkedStatId);
                    buffer.WriteFloat(config.healthStatMultiplier);
                    buffer.WriteInt(config.healthRegenAmount);
                    buffer.WriteInt(config.healthRegenEveryNTurns);
                    buffer.WriteInt(config.turnCount);
                    buffer.WriteInt(config.diceType);
                    buffer.WriteInt(config.diceCount);
                    buffer.WriteInt(config.diceModifier);
                    // Resources
                    buffer.WriteInt(resources.Count);
                    foreach (var r in resources)
                    {
                        buffer.WriteInt(r.id);
                        buffer.WriteString(r.name ?? "");
                        buffer.WriteString(r.description ?? "");
                        buffer.WriteInt(r.baseValue);
                        buffer.WriteInt(r.maxValue);
                        buffer.WriteFloat(r.color.X);
                        buffer.WriteFloat(r.color.Y);
                        buffer.WriteFloat(r.color.Z);
                        buffer.WriteFloat(r.color.W);
                        buffer.WriteInt(r.linkedStatId);
                        buffer.WriteFloat(r.statMultiplier);
                        buffer.WriteInt(r.regenAmount);
                        buffer.WriteInt(r.regenEveryNTurns);
                    }
                    await ClientTCP.SendDataAsync(buffer.ToArray());
                }
            }
            catch (Exception ex) { Plugin.PluginLog.Debug($"SaveCombatConfig error: {ex.Message}"); }
        }

        public static async Task SaveSkillClasses(Character character, int systemId, List<SkillClassData> classes)
        {
            if (!ClientTCP.IsConnected()) return;
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteInt((int)ClientPackets.CSaveSkillClasses);
                    buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                    buffer.WriteString(character.characterKey);
                    buffer.WriteInt(systemId);
                    buffer.WriteInt(classes.Count);
                    foreach (var c in classes)
                    {
                        buffer.WriteInt(c.id);
                        buffer.WriteString(c.name ?? "");
                        buffer.WriteString(c.description ?? "");
                        buffer.WriteInt(c.sortOrder);
                        buffer.WriteBool(c.allowCustomSkills);
                        buffer.WriteInt(c.iconId);
                        buffer.WriteInt(c.initialSkillPoints);
                    }
                    // Skill trees (per class)
                    int totalTrees = 0;
                    foreach (var c in classes) totalTrees += c.SkillTrees.Count;
                    buffer.WriteInt(totalTrees);
                    foreach (var c in classes)
                    {
                        foreach (var t in c.SkillTrees)
                        {
                            buffer.WriteInt(c.id);
                            buffer.WriteString(t.name ?? "Skill Tree");
                            buffer.WriteInt(t.sortOrder);
                        }
                    }
                    await ClientTCP.SendDataAsync(buffer.ToArray());
                }
            }
            catch (Exception ex) { Plugin.PluginLog.Debug($"SaveSkillClasses error: {ex.Message}"); }
        }

        public static async Task SaveSkills(Character character, int systemId, List<SkillData> skills, List<SkillConnectionData> connections)
        {
            if (!ClientTCP.IsConnected()) return;
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteInt((int)ClientPackets.CSaveSkills);
                    buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                    buffer.WriteString(character.characterKey);
                    buffer.WriteInt(systemId);
                    buffer.WriteInt(skills.Count);
                    foreach (var s in skills)
                    {
                        buffer.WriteInt(s.id);
                        buffer.WriteInt(s.classId);
                        buffer.WriteInt(s.treeIndex);
                        buffer.WriteString(s.name ?? "");
                        buffer.WriteString(s.description ?? "");
                        buffer.WriteInt(s.iconId);
                        buffer.WriteInt(s.gridX);
                        buffer.WriteInt(s.gridY);
                        buffer.WriteBool(s.isCastable);
                        buffer.WriteInt(s.cooldownTurns);
                        buffer.WriteInt(s.resourceId);
                        buffer.WriteInt(s.resourceCost);
                        buffer.WriteInt(s.maxTiers);
                    }
                    // Connections
                    buffer.WriteInt(connections.Count);
                    foreach (var c in connections)
                    {
                        buffer.WriteInt(c.fromSkillId);
                        buffer.WriteInt(c.toSkillId);
                        buffer.WriteInt(c.requiredPoints);
                    }
                    await ClientTCP.SendDataAsync(buffer.ToArray());
                }
            }
            catch (Exception ex) { Plugin.PluginLog.Debug($"SaveSkills error: {ex.Message}"); }
        }

        public static async Task SubmitCharacterSheet(Character character, int systemId, int classId,
            Dictionary<int, int> statAllocations, List<int> selectedSkills, int profileId = -1)
        {
            if (!ClientTCP.IsConnected()) return;
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteInt((int)ClientPackets.CSubmitCharacterSheet);
                    buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                    buffer.WriteString(character.characterKey);
                    buffer.WriteInt(systemId);
                    buffer.WriteInt(classId);
                    buffer.WriteString(character.characterName);
                    buffer.WriteString(character.characterWorld);
                    // Stat values
                    buffer.WriteInt(statAllocations.Count);
                    foreach (var kvp in statAllocations)
                    {
                        buffer.WriteInt(kvp.Key);
                        buffer.WriteInt(kvp.Value);
                    }
                    // Skills
                    buffer.WriteInt(selectedSkills.Count);
                    foreach (var skillId in selectedSkills)
                        buffer.WriteInt(skillId);
                    buffer.WriteInt(profileId);
                    await ClientTCP.SendDataAsync(buffer.ToArray());
                }
            }
            catch (Exception ex) { Plugin.PluginLog.Debug($"SubmitCharacterSheet error: {ex.Message}"); }
        }

        public static async Task FetchSystemRoster(Character character, int systemId)
        {
            if (!ClientTCP.IsConnected()) return;
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteInt((int)ClientPackets.CFetchSystemRoster);
                    buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                    buffer.WriteString(character.characterKey);
                    buffer.WriteInt(systemId);
                    await ClientTCP.SendDataAsync(buffer.ToArray());
                }
            }
            catch (Exception ex) { Plugin.PluginLog.Debug($"FetchSystemRoster error: {ex.Message}"); }
        }

        public static async Task RespondToSheet(Character character, int sheetId, int status, string reason)
        {
            if (!ClientTCP.IsConnected()) return;
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteInt((int)ClientPackets.CRespondToSheet);
                    buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                    buffer.WriteString(character.characterKey);
                    buffer.WriteInt(sheetId);
                    buffer.WriteInt(status);
                    buffer.WriteString(reason ?? "");
                    await ClientTCP.SendDataAsync(buffer.ToArray());
                }
            }
            catch (Exception ex) { Plugin.PluginLog.Debug($"RespondToSheet error: {ex.Message}"); }
        }

        /// Upload a banner or logo image for a system. imageType: 0=banner, 1=logo
        public static async Task JoinSystem(Character character, int systemId)
        {
            if (!ClientTCP.IsConnected()) return;
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteInt((int)ClientPackets.CJoinSystem);
                    buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                    buffer.WriteString(character.characterKey);
                    buffer.WriteInt(systemId);
                    await ClientTCP.SendDataAsync(buffer.ToArray());
                }
            }
            catch (Exception ex) { Plugin.PluginLog.Debug($"JoinSystem error: {ex.Message}"); }
        }

        public static async Task LeaveSystem(Character character, int systemId)
        {
            if (!ClientTCP.IsConnected()) return;
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteInt((int)ClientPackets.CLeaveSystem);
                    buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                    buffer.WriteString(character.characterKey);
                    buffer.WriteInt(systemId);
                    await ClientTCP.SendDataAsync(buffer.ToArray());
                }
            }
            catch (Exception ex) { Plugin.PluginLog.Debug($"LeaveSystem error: {ex.Message}"); }
        }

        public static async Task FetchJoinedSystems(Character character)
        {
            if (!ClientTCP.IsConnected()) return;
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteInt((int)ClientPackets.CFetchJoinedSystems);
                    buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                    buffer.WriteString(character.characterKey);
                    await ClientTCP.SendDataAsync(buffer.ToArray());
                }
            }
            catch (Exception ex) { Plugin.PluginLog.Debug($"FetchJoinedSystems error: {ex.Message}"); }
        }

        public static async Task BanFromSystem(Character character, int systemId, string charName, string charWorld, string reason)
        {
            if (!ClientTCP.IsConnected()) return;
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteInt((int)ClientPackets.CBanFromSystem);
                    buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                    buffer.WriteString(character.characterKey);
                    buffer.WriteInt(systemId);
                    buffer.WriteString(charName);
                    buffer.WriteString(charWorld);
                    buffer.WriteString(reason ?? "");
                    await ClientTCP.SendDataAsync(buffer.ToArray());
                }
            }
            catch (Exception ex) { Plugin.PluginLog.Debug($"BanFromSystem error: {ex.Message}"); }
        }

        public static async Task UnbanFromSystem(Character character, int systemId, int banId)
        {
            if (!ClientTCP.IsConnected()) return;
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteInt((int)ClientPackets.CUnbanFromSystem);
                    buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                    buffer.WriteString(character.characterKey);
                    buffer.WriteInt(systemId);
                    buffer.WriteInt(banId);
                    await ClientTCP.SendDataAsync(buffer.ToArray());
                }
            }
            catch (Exception ex) { Plugin.PluginLog.Debug($"UnbanFromSystem error: {ex.Message}"); }
        }

        public static async Task FetchSystemBans(Character character, int systemId)
        {
            if (!ClientTCP.IsConnected()) return;
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteInt((int)ClientPackets.CFetchSystemBans);
                    buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                    buffer.WriteString(character.characterKey);
                    buffer.WriteInt(systemId);
                    await ClientTCP.SendDataAsync(buffer.ToArray());
                }
            }
            catch (Exception ex) { Plugin.PluginLog.Debug($"FetchSystemBans error: {ex.Message}"); }
        }

        public static async Task UpdateCharacterSheet(Character character, int sheetId, Dictionary<int, int> statValues, List<int> learnedSkills)
        {
            if (!ClientTCP.IsConnected()) return;
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteInt((int)ClientPackets.CUpdateCharacterSheet);
                    buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                    buffer.WriteString(character.characterKey);
                    buffer.WriteInt(sheetId);
                    buffer.WriteInt(statValues.Count);
                    foreach (var kvp in statValues)
                    {
                        buffer.WriteInt(kvp.Key);
                        buffer.WriteInt(kvp.Value);
                    }
                    buffer.WriteInt(learnedSkills.Count);
                    foreach (var skillId in learnedSkills)
                        buffer.WriteInt(skillId);
                    await ClientTCP.SendDataAsync(buffer.ToArray());
                }
            }
            catch (Exception ex) { Plugin.PluginLog.Debug($"UpdateCharacterSheet error: {ex.Message}"); }
        }

        public static async Task UpdateSheetResources(Character character, int sheetId, int currentHealth, Dictionary<int, int> resourceValues)
        {
            if (!ClientTCP.IsConnected()) return;
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteInt((int)ClientPackets.CUpdateSheetResources);
                    buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                    buffer.WriteString(character.characterKey);
                    buffer.WriteInt(sheetId);
                    buffer.WriteInt(currentHealth);
                    buffer.WriteInt(resourceValues.Count);
                    foreach (var kvp in resourceValues)
                    {
                        buffer.WriteInt(kvp.Key);
                        buffer.WriteInt(kvp.Value);
                    }
                    await ClientTCP.SendDataAsync(buffer.ToArray());
                }
            }
            catch (Exception ex) { Plugin.PluginLog.Debug($"UpdateSheetResources error: {ex.Message}"); }
        }

        public static async Task UploadSystemImage(Character character, int systemId, int imageType, byte[] imageBytes)
        {
            if (!ClientTCP.IsConnected() || imageBytes == null) return;
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteInt((int)ClientPackets.CUploadSystemImage);
                    buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                    buffer.WriteString(character.characterKey);
                    buffer.WriteInt(systemId);
                    buffer.WriteInt(imageType); // 0=banner, 1=logo
                    buffer.WriteInt(imageBytes.Length);
                    buffer.WriteBytes(imageBytes);
                    await ClientTCP.SendDataAsync(buffer.ToArray());
                }
            }
            catch (Exception ex) { Plugin.PluginLog.Debug($"UploadSystemImage error: {ex.Message}"); }
        }

        public static async Task UpdateSheetLevelPoints(Character character, int sheetId, int level, int bonusSkillPoints, int bonusStatPoints)
        {
            if (!ClientTCP.IsConnected()) return;
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteInt((int)ClientPackets.CUpdateSheetLevelPoints);
                    buffer.WriteString(DataSender.plugin.Configuration.account.accountKey);
                    buffer.WriteString(character.characterKey);
                    buffer.WriteInt(sheetId);
                    buffer.WriteInt(level);
                    buffer.WriteInt(bonusSkillPoints);
                    buffer.WriteInt(bonusStatPoints);
                    await ClientTCP.SendDataAsync(buffer.ToArray());
                }
            }
            catch (Exception ex) { Plugin.PluginLog.Debug($"UpdateSheetLevelPoints error: {ex.Message}"); }
        }
    }
}
