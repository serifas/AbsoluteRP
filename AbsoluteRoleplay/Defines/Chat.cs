using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AbsoluteRP.Defines
{
    public class ARPChatGroups
    {
        public int id;
        public string name;
        public List<int> includedChannels;
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
