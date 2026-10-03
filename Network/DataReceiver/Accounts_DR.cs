using Networking;
using System;
using System.Collections.Generic;
using System.Text;
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
namespace AbsoluteRP.Network
{
    internal class Accounts_DR
    {
        public static Dictionary<int, AbsoluteRP.Defines.AccountProfile> AccountProfileCache =
            new Dictionary<int, AbsoluteRP.Defines.AccountProfile>();

        // Fires on every SendAccountProfile the server pushes down (ownerUserId, profile).
        public static event Action<int, AbsoluteRP.Defines.AccountProfile> AccountProfileReceived;
        public static void HandleSendAccountProfile(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    buffer.ReadInt(); // packet ID
                    int ownerUserId = buffer.ReadInt();
                    string displayName = buffer.ReadString();
                    string avatarUrl = buffer.ReadString();
                    string gender = buffer.ReadString();
                    int ageRaw = buffer.ReadInt();
                    int count = buffer.ReadInt();
                    var prefs = new List<string>(count > 0 ? count : 0);
                    for (int i = 0; i < count; i++) prefs.Add(buffer.ReadString());
                    // v2: bio + headerUrl appended at the end. Older servers won't send them - try/catch so a truncated payload still returns a usable profile with empty strings.
                    string bio = string.Empty, header = string.Empty;
                    try { bio = buffer.ReadString(); } catch { }
                    try { header = buffer.ReadString(); } catch { }
                    // v3: header framing offsets + zoom as int/1000.
                    int hox = 0, hoy = 0, hz = 1000;
                    try { hox = buffer.ReadInt(); } catch { }
                    try { hoy = buffer.ReadInt(); } catch { }
                    try { hz = buffer.ReadInt(); } catch { }

                    var profile = new AbsoluteRP.Defines.AccountProfile
                    {
                        displayName = displayName ?? string.Empty,
                        avatarUrl = avatarUrl ?? string.Empty,
                        gender = gender ?? string.Empty,
                        age = ageRaw > 0 ? (int?)ageRaw : null,
                        rpPreferences = prefs,
                        bio = bio ?? string.Empty,
                        headerUrl = header ?? string.Empty,
                        headerOffsetX = hox / 1000f,
                        headerOffsetY = hoy / 1000f,
                        headerZoom = hz > 0 ? hz / 1000f : 1f,
                    };

                    AccountProfileCache[ownerUserId] = profile;

                    // If this is the local user's own profile, mirror it into the persisted account config so UI reads it directly.
                    try
                    {
                        var cfg = Plugin.plugin?.Configuration;
                        if (cfg?.account != null && ownerUserId == cfg.account.userID)
                        {
                            cfg.account.profile = profile;
                            cfg.Save();
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.PluginLog.Debug("HandleSendAccountProfile config mirror failed: " + ex.Message);
                    }

                    try { AccountProfileReceived?.Invoke(ownerUserId, profile); }
                    catch (Exception ex) { Plugin.PluginLog.Debug("AccountProfileReceived subscriber threw: " + ex.Message); }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug("HandleSendAccountProfile Error: " + ex.Message);
            }
        }
    

        public static string restorationStatus = "";
        public static RankPermissions permissions { get; set; }
        public static Vector4 accounStatusColor, verificationStatusColor, forgotStatusColor, restorationStatusColor = new Vector4(255, 255, 255, 255);
        public static Dictionary<int, string> characters = new Dictionary<int, string>();
        public static Dictionary<int, string> adminCharacters = new Dictionary<int, string>();
        public static Dictionary<int, byte[]> adminCharacterAvatars = new Dictionary<int, byte[]>();
        public static Dictionary<int, byte[]> characterAvatars = new Dictionary<int, byte[]>();
        public static Dictionary<int, int> characterVerificationStatuses = new Dictionary<int, int>();
        public static bool loggedIn;
        public static bool isAdmin;

