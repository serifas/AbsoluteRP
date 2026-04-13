using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AbsoluteRP.Defines
{
    // Represents the user's AbsoluteRP account — credentials + server-assigned ID.
    // The accountKey is the encrypted token used to authenticate with the server.
    public class Account
    {
        public int userID { get; set; }
        public string accountName { get; set; } = string.Empty;
        public string accountKey { get; set; } = string.Empty;
        public RankPermissions permissions { get; set; } // what moderation actions this user can perform
    }

    // A linked FFXIV character — each character is verified via Lodestone
    // and has a unique characterKey assigned by the server.
    public class Character
    {
        public string characterName { get; set; } = string.Empty;
        public string characterWorld { get; set; } = string.Empty;
        public string characterKey { get; set; } = string.Empty; // 25-char verification key from Lodestone flow
    }

    // Flags controlling what moderation actions an account can perform.
    // Sent by the server on login based on the account's rank.
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

    // Account privilege levels — higher rank = more moderation power
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

    // Human-readable labels and descriptions for each moderation action.
    // Used in the moderator UI panel to explain what each action does.
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

    // JSON format used by the website's "Export Account" feature.
    // Has a nested account object with key+name, plus a character list.
    // exportType must be "AbsoluteRP_WebExport" for the import to accept it.
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

    // JSON format used by the plugin's "Export for Website" button.
    // Flat structure (no nested account object) — accountKey and accountName are top-level.
    // exportType is "AbsoluteRP_PluginExport" to distinguish from the website format.
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
