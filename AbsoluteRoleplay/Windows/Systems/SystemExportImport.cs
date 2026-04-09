using AbsoluteRP.Defines;
using AbsoluteRP.Helpers;
using AbsoluteRP.Windows.Listings;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiFileDialog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;

namespace AbsoluteRP.Windows.Systems
{
    /// <summary>
    /// Handles exporting/importing system data and roster backups to/from JSON files.
    /// </summary>
    internal class SystemExportImport
    {
        private static FileDialogManager _fileDialog = new FileDialogManager();
        private static string statusMessage = "";
        private static Vector4 statusColor = Vector4.One;

        public static void DrawFileDialogs()
        {
            _fileDialog.Draw();
        }

        // ── Export System ──
        public static void ExportSystem(SystemData system)
        {
            if (system == null || system.id <= 0) return;

            var exportData = new Dictionary<string, object>
            {
                ["name"] = system.name,
                ["description"] = system.description ?? "",
                ["basePointsAvailable"] = system.basePointsAvailable,
                ["requireApproval"] = system.requireApproval,
                ["rules"] = system.rules ?? "",
                ["stats"] = system.StatsData.Values.Select(s => new Dictionary<string, object>
                {
                    ["id"] = s.id, ["name"] = s.name, ["description"] = s.description ?? "",
                    ["colorR"] = s.color.X, ["colorG"] = s.color.Y, ["colorB"] = s.color.Z, ["colorA"] = s.color.W,
                    ["baseMin"] = s.baseMin, ["baseMax"] = s.baseMax,
                    ["canAddPoints"] = s.canAddPoints, ["canRemovePoints"] = s.canRemovePoints,
                    ["canGoNegative"] = s.canGoNegative, ["negativeGivesPoint"] = s.negativeGivesPoint,
                }).ToList(),
                ["combatConfig"] = new Dictionary<string, object>
                {
                    ["healthEnabled"] = system.CombatConfig.healthEnabled,
                    ["healthBase"] = system.CombatConfig.healthBase, ["healthMax"] = system.CombatConfig.healthMax,
                    ["healthLinkedStatId"] = system.CombatConfig.healthLinkedStatId,
                    ["healthStatMultiplier"] = system.CombatConfig.healthStatMultiplier,
                    ["healthRegenAmount"] = system.CombatConfig.healthRegenAmount,
                    ["healthRegenEveryNTurns"] = system.CombatConfig.healthRegenEveryNTurns,
                    ["turnCount"] = system.CombatConfig.turnCount, ["diceType"] = system.CombatConfig.diceType,
                    ["diceCount"] = system.CombatConfig.diceCount, ["diceModifier"] = system.CombatConfig.diceModifier,
                },
                ["resources"] = system.Resources.Select(r => new Dictionary<string, object>
                {
                    ["id"] = r.id, ["name"] = r.name, ["description"] = r.description ?? "",
                    ["baseValue"] = r.baseValue, ["maxValue"] = r.maxValue,
                    ["colorR"] = r.color.X, ["colorG"] = r.color.Y, ["colorB"] = r.color.Z, ["colorA"] = r.color.W,
                    ["linkedStatId"] = r.linkedStatId, ["statMultiplier"] = r.statMultiplier,
                    ["regenAmount"] = r.regenAmount, ["regenEveryNTurns"] = r.regenEveryNTurns,
                }).ToList(),
                ["skillClasses"] = system.SkillClasses.Select(c => new Dictionary<string, object>
                {
                    ["id"] = c.id, ["name"] = c.name, ["description"] = c.description ?? "",
                    ["sortOrder"] = c.sortOrder, ["iconId"] = c.iconId,
                    ["initialSkillPoints"] = c.initialSkillPoints,
                    ["trees"] = c.SkillTrees.Select(t => new Dictionary<string, object>
                    {
                        ["name"] = t.name, ["sortOrder"] = t.sortOrder,
                    }).ToList(),
                }).ToList(),
                ["skills"] = system.Skills.Select(s => new Dictionary<string, object>
                {
                    ["id"] = s.id, ["classId"] = s.classId, ["treeIndex"] = s.treeIndex,
                    ["name"] = s.name, ["description"] = s.description ?? "",
                    ["iconId"] = s.iconId, ["gridX"] = s.gridX, ["gridY"] = s.gridY,
                    ["isCastable"] = s.isCastable, ["cooldownTurns"] = s.cooldownTurns,
                    ["resourceId"] = s.resourceId, ["resourceCost"] = s.resourceCost, ["maxTiers"] = s.maxTiers,
                }).ToList(),
                ["skillConnections"] = system.SkillConnections.Select(c => new Dictionary<string, object>
                {
                    ["fromSkillId"] = c.fromSkillId, ["toSkillId"] = c.toSkillId, ["requiredPoints"] = c.requiredPoints,
                }).ToList(),
            };

            string json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
            string safeName = string.Join("_", system.name.Split(Path.GetInvalidFileNameChars()));

            _fileDialog.SaveFileDialog("Export System", $"{safeName}.json", $"{safeName}.json", ".json", (ok, path) =>
            {
                if (ok && !string.IsNullOrEmpty(path))
                {
                    try
                    {
                        File.WriteAllText(path, json);
                        statusMessage = $"Exported to {Path.GetFileName(path)}";
                        statusColor = new Vector4(0.3f, 1f, 0.3f, 1f);
                    }
                    catch (Exception ex)
                    {
                        statusMessage = $"Export failed: {ex.Message}";
                        statusColor = new Vector4(1f, 0.3f, 0.3f, 1f);
                    }
                }
            }, null, false);
        }

