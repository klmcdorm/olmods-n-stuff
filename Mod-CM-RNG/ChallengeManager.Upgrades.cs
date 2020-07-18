using UnityEngine;
using Overload;
using Harmony;
using System.Reflection;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace GameMod
{
    [HarmonyPatch(typeof(ChallengeManager), "WorkerUpgradeRandomMissile")]
    class ChallengeManager_WorkerUpgradeRandomMissile
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
        {
            var state = 0;
            var buf1 = new List<CodeInstruction>();
            foreach (var i in code)
            {
                switch(state)
                {
                    // reverse order of maxing out missile ammo and upgrading it (to ignore capacity upgrades)
                    default:
                        if(i.opcode == OpCodes.Stsfld && ((FieldInfo)i.operand).DeclaringType == typeof(ChallengeManager) && ((FieldInfo)i.operand).Name == "m_recent_upgrade_timer")
                        {
                          state = 1;
                        }
                        yield return i;
                        break;
                    case 1:
                        if(i.opcode == OpCodes.Stelem_I4)
                        {
                          state = 2; 
                        }
                        buf1.Add(i);
                        break;
                    case 2:
                        if(i.opcode == OpCodes.Pop)
                        {
                            yield return new CodeInstruction(OpCodes.Ldarg_0);
                            yield return new CodeInstruction(OpCodes.Ldarg_1);
                            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ChallengeManager_WorkerUpgradeRandomMissile), "MaybePreRefillMissiles"));

                            foreach (var j in buf1)
                            {
                                yield return j;
                            }
                            
                            yield return new CodeInstruction(OpCodes.Ldarg_0);
                            yield return new CodeInstruction(OpCodes.Ldarg_1);
                            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ChallengeManager_WorkerUpgradeRandomMissile), "MaybePostRefillMissiles"));

                            buf1.Clear();

                          state = 3;
                        }
                        break;
                    case 3:
                        yield return i;
                        break;
                }
            }
            if (buf1.Count > 0)
            {
                foreach (var j in buf1)
                {
                    yield return j;
                }
            }
            Debug.Log(string.Format("Patched WorkerUpgradeRandomMissile state={0}", state));
        }

        static void MaybePreRefillMissiles(Player p, int type)
        {
            switch(type)
            {
				// in these cases only one upgrade increases capacity, so refill before upgrading
                case (int)MissileType.CREEPER:
                case (int)MissileType.HUNTER:
                case (int)MissileType.FALCON:
                case (int)MissileType.MISSILE_POD:
                    p.AddMissileAmmo(1000, (MissileType)type, true, false);
                    break;
                default:
                    break;
            }
        }
        static void MaybePostRefillMissiles(Player p, int type)
        {
            switch (type)
            {
				// in these cases both upgrades increase capacity, so refill after upgrading
                case (int)MissileType.NOVA:
                case (int)MissileType.DEVASTATOR:
                case (int)MissileType.TIMEBOMB:
                case (int)MissileType.VORTEX:
                    p.AddMissileAmmo(1000, (MissileType)type, true, false);
                    break;
                default:
                    break;
            }
        }
    }
}