        // Received when the server acknowledges our connection. Updates the UI status bar.
        public static void HandleWelcomeMessage(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    var msg = buffer.ReadString();
                    _ = Task.Run(async () => { try { await Plugin.plugin.UpdateStatusAsync(); } catch { } });
                    // Handle the message as needed
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling Welcome message: {ex}");
            }

        }

        public static void BadLogin(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    var profiles = buffer.ReadString();
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling BadLogin message: {ex}");
            }
        }

        // Central status handler - processes login results, registration outcomes, account moderation actions (warnings, strikes, suspensions, bans), character verification via Lodestone, and other server responses. Updates UI status text/color and saves account keys on successful registration.
        public static void StatusMessage(byte[] data)
        {
            try
            {
                Plugin.PluginLog.Info($"[StatusMessage] CALLED - data length: {data.Length}");
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    Plugin.PluginLog.Info($"[StatusMessage] packetID: {packetID}");
                    int userID = buffer.ReadInt(); // ADDED: Read userID from server
                    Plugin.PluginLog.Info($"[StatusMessage] userID read from buffer: {userID}");
                    int status = buffer.ReadInt();
                    Plugin.PluginLog.Info($"[StatusMessage] status: {status}");
                    int rank = buffer.ReadInt();
                    bool announce = buffer.ReadBool();
                    bool suspend = buffer.ReadBool();
                    bool ban = buffer.ReadBool();
                    bool warn = buffer.ReadBool();
                    string message = buffer.ReadString();
                    string message2 = buffer.ReadString();
                    string characterName = buffer.ReadString();
                    string characterWorld = buffer.ReadString();
                    // Only the login reply carries the account's real rank; other status messages send an empty one.
                    if (status == (int)UI.StatusMessages.LOGIN_VERIFIED || permissions == null)
                        permissions = new RankPermissions() { can_announce = announce, can_suspend = suspend, can_ban = ban, rank = rank, can_warn = warn };

                    // Store userID for current user (later status messages may send 0)
                    if (userID > 0)
                    {
                        Accounts_DS.userID = userID;
                        Plugin.plugin.Configuration.account.userID = userID;
                    }
                    Plugin.PluginLog.Info($"[StatusMessage] Set userID to {userID}");

                    // Receive Status
                    if (status == (int)UI.StatusMessages.RECEIVE_SILENT)
                    {
                        Profiles_DR.silentUpdate = true;
                    }
                    if (status == (int)UI.StatusMessages.RECEIVE_UPDATES)
                    {
                        Profiles_DR.silentUpdate = false;
                    }

                    // account window
                    if (status == (int)UI.StatusMessages.LOGIN_BANNED)
                    {
                        AccountWindow.statusColor = new Vector4(255, 0, 0, 255);
                        AccountWindow.status = "Account Banned";
                    }
                    if (status == (int)UI.StatusMessages.LOGIN_WRONG_INFORMATION)
                    {
                        AccountWindow.statusColor = new Vector4(255, 0, 0, 255);
                        AccountWindow.status = "Account not found";
                        AccountWindow.loggedIn = false;
                        Plugin.plugin.loggedIn = false;
                    }
                    if (status == (int)UI.StatusMessages.LOGIN_UNVERIFIED)
                    {
                        AccountWindow.statusColor = new Vector4(255, 255, 0, 255);
                        AccountWindow.status = "Unverified Account";
                    }
                    if (status == (int)UI.StatusMessages.LOGIN_VERIFIED)
                    {
                        AccountWindow.status = "Logged In";
                        AccountWindow.statusColor = new Vector4(0, 255, 0, 255);
                        AccountWindow.loggedIn = true;
                        Plugin.plugin.loggedIn = true;
                        // Pull the account profile from the server so. Configuration.account.profile matches the DB.
                        try { Accounts_DS.RequestAccountProfile(0); }
                        catch (Exception ex) { Plugin.PluginLog.Debug("RequestAccountProfile(0) on login failed: " + ex.Message); }
                    }
                    if (status == (int)UI.StatusMessages.REGISTRATION_SUCCESSFUL)
                    {
                        AccountWindow.statusColor = new Vector4(0, 255, 0, 255);
                        AccountWindow.status = "Tag Creation Succeeded";
                        Account account = new Account()
                        {
                            accountKey = message,
                            accountName = message2
                        };
                        Plugin.plugin.Configuration.account = account;
                        // A new account owns no characters yet. Drop any that were cached under the previous account so the profile page doesn't show them as "verified" for this fresh identity - those keys belong to the old account and the new one has to redo the. Lodestone flow to claim any character.
                        if (Plugin.plugin.Configuration.characters.Count > 0)
                        {
                            Plugin.plugin.Configuration.characters.Clear();
                        }
                        Plugin.character = null;
                        Plugin.characterKeysVerified = false;
                        Plugin.plugin.Configuration.Save();

                    }

                    if (status == (int)UI.StatusMessages.REGISTRATION_DUPLICATE_TAG_NAME)
                    {
                        AccountWindow.statusColor = new Vector4(255, 255, 0, 255);
                        AccountWindow.status = "Tag already in use.";
                    }
                    if (status == (int)UI.StatusMessages.CHARACTER_REGISTRATION_VALID_LODESTONE)
                    {
                        // Remove any existing entry for this character to prevent duplicates with stale keys
                        Plugin.plugin.Configuration.characters.RemoveAll(c =>
                            string.Equals(c.characterName, characterName, StringComparison.OrdinalIgnoreCase));

                        // Prefer the game's world name over the scraped one when they refer to the same world.
                        var localWorld = Plugin.plugin?.playerworld;
                        var worldForEntry = !string.IsNullOrWhiteSpace(localWorld) && string.Equals(Plugin.plugin?.playername, characterName, StringComparison.OrdinalIgnoreCase) ? localWorld : characterWorld;
                        Character character = new Character()
                        {
                            characterName = characterName,
                            characterWorld = worldForEntry,
                            characterKey = message2,
                        };
                        Plugin.plugin.Configuration.characters.Add(character);

                        Plugin.plugin.Configuration.Save();

                        // The plugin keeps using the character object it resolved earlier, which is the entry just replaced (stale / empty key) - so requests kept failing until a reconnect. Point it at the verified one now and mark the keys usable.
                        if (Plugin.character == null
                            || (Plugin.character.characterName == characterName && Plugin.character.characterWorld == characterWorld))
                            Plugin.character = character;
                        Plugin.characterKeysVerified = true;

                        ProfilesPage.VerificationSucceeded = true;
                        ProfilesPage.checking = false;
                        Profiles_DS.FetchProfiles(character);
                        Profiles_DS.FetchProfile(character, true, 0, character.characterName, character.characterWorld, -1);
                    }
                    if (status == (int)UI.StatusMessages.CHARACTER_REGISTRATIO_INVALID_LODESTONE)
                    {
                        ProfilesPage.VerificationFailed = true;
                        ProfilesPage.VerificationSucceeded = false;
                        ProfilesPage.checking = false;
                    }
                    if (status == (int)UI.StatusMessages.CHARACTER_REGISTRATION_LODESTONE_KEY)
                    {
                        ProfilesPage.lodeStoneKey = message;
                    }
                    if (status == (int)UI.StatusMessages.FORGOT_REQUEST_RECEIVED)
                    {
                        AccountWindow.statusColor = new Vector4(0, 255, 0, 255);
                        AccountWindow.status = "Request received, please stand by...";
                    }
                    if (status == (int)UI.StatusMessages.FORGOT_REQUEST_INCORRECT)
                    {
                        AccountWindow.statusColor = new Vector4(255, 255, 0, 255);
                        AccountWindow.status = "There is no account with this email.";
                    }
                    // Restoration window

                    if (status == (int)UI.StatusMessages.REGISTRATION_INSUFFICIENT_DATA)
                    {
                        AccountWindow.statusColor = new Vector4(255, 0, 0, 255);
                        AccountWindow.status = "Please fill all fields.";
                    }
                    if (status == (int)UI.StatusMessages.ACCOUNT_WARNING)
                    {
                        ImportantNotice.messageTitle = "Warning";
                        ImportantNotice.moderatorMessage = message;
                        Plugin.plugin.OpenImportantNoticeWindow();
                    }
                    if (status == (int)UI.StatusMessages.ACCOUNT_STRIKE)
                    {
                        ImportantNotice.messageTitle = "Your account received a strike!";
                        ImportantNotice.moderatorMessage = message;
                        Plugin.plugin.OpenImportantNoticeWindow();
                    }
                    if (status == (int)UI.StatusMessages.ACCOUNT_SUSPENDED)
                    {
                        Plugin.plugin.DisconnectAndLogOut();
                        Plugin.plugin.Configuration.Save();
                        AccountWindow.statusColor = new Vector4(255, 0, 0, 255);
                        AccountWindow.status = "Account suspended"; ;
                        if (message != string.Empty)
                        {
                            ImportantNotice.messageTitle = "Account Suspended!";
                            ImportantNotice.moderatorMessage = message;
                            Plugin.plugin.OpenImportantNoticeWindow();
                        }
                    }
                    if (status == (int)UI.StatusMessages.ACCOUNT_BANNED)
                    {
                        Plugin.plugin.DisconnectAndLogOut();
                        AccountWindow.statusColor = new Vector4(255, 0, 0, 255);
                        AccountWindow.status = "Account banned";
                        ImportantNotice.messageTitle = "Account Banned!";
                        ImportantNotice.moderatorMessage = message;
                        Plugin.plugin.OpenImportantNoticeWindow();
                    }
                    if (status == (int)UI.StatusMessages.ACTION_SUCCESS)
                    {
                        ModPanel.status = "Action was submitted";
                        ModPanel.statusColor = new Vector4(255, 0, 0, 255);
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling StatusMessage message: {ex}");
            }
        }

        // Server rejected an action because the user lacks permission for that profile
        public static void ReceiveNoAuthorization(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    AccountWindow.statusColor = new Vector4(1, 0, 0, 1);
                    AccountWindow.status = "Unauthorized Access to Profile.";
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"Debug handling ReceiveNoAuthorization message: {ex}");
            }
        }

        /// Handles receiving verified characters from the server. Syncs missing characters to the plugin configuration.
        public static void HandleVerifiedCharacters(byte[] data)
        {
            try
            {
                using (var buffer = new ByteBuffer())
                {
                    buffer.WriteBytes(data);
                    var packetID = buffer.ReadInt();
                    int characterCount = buffer.ReadInt();

                    Plugin.PluginLog.Info($"[HandleVerifiedCharacters] Received {characterCount} verified characters from server");

                    bool configChanged = false;

                    // Build set of server-verified characters
                    var serverCharacters = new List<(string key, string name, string world)>();
                    for (int i = 0; i < characterCount; i++)
                    {
                        string characterKey = buffer.ReadString();
                        string characterName = buffer.ReadString();
                        string characterWorld = buffer.ReadString();
                        serverCharacters.Add((characterKey, characterName, characterWorld));
                    }

                    // Worlds as the server scraped them ("Mateus [Crystal]", odd spaces) vs the game's own name: compare the first word, case-insensitively.
                    static string NormWorld(string w)
                    {
                        if (string.IsNullOrWhiteSpace(w)) return string.Empty;
                        w = w.Replace((char)0xA0, ' ').Trim();
                        var br = w.IndexOf('['); if (br >= 0) w = w.Substring(0, br);
                        var sp = w.IndexOf(' '); if (sp > 0) w = w.Substring(0, sp);
                        return w.Trim();
                    }
                    static bool SameName(string a, string b) => string.Equals((a ?? "").Trim(), (b ?? "").Trim(), StringComparison.OrdinalIgnoreCase);
                    static bool SameWorld(string a, string b)
                    {
                        var na = NormWorld(a); var nb = NormWorld(b);
                        return na.Length == 0 || nb.Length == 0 || string.Equals(na, nb, StringComparison.OrdinalIgnoreCase);
                    }

                    // Add or update characters from server
                    foreach (var (characterKey, characterName, characterWorld) in serverCharacters)
                    {
                        var existingChar = Plugin.plugin.Configuration.characters.FirstOrDefault(c =>
                            SameName(c.characterName, characterName) && SameWorld(c.characterWorld, characterWorld));

                        if (existingChar == null && !string.IsNullOrEmpty(characterName) && !string.IsNullOrEmpty(characterWorld))
                        {
                            Plugin.PluginLog.Info($"[HandleVerifiedCharacters] Adding character: {characterName}@{characterWorld}");
                            Plugin.plugin.Configuration.characters.Add(new Character
                            {
                                characterName = characterName,
                                characterWorld = characterWorld,
                                characterKey = characterKey
                            });
                            configChanged = true;
                        }
                        else if (existingChar != null)
                        {
                            if (existingChar.characterKey != characterKey)
                            {
                                Plugin.PluginLog.Info($"[HandleVerifiedCharacters] Updating key for: {characterName}@{characterWorld}");
                                existingChar.characterKey = characterKey;
                                configChanged = true;
                            }
                            // Keep the game's own world name; fill it in if it was missing.
                            if (string.IsNullOrWhiteSpace(existingChar.characterWorld) && !string.IsNullOrWhiteSpace(characterWorld))
                            { existingChar.characterWorld = NormWorld(characterWorld); configChanged = true; }
                        }
                    }

                    // Remove local characters that the server no longer has verified
                    if (serverCharacters.Count > 0)
                    {
                        var toRemove = Plugin.plugin.Configuration.characters
                            .Where(c => !serverCharacters.Any(sc => SameName(sc.name, c.characterName) && SameWorld(sc.world, c.characterWorld)))
                            .ToList();
                        foreach (var stale in toRemove)
                        {
                            Plugin.PluginLog.Info($"[HandleVerifiedCharacters] Removing stale character: {stale.characterName}@{stale.characterWorld} (key={stale.characterKey})");
                            Plugin.plugin.Configuration.characters.Remove(stale);
                            configChanged = true;
                        }
                    }
                    else
                    {
                        // Server has 0 verified characters - clear all local characters
                        if (Plugin.plugin.Configuration.characters.Count > 0)
                        {
                            Plugin.PluginLog.Info($"[HandleVerifiedCharacters] Server has 0 verified characters, clearing {Plugin.plugin.Configuration.characters.Count} local characters");
                            Plugin.plugin.Configuration.characters.Clear();
                            configChanged = true;
                        }
                    }

                    if (configChanged)
                    {
                        Plugin.plugin.Configuration.Save();
                        Plugin.PluginLog.Info($"[HandleVerifiedCharacters] Configuration saved");
                    }

                    // Mark keys as verified so other systems can safely use them
                    Plugin.characterKeysVerified = true;
                    // Re-resolve Plugin.character with fresh keys
                    Plugin.character = null;

                    // Fetch joined systems on login (so imported systems appear without opening the Systems window)
                    // Delay slightly to let Plugin.character resolve on the next frame
                    _ = System.Threading.Tasks.Task.Run(async () =>
                    {
                        await System.Threading.Tasks.Task.Delay(1000);
                        if (Plugin.character != null)
                        {
                            AbsoluteRP.Network.Systems_DS.FetchJoinedSystems(Plugin.character);
                            AbsoluteRP.Network.Systems_DS.FetchMySystems(Plugin.character);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Error($"[HandleVerifiedCharacters] Error: {ex.Message}");
            }
        }
    }
}