        // ── Import System ──
        public static void ImportSystem()
        {
            _fileDialog.OpenFileDialog("Import System", ".json", (ok, paths) =>
            {
                if (ok && paths.Count > 0)
                {
                    try
                    {
                        string json = File.ReadAllText(paths[0]);
                        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                        if (data == null) { statusMessage = "Invalid file"; statusColor = new Vector4(1, 0.3f, 0.3f, 1); return; }

                        var system = new SystemData
                        {
                            name = data.ContainsKey("name") ? data["name"].GetString() : "Imported System",
                            description = data.ContainsKey("description") ? data["description"].GetString() : "",
                            basePointsAvailable = data.ContainsKey("basePointsAvailable") ? data["basePointsAvailable"].GetInt32() : 10,
                            requireApproval = data.ContainsKey("requireApproval") && data["requireApproval"].GetBoolean(),
                            rules = data.ContainsKey("rules") ? data["rules"].GetString() : "",
                        };

                        // Stats
                        // Stats — build old ID to new ID mapping
                        var statIdMap = new Dictionary<int, int>();
                        if (data.ContainsKey("stats"))
                        {
                            int sortIdx = 0;
                            foreach (var s in data["stats"].EnumerateArray())
                            {
                                int oldStatId = s.TryGetProperty("id", out var osid) ? osid.GetInt32() : sortIdx;
                                int newStatId = -(sortIdx + 1);
                                statIdMap[oldStatId] = newStatId;

                                system.StatsData[sortIdx] = new StatData
                                {
                                    id = newStatId,
                                    name = s.GetProperty("name").GetString() ?? "",
                                    description = s.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "",
                                    color = new Vector4(
                                        s.TryGetProperty("colorR", out var cr) ? cr.GetSingle() : 1,
                                        s.TryGetProperty("colorG", out var cg) ? cg.GetSingle() : 1,
                                        s.TryGetProperty("colorB", out var cb) ? cb.GetSingle() : 1,
                                        s.TryGetProperty("colorA", out var ca) ? ca.GetSingle() : 1),
                                    baseMin = s.TryGetProperty("baseMin", out var mn) ? mn.GetInt32() : 0,
                                    baseMax = s.TryGetProperty("baseMax", out var mx) ? mx.GetInt32() : 100,
                                    canAddPoints = !s.TryGetProperty("canAddPoints", out var ap) || ap.GetBoolean(),
                                    canRemovePoints = !s.TryGetProperty("canRemovePoints", out var rp) || rp.GetBoolean(),
                                    canGoNegative = s.TryGetProperty("canGoNegative", out var ng) && ng.GetBoolean(),
                                    negativeGivesPoint = s.TryGetProperty("negativeGivesPoint", out var ngp) && ngp.GetBoolean(),
                                };
                                sortIdx++;
                            }
                        }

                        // Combat — remap healthLinkedStatId
                        if (data.ContainsKey("combatConfig"))
                        {
                            var c = data["combatConfig"];
                            int oldHealthLinked = c.TryGetProperty("healthLinkedStatId", out var hls) ? hls.GetInt32() : -1;
                            int newHealthLinked = statIdMap.ContainsKey(oldHealthLinked) ? statIdMap[oldHealthLinked] : oldHealthLinked;
                            system.CombatConfig = new CombatConfigData
                            {
                                healthEnabled = c.TryGetProperty("healthEnabled", out var he) && he.GetBoolean(),
                                healthBase = c.TryGetProperty("healthBase", out var hb) ? hb.GetInt32() : 100,
                                healthMax = c.TryGetProperty("healthMax", out var hm) ? hm.GetInt32() : 100,
                                healthLinkedStatId = newHealthLinked,
                                healthStatMultiplier = c.TryGetProperty("healthStatMultiplier", out var hsm) ? hsm.GetSingle() : 1,
                                healthRegenAmount = c.TryGetProperty("healthRegenAmount", out var hra) ? hra.GetInt32() : 0,
                                healthRegenEveryNTurns = c.TryGetProperty("healthRegenEveryNTurns", out var hrt) ? hrt.GetInt32() : 0,
                                turnCount = c.TryGetProperty("turnCount", out var tc) ? tc.GetInt32() : 1,
                                diceType = c.TryGetProperty("diceType", out var dt2) ? dt2.GetInt32() : 20,
                                diceCount = c.TryGetProperty("diceCount", out var dc) ? dc.GetInt32() : 1,
                                diceModifier = c.TryGetProperty("diceModifier", out var dm) ? dm.GetInt32() : 0,
                            };
                        }

                        // Resources — remap linkedStatId, build resource ID map
                        var resourceIdMap = new Dictionary<int, int>();
                        if (data.ContainsKey("resources"))
                        {
                            int resIdx = 0;
                            foreach (var r in data["resources"].EnumerateArray())
                            {
                                int oldResId = r.TryGetProperty("id", out var orid) ? orid.GetInt32() : resIdx;
                                int newResId = -(resIdx + 1);
                                resourceIdMap[oldResId] = newResId;

                                int oldLinkedStat = r.TryGetProperty("linkedStatId", out var ls) ? ls.GetInt32() : -1;
                                int newLinkedStat = statIdMap.ContainsKey(oldLinkedStat) ? statIdMap[oldLinkedStat] : oldLinkedStat;

                                system.Resources.Add(new ResourceData
                                {
                                    id = newResId,
                                    name = r.GetProperty("name").GetString() ?? "",
                                    description = r.TryGetProperty("description", out var rd) ? rd.GetString() ?? "" : "",
                                    baseValue = r.TryGetProperty("baseValue", out var bv) ? bv.GetInt32() : 100,
                                    maxValue = r.TryGetProperty("maxValue", out var mv) ? mv.GetInt32() : 100,
                                    color = new Vector4(
                                        r.TryGetProperty("colorR", out var rcr) ? rcr.GetSingle() : 0.2f,
                                        r.TryGetProperty("colorG", out var rcg) ? rcg.GetSingle() : 0.5f,
                                        r.TryGetProperty("colorB", out var rcb) ? rcb.GetSingle() : 1f,
                                        r.TryGetProperty("colorA", out var rca) ? rca.GetSingle() : 1f),
                                    linkedStatId = newLinkedStat,
                                    statMultiplier = r.TryGetProperty("statMultiplier", out var sm) ? sm.GetSingle() : 1,
                                    regenAmount = r.TryGetProperty("regenAmount", out var ra) ? ra.GetInt32() : 0,
                                    regenEveryNTurns = r.TryGetProperty("regenEveryNTurns", out var rt) ? rt.GetInt32() : 0,
                                });
                                resIdx++;
                            }
                        }

                        // Skill classes — build old ID to new ID mapping
                        var classIdMap = new Dictionary<int, int>();
                        if (data.ContainsKey("skillClasses"))
                        {
                            int clsIdx = 0;
                            foreach (var cls in data["skillClasses"].EnumerateArray())
                            {
                                int oldClsId = cls.TryGetProperty("id", out var ocid) ? ocid.GetInt32() : clsIdx;
                                int newClsId = -(clsIdx + 1);
                                classIdMap[oldClsId] = newClsId;

                                var newClass = new SkillClassData
                                {
                                    id = newClsId,
                                    name = cls.GetProperty("name").GetString() ?? "",
                                    description = cls.TryGetProperty("description", out var cd) ? cd.GetString() ?? "" : "",
                                    sortOrder = cls.TryGetProperty("sortOrder", out var so) ? so.GetInt32() : clsIdx,
                                    iconId = cls.TryGetProperty("iconId", out var ic) ? ic.GetInt32() : 0,
                                    initialSkillPoints = cls.TryGetProperty("initialSkillPoints", out var isp) ? isp.GetInt32() : 0,
                                };
                                if (cls.TryGetProperty("trees", out var trees))
                                {
                                    foreach (var t in trees.EnumerateArray())
                                    {
                                        newClass.SkillTrees.Add(new SkillTreeData
                                        {
                                            name = t.GetProperty("name").GetString() ?? "Main Tree",
                                            sortOrder = t.TryGetProperty("sortOrder", out var tso) ? tso.GetInt32() : 0,
                                        });
                                    }
                                }
                                if (newClass.SkillTrees.Count == 0)
                                    newClass.SkillTrees.Add(new SkillTreeData { name = "Main Tree" });
                                system.SkillClasses.Add(newClass);
                                clsIdx++;
                            }
                        }

                        // Skills — build old ID to new ID mapping for connections
                        var skillIdMap = new Dictionary<int, int>();
                        if (data.ContainsKey("skills"))
                        {
                            int skIdx = 0;
                            foreach (var sk in data["skills"].EnumerateArray())
                            {
                                int oldId = sk.TryGetProperty("id", out var oid) ? oid.GetInt32() : skIdx;
                                int newId = -(skIdx + 1);
                                skillIdMap[oldId] = newId;

                                int oldClassId = sk.TryGetProperty("classId", out var scid) ? scid.GetInt32() : -1;
                                int newClassId = classIdMap.ContainsKey(oldClassId) ? classIdMap[oldClassId] : oldClassId;
                                int oldResId = sk.TryGetProperty("resourceId", out var sri) ? sri.GetInt32() : -1;
                                int newResId = resourceIdMap.ContainsKey(oldResId) ? resourceIdMap[oldResId] : oldResId;

                                system.Skills.Add(new SkillData
                                {
                                    id = newId,
                                    classId = newClassId,
                                    treeIndex = sk.TryGetProperty("treeIndex", out var sti) ? sti.GetInt32() : 0,
                                    name = sk.GetProperty("name").GetString() ?? "",
                                    description = sk.TryGetProperty("description", out var sd) ? sd.GetString() ?? "" : "",
                                    iconId = sk.TryGetProperty("iconId", out var si) ? si.GetInt32() : 0,
                                    gridX = sk.TryGetProperty("gridX", out var gx) ? gx.GetInt32() : 0,
                                    gridY = sk.TryGetProperty("gridY", out var gy) ? gy.GetInt32() : 0,
                                    isCastable = !sk.TryGetProperty("isCastable", out var sc) || sc.GetBoolean(),
                                    cooldownTurns = sk.TryGetProperty("cooldownTurns", out var scd) ? scd.GetInt32() : 0,
                                    resourceId = newResId,
                                    resourceCost = sk.TryGetProperty("resourceCost", out var src) ? src.GetInt32() : 0,
                                    maxTiers = sk.TryGetProperty("maxTiers", out var smt) ? smt.GetInt32() : 1,
                                });
                                skIdx++;
                            }
                        }

                        // Connections — remap old skill IDs to new temp IDs
                        if (data.ContainsKey("skillConnections"))
                        {
                            foreach (var conn in data["skillConnections"].EnumerateArray())
                            {
                                int oldFrom = conn.GetProperty("fromSkillId").GetInt32();
                                int oldTo = conn.GetProperty("toSkillId").GetInt32();
                                int newFrom = skillIdMap.ContainsKey(oldFrom) ? skillIdMap[oldFrom] : oldFrom;
                                int newTo = skillIdMap.ContainsKey(oldTo) ? skillIdMap[oldTo] : oldTo;
                                system.SkillConnections.Add(new SkillConnectionData
                                {
                                    fromSkillId = newFrom,
                                    toSkillId = newTo,
                                    requiredPoints = conn.TryGetProperty("requiredPoints", out var rpts) ? rpts.GetInt32() : 1,
                                });
                            }
                        }

                        // Add to managed systems and create on server
                        SystemsWindow.systemData.Add(system);
                        SystemsWindow.currentSystemIndex = SystemsWindow.systemData.Count - 1;
                        SystemsWindow.currentSystem = system;

                        if (Plugin.character != null)
                            Networking.DataSender.CreateSystem(Plugin.character, system.name, system.description ?? "");

                        statusMessage = $"Imported \"{system.name}\" — save to persist";
                        statusColor = new Vector4(0.3f, 1f, 0.3f, 1f);
                    }
                    catch (Exception ex)
                    {
                        statusMessage = $"Import failed: {ex.Message}";
                        statusColor = new Vector4(1f, 0.3f, 0.3f, 1f);
                    }
                }
            }, 1, null, false);
        }

