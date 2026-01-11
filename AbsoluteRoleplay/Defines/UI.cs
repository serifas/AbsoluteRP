using AbsoluteRP;
using AbsoluteRP.Defines;
using AbsoluteRP.Helpers;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.Internal;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Lumina.Data;
using Lumina.Data.Files;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;
using static AbsoluteRP.Misc;
using static AbsoluteRP.UI;
using static FFXIVClientStructs.FFXIV.Client.UI.Misc.GroupPoseModule;
using static System.Net.Mime.MediaTypeNames;
namespace AbsoluteRP
{
    public class GroupChatMessage
    {
        public int messageID { get; set; }
        public int groupID { get; set; }
        public int channelID { get; set; }
        public int senderUserID { get; set; }
        public string senderName { get; set; }
        public int senderProfileID { get; set; }
        public IDalamudTextureWrap avatar { get; set; }
        public string messageContent { get; set; }
        public long timestamp { get; set; }
        public bool isEdited { get; set; }
        public long? editedTimestamp { get; set; }
        public bool deleted { get; set; }
        public bool isPinned { get; set; }
    }

    public class GroupCategory
    {
        public int id { get; set; }
        public int groupID { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public int sortOrder { get; set; }
        public List<GroupChannel> channels { get; set; }
        public bool collapsed {get;set;}
    }

    public class GroupRosterField
    {
        public int id { get; set; }
        public int groupID { get; set; }
        public string name { get; set; }
        public string dropdownOptions { get; set; }
        public int fieldType { get; set; }
        public bool required { get; set; }
        public int sortOrder { get; set; }
    }

    public class GroupInvite
    {
        public int inviteID { get; set; }
        public int groupID { get; set; }
        public string groupName { get; set; }
        public string groupDescription { get; set; }
        public byte[] groupIcon { get; set; }
        public string groupLogoUrl { get; set; }
        public int inviterUserID { get; set; }
        public string inviterName { get; set; }
        public int inviteeUserID { get; set; }
        public string inviteeName { get; set; }
        public int inviteeProfileID { get; set; }
        public string message { get; set; }
        public byte status { get; set; } // 0=pending, 1=accepted, 2=declined, 3=cancelled
        public long createdAt { get; set; }
    }

    public class GroupMember
    {
        public int id { get; set; }
        public int userID { get; set; }
        public int profileID { get; set; }
        public bool owner { get; set; }
        public string name { get; set; }
        public string note { get; set; }
        public string lodestoneURL { get; set; }
        // Legacy single rank (for backwards compatibility)
        public GroupRank rank { get; set; }
        // Multiple ranks support
        public List<GroupRank> ranks { get; set; } = new List<GroupRank>();
        public IDalamudTextureWrap avatar { get; set; }
        // Self-assign roles and rules agreement
        public List<GroupSelfAssignRole> selfAssignedRoles { get; set; } = new List<GroupSelfAssignRole>();
        public bool hasAgreedToRules { get; set; }
        public int agreedRulesVersion { get; set; }
    }

    public class ProfileLike
    {
        public int likerUserID { get; set; }
        public int likerProfileID { get; set; }
        public string likerName { get; set; }
        public string comment { get; set; }
        public int likeCount { get; set; }
        public long likedAt { get; set; }
    }

    public class GroupRank
    {
        public int id { get; set; }
        public int groupID { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public int hierarchy { get; set; } // Higher number = higher rank, owner has highest
        public bool isDefaultMember { get; set; } // When true, new members joining the group get this rank
        public GroupRankPermissions permissions { get; set; }
    }

    public class GroupRankPermissions
    {
        // Members
        public bool canInvite { get; set; }
        public bool canKick { get; set; }
        public bool canBan { get; set; }
        public bool canPromote { get; set; }
        public bool canDemote { get; set; }

        // Messages
        public bool canCreateAnnouncement { get; set; }
        public bool canReadMessages { get; set; }
        public bool canSendMessages { get; set; }
        public bool canDeleteOthersMessages { get; set; }
        public bool canPinMessages { get; set; }

        // Categories
        public bool canCreateCategory { get; set; }
        public bool canEditCategory { get; set; }
        public bool canDeleteCategory { get; set; }
        public bool canLockCategory { get; set; }

        // Forums (Channels)
        public bool canCreateForum { get; set; }
        public bool canEditForum { get; set; }
        public bool canDeleteForum { get; set; }
        public bool canLockForum { get; set; }
        public bool canMuteForum { get; set; }

        // Rank Management
        public bool canManageRanks { get; set; }
        public bool canCreateRanks { get; set; }

        // Self-Assign Roles
        public bool canManageSelfAssignRoles { get; set; }

        // Forms
        public bool canCreateForms { get; set; }
    }

    // Form channel field definition
    public class FormField
    {
        public int id { get; set; }
        public int channelId { get; set; }
        public string title { get; set; }
        public int fieldType { get; set; } // 0=single-line, 1=multi-line
        public bool isOptional { get; set; }
        public int sortOrder { get; set; }
    }

    // Form submission
    public class FormSubmission
    {
        public int id { get; set; }
        public int channelId { get; set; }
        public int userId { get; set; }
        public int profileId { get; set; }
        public string profileName { get; set; }
        public DateTime submittedAt { get; set; }
        public List<FormSubmissionValue> values { get; set; } = new List<FormSubmissionValue>();
    }

    // Individual field value in a submission
    public class FormSubmissionValue
    {
        public int fieldId { get; set; }
        public string fieldTitle { get; set; }
        public string value { get; set; }
    }

    public class GroupForumCategory
    {
        public int id { get; set; }
        public int parentCategoryID { get; set; }
        public int categoryIndex { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public string icon { get; set; }
        public bool collapsed { get; set; }
        public byte categoryType { get; set; }
        public int sortOrder { get; set; }
        public int groupID { get; set; }
        public long createdAt { get; set; }
        public long updatedAt { get; set; }
        public List<GroupForumChannel> channels { get; set; }
    }

    public class GroupForumChannel
    {
        public int id { get; set; }
        public int parentChannelID { get; set; }
        public int channelIndex { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public byte channelType { get; set; }
        public bool isLocked { get; set; }
        public bool isNSFW { get; set; }
        public int sortOrder { get; set; }
        public int groupID { get; set; }
        public int categoryID { get; set; }
        public long createdAt { get; set; }
        public long updatedAt { get; set; }
        public long lastMessageAt { get; set; }
        public List<GroupForumChannel> subChannels { get; set; }
    }

    public class GroupForumChannelPermission
    {
        public int channelID { get; set; }
        public int rankID { get; set; }
        public int userID { get; set; }
        public bool canView { get; set; }
        public bool canPost { get; set; }
        public bool canReply { get; set; }
        public bool canCreateThreads { get; set; }
        public bool canEditOwn { get; set; }
        public bool canDeleteOwn { get; set; }
        public bool canManage { get; set; }
        public bool canPin { get; set; }
        public bool canLock { get; set; }
    }
    public class GroupChannel
    {
        public int id { get; set; }
        public int index { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public int categoryID { get; set; }
        public int groupID { get; set; }
        public int channelType { get; set; } // 0 = text, 1 = announcement, 2 = rules, 3 = role selection, 4 = form
        public bool isLocked { get; set; } // When true, no one can send messages
        public bool isNsfw { get; set; } // When true, shows warning before viewing
        public bool everyoneCanView { get; set; } = true; // When true, all members can see the channel
        public bool everyoneCanPost { get; set; } = true; // When true, all members can post in the channel
        public bool allowFormatTags { get; set; } = false; // Form channels: allow HTML rendering in submissions
        public List<GroupMember> AllowedMembers { get; set; }
        public List<GroupRank> AllowedRanks { get; set; }
        public List<GroupSelfAssignRole> AllowedRoles { get; set; } // Self-assign roles with channel access
        public List<ChannelMemberPermission> MemberPermissions { get; set; } // Members with canView/canPost flags
        public List<ChannelRankPermission> RankPermissions { get; set; } // Ranks with canView/canPost flags
        public List<ChannelRolePermission> RolePermissions { get; set; } // Roles with canView/canPost flags
        public int unreadCount { get; set; }
    }

    public class ChannelMemberPermission
    {
        public int memberID { get; set; }
        public string memberName { get; set; }
        public bool canView { get; set; }
        public bool canPost { get; set; }
    }

    public class ChannelRankPermission
    {
        public int rankID { get; set; }
        public string rankName { get; set; }
        public bool canView { get; set; }
        public bool canPost { get; set; }
    }

    public class ChannelRolePermission
    {
        public int roleID { get; set; }
        public string roleName { get; set; }
        public string roleColor { get; set; }
        public bool canView { get; set; }
        public bool canPost { get; set; }
    }
  
    public class GroupMemberMetadata
    {
        public int memberID { get; set; }
        public long joinDate { get; set; }
        public long lastActive { get; set; }
        public string customTitle { get; set; }
        public string statusMessage { get; set; }
        public string nicknameColor { get; set; }
        public bool isOnline { get; set; }
    }
  
    public class GroupMemberFieldValue
    {
        public int fieldID { get; set; }
        public string fieldValue { get; set; }
    }
    public class GroupChatReadStatus
    {
        public int userID { get; set; }
        public int channelID { get; set; }
        public int lastReadMessageID { get; set; }
        public long lastReadTimestamp { get; set; }
    }
  
    
    
    public class Group
    {
        public int groupID { get; set; }
        public string name { get; set; } = string.Empty;
        public IDalamudTextureWrap logo { get; set; } = UI.UICommonImage(CommonImageTypes.blank);
        public IDalamudTextureWrap background { get; set; } = UI.UICommonImage(CommonImageTypes.blank);
        public string logoUrl { get; set; } = string.Empty;
        public byte[] logoBytes { get; set; }
        public byte[] backgroundBytes { get; set; }
        public bool openInvite { get; set; }
        public bool visible { get; set; }
        public string description { get; set; }
        public List<GroupRank> ranks { get; set; }
        public List<GroupMember> members { get; set; }
        public List<GroupBans> bans { get; set; }
        public List<GroupInvite> invites { get; set; }
        public ApplicationForm application { get; set; }
        public ProfileData ProfileData { get; set; }
        public ProfileData OwnerProfile { get; set; }
        public List<GroupCategory> categories { get; set; }
        // Rules channel and self-assign roles
        public string rulesContent { get; set; }
        public int rulesVersion { get; set; }
        public int? rulesChannelID { get; set; }
        public int? roleSelectionChannelID { get; set; }
        public List<GroupSelfAssignRole> selfAssignRoles { get; set; } = new List<GroupSelfAssignRole>();

