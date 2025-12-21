using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AbsoluteRP.Defines
{
    public class Account
    {
        public string accountName { get; set; } = string.Empty;
        public string accountKey { get; set; } = string.Empty;
        public RankPermissions permissions { get; set; }
    }
    public class Character
    {
        public string characterName { get; set; } = string.Empty;
        public string characterWorld { get; set; } = string.Empty;
        public string characterKey { get; set; } = string.Empty;
    }
    public class RankPermissions
    {

        public int rank { get; set; }
        public bool can_announce { get; set; }
        internal bool can_warn { get; set; }
        public bool can_strike { get; set; }
        public bool can_suspend { get; set; }
        public bool can_ban { get; set; }
        public bool can_promote { get; set; }

    }
    public enum Rank
    {
        None = 0,
        Moderator = 1,
        Admin = 2,
        Owner = 3,
    }
    public enum ModeratorAction
    {
        None = 0,
        Warn = 1,
        Strike = 2,
        Suspended = 3,
        Ban = 4,
    }
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
  
}
