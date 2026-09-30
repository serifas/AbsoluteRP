using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AbsoluteRP.Defines
{
    // Represents the user's AbsoluteRP account - credentials + server-assigned ID. The accountKey is the encrypted token used to authenticate with the server.
    public class Account
    {
        public int userID { get; set; }
        public string accountName { get; set; } = string.Empty;
        public string accountKey { get; set; } = string.Empty;
        public RankPermissions permissions { get; set; } // what moderation actions this user can perform

        // Editable user-facing profile bound to the account. Currently client-persisted; server sync is a follow-up.
        public AccountProfile profile { get; set; } = new AccountProfile();

        // Convenience: prefer the user's chosen display name over their opaque account name. Used everywhere the UI shows "who am I".
        public string EffectiveDisplayName
            => !string.IsNullOrWhiteSpace(profile?.displayName) ? profile.displayName : accountName;
    }

    // User-facing profile info shown to other players / on the account panel. All fields optional - an empty profile falls back to plain account credentials in the UI.
    public class AccountProfile
    {
        public string displayName { get; set; } = string.Empty;
        public string avatarUrl   { get; set; } = string.Empty;
        public string gender      { get; set; } = string.Empty;    // free-form
        public int?   age         { get; set; }
        // Long-form self-introduction shown on the user's profile page. BBCode-formatted just like posts.
        public string bio         { get; set; } = string.Empty;
        // Banner image shown at the top of the user's profile page, above (and behind) the avatar. Same URL storage as avatar.
        public string headerUrl   { get; set; } = string.Empty;
        // Framing values for the header. offsetX/Y are the top-left of the visible portion in normalized image coords (0..1); zoom shrinks the visible rect so a value of 1 shows the entire image, 2 shows a 2x-tight crop. Defaults show the whole image upper-left, matching prior behaviour when no framing exists.
        public float  headerOffsetX { get; set; } = 0f;
        public float  headerOffsetY { get; set; } = 0f;
        public float  headerZoom    { get; set; } = 1f;
        // RP preference tags. Free-form list so we can add without a schema migration; the settings UI offers the common presets.
        public List<string> rpPreferences { get; set; } = new List<string>();
    }

    // A linked FFXIV character - each character is verified via Lodestone and has a unique characterKey assigned by the server.
    public class Character
    {
        public string characterName { get; set; } = string.Empty;
        public string characterWorld { get; set; } = string.Empty;
        public string characterKey { get; set; } = string.Empty; // 25-char verification key from Lodestone flow
    }

    // Flags controlling what moderation actions an account can perform. Sent by the server on login based on the account's rank.
    public class RankPermissions
    {
        public int rank { get; set; }            // numeric rank level (0=none, 1=mod, 2=admin, 3=owner)
        public bool can_announce { get; set; }   // can send server-wide announcements
        internal bool can_warn { get; set; }     // can issue warnings to other users
        public bool can_strike { get; set; }     // can give strikes (3 strikes = suspension)
        public bool can_suspend { get; set; }    // can temporarily suspend accounts
        public bool can_ban { get; set; }        // can permanently ban accounts
        public bool can_promote { get; set; }    // can change other users' ranks
    }

    // Account privilege levels - higher rank = more moderation power
    public enum Rank
    {
        None = 0,
        Moderator = 1,
        Admin = 2,
        Owner = 3,
    }

    // Types of disciplinary actions a moderator can take against a user
    public enum ModeratorAction
    {
        None = 0,
        Warn = 1,
        Strike = 2,
        Suspended = 3,
        Ban = 4,
    }

    // Human-readable labels and descriptions for each moderation action. Used in the moderator UI panel to explain what each action does.
    public class ModDefines
    {
        public static (string, string)[] ModAccountActionVals =
        {
            ("None", "Take no action on account, used to just give a friendly heads up."),
            ("Warn", "Send a warning, this does not issue a strike."),
            ("Strike", "Submit a strike to this users account, after 3 strikes the user will be suspended"),
            ("Suspend", "Suspend this user account, this action can be appealed."),
            ("Ban", "Ban this user, this action is perminant and no appeal can be made."),
        };
    }

    // JSON format used by the website's "Export Account" feature. Has a nested account object with key+name, plus a character list. exportType must be "AbsoluteRP_WebExport" for the import to accept it.
    public class WebExportData
    {
        public string exportType { get; set; } = string.Empty;
        public int exportVersion { get; set; }
        public string exportDate { get; set; } = string.Empty;
        public WebExportAccount account { get; set; } = new WebExportAccount();
        public List<WebExportCharacter> characters { get; set; } = new List<WebExportCharacter>();
    }

    public class WebExportAccount
    {
        public string accountKey { get; set; } = string.Empty;
        public string accountName { get; set; } = string.Empty;
    }

    // Shared character data format used by both WebExport and PluginExport
    public class WebExportCharacter
    {
        public string characterName { get; set; } = string.Empty;
        public string characterWorld { get; set; } = string.Empty;
        public string characterKey { get; set; } = string.Empty;
    }

    // JSON format used by the plugin's "Export for Website" button. Flat structure (no nested account object) - accountKey and accountName are top-level. exportType is "AbsoluteRP_PluginExport" to distinguish from the website format.
    public class PluginExportData
    {
        public string exportType { get; set; } = "AbsoluteRP_PluginExport";
        public int exportVersion { get; set; } = 1;
        public string exportDate { get; set; } = string.Empty;
        public string accountKey { get; set; } = string.Empty;
        public string accountName { get; set; } = string.Empty;
        public List<WebExportCharacter> characters { get; set; } = new List<WebExportCharacter>();
    }
}
