using UnityEngine;
using Overload;
using HarmonyLib;
using System.Reflection;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace GameMod
{
    [HarmonyPatch(typeof(ChallengeManager), "InitChallenge")]
    class ChallengeManager_InitChallenge_Loadouts
    {
        [HarmonyPriority(Priority.Normal)]
        static void Prefix()
        {
            if(FixedRngConfig.Loadout == LoadoutConfig.Default)
            {
                return;
            }
            
            PrepareLoadout(FixedRngConfig.Loadout);
        }

        protected static void PrepareLoadout(LoadoutConfig lc)
        {
            WeaponType firstUnlockedWeapon = WeaponType.NUM;
            MissileType firstUnlockedMissile = MissileType.NUM;

            IDictionary<WeaponType, LoadoutConfig.Weapon> weapons;
            if (lc.HasWeapons)
            {
                weapons = lc.Weapons;
            }
            else
            {
                // fake "no weapons" by giving a driller with 0 ammo spawns
                weapons = new Dictionary<WeaponType, LoadoutConfig.Weapon>
                {
                    { WeaponType.DRILLER, new LoadoutConfig.Weapon{ Type=WeaponType.DRILLER, Level=WeaponUnlock.LEVEL_2A} }
                };
            }

            for (int i = 0; i < 8; i++)
            {
                var wep = (WeaponType)i;
                var mis = (MissileType)i;

                ChallengeManager.AvailableWeapons[i] = weapons.ContainsKey(wep) && weapons[wep].Level != WeaponUnlock.LOCKED;
                ChallengeManager.LockedWeapons[i] = !weapons.ContainsKey(wep);
                if(firstUnlockedWeapon == WeaponType.NUM && ChallengeManager.AvailableWeapons[i])
                {
                    firstUnlockedWeapon = wep;
                }

                ChallengeManager.AvailableMissiles[i] = lc.Missiles.ContainsKey(mis) && lc.Missiles[mis].Level != WeaponUnlock.LOCKED;
                ChallengeManager.LockedMissiles[i] = !lc.Missiles.ContainsKey(mis);
                if (firstUnlockedMissile == MissileType.NUM && ChallengeManager.AvailableMissiles[i])
                {
                    firstUnlockedMissile = mis;
                }
            }

            for (int i = 0; i < ChallengeManager.m_starting_weapons.Length; i++)  { ChallengeManager.m_starting_weapons[i] = (int)WeaponType.NUM; }
            for (int i = 0; i < ChallengeManager.m_starting_missiles.Length; i++) { ChallengeManager.m_starting_missiles[i] = (int)MissileType.NUM; }
            ChallengeManager.m_starting_weapons[0] = (int)firstUnlockedWeapon;
            ChallengeManager.m_starting_missiles[0] = (int)firstUnlockedMissile;
        }
    }


    [HarmonyPatch(typeof(Item), "PrefabLegalToSpew")]
    class Item_PrefabLegalToSpew_Loadouts
    {
        static bool Prefix(bool __result, GameObject prefab)
        {
            // TODO also don't allow alien orbs?
            if(GameplayManager.IsChallengeMode && !FixedRngConfig.Loadout.HasWeapons && prefab.name == "entity_item_ammo")
            {
                // if no weapons allowed, don't drop ammo
                __result = false;
                return false;
            }
            return true;
        }
    }


    [HarmonyPatch(typeof(ChallengeManager), "AddDefaultMisileAmmo")]
    class ChallengeManager_AddDefaultMisileAmmo_Loadouts
    {
        static bool Prefix(Player player, MissileType mt)
        {
            if(mt == MissileType.NUM)
            {
                return false;
            }
            if (FixedRngConfig.Loadout == LoadoutConfig.Default)
            {
                return true;
            }

            return !(FixedRngConfig.Loadout.Missiles.ContainsKey(mt) && FixedRngConfig.Loadout.Missiles[mt].Ammo >= 0);
        }
    }

    [HarmonyPatch(typeof(ChallengeManager), "ActuallyGiveWeaponsAndMissiles")]
    class ChallengeManager_ActuallyGiveWeaponsAndMissiles_Loadouts
    {
        static void Prefix(Player player)
        {
            if (FixedRngConfig.Loadout == LoadoutConfig.Default)
            {
                return;
            }
        }

        static void Postfix(Player player)
        {
            if (FixedRngConfig.Loadout == LoadoutConfig.Default)
            {
                return;
            }

            Debug.Log(string.Format("server active? {0} on {1}", Server.IsActive(), Server.GetListenPort()));
            var lc = FixedRngConfig.Loadout;
            if(!lc.HasWeapons)
            {
                player.m_ammo = 0;
                player.m_weapon_level[(int)WeaponType.DRILLER] = WeaponUnlock.LEVEL_2A;
            }
            else if (lc.WeaponAmmo >= 0)
            {
                player.m_ammo = lc.WeaponAmmo;
            }

            // TODO weapon upgrades are getting overridden by an RPC call to unlock weapons
#if false
            foreach (var w in lc.Weapons.Values)
            {
                if(w.Level == WeaponUnlock.LOCKED)
                {
                    continue;
                }
                player.m_weapon_level[(int)w.Type] = w.Level;
            }
#else
            ChallengeManager_Update_Loadouts.flag = false;
#endif
            foreach (var m in lc.Missiles.Values)
            {
                if (m.Level == WeaponUnlock.LOCKED)
                {
                    continue;
                }
                player.m_missile_level[(int)m.Type] = m.Level;
                if (Server.IsActive())
                {
                    player.CallRpcSetMissileLevel((int)m.Type, m.Level);
                }
                if (m.Ammo >= 0)
                {
                    player.AddMissileAmmo(m.Ammo, m.Type, true, false);
                }
            }
        }
    }

    // HACK FIX:
    // Setting weapon upgrade levels doesn't work during challenge mode inititalization.
    // It's probably because messages to unlock weapons are sent during that time,
    // which would later force those weapons to be LEVEL_1.
    // So, change weapon upgrades on the first frame of gameplay.
    [HarmonyPatch(typeof(ChallengeManager), "Update")]
    class ChallengeManager_Update_Loadouts
    {
        public static bool flag = true;

        static void Prefix()
        {
            if (!flag)
            {
                FixWeaponUpgrades();
                flag = true;
            }
        }

        static void FixWeaponUpgrades()
        {
            var lc = FixedRngConfig.Loadout;
            if(lc == LoadoutConfig.Default)
            {
                return;
            }

            if (!lc.HasWeapons)
            {
                GameManager.m_local_player.m_weapon_level[(int)WeaponType.DRILLER] = WeaponUnlock.LEVEL_2A;
            }
            else
            {
                foreach (var w in lc.Weapons.Values)
                {
                    if (w.Level == WeaponUnlock.LOCKED)
                    {
                        continue;
                    }
                    GameManager.m_local_player.m_weapon_level[(int)w.Type] = w.Level;
                }
            }
            GameManager.m_local_player.UpdateCurrentWeaponName();
        }
    }
}