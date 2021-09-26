using UnityEngine;
using Overload;
using HarmonyLib;
using System.Reflection;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace GameMod
{
    // TODO must handle drops from runaway bots (best results require transpiling ExplodeNow)

    [HarmonyPatch(typeof(Robot), "MaybeDropItem")]
    class Robot_MaybeDropItem
    {
        static bool Prefix(ref bool __result, ref bool __state, ItemPrefab item_prefab, float default_chance)
        {
            __state = false;
            if (GameplayManager.IsChallengeMode)
            {
                if (!FixedRngConfig.AllowRobotItemDrops)
                {
                    __result = false;
                    return false;
                }

                __state = true;
                FixedRng.UseState();
            }
            return true;
        }

        static void Postfix(ref bool __result, ref bool __state)
        {
            if (__state)
            {
                FixedRng.RestoreState();
            }
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
        {
            int state = 0;
            var buffer = new List<CodeInstruction>();
            foreach(var i in code)
            {
                switch(state)
                {
                    default:
                        if (i.IsLoadStaticField(typeof(ChallengeManager), "ChallengeRobotsDestroyed"))
                        {
                            state = 1;
                            buffer.Add(i);
                        }
                        else if (i.opcode == OpCodes.Ldc_R4 && (float)i.operand == 500.0f)
                        {
                            state = 2;
                            buffer.Add(i);
                        }
                        else
                        {
                            yield return i;
                        }
                        break;
                    case 1: // found conditional on ChallengeRobotsDestroyed
                        if(i.opcode == OpCodes.Ble)
                        {
                            foreach(var j in buffer)
                            {
                                if(j.opcode == OpCodes.Ldc_I4 && (int)j.operand == 300)
                                {
                                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Property(typeof(FixedRngConfig), "ArmorDropoffStart").GetGetMethod());
                                }
                                else if (j.opcode == OpCodes.Ldc_I4 && (int)j.operand == 500)
                                {
                                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Property(typeof(FixedRngConfig), "ArmorDropoffEnd").GetGetMethod());
                                }
                                else
                                {
                                    yield return j;
                                }
                                
                            }
                            buffer.Clear();
                            yield return i;
                            state = 0;
                        }
                        else
                        {
                            buffer.Add(i);
                        }
                        break;
                    case 2: // lerp on drop chance using ChallengeRobotsDestroyed
                        if(i.opcode == OpCodes.Call && ((MethodInfo)i.operand).DeclaringType == typeof(Mathf) && ((MethodInfo)i.operand).Name == "Clamp01")
                        {
                            foreach(var j in buffer)
                            {
                                if (j.opcode == OpCodes.Ldc_I4 && (int)j.operand == 300)
                                {
                                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Property(typeof(FixedRngConfig), "ArmorDropoffStart").GetGetMethod());
                                }
                                else if (j.opcode == OpCodes.Ldc_R4 && (float)j.operand == 500.0f)
                                {
                                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Property(typeof(FixedRngConfig), "ArmorDropoffEnd").GetGetMethod());
                                    yield return new CodeInstruction(OpCodes.Conv_R4);
                                }
                                else if (j.opcode == OpCodes.Ldc_R4 && (float)j.operand == 200.0f)
                                {
                                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Property(typeof(FixedRngConfig), "ArmorDropoffEnd").GetGetMethod());
                                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Property(typeof(FixedRngConfig), "ArmorDropoffStart").GetGetMethod());
                                    yield return new CodeInstruction(OpCodes.Sub);
                                    yield return new CodeInstruction(OpCodes.Conv_R4);
                                }
                                else
                                {
                                    yield return j;
                                }
                            }
                            buffer.Clear();
                            yield return i;
                            state = 0;
                        }
                        else
                        {
                            buffer.Add(i);
                        }
                        break;
                }
            }
            //Debug.Log(string.Format("Patched MaybeDropItem state={0} buffer.Count={1}", state, buffer.Count));
            foreach (var j in buffer)
            {
                yield return j;
            }
        }
    }

    [HarmonyPatch(typeof(Robot), "MaybeDropComboBonusItem")]
    class Robot_MaybeDropComboBonusItem
    {
        static bool Prefix(ref bool __result, ref bool __state)
        {
            __state = false;
            if (GameplayManager.IsChallengeMode)
            {
                if (!FixedRngConfig.AllowRobotItemDrops)
                {
                    __result = false;
                    return false;
                }
                
                __state = true;
                FixedRng.UseState();
            }
            return true;
        }

        static void Postfix(ref bool __result, ref bool __state)
        {
            if (__state)
            {
                FixedRng.RestoreState();
            }
        }
    }

    [HarmonyPatch(typeof(Robot), "DropRunawayPowerups")]
    class Robot_DropRunawayPowerups
    {
        static bool Prefix(ref bool __state)
        {
            __state = false;
            if (GameplayManager.IsChallengeMode)
            {
                if (!FixedRngConfig.AllowRobotItemDrops)
                {
                    return false;
                }
                
                __state = true;
                FixedRng.UseState();
            }
            return true;
        }

        static void Postfix(ref bool __state)
        {
            if (__state)
            {
                FixedRng.RestoreState();
            }
        }
    }
}