using AbsoluteRP.Windows.Listings;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using AbsoluteRP.Windows.Social.Views;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Utility;
using Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AbsoluteRP.Windows.NavLayouts
{
    internal class NavigationLayouts
    {
        public static Navigation MainUINavigation()
        {
            Navigation navigation = new Navigation();
            navigation.names = new[]
            {
            "Profiles",
            "Social",
            "Systems - WIP",
            "Quests - WIP",
            "Events - WIP"
            };
            // Define actions for each button
            navigation.actions = new Action[]
            { 
                () => { /* Profiles logic */ 
                    if (Plugin.IsOnline())
                    {
                        Plugin.plugin.OpenAndLoadProfileWindow(true, ProfileWindow.profileIndex);
                    }
                },
                () => { /* Social logic */ 
                    if (Plugin.IsOnline())
                    {
                        Plugin.plugin.OpenSocialWindow();
                    }
                },
                () => {
                    if (Plugin.IsOnline())
                    {
                      //  Plugin.plugin.ToggleSystemsWindow();
                    }
                },
                () => { /* Quests logic */ },
                () => {
                    if (Plugin.IsOnline())
                    {
                       // Plugin.plugin.OpenListingsWindow();
                    }
                }
                };
            navigation.textureIDs = new ImTextureID[]{
                UI.UICommonImage(UI.CommonImageTypes.listingsPersonal).Handle,
                UI.UICommonImage(UI.CommonImageTypes.listingsGroup).Handle,
                UI.UICommonImage(UI.CommonImageTypes.listingsSystem).Handle,
                UI.UICommonImage(UI.CommonImageTypes.listingsQuests).Handle,
                UI.UICommonImage(UI.CommonImageTypes.listingsEvent).Handle,
            };
            return navigation;
        }

        public static Navigation GroupsNavigation(List<Group> groups)
        {
            Navigation navigation = new Navigation();

            List<string> groupnames = new List<string>();
            List<Action> actions = new List<Action>();
            List<ImTextureID> logos = new List<ImTextureID>();
            List<int> badges = new List<int>();

            foreach (Group group in groups)
            {
                groupnames.Add(group.name);
                logos.Add(group.logo.Handle);
                badges.Add(group.GetTotalUnreadCount());
                actions.Add(() => { /* Profiles logic */
                    if (Plugin.IsOnline())
                    {
                        GroupsData.openGroupCreation = false;
                        GroupsData.LoadGroup(group);

                        GroupsData.manageGroup = false;
                    }
                });
            }

            navigation.names = groupnames.ToArray();
            // Define actions for each button
            navigation.actions = actions.ToArray();
            navigation.textureIDs = logos.ToArray();
            navigation.badges = badges.ToArray();
            return navigation;
        }



        public static Navigation ExtraNavigation()
        {
            Navigation navigation = new Navigation();

            navigation.show = new[]
            {
            Plugin.plugin.Configuration.showKofi,
            Plugin.plugin.Configuration.showPatreon,
            Plugin.plugin.Configuration.showDisc,
            Plugin.plugin.Configuration.showWeb

            };

            navigation.names = new[]
            {
            "Support me on Ko-Fi",
            "Support me on Patreon",
            "Join the Discord",
            "Absolute Roleplay Website"
            };
            // Define actions for each button
            navigation.actions = new Action[]
            {
            () => {  Util.OpenLink("https://ko-fi.com/absoluteroleplay");},
            () => {  Util.OpenLink("https://patreon.com/AbsoluteRoleplay"); },
            () => {  Util.OpenLink("https://discord.gg/NnhspF2cSQ"); },
            () => {  Util.OpenLink("https://absolute-roleplay.net"); }
            };
            navigation.textureIDs =  new ImTextureID[] {
            UI.UICommonImage(UI.CommonImageTypes.kofiBtn).Handle,
            UI.UICommonImage(UI.CommonImageTypes.patreonBtn).Handle,
            UI.UICommonImage(UI.CommonImageTypes.discordBtn).Handle,
            UI.UICommonImage(UI.CommonImageTypes.websiteBtn).Handle
            };

           
            return navigation;
        }

        public static Navigation SystemNavigation()
        {

            Navigation navigation = new Navigation();
            navigation.names = new[]
            {
            "Stats",
            "Skills",
            "Combat",
            "Rules"
            };
            // Define actions for each button
            navigation.actions = new Action[]
            {
                () => {
                    if(SystemsWindow.systemData.Count > 0) {
                        SystemsWindow.drawStatLayout = true;    
                    }
                   
                },
                () => {/* skills logic*/    },
                () => { /* combat logic */ },
                () => { /* rules logic */ }
                };
            navigation.textureIDs = new ImTextureID[]{
                UI.UICommonImage(UI.CommonImageTypes.systems_stats).Handle,
                UI.UICommonImage(UI.CommonImageTypes.systems_skills).Handle,
                UI.UICommonImage(UI.CommonImageTypes.systems_combat).Handle,
                UI.UICommonImage(UI.CommonImageTypes.systems_rules).Handle,
            };
            return navigation;
        }
        public static Navigation SocialNavigation()
        {

            Navigation navigation = new Navigation();
            navigation.names = new[]
            {
            "Connections",
            "Bookmarks",
            "Groups",
            "Search"
            };
            // Define actions for each button
            navigation.actions = new Action[]
            {
                () => {
                    SocialWindow.view = SocialWindow.connections;
                },
                () => {
                    SocialWindow.view = SocialWindow.bookmarks;
                },
                () => {
                    SocialWindow.view = SocialWindow.groups;
                },
                () => {
                    SocialWindow.view = SocialWindow.search;
                }
                };
            navigation.textureIDs = new ImTextureID[]{
                UI.UICommonImage(UI.CommonImageTypes.socialConnections).Handle,
                UI.UICommonImage(UI.CommonImageTypes.socialBookmarks).Handle,
                UI.UICommonImage(UI.CommonImageTypes.socialGroups).Handle,
                UI.UICommonImage(UI.CommonImageTypes.socialSearch).Handle,
            };
            return navigation;
        }
    }
}
