using AbsoluteRP.Defines;
using System.Collections.Generic;

namespace AbsoluteRP.Helpers
{
    public static class ProfileTutorial
    {
        public const string Flow = "profile-window";

        public const string Anchor_Window           = "pw.window";
        public const string Anchor_TutorialToggle   = "pw.tutorialtoggle";
        public const string Anchor_VerifyBtn        = "pw.verify";
        public const string Anchor_AddProfileBtn    = "pw.addprofile";
        public const string Anchor_TypeDropdown     = "pw.typedropdown";
        public const string Anchor_ProfileTitle     = "pw.profiletitle";
        public const string Anchor_CreateBtn        = "pw.createbtn";

        public const string Anchor_Private          = "pw.private";
        public const string Anchor_Active           = "pw.active";
        public const string Anchor_Compass          = "pw.compass";
        public const string Anchor_NsfwTrigger      = "pw.nsfwtrigger";
        public const string Anchor_Spoilers         = "pw.spoilers";
        public const string Anchor_SaveBtn          = "pw.savebtn";
        public const string Anchor_EditAvatarBtn    = "pw.editavatar";
        public const string Anchor_TitleInput       = "pw.titleinput";
        public const string Anchor_SetBackground    = "pw.setbackground";

        public const string Anchor_AddTabBtn        = "pw.addtab";
        public const string Anchor_NewPageName      = "pw.newpagename";
        public const string Anchor_LayoutDropdown   = "pw.layoutdropdown";
        public const string Anchor_LayoutItem       = "pw.layoutitem";
        public const string Anchor_SubmitTab        = "pw.submittab";

        public const string Anchor_BioSetAsTooltip  = "bio.setastooltip";
        public const string Anchor_BioBasicInfo     = "bio.basicinfo";
        public const string Anchor_BioName          = "bio.name";
        public const string Anchor_BioRace          = "bio.race";
        public const string Anchor_BioGender        = "bio.gender";
        public const string Anchor_BioAge           = "bio.age";
        public const string Anchor_BioHeight        = "bio.height";
        public const string Anchor_BioCustomInfo    = "bio.custominfo";
        public const string Anchor_BioDetails       = "bio.details";
        public const string Anchor_BioCustomDetails = "bio.customdetails";
        public const string Anchor_BioTraits        = "bio.traits";
        public const string Anchor_BioCustomTraits  = "bio.customtraits";

        public const string Step_Welcome            = "welcome";
        public const string Step_TutorialToggle     = "tutorial-toggle";
        public const string Step_Verify             = "verify";
        public const string Step_AddProfile         = "add-profile";
        public const string Step_PickType           = "pick-type";
        public const string Step_PickTitle          = "pick-title";
        public const string Step_PressCreate        = "press-create";

        public const string Step_AfterCreate        = "after-create";
        public const string Step_Private            = "explain-private";
        public const string Step_Active             = "explain-active";
        public const string Step_Compass            = "explain-compass";
        public const string Step_NsfwTrigger        = "explain-nsfwtrigger";
        public const string Step_Spoilers           = "explain-spoilers";
        public const string Step_Save               = "explain-save";
        public const string Step_EditAvatar         = "explain-editavatar";
        public const string Step_TitleInput         = "explain-titleinput";
        public const string Step_SetBackground      = "explain-setbackground";

        public const string Step_TabsIntro          = "tabs-intro";
        public const string Step_AddTab             = "tabs-addtab";

        public const string Step_NewPageName        = "newpage-name";
        public const string Step_LayoutDropdown     = "newpage-dropdown";
        public const string Step_Layout_Tree        = "layout-tree";
        public const string Step_Layout_Bio         = "layout-bio";
        public const string Step_Layout_Details     = "layout-details";
        public const string Step_Layout_Story       = "layout-story";
        public const string Step_Layout_Info        = "layout-info";
        public const string Step_Layout_Gallery     = "layout-gallery";
        public const string Step_PressSubmit        = "newpage-submit";

