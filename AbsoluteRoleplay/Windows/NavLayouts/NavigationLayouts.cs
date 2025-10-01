using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using Dalamud.Bindings.ImGui;
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
            "Systems",
            "Quests",
            "Events"
            };
            // Define actions for each button
            navigation.actions = new Action[]
            {
                () => { /* Profiles logic */ 
                        if (Plugin.plugin.IsOnline())
                        {
                            Plugin.plugin.OpenAndLoadProfileWindow(true, ProfileWindow.profileIndex);
                        }
                },
                () => { /* Social logic */ 
                    DataSender.RequestConnections(Plugin.character);
                },
                () => { /* Systems logic */  },
                () => { /* Quests logic */ },
                () => { /* Events logic */ }
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
        public static Navigation ExtraNavigation()
        {
            Navigation navigation = new Navigation();

            navigation.show = new[]
            {
            Plugin.plugin.Configuration.showKofi,
            Plugin.plugin.Configuration.showPatreon,
            Plugin.plugin.Configuration.showDisc,
            };

            navigation.names = new[]
            {
            "Support me on Ko-Fi",
            "Support me on Patreon",
            "Join the Discord"
            };
            // Define actions for each button
            navigation.actions = new Action[]
            {
            () => {  Util.OpenLink("https://ko-fi.com/absoluteroleplay");},
            () => {  Util.OpenLink("https://patreon.com/AbsoluteRolelay"); },
            () => {  Util.OpenLink("https://discord.gg/absolute-roleplay"); }
            };
            navigation.textureIDs =  new ImTextureID[] {
            UI.UICommonImage(UI.CommonImageTypes.kofiBtn).Handle,
            UI.UICommonImage(UI.CommonImageTypes.patreonBtn).Handle,
            UI.UICommonImage(UI.CommonImageTypes.discordBtn).Handle
            };

           
            return navigation;
        }
    }
}
