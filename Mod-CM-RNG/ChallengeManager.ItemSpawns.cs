using UnityEngine;
using Overload;
using HarmonyLib;
using System.Reflection;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace GameMod
{
    [HarmonyPatch(typeof(ChallengeManager), "RandomGreatPowerupNoMissile")]
    class ChallengeManager_RandomGreatPowerupNoMissile
    {
        static bool Prefix(ItemPrefab __result)
        {
            if (!FixedRngConfig.AllowGreatItemSpawns)
            {
                __result = ItemPrefab.none;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(ChallengeManager), "SpawnRandomGreatPowerup")]
    class ChallengeManager_SpawnRandomGreatPowerup
    {
        static bool Prefix(Player player)
        {
            return FixedRngConfig.AllowGreatItemSpawns;
        }
    }

    [HarmonyPatch(typeof(ChallengeManager), "SpawnRandomNormalItem")]
    class ChallengeManager_SpawnRandomNormalItem
    {
        static bool Prefix(Player player)
        {
            return FixedRngConfig.AllowNormalItemSpawns;
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
        {
            var state = 0;
            foreach(var i in code)
            {
                switch(state)
                {
                    default:
                        if(i.LoadsField(AccessTools.Field(typeof(ChallengeManager), "ChallengeRobotsDestroyed")))
                        {
                            state = 1;
                        }
                        yield return i;
                        break;
                    case 1:
                        if (i.opcode == OpCodes.Ldc_I4 && (int)i.operand == 300)
                        {
                            yield return new CodeInstruction(OpCodes.Call, AccessTools.Property(typeof(FixedRngConfig), "ArmorDropoffStart").GetGetMethod());
                            state = 2;
                        }
                        else
                        {
                            yield return i;
                        }
                        break;
                    case 2:
                        yield return i;
                        break;
                }
            }
            Debug.Log(string.Format("Patched SpawnRandomNormalItem state={0}", state));
        }
    }

    [HarmonyPatch(typeof(ChallengeManager), "SpawnRandomWeaponOrItem")]
    class ChallengeManager_SpawnRandomWeaponOrItem
    {
        static bool Prefix(Player player)
        {
            return FixedRngConfig.AllowNormalItemSpawns;
        }
    }

    [HarmonyPatch(typeof(ChallengeManager), "SpawnItem")]
    class ChallengeManager_SpawnItem
    {
        static bool Prefix(ItemPrefab item_prefab, bool start, bool make_super)
        {
            switch(item_prefab)
            {
                    // great powerups
                case ItemPrefab.entity_item_rapid:
                case ItemPrefab.entity_item_cloak:
                case ItemPrefab.entity_item_invuln:
                    if(!FixedRngConfig.AllowGreatItemSpawns)
                    {
                        Debug.Log("Attempted to spawn great item even though those are disabled");
                        return false;
                    }
                    break;
                    // basic items
                case ItemPrefab.entity_item_alien_orb:
                case ItemPrefab.entity_item_ammo:
                case ItemPrefab.entity_item_shields:
                    if (!FixedRngConfig.AllowNormalItemSpawns)
                    {
                        Debug.Log("Attempted to spawn normal item even though those are disabled");
                        return false;
                    }
                    break;
                    // weapons
                case ItemPrefab.entity_item_thunderbolt:
                case ItemPrefab.entity_item_crusher:
                case ItemPrefab.entity_item_cyclone:
                case ItemPrefab.entity_item_reflex:
                case ItemPrefab.entity_item_driller:
                case ItemPrefab.entity_item_energy:
                case ItemPrefab.entity_item_lancer:
                case ItemPrefab.entity_item_flak:
                case ItemPrefab.entity_item_impulse:
                    if (!FixedRngConfig.AllowNormalItemSpawns)
                    {
                        Debug.Log("Attempted to spawn normal item even though those are disabled");
                        return false;
                    }
                    break;
                    // missiles (super missiles are a great item)
                case ItemPrefab.entity_item_hunter4pack:
                case ItemPrefab.entity_item_missile_pod:
                case ItemPrefab.entity_item_nova:
                case ItemPrefab.entity_item_falcon4pack:
                case ItemPrefab.entity_item_devastator:
                case ItemPrefab.entity_item_creeper:
                case ItemPrefab.entity_item_timebomb:
                case ItemPrefab.entity_item_vortex:
                    if(!make_super && !FixedRngConfig.AllowNormalItemSpawns)
                    {
                        Debug.Log("Attempted to spawn normal item even though those are disabled");
                        return false;
                    }
                    if (make_super && !FixedRngConfig.AllowGreatItemSpawns)
                    {
                        Debug.Log("Attempted to spawn great item even though those are disabled");
                        return false;
                    }
                    break;
                default:
                    break;
            }
            return true;
        }
    }
}