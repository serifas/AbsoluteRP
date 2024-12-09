using Dalamud.Interface.Internal;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Lumina.Data;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Xml.Linq;
using static FFXIVClientStructs.FFXIV.Client.UI.Misc.GroupPoseModule;
using static System.Net.Mime.MediaTypeNames;

namespace AbsoluteRoleplay
{
    
    public class PlayerProfile
    {
        public IDalamudTextureWrap avatar;
        public string Name { get; set; }
        public string Race { get; set; }
        public string Gender { get; set; }
        public string Age { get; set; }
        public string Height { get; set; }
        public string Weight { get; set; }
        public string AFG { get; set; }
        public int Alignment { get; set; }
        public int Personality_1 { get; set; }
        public int Personality_2 { get; set; }
        public int Personality_3 { get; set; }
        public List<Hooks> Hooks { get; set; }
        public List<ProfileGalleryImage> GalleryImages { get; set; }
        public Story Story { get; set; }
        public string OOC { get; set; }
   
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


    public class ProfileGalleryImage
    {
        public string url;
        public string tooltip;
        public bool nsfw;
        public bool trigger;
    }

    public class Story
    {
        public List<Chapter> chapters { get; set; }
    }
    public class Chapter
    {
        public string name { get; set; }
        public string content { get; set; }
    }
    public class Hooks
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
            REGISTRATION_DUPLICATE_EMAIL = 5,
            REGISTRATION_DUPLICATE_USERNAME = 6,
            //Password Change
            PASSCHANGE_PASSWORD_CHANGED = 7,
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
            //systems
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
        public static IDalamudTextureWrap UICommonImage(CommonImageTypes imageType)
        {
            IDalamudTextureWrap commonImage = null;
            if (Plugin.PluginInterface is { AssemblyLocation.Directory.FullName: { } path })
            {
                if (imageType == CommonImageTypes.discordBtn) { commonImage = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/disc_btn.png"))).Result; }
                if (imageType == CommonImageTypes.kofiBtn) { commonImage = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/kofi_btn.png"))).Result; }
                if (imageType == CommonImageTypes.blankPictureTab) { commonImage = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/profiles/galleries/picturetab.png"))).Result; }
                if (imageType == CommonImageTypes.NSFW) { commonImage = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/profiles/galleries/nsfw.png"))).Result;}
                if (imageType == CommonImageTypes.NSFWTRIGGER) { commonImage = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/profiles/galleries/nsfw_trigger.png"))).Result;}
                if (imageType == CommonImageTypes.TRIGGER) { commonImage = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/profiles/galleries/trigger.png"))).Result;}
                if (imageType == CommonImageTypes.avatarHolder) { commonImage = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/profiles/avatar_holder.png"))).Result;}
                if (imageType == CommonImageTypes.profileSection) { commonImage = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/section_profiles.png"))).Result;}
                if (imageType == CommonImageTypes.systemsSection) { commonImage = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/section_systems.png"))).Result;}
                if (imageType == CommonImageTypes.eventsSection) { commonImage = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/section_events.png"))).Result;}
                if (imageType == CommonImageTypes.connectionsSection) { commonImage = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/section_connections.png"))).Result;}
                //profiles
                if (imageType == CommonImageTypes.profileCreateProfile) { commonImage = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/profiles/profile_create.png"))).Result;}
                if (imageType == CommonImageTypes.profileCreateNPC) { commonImage = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/profiles/npc_create.png"))).Result;}
                if (imageType == CommonImageTypes.profileBookmarkProfile) { commonImage = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/profiles/profile_bookmarks.png"))).Result;}
                if (imageType == CommonImageTypes.profileBookmarkNPC) { commonImage = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/profiles/npc_bookmarks.png"))).Result;}
                //target images
                if (imageType == CommonImageTypes.targetConnections) { commonImage = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/targets/assign_connection.png"))).Result;}
                if (imageType == CommonImageTypes.targetBookmark) { commonImage = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/targets/bookmark.png"))).Result;}
                if (imageType == CommonImageTypes.targetGroupInvite) { commonImage = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/targets/group_invite.png"))).Result;}
                if (imageType == CommonImageTypes.targetViewProfile) { commonImage = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/targets/profile_view.png"))).Result;}
                if (imageType == CommonImageTypes.reconnect) { commonImage = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/connect.png"))).Result;}
                //listings
                if (imageType == CommonImageTypes.listingsCampaign) { commonImage = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/listings/campaign.png"))).Result; }
                if (imageType == CommonImageTypes.listingsEvent) { commonImage = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/listings/event.png"))).Result; }
                if (imageType == CommonImageTypes.listingsFC) { commonImage = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/listings/fc.png"))).Result; }
                if (imageType == CommonImageTypes.listingsGroup) { commonImage = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/listings/group.png"))).Result; }
                if (imageType == CommonImageTypes.listingsPersonal) { commonImage = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/listings/personal.png"))).Result; }
                if (imageType == CommonImageTypes.listingsVenue) { commonImage = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/listings/venue.png"))).Result; }
                if (imageType == CommonImageTypes.listingsCampaignBig) { commonImage = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/listings/campaign_big.png"))).Result; }
                if (imageType == CommonImageTypes.listingsEventBig) { commonImage = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/listings/event_big.png"))).Result; }
                if (imageType == CommonImageTypes.listingsFCBig) { commonImage = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/listings/fc_big.png"))).Result; }
                if (imageType == CommonImageTypes.listingsGroupBig) { commonImage = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/listings/group_big.png"))).Result; }
                if (imageType == CommonImageTypes.listingsPersonalBig) { commonImage = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/listings/personal_big.png"))).Result; }
                if (imageType == CommonImageTypes.listingsVenueBig) { commonImage = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/listings/venue_big.png"))).Result; }
                //misc
                if (imageType == CommonImageTypes.blank) { commonImage = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/blank.png"))).Result; }

            }

            return commonImage;
            
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

        public static IDalamudTextureWrap AlignementIcon(int id)
        {
            IDalamudTextureWrap alignmentIcon = null;
            if (Plugin.PluginInterface is { AssemblyLocation.Directory.FullName: { } path })
            {
                if (id == (int)Alignments.LawfulGood) { alignmentIcon = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/profiles/alignments/lawful_good.png"))).Result;}
                if (id == (int)Alignments.NeutralGood) { alignmentIcon = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/profiles/alignments/neutral_good.png"))).Result;}
                if (id == (int)Alignments.ChaoticGood) { alignmentIcon = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/profiles/alignments/chaotic_good.png"))).Result; }
                if (id == (int)Alignments.LawfulNeutral) { alignmentIcon = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/profiles/alignments/lawful_neutral.png"))).Result; }
                if (id == (int)Alignments.TrueNeutral) { alignmentIcon = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/profiles/alignments/true_neutral.png"))).Result; }
                if (id == (int)Alignments.ChaoticNeutral) { alignmentIcon = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/profiles/alignments/chaotic_neutral.png"))).Result; }
                if (id == (int)Alignments.LawfulEvil) { alignmentIcon = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/profiles/alignments/lawful_evil.png"))).Result; }
                if (id == (int)Alignments.NeutralEvil) { alignmentIcon = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/profiles/alignments/neutral_evil.png"))).Result; }
                if (id == (int)Alignments.ChaoticEvil) { alignmentIcon = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/profiles/alignments/chaotic_evil.png"))).Result;}
                if (id == (int)Alignments.None) { alignmentIcon = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/profiles/personalities/none.png"))).Result;}
            }

            return alignmentIcon;
        }
        public static IDalamudTextureWrap PersonalityIcon(int id)
        {
            IDalamudTextureWrap personalityIcon = null;
            if (Plugin.PluginInterface is { AssemblyLocation.Directory.FullName: { } path })
            {

                if (id == (int)Personalities.Abrasive) { personalityIcon = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/profiles/personalities/abrasive.png"))).Result;}
                if (id == (int)Personalities.AbsentMinded) { personalityIcon = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/profiles/personalities/absentminded.png"))).Result; }
                if (id == (int)Personalities.Aggressive) { personalityIcon = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/profiles/personalities/aggressive.png"))).Result; }
                if (id == (int)Personalities.Artistic) { personalityIcon = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/profiles/personalities/artistic.png"))).Result;}
                if (id == (int)Personalities.Cautious) { personalityIcon = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/profiles/personalities/cautious.png"))).Result;}
                if (id == (int)Personalities.Charming) { personalityIcon = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/profiles/personalities/charming.png"))).Result;}
                if (id == (int)Personalities.Compassionate) { personalityIcon = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/profiles/personalities/compassionate.png"))).Result;}
                if (id == (int)Personalities.Daredevil) { personalityIcon = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/profiles/personalities/daredevil.png"))).Result;}
                if (id == (int)Personalities.Dishonest) { personalityIcon = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/profiles/personalities/dishonest.png"))).Result;}
                if (id == (int)Personalities.Dutiful) { personalityIcon = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/profiles/personalities/dutiful.png"))).Result;}
                if (id == (int)Personalities.Easygoing) { personalityIcon = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/profiles/personalities/easygoing.png"))).Result;}
                if (id == (int)Personalities.Eccentric) { personalityIcon = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/profiles/personalities/eccentric.png"))).Result;}
                if (id == (int)Personalities.Honest) { personalityIcon = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/profiles/personalities/honest.png"))).Result;}
                if (id == (int)Personalities.Knowledgable) { personalityIcon = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/profiles/personalities/knowledgable.png"))).Result;}
                if (id == (int)Personalities.Optimistic) { personalityIcon = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/profiles/personalities/optimistic.png"))).Result;}
                if (id == (int)Personalities.Polite) { personalityIcon = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/profiles/personalities/polite.png"))).Result;}
                if (id == (int)Personalities.Relentless) { personalityIcon = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/profiles/personalities/relentless.png"))).Result;}
                if (id == (int)Personalities.Resentful) { personalityIcon = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/profiles/personalities/resentful.png"))).Result;}
                if (id == (int)Personalities.Reserved) { personalityIcon = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/profiles/personalities/reserved.png"))).Result;}
                if (id == (int)Personalities.Romantic) { personalityIcon = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/profiles/personalities/romantic.png"))).Result;}
                if (id == (int)Personalities.Spiritual) { personalityIcon = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/profiles/personalities/spiritual.png"))).Result;}
                if (id == (int)Personalities.Superior) { personalityIcon = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/profiles/personalities/superior.png"))).Result;}
                if (id == (int)Personalities.Tormented) { personalityIcon = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/profiles/personalities/tormented.png"))).Result;}
                if (id == (int)Personalities.Tough) { personalityIcon = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/profiles/personalities/tough.png"))).Result;}
                if (id == (int)Personalities.Wild) { personalityIcon = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/profiles/personalities/wild.png"))).Result;}
                if (id == (int)Personalities.Worldly) { personalityIcon = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/profiles/personalities/worldly.png"))).Result;}
                if (id == (int)Personalities.None) { personalityIcon = Plugin.TextureProvider.CreateFromImageAsync(Misc.ImageToByteArray(Path.Combine(path, "UI/common/profiles/personalities/worldly.png"))).Result;}
            }
            return personalityIcon;
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


                //Worldly
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

            ("Received",    "Received requests to see your profile"),

            ("Blocked",  "Blocked requests to see your current profile"),
        };


        public static readonly (int, string, IDalamudTextureWrap)[] ListingNavigationVals =
        {
            (0, "Events", UICommonImage(CommonImageTypes.listingsEvent)),

            (1, "Campaigns", UICommonImage(CommonImageTypes.listingsCampaign)),

            (2, "Venues", UICommonImage(CommonImageTypes.listingsVenue)),

            (3, "Groups", UICommonImage(CommonImageTypes.listingsGroup)),

            (4, "FCs", UICommonImage(CommonImageTypes.listingsFC)),

            (5, "Personals", UICommonImage(CommonImageTypes.listingsPersonal)),

        };
        public static readonly (string, string)[] ListingCategoryVals =
        {
            ("Event",     "For advertising parts of a campaign or short term RPs."),

            ("Campaign",    "For an ongoing roleplay campaign, used for advertising long term Rps or a list of events."),

            ("Venue",    "Mostly for RP held at estates."),

            ("Group",  "For forming groups to roleplay with."),

            ("FC",  "Mainly for advertising Free Companies / FC recruitment."),

            ("Personal",  "For trying to find any of the above as a single player or to find like-minded players to RP with."),
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



    }

    public class Attribute()
    {
        public string Name { get; set; }
        public string Description { get; set; }

    }
    public class Listing
        {
            public string name { get; set; }
            public string description { get; set; }
            public string rules { get; set; }
            public int category { get; set; }
            public int type { get; set; }
            public int focus { get; set; }
            public int setting { get; set; }
            public IDalamudTextureWrap banner { get; set; }
            public int inclusion { get; set; }
            public string startDate { get; set; }
            public string endDate { get; set; }

            public Listing(string Name, string Description, string Rules, int Category, int Type, int Focus, int Setting, IDalamudTextureWrap Banner, int Inclusion, string StartDate, string EndDate)
            {
                name = Name;
                description = Description;
                rules = Rules;
                category = Category;
                type = Type;
                focus = Focus;
                setting = Setting;
                banner = Banner;
                inclusion = Inclusion;
                startDate = StartDate;
                endDate = EndDate;
            }
        }
}

