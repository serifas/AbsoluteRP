using System;
using System.Collections.Generic;

namespace AbsoluteRP.Defines
{
    internal enum FFXIVRegion
    {
        NorthAmerica,
        Europe,
        Japan,
        Oceania
    }

    internal enum FFXIVDataCenter
    {
        // North America
        Aether,
        Primal,
        Crystal,
        Dynamis,
        // Europe
        Chaos,
        Light,
        // Japan
        Elemental,
        Gaia,
        Mana,
        Meteor,
        // Oceania
        Materia
    }

    internal enum FFXIVWorld
    {
        // Aether
        Aether_Adamantoise,
        Aether_Cactuar,
        Aether_Faerie,
        Aether_Gilgamesh,
        Aether_Jenova,
        Aether_Midgardsormr,
        Aether_Sargatanas,
        Aether_Siren,
        // Primal
        Primal_Behemoth,
        Primal_Excalibur,
        Primal_Exodus,
        Primal_Famfrit,
        Primal_Hyperion,
        Primal_Lamia,
        Primal_Leviathan,
        Primal_Ultros,
        // Crystal
        Crystal_Balmung,
        Crystal_Brynhildr,
        Crystal_Coeurl,
        Crystal_Diabolos,
        Crystal_Goblin,
        Crystal_Malboro,
        Crystal_Mateus,
        Crystal_Zalera,
        // Dynamis
        Dynamis_Cuchulainn, //new
        Dynamis_Golem, //new
        Dynamis_Halicarnassus,
        Dynamis_Kraken, //new
        Dynamis_Maduin,
        Dynamis_Marilith,
        Dynamis_Rafflesia, //new
        Dynamis_Seraph,
        // Chaos
        Chaos_Cerberus,
        Chaos_Louisoix,
        Chaos_Moogle,
        Chaos_Omega,
        Chaos_Phantom, //new
        Chaos_Ragnarok,
        Chaos_Sagittarius,
        Chaos_Spriggan,
        // Light
        Light_Alpha, //new
        Light_Lich,
        Light_Odin,
        Light_Phoenix,
        Light_Raiden, // new
        Light_Shiva,
        Light_Twintania,
        Light_Zodiark,
        // Elemental
        Elemental_Aegis,
        Elemental_Atomos,
        Elemental_Carbuncle,
        Elemental_Garuda,
        Elemental_Gungnir,
        Elemental_Kujata,
        Elemental_Tonberry,
        Elemental_Typhon,
        // Gaia
        Gaia_Alexander,
        Gaia_Bahamut,
        Gaia_Durandal,
        Gaia_Fenrir,
        Gaia_Ifrit,
        Gaia_Ridill,
        Gaia_Tiamat,
        Gaia_Ultima,
        // Mana
        Mana_Anima,
        Mana_Asura,
        Mana_Chocobo,
        Mana_Hades,
        Mana_Ixion,
        Mana_Masamune,
        Mana_Pandemonium,
        Mana_Titan,
        // Meteor
        Meteor_Belias,
        Meteor_Mandragora,
        Meteor_Ramuh,
        Meteor_Shinryu,
        Meteor_Unicorn,
        Meteor_Valefor,
        Meteor_Yojimbo,
        Meteor_Zeromus, //new
        // Materia
        Materia_Bismarck,
        Materia_Ravana,
        Materia_Sephirot,
        Materia_Sophia,
        Materia_Zurvan
    }
    internal class GameData
    {
        // Returns all FFXIV regions as a list of enum values
        public static List<FFXIVRegion> GetAllRegions()
        {
            return new List<FFXIVRegion>((FFXIVRegion[])Enum.GetValues(typeof(FFXIVRegion)));
        }

        // Returns the display name for a given region enum value
        public static string GetRegionName(FFXIVRegion region)
        {
            return region switch
            {
                FFXIVRegion.NorthAmerica => "North America",
                FFXIVRegion.Europe => "Europe",
                FFXIVRegion.Japan => "Japan",
                FFXIVRegion.Oceania => "Oceania",
                _ => "Unknown"
            };
        }