        // ── Export Roster ──
        public static void ExportRoster(SystemData system, List<CharacterSheetData> sheets)
        {
            if (system == null || sheets == null || sheets.Count == 0) return;

            var exportData = sheets.Select(s => new Dictionary<string, object>
            {
                ["characterName"] = s.characterName,
                ["characterWorld"] = s.characterWorld,
                ["classId"] = s.classId,
                ["statValues"] = s.statValues,
                ["learnedSkills"] = s.learnedSkills,
                ["level"] = s.level,
                ["bonusSkillPoints"] = s.bonusSkillPoints,
                ["status"] = s.status,
                ["profileName"] = s.profileName ?? "",
            }).ToList();

            string json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
            string safeName = string.Join("_", system.name.Split(Path.GetInvalidFileNameChars()));

            _fileDialog.SaveFileDialog("Export Roster", $"{safeName}_roster.json", $"{safeName}_roster.json", ".json", (ok, path) =>
            {
                if (ok && !string.IsNullOrEmpty(path))
                {
                    try
                    {
                        File.WriteAllText(path, json);
                        statusMessage = $"Roster exported ({sheets.Count} sheets)";
                        statusColor = new Vector4(0.3f, 1f, 0.3f, 1f);
                    }
                    catch (Exception ex)
                    {
                        statusMessage = $"Roster export failed: {ex.Message}";
                        statusColor = new Vector4(1f, 0.3f, 0.3f, 1f);
                    }
                }
            }, null, false);
        }

