using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AbsoluteRoleplay.Defines
{
    public class RankPermissions
    {
        public int rank { get; set; }
        public bool can_announce { get; set; }
        public bool can_strike { get; set; }
        public bool can_ban { get; set; }
        public bool can_warn { get; set; }
    }
    public enum Rank
    {
        None = 0,
        Moderator = 1,
        Admin = 2,
        Owner = 3,
    }
}