        // Returns all FFXIV data centers as a list of enum values
        public static List<FFXIVDataCenter> GetAllDataCenters()
        {
            return new List<FFXIVDataCenter>((FFXIVDataCenter[])Enum.GetValues(typeof(FFXIVDataCenter)));
        }

        // Returns all data centers for a given region
        public static List<FFXIVDataCenter> GetDataCentersByRegion(FFXIVRegion region)
        {
            return region switch
            {
                FFXIVRegion.NorthAmerica => new List<FFXIVDataCenter> { FFXIVDataCenter.Aether, FFXIVDataCenter.Primal, FFXIVDataCenter.Crystal, FFXIVDataCenter.Dynamis },
                FFXIVRegion.Europe => new List<FFXIVDataCenter> { FFXIVDataCenter.Chaos, FFXIVDataCenter.Light },
                FFXIVRegion.Japan => new List<FFXIVDataCenter> { FFXIVDataCenter.Elemental, FFXIVDataCenter.Gaia, FFXIVDataCenter.Mana, FFXIVDataCenter.Meteor },
                FFXIVRegion.Oceania => new List<FFXIVDataCenter> { FFXIVDataCenter.Materia },
                _ => new List<FFXIVDataCenter>()
            };
        }

        // Returns the display name for a given data center enum value
        public static string GetDataCenterName(FFXIVDataCenter dataCenter)
        {
            return dataCenter switch
            {
                FFXIVDataCenter.Aether => "Aether",
                FFXIVDataCenter.Primal => "Primal",
                FFXIVDataCenter.Crystal => "Crystal",
                FFXIVDataCenter.Dynamis => "Dynamis",
                FFXIVDataCenter.Chaos => "Chaos",
                FFXIVDataCenter.Light => "Light",
                FFXIVDataCenter.Elemental => "Elemental",
                FFXIVDataCenter.Gaia => "Gaia",
                FFXIVDataCenter.Mana => "Mana",
                FFXIVDataCenter.Meteor => "Meteor",
                FFXIVDataCenter.Materia => "Materia",
                _ => "Unknown"
            };
        }