        /// <summary>
        /// Gets the total unread message count across all channels in this group
        /// </summary>
        public int GetTotalUnreadCount()
        {
            if (categories == null) return 0;
            int total = 0;
            foreach (var category in categories)
            {
                if (category.channels != null)
                {
                    foreach (var channel in category.channels)
                    {
                        total += channel.unreadCount;
                    }
                }
            }
            return total;
        }
    }
    
   
    public class GroupBans
    {
        public int id { get; set; }
        public int userID { get; set; }
        public int profileID { get; set; }
        public string name { get; set; }
        public string lodestoneURL { get; set; }
    }

    /// <summary>
    /// Lightweight group result for search display
    /// </summary>
    public class GroupSearchResult
    {
        public int groupID { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public string logoUrl { get; set; }
        public int memberCount { get; set; }
        public IDalamudTextureWrap logo { get; set; }
    }
    public class ApplicationForm
    {
        public int id { get; set; }
        public string name { get; set; }
        public List<ApplicationSection> sections { get; set; }
    }
    public class ApplicationSection
    {
        public int index { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public List<Inputs> inputs { get; set; }
    }
    public class Inputs
    {
        public int index { get; set; }
        public int type { get; set; }
        public string name { get; set; }
        public string description { get; set; }
    }
    
    public class Navigation
    {
        public string[] names { get; set; } = new string[0];
        public Action[] actions { get; set; } = new Action[0];
        public ImTextureID[] textureIDs { get; set; } = new ImTextureID[0];
        public bool[] show { get; set; } = new bool[0];
        public int[] badges { get; set; } = new int[0]; // Unread count badges

    }
    public class SystemData
    {
        public string name { get; set; } = string.Empty;
        public string description { get; set; } = string.Empty;
        public SortedList<int, StatData> StatsData { get; set; } = new SortedList<int, StatData>();

    }
    public class StatData
    {
        public string name { get; set; } = string.Empty;
        public string description { get; set; } = string.Empty;
        public Vector4 color { get; set; } = Vector4.Zero;
        public int statValue { get; set; } = 0;
    }

