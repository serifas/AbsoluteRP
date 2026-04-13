using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AbsoluteRP.Defines
{
    // A chat group that bundles multiple channels together under one name.
    // Used to let users filter chat messages to only see channels they care about.
    public class ARPChatGroups
    {
        public int id;
        public string name;                    // display name shown in the chat UI
        public List<int> includedChannels;     // which channel IDs are part of this group
    }
   
}