        // Returns all world servers for a given data center
        public static List<FFXIVWorld> GetWorldsByDataCenter(FFXIVDataCenter dataCenter)
        {
            return dataCenter switch
            {
                FFXIVDataCenter.Aether => new List<FFXIVWorld>
            {
            FFXIVWorld.Aether_Adamantoise, FFXIVWorld.Aether_Cactuar, FFXIVWorld.Aether_Faerie, FFXIVWorld.Aether_Gilgamesh,
            FFXIVWorld.Aether_Jenova, FFXIVWorld.Aether_Midgardsormr, FFXIVWorld.Aether_Sargatanas, FFXIVWorld.Aether_Siren
            },
                FFXIVDataCenter.Primal => new List<FFXIVWorld>
            {
            FFXIVWorld.Primal_Behemoth, FFXIVWorld.Primal_Excalibur, FFXIVWorld.Primal_Exodus, FFXIVWorld.Primal_Famfrit,
            FFXIVWorld.Primal_Hyperion, FFXIVWorld.Primal_Lamia, FFXIVWorld.Primal_Leviathan, FFXIVWorld.Primal_Ultros
            },
                FFXIVDataCenter.Crystal => new List<FFXIVWorld>
            {
            FFXIVWorld.Crystal_Balmung, FFXIVWorld.Crystal_Brynhildr, FFXIVWorld.Crystal_Coeurl, FFXIVWorld.Crystal_Diabolos,
            FFXIVWorld.Crystal_Goblin, FFXIVWorld.Crystal_Malboro, FFXIVWorld.Crystal_Mateus, FFXIVWorld.Crystal_Zalera
            },
                FFXIVDataCenter.Dynamis => new List<FFXIVWorld>
            {
            FFXIVWorld.Dynamis_Halicarnassus, FFXIVWorld.Dynamis_Maduin, FFXIVWorld.Dynamis_Marilith, FFXIVWorld.Dynamis_Seraph,
            FFXIVWorld.Dynamis_Cuchulainn, FFXIVWorld.Dynamis_Golem, FFXIVWorld.Dynamis_Kraken, FFXIVWorld.Dynamis_Rafflesia
            },
                FFXIVDataCenter.Chaos => new List<FFXIVWorld>
            {
            FFXIVWorld.Chaos_Cerberus, FFXIVWorld.Chaos_Louisoix, FFXIVWorld.Chaos_Moogle, FFXIVWorld.Chaos_Omega,
            FFXIVWorld.Chaos_Ragnarok, FFXIVWorld.Chaos_Sagittarius, FFXIVWorld.Chaos_Spriggan, FFXIVWorld.Chaos_Phantom
            },
                FFXIVDataCenter.Light => new List<FFXIVWorld>
            {
            FFXIVWorld.Light_Lich, FFXIVWorld.Light_Odin, FFXIVWorld.Light_Phoenix, FFXIVWorld.Light_Shiva,
            FFXIVWorld.Light_Twintania, FFXIVWorld.Light_Zodiark, FFXIVWorld.Light_Alpha, FFXIVWorld.Light_Raiden
            },
                FFXIVDataCenter.Elemental => new List<FFXIVWorld>
            {
            FFXIVWorld.Elemental_Aegis, FFXIVWorld.Elemental_Atomos, FFXIVWorld.Elemental_Carbuncle, FFXIVWorld.Elemental_Garuda,
            FFXIVWorld.Elemental_Gungnir, FFXIVWorld.Elemental_Kujata, FFXIVWorld.Elemental_Tonberry, FFXIVWorld.Elemental_Typhon
            },
                FFXIVDataCenter.Gaia => new List<FFXIVWorld>
            {
            FFXIVWorld.Gaia_Alexander, FFXIVWorld.Gaia_Bahamut, FFXIVWorld.Gaia_Durandal, FFXIVWorld.Gaia_Fenrir,
            FFXIVWorld.Gaia_Ifrit, FFXIVWorld.Gaia_Ridill, FFXIVWorld.Gaia_Tiamat, FFXIVWorld.Gaia_Ultima
            },
                FFXIVDataCenter.Mana => new List<FFXIVWorld>
            {
            FFXIVWorld.Mana_Anima, FFXIVWorld.Mana_Asura, FFXIVWorld.Mana_Chocobo, FFXIVWorld.Mana_Hades,
            FFXIVWorld.Mana_Ixion, FFXIVWorld.Mana_Masamune, FFXIVWorld.Mana_Pandemonium, FFXIVWorld.Mana_Titan
            },
                FFXIVDataCenter.Meteor => new List<FFXIVWorld>
            {
            FFXIVWorld.Meteor_Belias, FFXIVWorld.Meteor_Mandragora, FFXIVWorld.Meteor_Ramuh, FFXIVWorld.Meteor_Shinryu,
            FFXIVWorld.Meteor_Unicorn, FFXIVWorld.Meteor_Valefor, FFXIVWorld.Meteor_Yojimbo, FFXIVWorld.Meteor_Zeromus
            },
                FFXIVDataCenter.Materia => new List<FFXIVWorld>
            {
            FFXIVWorld.Materia_Bismarck, FFXIVWorld.Materia_Ravana, FFXIVWorld.Materia_Sephirot, FFXIVWorld.Materia_Sophia, FFXIVWorld.Materia_Zurvan
            },
                _ => new List<FFXIVWorld>()
            };
        }
        public static List<string> GetWorldNamesByDataCenter(FFXIVDataCenter dataCenter)
        {
            var worlds = GetWorldsByDataCenter(dataCenter);
            var worldNames = new List<string>();

            foreach (var world in worlds)
            {
                // Remove the DataCenter prefix for display, e.g., "Aether_Adamantoise" -> "Adamantoise"
                var name = world.ToString();
                var underscoreIndex = name.IndexOf('_');
                if (underscoreIndex >= 0 && underscoreIndex < name.Length - 1)
                    name = name.Substring(underscoreIndex + 1);

                worldNames.Add(name);
            }

            return worldNames;
        }
    }
}