        public const string Step_BioIntro           = "bio-intro";
        public const string Step_BioSetAsTooltip    = "bio-setastooltip";
        public const string Step_BioBasicInfo       = "bio-basicinfo";
        public const string Step_BioName            = "bio-name";
        public const string Step_BioRace            = "bio-race";
        public const string Step_BioGender          = "bio-gender";
        public const string Step_BioAge             = "bio-age";
        public const string Step_BioHeight          = "bio-height";
        public const string Step_BioCustomInfo      = "bio-custominfo";
        public const string Step_BioDetails         = "bio-details";
        public const string Step_BioCustomDetails   = "bio-customdetails";
        public const string Step_BioTraits          = "bio-traits";
        public const string Step_BioCustomTraits    = "bio-customtraits";
        public const string Step_BioSaveReminder    = "bio-savereminder";
        public const string Step_Finish             = "finish";

        public static void Install()
        {
            var steps = new List<TutorialManager.Step>
            {
                new()
                {
                    Id = Step_Welcome,
                    AnchorId = Anchor_Window,
                    Title = "Welcome to your profile",
                    Body = "This window is where you build, edit, and publish character profiles. Each character can have several profiles; you pick which one is currently shown to others. Let's walk through it together.",
                    NextStepId = Step_TutorialToggle,
                },
                new()
                {
                    Id = Step_TutorialToggle,
                    AnchorId = Anchor_TutorialToggle,
                    Title = "Tutorials checkbox",
                    Body = "This toggle controls whether tutorials appear when you open profile-related windows. It's on by default. Unchecking it disables every guided walkthrough; turning it back on shows them again. Your choice is saved.",
                    PrevStepId = Step_Welcome,
                    NextStepId = Step_AddProfile,
                },
                new()
                {
                    Id = Step_Verify,
                    AnchorId = Anchor_VerifyBtn,
                    Title = "Verify your character first",
                    Body = "You need to verify ownership of this character via Lodestone before you can create a profile. Click Verify Character, paste your Lodestone URL, and follow the in-game prompt.",
                    NextStepId = Step_AddProfile,
                },
                new()
                {
                    Id = Step_AddProfile,
                    AnchorId = Anchor_AddProfileBtn,
                    Title = "Add a profile",
                    Body = "Click Add Profile to open the creation popup. You can have multiple profiles for the same character (e.g. one personal, one for a venue, one for a group).",
                    PrevStepId = Step_TutorialToggle,
                    NextStepId = Step_PickType,
                    AutoAdvanceWhen = () => AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileWindow.showTypeCreation,
                    HideNextUntilAdvance = true,
                },
                new()
                {
                    Id = Step_PickType,
                    AnchorId = Anchor_TypeDropdown,
                    Title = "Pick a profile type",
                    Body = "The type controls how this profile is categorized in public listings. Character is the safe default for an OC. Venue, Group, FC, etc. surface your profile under those categories in the social listings.",
                    NextStepId = Step_PickTitle,
                    AutoAdvanceWhen = () =>
                        !AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileWindow.showTypeCreation
                        && AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileWindow.ExistingProfile,
                    AutoAdvanceToStepId = Step_AfterCreate,
                },
                new()
                {
                    Id = Step_PickTitle,
                    AnchorId = Anchor_ProfileTitle,
                    Title = "Give it a title",
                    Body = "This is the headline shown at the top of the profile and on profile cards. You can change it any time later.",
                    PrevStepId = Step_PickType,
                    NextStepId = Step_PressCreate,
                    AutoAdvanceWhen = () =>
                        !AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileWindow.showTypeCreation
                        && AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileWindow.ExistingProfile,
                    AutoAdvanceToStepId = Step_AfterCreate,
                },
                new()
                {
                    Id = Step_PressCreate,
                    AnchorId = Anchor_CreateBtn,
                    Title = "Create the profile",
                    Body = "Click Create when you're ready. Once created, we'll keep going through the editor.",
                    PrevStepId = Step_PickTitle,
                    NextStepId = Step_AfterCreate,
                    AutoAdvanceWhen = () =>
                        !AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileWindow.showTypeCreation
                        && AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileWindow.ExistingProfile,
                    AutoAdvanceToStepId = Step_AfterCreate,
                    HideNextUntilAdvance = true,
                },

                new()
                {
                    Id = Step_AfterCreate,
                    AnchorId = Anchor_Private,
                    Title = "Profile flags",
                    Body = "You're in the editor now. The next handful of steps explain each toggle at the top.",
                    NextStepId = Step_Private,
                },
                new()
                {
                    Id = Step_Private,
                    AnchorId = Anchor_Private,
                    Title = "Set Private",
                    Body = "Hides this profile from public listings and from anyone who looks you up. Useful for drafts or NSFW alts.",
                    PrevStepId = Step_AfterCreate,
                    NextStepId = Step_Active,
                },
                new()
                {
                    Id = Step_Active,
                    AnchorId = Anchor_Active,
                    Title = "Set As Current",
                    Body = "This is the one profile that other players see when they hover or open your tooltip. Only one profile per character can be active at once.",
                    PrevStepId = Step_Private,
                    NextStepId = Step_Compass,
                },
                new()
                {
                    Id = Step_Compass,
                    AnchorId = Anchor_Compass,
                    Title = "Show on Compass",
                    Body = "Adds you to the public compass while this profile is current. Other players can locate you in the world.",
                    PrevStepId = Step_Active,
                    NextStepId = Step_NsfwTrigger,
                },
                new()
                {
                    Id = Step_NsfwTrigger,
                    AnchorId = Anchor_NsfwTrigger,
                    Title = "18+ and Triggering",
                    Body = "Flag this profile as 18+ or as containing triggering content. Viewers with content filters get a warning before reading.",
                    PrevStepId = Step_Compass,
                    NextStepId = Step_Spoilers,
                },
                new()
                {
                    Id = Step_Spoilers,
                    AnchorId = Anchor_Spoilers,
                    Title = "Story spoilers",
                    Body = "If your character's backstory leans on MSQ events, tick the matching expansion(s). Viewers can avoid spoilers.",
                    PrevStepId = Step_NsfwTrigger,
                    NextStepId = Step_Save,
                },
                new()
                {
                    Id = Step_Save,
                    AnchorId = Anchor_SaveBtn,
                    Title = "Save Profile",
                    Body = "Writes every change you make to the server. Auto-save isn't implemented yet, so click this when you're done editing a chunk of fields.",
                    PrevStepId = Step_Spoilers,
                    NextStepId = Step_EditAvatar,
                },
                new()
                {
                    Id = Step_EditAvatar,
                    AnchorId = Anchor_EditAvatarBtn,
                    Title = "Edit Avatar",
                    Body = "The portrait shown on your profile and on cards. Pick a square-ish image for best framing.",
                    PrevStepId = Step_Save,
                    NextStepId = Step_TitleInput,
                },
                new()
                {
                    Id = Step_TitleInput,
                    AnchorId = Anchor_TitleInput,
                    Title = "Title and color",
                    Body = "Edit the profile title and tweak its color. The color is purely cosmetic and shows on the tooltip header.",
                    PrevStepId = Step_EditAvatar,
                    NextStepId = Step_SetBackground,
                },
                new()
                {
                    Id = Step_SetBackground,
                    AnchorId = Anchor_SetBackground,
                    Title = "Set Background",
                    Body = "Background image painted faintly behind the profile body. The X button removes it.",
                    PrevStepId = Step_TitleInput,
                    NextStepId = Step_TabsIntro,
                },

                new()
                {
                    Id = Step_TabsIntro,
                    AnchorId = Anchor_AddTabBtn,
                    Title = "Tabs",
                    Body = "A profile is built from tabs. Each tab has a layout type (Bio, Details, Story, Gallery, etc.) and holds the actual content. Let's create a tab now.",
                    PrevStepId = Step_SetBackground,
                    NextStepId = Step_AddTab,
                },
                new()
                {
                    Id = Step_AddTab,
                    AnchorId = Anchor_AddTabBtn,
                    Title = "Add a new tab",
                    Body = "Click the + button to open the New Page popup.",
                    PrevStepId = Step_TabsIntro,
                    NextStepId = Step_NewPageName,
                    AutoAdvanceWhen = () => AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileWindow.IsNewPagePopupOpen(),
                    HideNextUntilAdvance = true,
                },

                new()
                {
                    Id = Step_NewPageName,
                    AnchorId = Anchor_NewPageName,
                    Title = "Page name",
                    Body = "Give the tab a short, descriptive name. This is what other players see on the tab strip.",
                    PrevStepId = Step_AddTab,
                    NextStepId = Step_LayoutDropdown,
                    RetreatWhen = () => !AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileWindow.IsNewPagePopupOpen(),
                    RetreatToStepId = Step_AddTab,
                },
                new()
                {
                    Id = Step_LayoutDropdown,
                    AnchorId = Anchor_LayoutDropdown,
                    Title = "Layout type",
                    Body = "This dropdown picks which layout the tab uses. We're going to walk through each option one at a time so you can pick the right one. Click Next to see each layout type explained.",
                    PrevStepId = Step_NewPageName,
                    NextStepId = Step_Layout_Tree,
                    RetreatWhen = () => !AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileWindow.IsNewPagePopupOpen(),
                    RetreatToStepId = Step_AddTab,
                },
                new()
                {
                    Id = Step_Layout_Tree,
                    AnchorId = Anchor_LayoutItem,
                    Title = "Tree",
                    Body = "A 5x8 grid for relationship maps or talent trees. Place each entry on a slot, link them visually. Good for character webs of friends/enemies/family.",
                    PrevStepId = Step_LayoutDropdown,
                    NextStepId = Step_Layout_Bio,
                    OnEnter = () => AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileWindow.currentLayoutType = (int)LayoutTypes.Relationship,
                    RetreatWhen = () => !AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileWindow.IsNewPagePopupOpen(),
                    RetreatToStepId = Step_AddTab,
                },
                new()
                {
                    Id = Step_Layout_Bio,
                    AnchorId = Anchor_LayoutItem,
                    Title = "Bio",
                    Body = "Character biography with structured fields (name, race, gender, age, height, weight), alignment, personality traits, and custom fields. This is the layout most profiles start with. We'll use Bio for this walkthrough.",
                    PrevStepId = Step_Layout_Tree,
                    NextStepId = Step_Layout_Details,
                    OnEnter = () => AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileWindow.currentLayoutType = (int)LayoutTypes.Bio,
                    RetreatWhen = () => !AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileWindow.IsNewPagePopupOpen(),
                    RetreatToStepId = Step_AddTab,
                },
                new()
                {
                    Id = Step_Layout_Details,
                    AnchorId = Anchor_LayoutItem,
                    Title = "Details",
                    Body = "Freeform list of name->description rows. Good for a 'quick facts' page that doesn't fit Bio's structured fields - hobbies, favorites, schedules, anything you want labeled.",
                    PrevStepId = Step_Layout_Bio,
                    NextStepId = Step_Layout_Story,
                    OnEnter = () => AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileWindow.currentLayoutType = (int)LayoutTypes.Details,
                    RetreatWhen = () => !AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileWindow.IsNewPagePopupOpen(),
                    RetreatToStepId = Step_AddTab,
                },
                new()
                {
                    Id = Step_Layout_Story,
                    AnchorId = Anchor_LayoutItem,
                    Title = "Story",
                    Body = "Long-form storytelling broken into chapters. Each chapter has its own title and rich-text body. Best for full backstories or ongoing serials.",
                    PrevStepId = Step_Layout_Details,
                    NextStepId = Step_Layout_Info,
                    OnEnter = () => AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileWindow.currentLayoutType = (int)LayoutTypes.Story,
                    RetreatWhen = () => !AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileWindow.IsNewPagePopupOpen(),
                    RetreatToStepId = Step_AddTab,
                },
                new()
                {
                    Id = Step_Layout_Info,
                    AnchorId = Anchor_LayoutItem,
                    Title = "Info",
                    Body = "A single freeform text field. Great for OOC notes, RP hooks, contact info - anything that's just one block of text.",
                    PrevStepId = Step_Layout_Story,
                    NextStepId = Step_Layout_Gallery,
                    OnEnter = () => AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileWindow.currentLayoutType = (int)LayoutTypes.Info,
                    RetreatWhen = () => !AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileWindow.IsNewPagePopupOpen(),
                    RetreatToStepId = Step_AddTab,
                },
                new()
                {
                    Id = Step_Layout_Gallery,
                    AnchorId = Anchor_LayoutItem,
                    Title = "Gallery",
                    Body = "A grid of images you upload to the server. Click a thumbnail to preview at full size. Use for character art, screenshots, mood boards.",
                    PrevStepId = Step_Layout_Info,
                    NextStepId = Step_PressSubmit,
                    OnEnter = () =>
                    {
                        AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileWindow.currentLayoutType = (int)LayoutTypes.Gallery;
                    },
                    RetreatWhen = () => !AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileWindow.IsNewPagePopupOpen(),
                    RetreatToStepId = Step_AddTab,
                },
                new()
                {
                    Id = Step_PressSubmit,
                    AnchorId = Anchor_SubmitTab,
                    Title = "Create the tab",
                    Body = "Now that you know what each layout does, we'll create a Bio tab so we can walk through Bio's fields together. Make sure the layout is set to Bio, name the tab, and press Submit.",
                    PrevStepId = Step_Layout_Gallery,
                    NextStepId = Step_BioIntro,
                    OnEnter = () => AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileWindow.currentLayoutType = (int)LayoutTypes.Bio,
                    AutoAdvanceWhen = () =>
                        !AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileWindow.IsNewPagePopupOpen()
                        && AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileWindow.CurrentProfile != null
                        && AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileWindow.CurrentProfile.customTabs != null
                        && AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileWindow.CurrentProfile.customTabs.Count > 0,
                    HideNextUntilAdvance = true,
                },

                new()
                {
                    Id = Step_BioIntro,
                    AnchorId = Anchor_BioSetAsTooltip,
                    Title = "Bio tab",
                    Body = "You're inside a Bio tab now. It's broken into collapsible sections: Basic Info, Custom Info, Details, Custom Details, Traits, and Custom Traits. We'll walk each one, starting with the most important toggle at the very top.",
                    NextStepId = Step_BioSetAsTooltip,
                },
                new()
                {
                    Id = Step_BioSetAsTooltip,
                    AnchorId = Anchor_BioSetAsTooltip,
                    Title = "Set as tooltip",
                    Body = "This is the key toggle on this tab. When you check it, this bio becomes the data other players see when they hover their cursor over your character in-game. Only one bio tab across your whole profile can be the tooltip at a time - checking it here automatically un-checks it on every other bio tab. If you want a quick-info view of your character that shows on hover, this is where you set it.",
                    PrevStepId = Step_BioIntro,
                    NextStepId = Step_BioBasicInfo,
                },
                new()
                {
                    Id = Step_BioBasicInfo,
                    AnchorId = Anchor_BioBasicInfo,
                    Title = "Basic Info",
                    Body = "Click the Basic Info header to expand it. Inside are the structured fields every bio supports: Name, Race, Gender, Age, Height, Weight. Fill in what you want; blank fields are simply hidden in the public view.",
                    PrevStepId = Step_BioSetAsTooltip,
                    NextStepId = Step_BioName,
                },
                new()
                {
                    Id = Step_BioName,
                    AnchorId = Anchor_BioName,
                    Title = "Name",
                    Body = "The IC name (or nickname) of the character. This is separate from your real character name and can be anything you'd like other players to call them.",
                    PrevStepId = Step_BioBasicInfo,
                    NextStepId = Step_BioRace,
                },
                new()
                {
                    Id = Step_BioRace,
                    AnchorId = Anchor_BioRace,
                    Title = "Race",
                    Body = "Their in-character race. You can use any race - canon FFXIV race names work, but so does whatever fits your story.",
                    PrevStepId = Step_BioName,
                    NextStepId = Step_BioGender,
                },
                new()
                {
                    Id = Step_BioGender,
                    AnchorId = Anchor_BioGender,
                    Title = "Gender",
                    Body = "IC gender (or however the character identifies). Freeform text - whatever fits.",
                    PrevStepId = Step_BioRace,
                    NextStepId = Step_BioAge,
                },
                new()
                {
                    Id = Step_BioAge,
                    AnchorId = Anchor_BioAge,
                    Title = "Age",
                    Body = "The character's IC age. If you plan to set the profile as 18+ this must be filled with a number that's clearly 18 or older.",
                    PrevStepId = Step_BioGender,
                    NextStepId = Step_BioHeight,
                },
                new()
                {
                    Id = Step_BioHeight,
                    AnchorId = Anchor_BioHeight,
                    Title = "Height & Weight",
                    Body = "Two more freeform fields right below. Optional - leave blank if it isn't relevant to your character concept.",
                    PrevStepId = Step_BioAge,
                    NextStepId = Step_BioCustomInfo,
                },
                new()
                {
                    Id = Step_BioCustomInfo,
                    AnchorId = Anchor_BioCustomInfo,
                    Title = "Custom Info",
                    Body = "Add your own name->value rows here. Hit Add Field, type a label on the left (Occupation, Hometown, Pronouns...), and the value on the right. They render alongside Basic Info.",
                    PrevStepId = Step_BioHeight,
                    NextStepId = Step_BioDetails,
                },
                new()
                {
                    Id = Step_BioDetails,
                    AnchorId = Anchor_BioDetails,
                    Title = "Details (At First Glance)",
                    Body = "A multi-line text field used for the first impression someone gets - body language, scent, the read your character gives off. Rich text formatting works here.",
                    PrevStepId = Step_BioCustomInfo,
                    NextStepId = Step_BioCustomDetails,
                },
                new()
                {
                    Id = Step_BioCustomDetails,
                    AnchorId = Anchor_BioCustomDetails,
                    Title = "Custom Details",
                    Body = "Long-form custom fields. Each entry has a label and a multi-line description. Use for things that need more than one line - 'Distinguishing features', 'Equipment', 'Magical Abilities'.",
                    PrevStepId = Step_BioDetails,
                    NextStepId = Step_BioTraits,
                },
                new()
                {
                    Id = Step_BioTraits,
                    AnchorId = Anchor_BioTraits,
                    Title = "Traits",
                    Body = "Pick an alignment plus up to three personality descriptors from the curated lists. These are the snappy at-a-glance traits other players use to size up your character.",
                    PrevStepId = Step_BioCustomDetails,
                    NextStepId = Step_BioCustomTraits,
                },
                new()
                {
                    Id = Step_BioCustomTraits,
                    AnchorId = Anchor_BioCustomTraits,
                    Title = "Custom Traits",
                    Body = "If the curated trait list doesn't capture something you care about, add your own here. Each has a name and a short description.",
                    PrevStepId = Step_BioTraits,
                    NextStepId = Step_BioSaveReminder,
                },
                new()
                {
                    Id = Step_BioSaveReminder,
                    AnchorId = Anchor_SaveBtn,
                    Title = "Don't forget to save",
                    Body = "Scroll back up and click Save Profile to write all of this to the server. That's it - your bio is published.",
                    PrevStepId = Step_BioCustomTraits,
                    NextStepId = Step_Finish,
                },
                new()
                {
                    Id = Step_Finish,
                    AnchorId = Anchor_Window,
                    Title = "All done",
                    Body = "That's the whole flow. The tutorial won't open again automatically. Use the Start button next to the Tutorials checkbox to re-run it any time.",
                    PrevStepId = Step_BioSaveReminder,
                    OnFinish = () =>
                    {
                        try
                        {
                            var cfg = Plugin.plugin.Configuration;
                            cfg.ProfileTutorialCompleted = true;
                            cfg.Save();
                        }
                        catch { }
                    },
                },
            };

            TutorialManager.RegisterFlow(Flow, steps, Step_Welcome);
        }
    }
}
