using UnityEngine;
using Overload;
using Harmony;
using System.Reflection;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace GameMod
{
    public class LoadoutConfig
    {
        public class SavedFormat
        {
            public class WeaponData { public string Level { get; set; } }
            public class MissileData { public string Level { get; set; } public int? Ammo { get; set; } }

            public Dictionary<string, WeaponData> Weapons { get; set; }
            public Dictionary<string, MissileData> Missiles { get; set; }
            public int? WeaponAmmo { get; set; }
        }

        public class Weapon
        {
            public WeaponType Type { get; set; }
            public WeaponUnlock Level { get; set; }
        }
        public class Missile
        {
            public MissileType Type { get; set; }
            public WeaponUnlock Level { get; set; }
            public int Ammo { get; set; }
        }

        public IDictionary<WeaponType, Weapon> Weapons { get; set; }
        public IDictionary<MissileType, Missile> Missiles { get; set; }
        public int WeaponAmmo { get; set; }

        public bool HasWeapons { get { return this == Default || Weapons.Count > 0; } }

        public static readonly LoadoutConfig Default = new LoadoutConfig();

        public LoadoutConfig()
        {
            Weapons = new Dictionary<WeaponType, Weapon>();
            Missiles = new Dictionary<MissileType, Missile>();
            WeaponAmmo = -1;
        }
        public LoadoutConfig(SavedFormat sf)
        {
            WeaponAmmo = sf.WeaponAmmo ?? -1;
            Weapons = new Dictionary<WeaponType, Weapon>();
            Missiles = new Dictionary<MissileType, Missile>();

            foreach (var kvp in sf.Weapons)
            {
                WeaponType wtype = (WeaponType)System.Enum.Parse(typeof(WeaponType), kvp.Key.ToUpperInvariant());
                WeaponUnlock level;
                switch(kvp.Value?.Level?.ToUpperInvariant())
                {
                    case "0":
                        level = WeaponUnlock.LEVEL_0;
                        break;
                    case "2A":
                        level = WeaponUnlock.LEVEL_2A;
                        break;
                    case "2B":
                        level = WeaponUnlock.LEVEL_2B;
                        break;
                    case "LOCKED":
                        level = WeaponUnlock.LOCKED;
                        break;
                    case "1":
                    default:
                        level = WeaponUnlock.LEVEL_1;
                        break;
                }

                Weapons[wtype] = new Weapon
                {
                    Type = wtype,
                    Level = level
                };
            }

            foreach (var kvp in sf.Missiles)
            {
                MissileType mtype = (MissileType)System.Enum.Parse(typeof(MissileType), kvp.Key.ToUpperInvariant());
                WeaponUnlock level;
                switch (kvp.Value?.Level?.ToUpperInvariant())
                {
                    case "0":
                        level = WeaponUnlock.LEVEL_0;
                        break;
                    case "2A":
                        level = WeaponUnlock.LEVEL_2A;
                        break;
                    case "2B":
                        level = WeaponUnlock.LEVEL_2B;
                        break;
                    case "LOCKED":
                        level = WeaponUnlock.LOCKED;
                        break;
                    case "1":
                    default:
                        level = WeaponUnlock.LEVEL_1;
                        break;
                }

                Missiles[mtype] = new Missile
                {
                    Type = mtype,
                    Level = level,
                    Ammo = kvp.Value?.Ammo ?? -1
                };
            }
        }
    }

    public class FixedRngConfig
    {
        public class SavedFormat
        {
            public string Seed { get; set; }
            public bool? AllowGreatItemSpawns { get; set; }
            public bool? AllowNormalItemSpawns { get; set; }
            public bool? AllowRobotItemDrops { get; set; }
            
            public int? ArmorDropoffStart { get; set; }
            public int? ArmorDropoffEnd { get; set; }

            public bool? UseLoadout { get; set; }
            public LoadoutConfig.SavedFormat Loadout { get; set; }
        }

        protected static SavedFormat defaults;

        public static bool FixedSeed { get; private set; }
        public static int Seed { get; private set; }
        public static bool AllowGreatItemSpawns { get; private set; }
        public static bool AllowNormalItemSpawns { get; private set; }
        public static bool AllowRobotItemDrops { get; private set; }

        public static int ArmorDropoffStart { get; private set; }
        public static int ArmorDropoffEnd { get; private set; }

        public static LoadoutConfig Loadout { get; set; }

        const string FileName = "Mod-CM-RNG.settings.json";

        static FixedRngConfig()
        {
            defaults = new SavedFormat
            {
                Seed = null,
                AllowGreatItemSpawns = true,
                AllowNormalItemSpawns = true,
                AllowRobotItemDrops = true,

                ArmorDropoffStart = 300,
                ArmorDropoffEnd = 500,

                Loadout = null
            };
            UseValues(defaults);
        }

        public static void Load()
        {
            try
            {
                if (System.IO.File.Exists(FileName))
                {
                    var text = System.IO.File.ReadAllText(FileName);
                    var converted = Newtonsoft.Json.JsonConvert.DeserializeObject<SavedFormat>(text);
                    UseValues(converted);

                    Debug.Log(string.Format("Successfully loaded CM RNG settings"));
                }
                else
                {
                    Debug.Log(string.Format("Config file '{0}' not found, using defaults", FileName));
                    UseValues(defaults);
                }
            }
            catch (System.Exception e)
            {
                Debug.Log(string.Format("Failed to load CM RNG settings (error={0}), using defaults", e.Message));
                UseValues(defaults);
            }
        }

        protected static void UseValues(SavedFormat converted)
        {
            if (string.IsNullOrEmpty(converted.Seed))
            {
                FixedSeed = false;
                Seed = 0;
            }
            else if (int.TryParse(converted.Seed, out int s))
            {
                FixedSeed = true;
                Seed = s;
            }
            else
            {
                FixedSeed = true;
                Seed = converted.Seed.GetHashCode();
            }
            AllowGreatItemSpawns = converted?.AllowGreatItemSpawns ?? defaults.AllowGreatItemSpawns.Value;
            AllowNormalItemSpawns = converted?.AllowNormalItemSpawns ?? defaults.AllowNormalItemSpawns.Value;
            AllowRobotItemDrops = converted?.AllowRobotItemDrops ?? defaults.AllowRobotItemDrops.Value;

            ArmorDropoffStart = converted?.ArmorDropoffStart ?? defaults.ArmorDropoffStart.Value;
            ArmorDropoffEnd = converted?.ArmorDropoffEnd ?? defaults.ArmorDropoffEnd.Value;
            if (ArmorDropoffEnd <= ArmorDropoffStart)
            {
                throw new System.Exception("Armor dropoff end must be greater than armor dropoff start");
            }

            Loadout = LoadoutConfig.Default;
            if (converted.UseLoadout != false && converted.Loadout != null)
            {
                try
                {
                    Loadout = new LoadoutConfig(converted.Loadout);
                }
                catch (System.Exception e)
                {
                    Debug.Log(string.Format("Failed to read loadout config (error={0}), using default loadout", e.Message));
                    Loadout = LoadoutConfig.Default;
                }
            }
        }
    }


    public class FixedRng
    {
        private static Random.State ambientState;
        private static Random.State? state;
        private static int count = 0;
        private static int seed = 0;

        public static string Status()
        {
            return string.Format("<active={0} state={1}>", count > 0 ? "yes" : "no", count > 0 ? Random.state.GetHashCode() : state?.GetHashCode());
        }

        public static void Reset(int seed = 0)
        {
            FixedRng.seed = seed;
            count = 0;
            state = null;
        }

        public static void UseState()
        {
            if (count == 0)
            {
                ambientState = Random.state;

                if (state == null)
                {
                    //Debug.Log("UseState s1=" + seed);
                    Random.InitState(seed);
                    //Debug.Log("UseState s2=" + Random.state.GetHashCode());
                }
                else
                {
                    Random.state = state.Value;
                }
            }
            count++;
        }

        public static void RestoreState()
        {
            if (count <= 0)
            {
                return;
            }

            count--;
            if (count == 0)
            {
                state = Random.state;
                Random.state = ambientState;
                //Debug.Log("restored RNG state");
            }
        }
    }

    public static class CodeInstructionExtensions
    {
        public static bool IsLoadStaticField(this CodeInstruction i, System.Type declaringType, string field)
        {
            return i.opcode == OpCodes.Ldsfld && ((FieldInfo)i.operand).DeclaringType == declaringType && ((FieldInfo)i.operand).Name == field;
        }
    }

}