    public class IconData 
    { 
        public string type { get; set; } = string.Empty;
        public uint iconID { get; set; } = 0;
        public string category { get; set; } = string.Empty;
        public IDalamudTextureWrap icon { get; set; } = null;
    }
    public class TooltipData
    {
        public string title { get; set; } = string.Empty;
        public Vector4 titleColor {  get; set; } = new Vector4(1,1,1,1);
        public IDalamudTextureWrap avatar { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Race { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public string Age { get; set; } = string.Empty;
        public string Height { get; set; } = string.Empty;
        public string Weight { get; set; } = string.Empty;
        public int Alignment { get; set; } = 0;
        public int Personality_1 { get; set; } = 0;
        public int Personality_2 { get; set; } = 0;
        public int Personality_3 { get; set; } = 0;
        public  List<field> fields = new List<field>();
        public  List<descriptor> descriptors = new List<descriptor>();
        public  List<trait> personalities = new List<trait>();
        public IDalamudTextureWrap alignmentImg { get; set; }
        public IDalamudTextureWrap personality_1Img { get; set; }
        public IDalamudTextureWrap personality_2Img { get; set; }
        public IDalamudTextureWrap personality_3Img { get; set; }
    }
    public class ProfileData
    {
        public int index {  get; set; }
        public int id { get; set; }
        public int accountID { get; set; }
        public string playerName { get; set; } = string.Empty;
        public string playerWorld { get; set; } = string.Empty;
        public IDalamudTextureWrap avatar;
        public IDalamudTextureWrap background;
        public bool SHOW_ON_COMPASS { get; set; } = false;
        public bool NSFW { get; set; } = false;
        public bool TRIGGERING { get; set; } = false;
        public bool SpoilerARR { get; set; } = false;
        public bool SpoilerHW { get; set; } = false;
        public bool SpoilerSB { get; set; } = false;
        public bool SpoilerSHB { get; set; } = false;
        public bool SpoilerEW { get; set; } = false;
        public bool SpoilerDT { get; set; } = false;
        public byte[] avatarBytes { get; set; } = new byte[0];
        public byte[] backgroundBytes { get; set; } = new byte[0];
        public string title { get; set; }
        public Vector4 titleColor { get; set; }
        public bool isPrivate { get; set; }
        public bool isActive { get; set; }
        public List<CustomTab> customTabs { get; set; } = new List<CustomTab>();
       
        public string OOC { get; set; }

    }
    
    public class descriptor
    {
        public int index { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        // Add parsed cache
        public ParsedNode parsedName;
        public string lastName;
        public ParsedNode parsedDescription;
        public string lastDescription;
    }
    public class field
    {
        public int index { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        // Add parsed cache
        public ParsedNode parsedName;
        public string lastName;
        public ParsedNode parsedDescription;
        public string lastDescription;
    }
    public class trait
    {
        public int iconID { get; set; }
        public int index { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public bool modifying { get; set; } // Controls window visibility
        public IconElement icon { get; set; } = new IconElement { icon = UICommonImage(CommonImageTypes.blank) };
        // Add parsed cache
        public ParsedNode parsedName;
        public string lastName;
        public ParsedNode parsedDescription;
        public string lastDescription;
    }

    public class RosterLayout : CustomLayout
    {
        public int tabIndex { get; set; } = 0;
        public List<ProfileData> members { get; set; } = new List<ProfileData>();
        public List<ProfileData> affiliates { get; set; } = new List<ProfileData>();
    }

    public enum ProfileTypes
    {
        Personal = 0,
        Character = 1,
        Group = 2,
        Venue = 3,
        FC = 4,
        Event = 5,
        Campaign = 6,
    }
    public enum LayoutTypes
    {
        Relationship = 0,
        Roster = 1,
        Bio = 2,
        Details = 3,
        Story = 4,
        Info = 5,
        Gallery = 6,   
        Inventory = 7,
        VenueInfo = 8,
    }
    public enum SpoilerTypes
    {
        None = 0,
        ARR = 1,
        HW = 2,
        SB = 3,
        SHB = 4,
        EW = 5,
        DT = 6,
    }

    public enum StatusType
    {
        Positive, Negative, Special
    }
    public struct IconInfo
    {
        public string Name;
        public uint IconID;
        public StatusType Type;
        public string Description;
    }

    public class RenderElement
    {
        public enum ElementType { Text, Image, Color, Url }
        public ElementType Type;
        public string Content;
        public float Scale;
        public string ColorHex;
        public string Url;
        public string Tooltip;
    }
    public class ProfileGalleryImage
    {
        public int index;
        public string url = string.Empty;
        public string tooltip = string.Empty;
        public bool nsfw = false;
        public bool trigger = false;
        public IDalamudTextureWrap image = UI.UICommonImage(UI.CommonImageTypes.blankPictureTab);
        public IDalamudTextureWrap thumbnail = UI.UICommonImage(UI.CommonImageTypes.blankPictureTab);
        public byte[] imageBytes = Misc.ImageToByteArray(Path.Combine(Plugin.PluginInterface.AssemblyLocation.Directory.FullName, "UI/profiles/galleries/picturetab.png"));
    }


    public class HookValues
    {
        public string name { get; set; }
        public string content { get; set; }
    }
    public class ProfileTab
    {
        public string name { set; get; }
        public string tooltip { set; get; }
        public bool showValue { set; get; }
        public Action action { set; get; }
    }
    internal class UI
    {
        public static List<ImFontPtr> fontList = new List<ImFontPtr>();
        public enum AlertPositions
        {
            BottomLeft = 0, 
            BottomRight = 1, 
            TopLeft = 2,
            TopRight = 3, 
            Center = 4 
        }
        public enum WindowAlignment
        {
            Center,
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight,
            Top,
            Left,
            Right,
            Bottom
        }
        public enum StatusMessages
        {
            //Login
            LOGIN_BANNED = -1,
            LOGIN_UNVERIFIED = 0,
            LOGIN_VERIFIED = 1,
            LOGIN_WRONG_INFORMATION = 2,
            //Forgot Info
            FORGOT_REQUEST_RECEIVED = 3,
            FORGOT_REQUEST_INCORRECT = 4,
            //Registration
            REGISTRATION_DUPLICATE_TAG_NAME = 5,
            CHARACTER_REGISTRATION_DUPLICATE_LODESTONE = 6,
            REGISTRATION_SUCCESSFUL = 15,
            CHARACTER_REGISTRATION_LODESTONE_KEY = 7,
            //Password Change
            PASSCHANGE_INCORRECT_RESTORATION_KEY = 8,
            //Verification
            VERIFICATION_INCORRECT_KEY = 9,
            VERIFICATION_KEY_VERIFIED = 10,
            //Gallery
            GALLERY_INCORRECT_IMAGE = 11,
            //other
            REGISTRATION_INSUFFICIENT_DATA = 12,
            //Profile Access
            NO_AVAILABLE_PROFILE = 13,
            //Account Messages
            ACCOUNT_WARNING = 14,
            ACCOUNT_STRIKE = 16,
            ACCOUNT_BANNED = 17,
            NONE = 18,
            ACTION_SUCCESS = 19,
            ACCOUNT_SUSPENDED = 20,
            //Update
            RECEIVE_UPDATES = 21,
            //Silent
            RECEIVE_SILENT = 22,
            //Trade
            TRADE_CANCEL = 23,
            TRADE_ACCEPTED = 24,
            //Lodestone Info
            CHARACTER_REGISTRATION_VALID_LODESTONE = 25,
            CHARACTER_REGISTRATIO_INVALID_LODESTONE = 26,
            //no access

        }

        public enum BioFieldTypes
        {
            name = 0,
            race = 1,
            gender = 2,
            age = 3,
            height = 4,
            weight = 5,
            afg = 6,
        }
        public static bool nameLoaded = false, raceLoaded = false, genderLoaded = false, ageLoaded = false, heightLoaded = false, weightLoaded = false, afgLoaded = false;
        public static string[] amPmOptions = { "AM", "PM" };
        public static string[] inclusions = { "Public", "World Only", "Datacenter Only", "FC Only", "Connections Only", "Invite Only" };
        public static string[] months = { "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };
        public static string[] timezones = { "Eastern Standard Time (EST)", "Eastern Daylight Time (EDT)", "Central Standard Time (CST)", "Central Daylight Time (CDT)", "Mountain Standard Time (MST)", "Mountain Daylight Time (MDT)", "Pacific Standard Time (PST)", "Pacific Daylight Time (PDT)" };
         internal static string inputHelperUrlInfo =
            "URL: \n <url>https://urlhere</url>\n" +
            "You can use this to link to your Discord, Ko-Fi, Patreon, or any other site you wish to link to solong as they to not contain jumpscares or illigal / triggering content.\n\n" +

            "IMAGE: \n <img>https://urlhere</img>\n" +
            "You can use this to link to an image url you wish to display in your tooltipData solong as it abides by the Rules and ToS.\n\n" +

            "COLOR: \n <color hex=FF0000>Red Text</color>\n" +
            "You can use this to color text in your tooltipData. The hex value can be any valid hex color code.\n\n" +

            "TABLE: \n <table>content or columns here</table> \n" +
            "COLUMNS: \n <column>some content here</column> \n" +
            "You can use this to create a table in your tooltipData. The table can contain columns to align content side by side and so on.\n" +
            "The columns tag is used to define the number of columns in the table.\n There is currently no row support.\n\n"+
            
            "NAVIGATION: \n <nav>content here</nav> \n" +
            "You can use this to create a navigation bar in your tooltipData. The navigation bar can contain page tags to set the pages content to navigate.\n" +

            "PAGE: \n <page>content here</page> \n" +
            "You can use this to create a page in your tooltipData. The page can contain any content including tags. (Must be placed within nav tags) \n\n" +

            "<<Although most these features are not very suited for some fields they are still enabled. Get creative! ♥>>"
            ;
             








        public enum InputTypes
        {
            multiline = 1,
            single = 2,
        }
        public enum Alignments
        {
            LawfulGood = 0,
            NeutralGood = 1,
            ChaoticGood = 2,
            LawfulNeutral = 3,
            TrueNeutral = 4,
            ChaoticNeutral = 5,
            LawfulEvil = 6,
            NeutralEvil = 7,
            ChaoticEvil = 8,
            None = 9,
        }

        public enum ConnectionStatus
        {
            blocked = -1,
            canceled = 0,
            pending = 1,
            refused = 2,
            accepted = 3,
            removed = 4,
        }
        public enum Personalities
        {
            Abrasive = 0,
            AbsentMinded = 1,
            Aggressive = 2,
            Artistic = 3,
            Cautious = 4,
            Charming = 5,
            Compassionate = 6,
            Daredevil = 7,
            Dishonest = 8,
            Dutiful = 9,
            Easygoing = 10,
            Eccentric = 11,
            Honest = 12,
            Knowledgable = 13,
            Optimistic = 14,
            Polite = 15,
            Relentless = 16,
            Resentful = 17,
            Reserved = 18,
            Romantic = 19,
            Spiritual = 20,
            Superior = 21,
            Tormented = 22,
            Tough = 23,
            Wild = 24,
            Worldly = 25,
            None = 26,
        }
        public enum BodyForms
        {
            Emaciated = 1,
            Thin = 2,
            Healthy = 3,
            Fit = 4,
            Stocky = 5,
            Husky = 6,
            Overwheight = 7,
            Obese = 8,
        }
        public enum CommonImageTypes
        {
            discordBtn = 1,
            kofiBtn = 2,
            blankPictureTab = 3,
            avatarHolder = 4,
            profileSection = 5,
            eventsSection = 6,
            systemsSection = 7,
            connectionsSection = 8,
            //profiles
            profileCreateProfile = 9,
            profileCreateNPC = 10,
            profileBookmarkProfile = 11,
            profileBookmarkNPC = 12,
            //events and venues
            eventEvents = 13,
            eventBookmarkEvent = 14,
            //listingsSystem
            systemsCombatSystem = 15,
            systemSheetSystem = 16,
            //connections
            //targets
            targetConnections = 17,
            targetBookmark = 18,
            targetGroupInvite = 19,
            targetViewProfile = 20,
            //gallery nsfw and triggers
            NSFW = 21,
            TRIGGER = 22,
            NSFWTRIGGER = 23,
            //Connection Button
            reconnect = 24,
            //listings
            listingsCampaign = 25,
            listingsEvent = 26,
            listingsFC = 27,
            listingsGroup = 28,
            listingsPersonal = 29,
            listingsVenue = 30,
            listingsCampaignBig = 31,
            listingsEventBig = 32,
            listingsFCBig = 33,
            listingsGroupBig = 34,
            listingsPersonalBig = 35,
            listingsVenueBig = 36,
            blank = 37,
            inventoryTab = 38,
            patreonBtn = 39,
            move = 40,
            circleMask = 41,
            starMask = 42,
            heartMask = 43,
            pin = 44,
            unpin = 45,
            display = 46,
            hide = 47,
            dock = 48,
            undock = 49,
            edit = 50,
            move_cancel = 51,
            backgroundHolder = 52,
            listingsQuests = 53,
            listingsSystem = 54,
            systems_stats = 55,
            systems_skills = 56,
            systems_combat = 57,
            systems_rules = 58,
            socialBookmarks = 59,
            socialConnections = 60,
            socialGroups = 61,
            socialSearch = 62,
            create = 63,
            socialGroupSettings = 64,
        }
        public enum ListingCategory
        {
            Event = 1,
            Campaign = 2,
            Venue = 3,
            Group = 4,
            FC = 5,
            Personal = 6,
        }

        public enum ListingType
        {
            Casual = 1,
            StoryBased = 2,
            DarkMature = 3,
        }

        public enum ListingFocus
        {
            SliceOfLife = 1,
            Social = 2,
            Adventure = 3,
            Combat = 4,
            Crime = 5,
            Humanitarian = 6,
            Worship = 7,
            Mercenary = 8,
        }
        public enum TabType
        {
            
            Tree = 0,
            Dynamic = 1,
            Bio = 2,
            Details = 3,
            Story = 4,
            Info = 5,
            Gallery = 6,
            Inventory = 7,
        }
        public enum ListingSetting
        {
            Shop = 1,
            Restaurant = 2,
            Library = 3,
            Inn = 4,
            School = 5,
            Home = 6,
            Shrine = 7,
            Coven = 8,
            Bathhouse = 9,
            Other = 10,
            Infirmary = 11,
        }
        private static readonly Dictionary<CommonImageTypes, IDalamudTextureWrap> _imageCache = new();
        private static readonly Dictionary<int, IDalamudTextureWrap> _alignmentIconCache = new();
        private static readonly Dictionary<int, IDalamudTextureWrap> _personalityIconCache = new(); 
        public static byte[] imageBytes(string ImgPath)
        {
            if (Plugin.PluginInterface is { AssemblyLocation.Directory.FullName: { } path })
            {
                if(File.Exists(Path.Combine(path, ImgPath)))
                {
                    return Misc.ImageToByteArray(Path.Combine(path, ImgPath));
                }
                return null;
                
            }
            return null;

        }
        

        public static byte[] baseAvatarBytes()
        {
            if (Plugin.PluginInterface is { AssemblyLocation.Directory.FullName: { } path })
            {
                if (File.Exists(Path.Combine(path, "UI/profiles/avatar_holder.png")))
                {
                    return Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/avatar_holder.png"));
                }
            }
            return null;
        }
        public static byte[] baseImageBytes()
        {
            if (Plugin.PluginInterface is { AssemblyLocation.Directory.FullName: { } path })
            {
                var fullPath = Path.Combine(path, "UI/profiles/background_holder.png");
                if (!File.Exists(fullPath))
                {
                    Plugin.PluginLog.Debug($"[baseImageBytes] File does not exist: {fullPath}");
                    return null;
                }
                // This should just read the file as bytes
                return File.ReadAllBytes(fullPath);
            }
            Plugin.PluginLog.Debug("[baseImageBytes] PluginInterface or path is null.");
            return null;
        }

        public static readonly Dictionary<CommonImageTypes, IDalamudTextureWrap> commonImageWraps = new();

        public static IDalamudTextureWrap UICommonImage(CommonImageTypes imageType)
        {
            if (!commonImageWraps.TryGetValue(imageType, out var wrap))
            {
                if (Plugin.PluginInterface is { AssemblyLocation.Directory.FullName: { } path })
                {
                    wrap = imageType switch
                    {
                        CommonImageTypes.create => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/general/create.png"))).Result,
                        CommonImageTypes.discordBtn => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/disc_btn.png"))).Result,
                        CommonImageTypes.kofiBtn => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/kofi_btn.png"))).Result,
                        CommonImageTypes.patreonBtn => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/patreon_btn.png"))).Result,
                        CommonImageTypes.blankPictureTab => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/galleries/picturetab.png"))).Result,
                        CommonImageTypes.NSFW => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/galleries/nsfw.png"))).Result,
                        CommonImageTypes.NSFWTRIGGER => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/galleries/nsfw_trigger.png"))).Result,
                        CommonImageTypes.TRIGGER => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/galleries/trigger.png"))).Result,
                        CommonImageTypes.avatarHolder => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/avatar_holder.png"))).Result,
                        CommonImageTypes.backgroundHolder => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/background_holder.png"))).Result,
                        CommonImageTypes.profileCreateProfile => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/profile_create.png"))).Result,
                        CommonImageTypes.profileCreateNPC => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/npc_create.png"))).Result,
                        CommonImageTypes.profileBookmarkProfile => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/profile_bookmarks.png"))).Result,
                        CommonImageTypes.profileBookmarkNPC => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/npc_bookmarks.png"))).Result,
                        CommonImageTypes.reconnect => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/connect.png"))).Result,
                        CommonImageTypes.listingsCampaign => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/listings/campaign.png"))).Result,
                        CommonImageTypes.listingsEvent => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/event.png"))).Result,
                        CommonImageTypes.listingsGroup => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/group.png"))).Result,
                        CommonImageTypes.listingsPersonal => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/personal.png"))).Result,
                        CommonImageTypes.listingsVenue => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/listings/venue.png"))).Result,
                        CommonImageTypes.listingsCampaignBig => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/listings/campaign_big.png"))).Result,
                        CommonImageTypes.listingsEventBig => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/listings/event_big.png"))).Result,
                        CommonImageTypes.listingsFCBig => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/listings/fc_big.png"))).Result,
                        CommonImageTypes.listingsGroupBig => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/listings/group_big.png"))).Result,
                        CommonImageTypes.listingsPersonalBig => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/listings/personal_big.png"))).Result,
                        CommonImageTypes.listingsVenueBig => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/listings/venue_big.png"))).Result,
                        CommonImageTypes.listingsQuests => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/quests.png"))).Result,
                        CommonImageTypes.listingsSystem => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/systems.png"))).Result,
                        CommonImageTypes.blank => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/blank.png"))).Result,
                        CommonImageTypes.inventoryTab => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/invTab.png"))).Result,
                        CommonImageTypes.circleMask => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/masks/circle.png"))).Result,
                        CommonImageTypes.starMask => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/masks/star.png"))).Result,
                        CommonImageTypes.heartMask => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/masks/heart.png"))).Result,
                        CommonImageTypes.pin => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/dynamic/pin.png"))).Result,
                        CommonImageTypes.unpin => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/dynamic/unpin.png"))).Result,
                        CommonImageTypes.display => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/dynamic/display.png"))).Result,
                        CommonImageTypes.hide => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/dynamic/hide.png"))).Result,
                        CommonImageTypes.dock => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/dynamic/dock.png"))).Result,
                        CommonImageTypes.undock => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/dynamic/undock.png"))).Result,
                        CommonImageTypes.edit => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/dynamic/edit.png"))).Result,
                        CommonImageTypes.move => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/dynamic/move.png"))).Result,
                        CommonImageTypes.move_cancel => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/dynamic/move_cancel.png"))).Result,
                        CommonImageTypes.systems_stats => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/systems/systems_stats.png"))).Result,
                        CommonImageTypes.systems_skills => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/systems/systems_skills.png"))).Result,
                        CommonImageTypes.systems_combat => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/systems/systems_combat.png"))).Result,
                        CommonImageTypes.systems_rules => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/systems/systems_rules.png"))).Result,
                        CommonImageTypes.socialBookmarks => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/social/bookmark.png"))).Result,
                        CommonImageTypes.socialConnections => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/social/connections.png"))).Result,
                        CommonImageTypes.socialGroups => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/social/groups.png"))).Result,
                        CommonImageTypes.socialSearch => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/social/search.png"))).Result,
                        CommonImageTypes.socialGroupSettings => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/social/groups/settings.png"))).Result,
                        _ => null
                    };
                    if (wrap != null)
                        commonImageWraps[imageType] = wrap;
                }
            }
            return wrap;
        }
        public static string AlignmentName(int alignment)
        {
            string alignmentName = string.Empty;
            if (alignment == (int)Alignments.LawfulGood) { alignmentName = "Lawful Good"; };
            if (alignment == (int)Alignments.NeutralGood) { alignmentName = "Neutral Good"; };
            if (alignment == (int)Alignments.ChaoticGood) { alignmentName = "Chaotic Good"; };
            if (alignment == (int)Alignments.LawfulNeutral) { alignmentName = "Lawful Neutral"; };
            if (alignment == (int)Alignments.TrueNeutral) { alignmentName = "True Neutral"; };
            if (alignment == (int)Alignments.ChaoticNeutral) { alignmentName = "Chaotic Neutral"; };
            if (alignment == (int)Alignments.LawfulEvil) { alignmentName = "Lawful Evil"; };
            if (alignment == (int)Alignments.NeutralEvil) { alignmentName = "Neutral Evil"; };
            if (alignment == (int)Alignments.ChaoticEvil) { alignmentName = "Chaotic Evil"; };
            return alignmentName;
        }
        public static string PersonalityNames(int personality)
        {
            string personalityName = string.Empty;
            if (personality == (int)Personalities.Abrasive) { personalityName = "Abrasive"; };//Abrasive
            if (personality == (int)Personalities.AbsentMinded) { personalityName = "Absent-Minded"; };//Absent-Minded
            if (personality == (int)Personalities.Aggressive) { personalityName = "Aggressive"; };//Agressive
            if (personality == (int)Personalities.Artistic) { personalityName = "Artistic"; };//Artistic
            if (personality == (int)Personalities.Cautious) { personalityName = "Cautious"; };//Cautious
            if (personality == (int)Personalities.Charming) { personalityName = "Charming"; };//Charming
            if (personality == (int)Personalities.Compassionate) { personalityName = "Compassionate"; };//Compassionate
            if (personality == (int)Personalities.Daredevil) { personalityName = "Daredevil"; };//Daredevil
            if (personality == (int)Personalities.Dishonest) { personalityName = "Dishonest"; };//Dishonest
            if (personality == (int)Personalities.Dutiful) { personalityName = "Dutiful"; };//Dutiful
            if (personality == (int)Personalities.Easygoing) { personalityName = "Easygoing"; };//Easygoing
            if (personality == (int)Personalities.Eccentric) { personalityName = "Eccentric"; };//Eccentric
            if (personality == (int)Personalities.Honest) { personalityName = "Honest"; };//Honest
            if (personality == (int)Personalities.Knowledgable) { personalityName = "Knowledgable"; };//Knowledgable
            if (personality == (int)Personalities.Optimistic) { personalityName = "Optimistic"; };//Optimistic
            if (personality == (int)Personalities.Polite) { personalityName = "Polite"; };//Polite
            if (personality == (int)Personalities.Relentless) { personalityName = "Relentless"; };//Relentless
            if (personality == (int)Personalities.Resentful) { personalityName = "Resentful"; };//Resentful
            if (personality == (int)Personalities.Reserved) { personalityName = "Reserved"; }; //Reserved
            if (personality == (int)Personalities.Romantic) { personalityName = "Romantic"; }; //Romantic
            if (personality == (int)Personalities.Spiritual) { personalityName = "Spiritual"; };//Spiritual
            if (personality == (int)Personalities.Superior) { personalityName = "Superior"; };//Superior
            if (personality == (int)Personalities.Tormented) { personalityName = "Tormented"; };//Tormentex
            if (personality == (int)Personalities.Tough) { personalityName = "Tough"; };//Tough
            if (personality == (int)Personalities.Wild) { personalityName = "Wild"; };//Wild
            if (personality == (int)Personalities.Worldly) { personalityName = "Worldly"; };//Worldly
            if (personality == (int)Personalities.None) { personalityName = "None"; };//None
            return personalityName;
        }

        public static string BodyFormNames(int form)
        {
            string formName = string.Empty;
            if (form == (int)BodyForms.Emaciated) { formName = "Emaciated"; };
            if (form == (int)BodyForms.Thin) { formName = "Thin"; };
            if (form == (int)BodyForms.Healthy) { formName = "Healthy"; };
            if (form == (int)BodyForms.Fit) { formName = "Fit"; };
            if (form == (int)BodyForms.Stocky) { formName = "Stocky"; };
            if (form == (int)BodyForms.Husky) { formName = "Husky"; };
            if (form == (int)BodyForms.Overwheight) { formName = "Overweight"; };
            if (form == (int)BodyForms.Obese) { formName = "Obese"; };
            return formName;
        }


        public static SortedList<int, Tuple<string, string>> BodyFormValues()
        {
            SortedList<int, Tuple<string, string>> bodyFormValues = new SortedList<int, Tuple<string, string>>
            {
                { (int) BodyForms.Emaciated, Tuple.Create(BodyFormNames((int)BodyForms.Emaciated), "You are underfed and starving. Unhealthy")},
                { (int) BodyForms.Thin, Tuple.Create(BodyFormNames((int) BodyForms.Thin), "You are underweight, usually from high metabolism") },
                { (int) BodyForms.Healthy, Tuple.Create(BodyFormNames((int) BodyForms.Healthy), "Your body is not muscular, but not overweight. You take good care of yourself, without too much excersise or weight watching.") },
                { (int) BodyForms.Fit, Tuple.Create(BodyFormNames((int) BodyForms.Fit), "Your body is lean and muscular, you take good care of yourself and excercise regularly.") },
                { (int) BodyForms.Stocky, Tuple.Create(BodyFormNames((int) BodyForms.Stocky), "Your body is large but muscular, like a body builder.") },
                { (int) BodyForms.Husky, Tuple.Create(BodyFormNames((int) BodyForms.Husky), "Your body is heavy set, but not to an unhealthy extent.") },
                { (int) BodyForms.Overwheight, Tuple.Create(BodyFormNames((int) BodyForms.Overwheight), "You are overweight, you do little excercise and do not watch much of what you eat.") },
                { (int) BodyForms.Obese, Tuple.Create(BodyFormNames((int) BodyForms.Obese), "Your body is large and you have little muscle. Most of your body consists of fat. you do not excercise or watch your diet at all.") },
            };
            return bodyFormValues;
        }

        public static readonly Dictionary<int, IDalamudTextureWrap> alignmentImageWraps = new();
        public static readonly Dictionary<int, IDalamudTextureWrap> personalityImageWraps = new();

        public static IDalamudTextureWrap AlignmentIcon(int id)
        {
            if (!alignmentImageWraps.TryGetValue(id, out var wrap))
            {
                if (Plugin.PluginInterface is { AssemblyLocation.Directory.FullName: { } path })
                {
                    wrap = (UI.Alignments)id switch
                    {
                        UI.Alignments.LawfulGood => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/alignments/lawful_good.png"))).Result,
                        UI.Alignments.NeutralGood => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/alignments/neutral_good.png"))).Result,
                        UI.Alignments.ChaoticGood => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/alignments/chaotic_good.png"))).Result,
                        UI.Alignments.LawfulNeutral => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/alignments/lawful_neutral.png"))).Result,
                        UI.Alignments.TrueNeutral => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/alignments/true_neutral.png"))).Result,
                        UI.Alignments.ChaoticNeutral => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/alignments/chaotic_neutral.png"))).Result,
                        UI.Alignments.LawfulEvil => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/alignments/lawful_evil.png"))).Result,
                        UI.Alignments.NeutralEvil => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/alignments/neutral_evil.png"))).Result,
                        UI.Alignments.ChaoticEvil => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/alignments/chaotic_evil.png"))).Result,
                        UI.Alignments.None => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/personalities/none.png"))).Result,
                        _ => null
                    };
                    if (wrap != null)
                        alignmentImageWraps[id] = wrap;
                }
            }
            return wrap;
        }

        public static IDalamudTextureWrap PersonalityIcon(int id)
        {
            if (!personalityImageWraps.TryGetValue(id, out var wrap))
            {
                if (Plugin.PluginInterface is { AssemblyLocation.Directory.FullName: { } path })
                {
                    wrap = (UI.Personalities)id switch
                    {
                        UI.Personalities.Abrasive => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/personalities/abrasive.png"))).Result,
                        UI.Personalities.AbsentMinded => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/personalities/absentminded.png"))).Result,
                        UI.Personalities.Aggressive => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/personalities/aggressive.png"))).Result,
                        UI.Personalities.Artistic => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/personalities/artistic.png"))).Result,
                        UI.Personalities.Cautious => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/personalities/cautious.png"))).Result,
                        UI.Personalities.Charming => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/personalities/charming.png"))).Result,
                        UI.Personalities.Compassionate => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/personalities/compassionate.png"))).Result,
                        UI.Personalities.Daredevil => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/personalities/daredevil.png"))).Result,
                        UI.Personalities.Dishonest => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/personalities/dishonest.png"))).Result,
                        UI.Personalities.Dutiful => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/personalities/dutiful.png"))).Result,
                        UI.Personalities.Easygoing => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/personalities/easygoing.png"))).Result,
                        UI.Personalities.Eccentric => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/personalities/eccentric.png"))).Result,
                        UI.Personalities.Honest => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/personalities/honest.png"))).Result,
                        UI.Personalities.Knowledgable => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/personalities/knowledgable.png"))).Result,
                        UI.Personalities.Optimistic => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/personalities/optimistic.png"))).Result,
                        UI.Personalities.Polite => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/personalities/polite.png"))).Result,
                        UI.Personalities.Relentless => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/personalities/relentless.png"))).Result,
                        UI.Personalities.Resentful => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/personalities/resentful.png"))).Result,
                        UI.Personalities.Reserved => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/personalities/reserved.png"))).Result,
                        UI.Personalities.Romantic => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/personalities/romantic.png"))).Result,
                        UI.Personalities.Spiritual => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/personalities/spiritual.png"))).Result,
                        UI.Personalities.Superior => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/personalities/superior.png"))).Result,
                        UI.Personalities.Tormented => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/personalities/tormented.png"))).Result,
                        UI.Personalities.Tough => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/personalities/tough.png"))).Result,
                        UI.Personalities.Wild => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/personalities/wild.png"))).Result,
                        UI.Personalities.Worldly => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/personalities/worldly.png"))).Result,
                        UI.Personalities.None => Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/profiles/personalities/worldly.png"))).Result,
                        _ => null
                    };
                    if (wrap != null)
                        personalityImageWraps[id] = wrap;
                }
            }
            return wrap;
        }

        public static readonly (string, string, string, InputTypes)[] BioFieldVals =
        {

           // string[] nameFields = new string[] { "Name:   ", "##playername", $"Character Name (The name or nickname of the character you are currently playing as)", name};
            ("NAME:   ", "##playername",  $"Character Name (The name or nickname of the character you are currently playing as)",  InputTypes.single),
            ("RACE:    ", "##race", $"The IC Race of your character", InputTypes.single),
            ("GENDER: ", "##gender", $"The IC gender of your character", InputTypes.single),
            ("AGE:   ", "##age", $"Must be specified to post in nsfw. No nsfw if not 18+", InputTypes.single),
            ("HEIGHT:", "##height", $"Your OC's IC Height", InputTypes.single),
            ("WEIGHT:", "##weight", $"Your OC's IC Weight", InputTypes.single),
            ("AT FIRST GLANCE:", "##afg", $"What people see when they first glance at your character", InputTypes.multiline),

        };

        public static Vector2 PivotAlignment(WindowAlignment alignment)
        {
            switch (alignment)
            {
                case WindowAlignment.Center:
                    return new Vector2(0.5f, 0.5f);
                case WindowAlignment.TopRight:
                    return new Vector2(1, 0);
                case WindowAlignment.BottomLeft:
                    return new Vector2(0, 1);
                case WindowAlignment.BottomRight:
                    return new Vector2(1, 1);
                case WindowAlignment.Top:
                    return new Vector2(0.5f, 0);
                case WindowAlignment.Left:
                    return new Vector2(0, 0.5f);
                case WindowAlignment.Right:
                    return new Vector2(1, 0.5f);
                case WindowAlignment.Bottom:
                    return new Vector2(0.5f, 1);
                default:
                    return new Vector2(0, 0);

            }
        }
        public static readonly (string, string)[] PersonalityValues =
        {
            
           
                //Abrasive
                (PersonalityNames((int)Personalities.Abrasive), "• I am usually hostile or offensive in social interactions \n " +
                                                                                                           "• I am very standoffish and do not like being approached. \n" +
                                                                                                           "• I do not respond kindly to advice and especially orders."),
                //Absent-Minded
                (PersonalityNames((int)Personalities.AbsentMinded), "• I’m easily distracted and always have a string of unfinished \n" +
                                                                                                                   "   tasks on my to-do list. \n" +
                                                                                                                   "• I’m pretty oblivious to my surroundings.\n" +
                                                                                                                   "• I’m always wandering off to look at inconsequential \n" +
                                                                                                                   "   things and get lost easily.\n" +
                                                                                                                   "• I flake out on promises a lot. I don’t mean to…I just forget!\n" +
                                                                                                                   "• If there’s a plan, I’ll forget it. If I don’t forget it… \n" +
                                                                                                                   "   I usually end up ignoring it."),
                //Agressive
                (PersonalityNames((int)Personalities.Aggressive),     "• I feel the most peaceful in the heat of battle as I vanquish my foes.\n" +
                                                                                                                   "• I’m always the first to run into battle.\n" +
                                                                                                                   "• I’ll start a fight over anything. I’ll even do it just because I’m bored!\n" +
                                                                                                                   "• Violence is my first response to any challenge.\n" +
                                                                                                                   "• I get unreasonably angry at the slightest insult.\n" +
                                                                                                                   "• I use language so foul that I can make sailors blush—and not afraid to give someone a tongue lashing."),
                //Artistic
                (PersonalityNames((int)Personalities.Artistic),  "• I’m always doodling on the margins of my spellbook.\n" +
                                                                                                            "• I paint all the fascinating flora and fauna I see in my travel journal.\n" +
                                                                                                            "• I can see the true beauty in anything…even a beholder!\n" +
                                                                                                            "• I love to improvise new songs, but not everyone appreciates the fact that I’m always humming under my breath."),
                //Cautious
                (PersonalityNames((int)Personalities.Cautious),  "• I’m slow to trust and often assume the worst of people, but once they win me over, I’m a friend for life.\n" +
                                                                                                            "• I never trust anyone other than myself and don’t intend to change that.\n" +
                                                                                                            "• I make sure to know my enemy before I act, lest I bite off more than I can chew.\n" +
                                                                                                            "• I’m the first one to panic (or advocate a speedy retreat) when a threat arises."),
                //Charming
                (PersonalityNames((int)Personalities.Charming),  "• I can (and do) sweet-talk just about anyone. Flattery gets me everywhere!\n" +
                                                                                                            "• I absolutely adore a well-crafted insult, even one that’s directed at me.\n" +
                                                                                                            "• I make witty jokes and quips at all the worst moments.\n" +
                                                                                                            "• I love gossip and know how to pry the juicy details out of anyone’s mouth."),
                //Compassionate
                (PersonalityNames((int)Personalities.Compassionate),  "• I can empathize and find common ground between even the most hostile enemies.\n" +
                                                                                                                      "• I fight for those who can’t fight for themselves.\n" +
                                                                                                                      "• I go out of my way to help people, even if it puts me in danger.\n" +
                                                                                                                      "• I can never say “no” when someone asks me for a favor.\n" +
                                                                                                                      "• I watch over my friends like a protective mother, whether they want me to or not."),
                //Daredevil
                (PersonalityNames((int)Personalities.Daredevil),  "• I like helping people…but I also like the rush I get from throwing myself into danger.\n" +
                                                                                                              "• I’ve always wanted to be an adventurer. Travel and constant danger sound fun!\n" +
                                                                                                              "• I’ll say “yes” to anything—it doesn’t matter what I’m asked to do.\n" +
                                                                                                              "• I'm a gambler. I can't resist the thrill of taking a risk for a possible payoff."),
                //Dishonest
                (PersonalityNames((int)Personalities.Dishonest),  "• I lie about almost everything, even when there's no reason to do it.\n" +
                                                                                                              "• I have a false identity and will tell any lie to protect it.\n" +
                                                                                                              "• I steal anything I see that might have some value.\n" +
                                                                                                              "• I love swindling people who are more powerful than me!\n" +
                                                                                                              "• I don’t consider it “stealing” when I need something more than the person who has it."),
                //Dutiful
                (PersonalityNames((int)Personalities.Dutiful),  "• I would lay down my life for my comrades in a heartbeat.\n" +
                                                                                                          "• I feel obligated to fulfill a mission given to me by my order, even though I don’t want to.\n" +
                                                                                                          "• I tend to judge people who forsake their duties harshly.\n" +
                                                                                                          "• My parent taught me a sense of duty, and I’ll always uphold it, even when the odds are against me."),
                //Easygoing
                (PersonalityNames((int)Personalities.Easygoing),  "• I’m always on the lookout for new friends—I love introducing myself to people wherever I go.\n" +
                                                                                                              "• I’m pretty likable, so I tend to assume everyone wants to be friends with me.\n" +
                                                                                                              "• I tend to let other people do all the planning. I prefer to go with the flow.\n" +
                                                                                                              "• I’m comfortable in any social situation, no matter how tense it gets."),
                //Eccentric
                (PersonalityNames((int)Personalities.Eccentric),  "• I always leave a calling card, no matter where I go.\n" +
                                                                                                              "• Sometimes I mutter about myself in the third person…even when others can hear me.\n" +
                                                                                                              "• I change my mood or my mind as quickly as the wind changes directions.\n" +
                                                                                                              "• I always dress in formal clothes, even when I’m slogging through a cave or exploring a dungeon.\n" +
                                                                                                              "• I can’t control my reaction when surprised or afraid. One time I burned down a building that had a spider in it!"),
                //Honest
                (PersonalityNames((int)Personalities.Honest),  "• I’m very earnest and unusually direct. Sometimes people are taken aback by my bluntness.\n" +
                                                                                                        "• It’s hard to conceal my emotions. I always wear my heart on my sleeve!\n" +
                                                                                                        "• I made a vow never to tell a lie, and I intend to keep it.\n" +
                                                                                                        "• I’m bad at keeping secrets. I always end up blurting out the truth!"),
                //Knowledgable
                (PersonalityNames((int)Personalities.Knowledgable),  "• I read every book I can get my hands on and travel with a dozen in my pack.\n" +
                                                                                                                    "• I love a good puzzle! Once I get wind of a mystery, I’ll stop at nothing to solve it.\n" +
                                                                                                                    "• I would risk life and limb (my own or someone else’s) to obtain new knowledge.\n" +
                                                                                                                    "• I don’t believe in “forbidden” knowledge. What matters is what you do with it, right?\n" +
                                                                                                                    "• I tend to assume I know more about a particular subject than anyone else around me.\n" +
                                                                                                                    "• I have a single obscure hobby and will eagerly discuss it in detail."),
                //Optimistic
                (PersonalityNames((int)Personalities.Optimistic),  "• Absolutely nothing can shake my sunny disposition!\n" +
                                                                                                                "• In a bad situation, I’m the one telling everyone to look on the bright side.\n" +
                                                                                                                "• I’m more likely to laugh or crack a joke than cry, which sometimes rubs people the wrong way.\n" +
                                                                                                                "• I encourage everyone to be the best version of themselves that they can be."),
                //Polite
                (PersonalityNames((int)Personalities.Polite),  "• I genuinely believe that manners are important, no matter my situation.\n" +
                                                                                                                "• My elegance and refinement are tools I use to avoid arousing suspicion from others.\n" +
                                                                                                                "• No one can fake a smile, a handshake, or an interested nod like me!\n" +
                                                                                                                "• I was raised to have the manners of a noble, and I can’t imagine a world where I don’t use them."),
                //Relentless
                (PersonalityNames((int)Personalities.Relentless),  "• I’m convinced I’ll find riches beyond my imagination if I keep looking for it.\n" +
                                                                                                                "• I fail often, but I’ll never, ever give up.\n" +
                                                                                                                "• I will stop at nothing to achieve my goals, even if I make a few enemies along the way.\n" +
                                                                                                                "• If someone questions my courage, I’ll never back down, no matter how dangerous the situation is.\n" +
                                                                                                                "• I’m going to recover something that was taken from me if it’s the last thing I do, and I have no time for distractions…or friends."),
                //Resentful
                (PersonalityNames((int)Personalities.Resentful),  "• I always remember an insult, no matter how inconsequential.\n" +
                                                                                                              "• I never show my anger—but I do plot my revenge.\n" +
                                                                                                              "• I get upset when I’m not the center of attention.\n" +
                                                                                                              "• I’m slow to forgive when I feel like someone has slighted me.\n" +
                                                                                                              "• I’ll never forget the crushing defeat I suffered at my enemy’s hands, and I’ll pay them back dearly for it."),
                //Reserved
                (PersonalityNames((int)Personalities.Reserved),  "• I speak very slowly and carefully like I’m choosing each word before I say it.\n" +
                                                                                                            "• I’m more likely to communicate with a grunt or hand gesture than with actual words.\n" +
                                                                                                            "• I’d rather stand back and observe people than get involved.\n" +
                                                                                                            "• I endure any injury or insult with quiet, steely discipline.\n" +
                                                                                                            "• I always wait for the other person to talk first. There’s no such thing as an awkward silence!"),
                //Romantic
                (PersonalityNames((int)Personalities.Romantic),  "• I'm a hopeless romantic. Wherever I go, I’m always looking for “the one.”\n" +
                                                                                                            "• I fall in love in the blink of an eye…and fall out of love just as quickly.\n" +
                                                                                                            "• I have a weakness for great beauty—from breathtaking landscapes to pretty faces.\n" +
                                                                                                            "• I got rejected by someone I’m convinced is the love of my life, and I hope to prove myself worthy of them through my daring adventures!"),
                //Spiritual
                (PersonalityNames((int)Personalities.Spiritual),  "• I put too much trust in my religious institution and its hierarchy.\n" +
                                                                                                              "• I constantly quote (or misquote) sacred texts and proverbs.\n" +
                                                                                                              "• I idolize a particular hero of my faith and constantly revisit their deeds.\n" +
                                                                                                              "• I believe everything that happens to me is part of a greater divine plan.\n" +
                                                                                                              "• I keep holy symbols from every pantheon with me. Who knows which one I’ll need next?\n" +
                                                                                                              "• I see omens—both good and bad—everywhere. The gods are speaking to us, and we must listen."),
                //Superior
                (PersonalityNames((int)Personalities.Superior),  "• I never settle for anything less than perfection.\n" +
                                                                                                            "• I never admit to any mistakes because I’m scared they’ll be used against me.\n" +
                                                                                                            "• I'm used to the very best in life, so I don’t appreciate the rustic adventuring life.\n" +
                                                                                                            "• I’d kill to get a noble title (and the respect that comes with it)."),
                //Tormented
                (PersonalityNames((int)Personalities.Tormented),  "• I have awful visions of the future, but I don’t know how to prevent them from happening.\n" +
                                                                                                              "• I’m plagued by bloodthirsty urges that won’t let up no matter what I do.\n" +
                                                                                                              "• I’m haunted by my past and wake at night frightened by horrors I can barely remember.\n" +
                                                                                                              "• I faced the worst a vampire could throw at me and survived. I’m fearless, and my resolve is unwavering."),
                //Tough
                (PersonalityNames((int)Personalities.Tough),  "• I feel the need to prove that I’m the toughest person in the room.\n" +
                                                                                                      "• I’m thick-skinned. It’s very hard to get a rise out of me!\n" +
                                                                                                      "• It’s hard for me to respect anyone unless they’re a proven warrior (like me).\n" +
                                                                                                      "• Anyone who wants to earn my trust has to spar with me first.\n" +
                                                                                                      "• I have an iron stomach. I’ve never entered a drinking contest that I haven’t won!"),
                //Wild
                (PersonalityNames((int)Personalities.Wild),  "• I prefer animals to people by a long shot.\n" +
                                                                                                    "• I’m always learning how to be among others—when to stay quiet and when to crack a joke.\n" +
                                                                                                    "• My personal hygiene is nonexistent, and so are my manners.\n" +
                                                                                                    "• I’m a forest-dweller who grew up in a tent in the woods, so I’m totally ignorant of city life.\n" +
                                                                                                    "• I was actually raised by wolves (or some other wild animal)."),
                //Worldly
                (PersonalityNames((int)Personalities.Worldly),  "• I'm tolerant of people different from me, and I love exploring other cultures.\n" +
                                                                                                          "• I love to tell stories of my travels to faraway lands…even if I tend to embellish a little!\n" +
                                                                                                          "• I’m filled with glee at the idea of seeing things most people don’t. The more unsettling, the better.\n" +
                                                                                                          "• I’m desperately trying to escape my past and never stay in one place—so I’ve been everywhere."),


                //Unspecified
                (PersonalityNames((int)Personalities.None),  "• Unspecified."),
        };

        public static readonly (string, string)[] AlignmentVals =
        {
            ("Lawful Good",     "These characters always do the right thing as expected by society.\n" +
                                "They always follow the rules, tell the truth and help people out.\n" +
                                "They like order, trust and believe in people with social authority, and they aim to be an upstanding citizen.\n"),

            ("Neutral Good",    "These characters do their best to help others\n" +
                                "but they do it because they want to, not because they have\n" +
                                "been told to by a person in authority or by society’s laws.\n" +
                                "A Neutral Good person will break the rules if they are doing it\n" +
                                "for good reasons and they will feel confident\n" +
                                "and justified in their actions."),

            ("Chaotic Good",    "Chaotic Good characters do what their conscience tells\n" +
                                "them to for the greater good. They do not care about following society’s rules,\n" +
                                "they care about doing what’s right.\n" +
                                "A Chaotic Good character will speak up for and help, those who are being needlessly\n" +
                                "held back because of arbitrary rules and laws. They do not like seeing people\n" +
                                "being told what to do for nonsensical reasons.\n"),

            ("Lawful Neutral",  "A Lawful Neutral character behaves in a way that matches\n" +
                                "the organization, authority or tradition they follow.\n" +
                                "They live by this code and uphold it above all else, taking actions\n" +
                                "that are sometimes considered Good and sometimes considered Evil by others.\n" +
                                "The Lawful Neutral character does not care about what others think of\n" +
                                "their actions, they only care about their actions being correct according\n" +
                                "to their code.But they do not preach their code to others and try to convert them. \n"),

            ("True Neutral",    "True Neutral characters don’t like to take sides.\n" +
                                "They are pragmatic rather than emotional in their actions,\n" +
                                "choosing the response which makes the most sense for them in each situation.\n " +
                                "Neutral characters don’t believe in upholding the rules and laws of society, but nor\n" +
                                "do they feel the need to rebel against them. There will be times when a Neutral character\n" +
                                "has to make a choice between siding with Good or Evil, perhaps casting the deciding vote\n" +
                                "in a party. They will make a choice in these situations, usually siding with whichever causes\n" +
                                "them the least hassle, or they stand to gain the most from."),

            ("Chaotic Neutral", "Chaotic Neutral characters are free spirits.\n" +
                                "They do what they want but don’t seek to disrupt the usual norms and laws of society.\n" +
                                "These individuals don’t like being told what to do, following traditions,\n" +
                                "or being controlled. That said, they will not work to change these restrictions,\n" +
                                "instead, they will just try to avoid them in the first place.\n" +
                                "Their need to be free is the most important thing.\n"),

            ("Lawful Evil",     "Lawful Evil characters operate within a strict code of laws and traditions.\n" +
                                "Upholding these values and living by these is more important than anything,\n" +
                                "even the lives of others. They may not consider themselves to be Evil,\n" +
                                "they may believe what they are doing is right.\n" +
                                "These characters enforce their system of control through force.\n" +
                                "Anyone who doesn’t follow their code or acts out of line will face consequences.\n" +
                                "Lawful Evil characters feel no guilt or remorse for causing harm to others in this way."),

            ("Neutral Evil",    "Neutral Evil characters are selfish. Their actions are driven by their own wants\n" +
                                "whether that’s power, greed, attention, or something else.\n" +
                                "They will follow laws if they happen to align with their ambitions, but they will not\n" +
                                "hesitate to break them if they don’t.They don’t believe that following laws\n" +
                                "and traditions makes anyone a better person.\n" +
                                "Instead, they use other people’s beliefs in codes and loyalty against them, using it\n" +
                                "as a tool to influence their behaviour. "),

            ("Chaotic Evil",    "Chaotic Evil characters care only for themselves with a complete disregard\n" +
                                "for all law and order and for the welfare and freedom of others.\n" +
                                "They harm others out of anger or just for fun.\n" +
                                "Characters aligned with Chaotic Evil usually operate alone\n" +
                                "because they do not work well with others."),

            ("None",            "Not Specified"),
        };

        public static readonly (string, string)[] ConnectionListingVals =
              {
            ("Connected",     "Private profiles you have access to."),

            ("Sent",    "Sent requests to view private profiles"),

            ("Received",    "Received requests to see your tooltipData"),

            ("Blocked",  "Blocked requests to see your current tooltipData"),
        };

        public static readonly (string, string)[] ListingCategoryVals =
        {

            ("Event",     "Parts of a campaign or short term RPs."),

            ("Campaign",    "For an ongoing roleplay campaign, used for advertising long term Rps or a list of events."),

            ("Venue",    "Mostly for RP held at estates."),

            ("Group",  "For forming groups to roleplay with."),

            ("FC",  "Mainly for advertising Free Companies / FC recruitment."),

            ("Character",  "For trying to find a single player or to find like-minded players to RP with."),

            ("NPC",  "Create NPCs for public or private use."),
        };
        public static readonly (string, string)[] ListingCategorySearchVals =
        {

            ("All",     "All categories."),

            ("Event",     "For advertising parts of a campaign or short term RPs."),

            ("Campaign",    "For an ongoing roleplay campaign, long term Rps or a list of events."),

            ("Venue",    "RP held at estates or a persistant location."),

            ("Group",  "Groups to roleplay with."),

            ("FC",  "Free Companies / FC recruitment."),

            ("Character",  "Individual character profiles."),

            ("NPC",  "NPCs for public or private use."),
        };


        public static readonly (string, string)[] ListingTypeVals =
        {
            ("Casual",     "Simple mindless RP for taking things easy."),

            ("Story Based",    "Set base narrative, usually taken more seriously."),

            ("Dark",    "More heavy settings such as crime, drug use or the like."),
        };

        public static readonly (string, string)[] ListingFocusVals =
        {
            ("Slice of Life",     "For the simple things, everyday life and interactions."),

            ("Social",    "For mingling and companionship among friends or partners."),

            ("Adventure",    "Exploring the unknown or venturing into far away lands."),

            ("Combat",  "For settling differences or defeating enemies, or simply testing your prowess."),

            ("Crime",  "Focused on criminal activities and the life of crime."),

            ("Humanitarian",  "Caring for others is any way possible. / Providing support to those in need."),

            ("Worship",  "Focused on the worship of any entity, fitting for shrines and covens."),

            ("Mercenary",  "Blades for hire, focused on a soldiers of fortune narrative"),
        };

        public static readonly (string, string)[] ListingSettingVals =
    {
            ("Shop",     "Setting takes place in a public store."),

            ("Restaurant",    "Setting that takes place in a diner or food oriented market."),

            ("Library",    "Setting takes place in a book store, library or archive."),

            ("Inn",  "Setting takes place in a hotel of respite and possibly a vacation home."),

            ("School",  "Setting takes place in a public or private school."),

            ("Home",  "Setting takes place in a personal or company owned home."),

            ("Shrine",  "Setting takes place at a place of worship."),

            ("Coven",  "Setting takes place in a place of magic practice or mystical happenings."),

            ("Bathhouse",  "Setting takes place in a public or private bathing area."),

            ("Infirmary",  "Setting takes place in a hospital or medical facility."),

            ("Other",  "Setting unspecified."),
        };

        public static string ListingCategoryNames(int category)
        {
            string CategoryName = string.Empty;
            if (category == (int)ListingCategory.Event) { CategoryName = "Event"; };
            if (category == (int)ListingCategory.Campaign) { CategoryName = "Campaign"; };
            if (category == (int)ListingCategory.Venue) { CategoryName = "Venue"; };
            if (category == (int)ListingCategory.Group) { CategoryName = "Group"; };
            if (category == (int)ListingCategory.FC) { CategoryName = "FC"; };
            if (category == (int)ListingCategory.Personal) { CategoryName = "Personal"; };
            return CategoryName;
        }
        public static string ListingTypeNames(int type)
        {
            string TypeName = string.Empty;
            if (type == (int)ListingType.Casual) { TypeName = "Casual"; };
            if (type == (int)ListingType.StoryBased) { TypeName = "Story Based"; };
            if (type == (int)ListingType.DarkMature) { TypeName = "Dark / Mature"; };
            return TypeName;
        }
        public static string ListingFocusNames(int focus)
        {
            string FocusName = string.Empty;
            if (focus == (int)ListingFocus.SliceOfLife) { FocusName = "Slice of Life"; };
            if (focus == (int)ListingFocus.Social) { FocusName = "Social"; };
            if (focus == (int)ListingFocus.Adventure) { FocusName = "Adventure"; };
            if (focus == (int)ListingFocus.Combat) { FocusName = "Combat"; };
            if (focus == (int)ListingFocus.Crime) { FocusName = "Crime"; };
            if (focus == (int)ListingFocus.Humanitarian) { FocusName = "Humanitarian"; };
            if (focus == (int)ListingFocus.Worship) { FocusName = "Worship"; };
            if (focus == (int)ListingFocus.Mercenary) { FocusName = "Mercinary"; };
            return FocusName;
        }
        public static string ListingSettingNames(int setting)
        {
            string SettingName = string.Empty;
            if (setting == (int)ListingSetting.Shop) { SettingName = "Shop"; };
            if (setting == (int)ListingSetting.Restaurant) { SettingName = "Restaurant"; };
            if (setting == (int)ListingSetting.Library) { SettingName = "Library"; };
            if (setting == (int)ListingSetting.Inn) { SettingName = "Inn"; };
            if (setting == (int)ListingSetting.School) { SettingName = "School"; };
            if (setting == (int)ListingSetting.Home) { SettingName = "Home"; };
            if (setting == (int)ListingSetting.Shrine) { SettingName = "Shrine"; };
            if (setting == (int)ListingSetting.Coven) { SettingName = "Coven"; };
            if (setting == (int)ListingSetting.Bathhouse) { SettingName = "Bathhouse"; };
            if (setting == (int)ListingSetting.Infirmary) { SettingName = "Infirmary"; };
            if (setting == (int)ListingSetting.Other) { SettingName = "Other"; };
            return SettingName;
        }

       
   


        public static readonly (string, string, Type)[] LayoutTypeVals =
        {
            ( "Tree", "For talent trees or social connections.", typeof(Relationship)),
            ( "Roster", "Coming soon", typeof(Relationship)),
            ( "Bio", "Biography page with pre-defined and custom input elements.", typeof(Bio)),
            ( "Details", "Details page with input element creation.", typeof(Details)),
            ( "Story", "Story page with chapters creation.", typeof(Story)),
            ( "Info", "A page with a single text field.", typeof(Info)),
            ( "Gallery", "A gallery page to add all your favorite images.", typeof(Gallery)),
            ( "Inventory", "A inventory page to hold all your items.", typeof(Inventory))
        };

    }
    public class CustomTab
    {
        public int ID;
        public string Name = string.Empty;
        public bool IsOpen;
        public bool ShowPopup;
        public int type;
        public CustomLayout Layout;
    }
    public class Attribute()
    {
        public string Name { get; set; }
        public string Description { get; set; }

    }


    public class BaseListingData
    {
        public int id { get; set; }
        public string name { get; set; }
    }

    public class Listing
    {
        public int id { get; set; } = 0;
        public string name { get; set; } = string.Empty;
        public IDalamudTextureWrap avatar { get; set; } = UI.UICommonImage(UI.CommonImageTypes.avatarHolder);
        public string description { get; set; } = string.Empty;
        public string rules { get; set; } = string.Empty;
        public int category { get; set; } = 0;
        public int type { get; set; } = 0; // 0 = all, 1 = personals
        public int focus { get; set; } = 0;
        public int setting { get; set; } = 0;
        public IDalamudTextureWrap banner { get; set; } = UI.UICommonImage(UI.CommonImageTypes.blankPictureTab);
        public int inclusion { get; set; } = 0;
        public string startDate { get; set; } = string.Empty;
        public string endDate { get; set; } = string.Empty;
        public bool ARR { get; set; } = false;
        public bool HW { get; set; } = false;
        public bool SB { get; set; } = false;
        public bool SHB { get; set; } = false;
        public bool EW { get; set; } = false;
        public bool DT { get; set; } = false;
        public Vector4 color { get; set; } = new Vector4(1, 1, 1, 1); // Default white color
    }
}

public class SpellDefinition
{
    public string name { get; set; }
    public string description { get; set; }
    public SpellType type { get; set; } // 0 = Offensive, 1 = Defensive, 2 = Utility
    public int iconID { get; set; }
    public int level { get; set; } // Level required to use the spell
    public int manaCost { get; set; } // Mana cost to use the spell
}

public class SpellType
{
    public int id { get; set; }
    public string name { get; set; }
}

public class ItemDefinition
{
    public string name { get; set; }
    public string description { get; set; }
    public int type { get; set; }
    public int subtype { get; set; }
    public int iconID { get; set; }
    public IDalamudTextureWrap iconTexture { get; set; } = null; // Texture for the item icon
    public int slot { get; set; }
    public int quality { get; set; }
}

public class Relationship
{
    public string Name { get; set; } = string.Empty;
    public Vector4 NameColor { get; set; } = new Vector4(1, 1, 1, 1);
    public string Description { get; set; } = string.Empty;
    public Vector4 DescriptionColor { get; set; } = new Vector4(1, 1, 1, 1);
    public int IconID { get; set; } = -1; // Icon ID for the relationship, -1 means no icon
    public IDalamudTextureWrap ImageTexture { get; set; } = null;
    public IDalamudTextureWrap IconTexture { get; set; } = null;
    public (int x, int y)? Slot { get; set; } = null; // The grid slot this relationship is attached to

    // Line/Link data
    public List<RelationshipLink> Links { get; set; } = new(); // All connections/lines for this relationship

    public Vector4 LineColor { get; set; } = new Vector4(1, 1, 0.3f, 1);
    public float LineThickness { get; set; } = 6f;
    public bool active { get; set; } = false; // Whether the item is learned or not
}

public class RelationshipLink
{
    public (int x, int y) From { get; set; }
    public (int x, int y) To { get; set; }
    public Vector4? LineColor { get; set; } // Optional: override per-link
    public float? LineThickness { get; set; } // Optional: override per-link
}

public class InventoryLayout : CustomLayout
{
    public int tabIndex { get; set; } = 0; // Index of the currently selected tab   
    public string tabName { get; set; } = string.Empty;

    public Dictionary<int, ItemDefinition> inventorySlotContents = new Dictionary<int, ItemDefinition>();
    public Dictionary<int, ItemDefinition> tradeSlotContents = new Dictionary<int, ItemDefinition>();
    public Dictionary<int, ItemDefinition> traderSlotContents = new Dictionary<int, ItemDefinition>();
}
public class StaticLayout : CustomLayout
{
    public  string tabName { get; set; } = string.Empty;
    public List<LayoutElement> elements { get; set; } = new List<LayoutElement>();
}
public class DynamicLayout : CustomLayout
{
    internal int tabIndex;

    public  string tabName { get; set; } = string.Empty;
    public List<LayoutElement> elements { get; set; } = new List<LayoutElement>();
    public LayoutTreeNode RootNode { get; set; } = new LayoutTreeNode("Root", true ,-1, null); // Each layout has its own tre
}
public class BioLayout : CustomLayout 
{
    public int tabIndex { get; set; } = 0; // Index of the currently selected tab
    public  string tabName { get; set; } = string.Empty;
    public bool isTooltip { get; set; } = false; // Whether the layout is being used as a tooltip
    public string name { get; set; } = string.Empty;
    public string race { get; set; } = string.Empty;
    public string gender { get; set; } = string.Empty;
    public string age { get; set; } = string.Empty;
    public string height { get; set; } = string.Empty;
    public string weight { get; set; } = string.Empty;
    public string afg { get; set; } = string.Empty; // Affiliation, e.g. Free Company, Group, etc.
    public int alignment { get; set; } = 0; // 0 = None, 1 = Lawful Good, 2 = Neutral Good, etc.
    public int personality_1 { get; set; } = 0; // 0 = None, 1 = Optimistic, 2 = Polite, etc.
    public int personality_2 { get; set; } = 0; // 0 = None, 1 = Optimistic, 2 = Polite, etc.
    public int personality_3 { get; set; } = 0; // 0 = None, 1 = Optimistic, 2 = Polite, etc.
    public List<descriptor> descriptors { get; set; } = new List<descriptor>(); // List of descriptors
    public List<trait> traits { get; set; } = new List<trait>(); // List of traits
    public List<field> fields { get; set; } = new List<field>(); // List of custom fields
                                                                 // Add parsed node caches and last-value tracking for each property
    public ParsedNode parsedName;
    public string lastName;
    public ParsedNode parsedRace;
    public string lastRace;
    public ParsedNode parsedGender;
    public string lastGender;
    public ParsedNode parsedAge;
    public string lastAge;
    public ParsedNode parsedHeight;
    public string lastHeight;
    public ParsedNode parsedWeight;
    public string lastWeight;
    public ParsedNode parsedAfg;
    public string lastAfg;

}
public class GalleryLayout : CustomLayout
{
    public  string tabName { get; set; } = string.Empty;
    public int tabIndex { get; set; } = 0; // Index of the currently selected tab
    public List<ProfileGalleryImage> images { get; set; } = new List<ProfileGalleryImage>(); // List of images in the gallery
}
public class InfoLayout : CustomLayout
{
    public string tabName { get; set; }
    public int tabIndex { get; set; }
    public string text { get; set; }
    // Add parsed cache
    public ParsedNode parsedText;
    public string lastText;
}

public class DetailsLayout : CustomLayout
{
    public  string tabName { get; set; } = string.Empty;
    public int tabIndex { get; set; } = 0; // Index of the currently selected tab
    public List<Detail> details { get; set; } = new List<Detail>(); // List of details for the Details layout
}

public class  Detail
{
    public int id { get; set; }
    public string name { get; set; }
    public string content { get; set; }
}

public class StoryLayout : CustomLayout
{
    public string tabName { get; set; } = string.Empty;
    public int tabIndex { get; set; } = 0; // Index of the currently selected tab
    public List<StoryChapter> chapters { get; set; } = new List<StoryChapter>(); // List of chapters in the story
    public bool loadChapters { get; set; } = false; // Whether to load chapters from the database

}


public class StoryChapter
{
    public int id { get; set; }
    public string title { get; set; } = string.Empty;
    public string content { get; set; } = string.Empty;
}

public class TreeLayout : CustomLayout
{
    public (int x, int y)? ActionSourceSlot { get; set; }
    public string tabName { get; set; } = string.Empty;
    public int tabIndex { get; set; } = 0; // Index of the currently selected tab
    public (int x, int y)? SelectedSlot { get; set; }
    public (int x, int y)? PreviousSlot { get; set; }
    public List<((int x, int y) from, (int x, int y) to)> Connections { get; set; } = new();
    public List<List<(int x, int y)>> Paths { get; set; } = new(); // Each path is a list of slots
    public List<List<((int x, int y) from, (int x, int y) to)>> PathConnections { get; set; } = new(); // Each path's connections
    public int CurrentPathIndex { get; set; } = 0; 
    public enum RelationshipAction { None, Create, Break }
    public RelationshipAction CurrentAction { get; set; } = RelationshipAction.None;
    public List<Relationship> relationships { get; set; } = new List<Relationship>(); // List of relationships in the layout
}
public enum RelationshipType
{
    Friend = 0,
    Enemy = 1,
    Rival = 2,
    Mentor = 3,
    Student = 4,
    Family = 5,
    Workplace = 6,
    Other = 7
}
public class DateTimeElement
{
    public int selectedStartYear { get; set; }
    public int selectedEndYear { get; set; }
    public int selectedStartMonth { get; set; }
    public int selectedEndMonth { get; set; }
    public int selectedStartDay { get; set; }
    public int selectedEndDay { get; set; }
    public int selectedStartHour { get; set; }
    public int selectedEndHour { get; set; }
    public int selectedStartMinute { get; set; }
    public int selectedEndMinute { get; set; }
    public int selectedStartAmPm { get; set; }
    public int selectedEndAmPm { get; set; }
    public int selectedStartTimezone { get; set; }
    public int selectedEndTimezone { get; set; }
}
public class LayoutElement
{
    public int layoutID { get; set; }
    public bool IsFolder { get; set; }
    public bool Lockstatus { get; set; } = true;
    public bool EditStatus { get; set; } = false;
    public int id { set; get; }
    public string name { set; get; }
    public bool isBeingRenamed { get; set; } = false; 
    public bool locked { get; set; } = true;
    public float PosX { get; set; }
    public float PosY { get; set; }
    public bool added { get; set; } = false;
    public bool modifying { set; get; }
    public bool canceled { set; get; } = false;
    public bool dragging { set; get; }
    public bool resizing { set; get; } = false;
    public byte[] tooltipBGBytes { set; get; }
    public IDalamudTextureWrap tooltipBG { set; get; } = null;
    public string tooltipTitle { set; get; } = string.Empty;
    public string tooltipDescription { set; get; } = string.Empty;
    public int type { set; get; }
    public Vector2 dragOffset { get; set; }
    public LayoutTreeNode relatedNode { get; set;}


}
public class FolderElement : LayoutElement
{
    internal bool loaded { get; set; }
    public int id { set; get; }
    public string text { set; get; }

}
public class EmptyElement : LayoutElement
{
    internal bool loaded { get; set; }

    public int id { set; get; }
    public string text { set; get; }
    public Vector4 color { set; get; }
}
public class IconElement : LayoutElement
{
    internal bool loaded { get; set; }

    public enum IconState
    {
        Displaying,
        Modifying
    }
    public int iconID { get; set; } // ID for the icon, used to fetch the icon from the database or resources
    public IDalamudTextureWrap icon { get; set; }
    public IconState State { get; set; } = IconState.Displaying; // Default state
  
}
public enum LayoutElementTypes
{
    Empty = -1,
    Folder = 0,
    Text = 1,
    TextMultiline = 2,
    Image = 3,
    Icon = 4,
}

public class TextElement : LayoutElement
{
    internal bool loaded { get; set; }
    public int subType { set; get; }
    public string text { set; get; } = string.Empty;
    public Vector4 color { set; get; }
    public float width = 300; // default dimensions
    public float height = 120;
    public bool resizing = false;
}

public class DynamicLayoutNode
{
    public int ID { get; set; } // Unique ID for ImGui elements
    public string Name { get; set; } // Name of the node
    public bool IsFolder { get; set; } // Whether the node is a folder
    public int parentID { get; set; } // Parent ID for hierarchy

}
public class LayoutTreeNode
{
    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set
        {
            if (value == null)
                Plugin.PluginLog.Debug($"[DEBUG] Attempted to set node.Name to null for node ID {ID}");
            _name = value ?? string.Empty;
        }
    }
    public bool takenName = false; // Used to check if the name is already taken in the tree
    public bool IsFolder { get; set; }
    public List<LayoutTreeNode> Children { get; set; } = new List<LayoutTreeNode>();
    public LayoutTreeNode? Parent { get; set; } // Needed for Drag & Drop
    public LayoutElement relatedElement { get; set; }
    public int ID { get; set; } // Unique ID for ImGui elements
    public int ParentID { get; set; } // Parent ID for hierarchy
    public bool IsBeingRenamed { get; set; } = false;
    public IDalamudTextureWrap editBtn = UI.UICommonImage(UI.CommonImageTypes.edit);
    public IDalamudTextureWrap moveBtn = UI.UICommonImage(UI.CommonImageTypes.move);
    public IDalamudTextureWrap moveCancelBtn = UI.UICommonImage(UI.CommonImageTypes.move_cancel);
    public int layoutID { get; set; }

    private static int nextID = 0;

    public LayoutTreeNode(string name, bool isFolder, int nodeID, LayoutTreeNode? parent = null)
    {
        Name = name;
        IsFolder = isFolder;
        Parent = parent;
        ID = nodeID;
    }

    public void AddChild(LayoutTreeNode child)
    {
        child.Parent = this;
        Children.Add(child);
    }
}


public class ImageElement : LayoutElement
{

    internal bool loaded { get; set; } = false;
    public float LastWindowWidth = -1f;
    public float RelativeX = -1f;
    public string url { get; set; } // URL for the image, used for fetching from the database or resources
    public byte[] bytes { get; set; }
    public IDalamudTextureWrap textureWrap { set; get; } = UI.UICommonImage(UI.CommonImageTypes.blankPictureTab);
    public string tooltip { get; set; }
    public float width { get; set; }
    public float height { get; set; }
    public bool initialized { get; set; }
    public bool proprotionalEditing { set; get; } = true;
    public bool hasTooltip { get; set; }
    internal bool maximizable { get; set; }   

}
public class CustomLayout
{
    public int id { get; set; }
    public string name { get; set; }
    public LayoutTypes layoutType { get; set; }
    public bool viewable { get; set; } = true; // If the layout is viewable by others
}

// Section for organizing self-assign roles
public class GroupRoleSection
{
    public int id { get; set; }
    public int groupID { get; set; }
    public string name { get; set; }
    public int sortOrder { get; set; }
}

// Self-assign role that members can toggle on/off
public class GroupSelfAssignRole
{
    public int id { get; set; }
    public int groupID { get; set; }
    public int sectionID { get; set; } // 0 = no section (Uncategorized)
    public string name { get; set; }
    public string color { get; set; } // Hex color code (e.g., "#FF5733")
    public string description { get; set; }
    public int sortOrder { get; set; }
    public long createdAt { get; set; }
    public long updatedAt { get; set; }
    // Channel permissions granted by this role
    public List<GroupChannelRolePermission> channelPermissions { get; set; } = new List<GroupChannelRolePermission>();
}

// Permission granted by a self-assign role for a specific channel
public class GroupChannelRolePermission
{
    public int id { get; set; }
    public int channelID { get; set; }
    public int roleID { get; set; }
    public bool canView { get; set; }
    public bool canPost { get; set; }
}