        // ── Import Roster ──
        public static void ImportRoster(SystemData system)
        {
            if (system == null) return;

            _fileDialog.OpenFileDialog("Import Roster", ".json", (ok, paths) =>
            {
                if (ok && paths.Count > 0)
                {
                    try
                    {
                        string json = File.ReadAllText(paths[0]);
                        var imported = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(json);
                        if (imported == null) { statusMessage = "Invalid roster file"; statusColor = new Vector4(1, 0.3f, 0.3f, 1); return; }

                        int count = 0;
                        foreach (var entry in imported)
                        {
                            var sheet = new CharacterSheetData
                            {
                                systemId = system.id,
                                characterName = entry.ContainsKey("characterName") ? entry["characterName"].GetString() ?? "" : "",
                                characterWorld = entry.ContainsKey("characterWorld") ? entry["characterWorld"].GetString() ?? "" : "",
                                classId = entry.ContainsKey("classId") ? entry["classId"].GetInt32() : -1,
                                level = entry.ContainsKey("level") ? entry["level"].GetInt32() : 1,
                                bonusSkillPoints = entry.ContainsKey("bonusSkillPoints") ? entry["bonusSkillPoints"].GetInt32() : 0,
                                status = entry.ContainsKey("status") ? entry["status"].GetInt32() : 1,
                                profileName = entry.ContainsKey("profileName") ? entry["profileName"].GetString() ?? "" : "",
                            };

                            if (entry.ContainsKey("statValues"))
                                sheet.statValues = JsonSerializer.Deserialize<Dictionary<int, int>>(entry["statValues"].GetRawText()) ?? new Dictionary<int, int>();
                            if (entry.ContainsKey("learnedSkills"))
                                sheet.learnedSkills = JsonSerializer.Deserialize<List<int>>(entry["learnedSkills"].GetRawText()) ?? new List<int>();

                            Roster.Roster.sheets.Add(sheet);
                            count++;
                        }

                        statusMessage = $"Imported {count} roster entries";
                        statusColor = new Vector4(0.3f, 1f, 0.3f, 1f);
                    }
                    catch (Exception ex)
                    {
                        statusMessage = $"Roster import failed: {ex.Message}";
                        statusColor = new Vector4(1f, 0.3f, 0.3f, 1f);
                    }
                }
            }, 1, null, false);
        }

        public static void DrawStatusMessage()
        {
            if (!string.IsNullOrEmpty(statusMessage))
            {
                ImGui.TextColored(statusColor, statusMessage);
            }
        }

        public static void ClearStatus()
        {
            statusMessage = "";
        }
    }
}
