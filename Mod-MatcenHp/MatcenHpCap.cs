using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace GameMod
{
    /// <summary>
    /// Matcens get extra HP on later levels in a single player campaign.
    /// This patch adds a ceiling to this effect so that matcens don't get too bulky.
    /// </summary>
    [HarmonyPatch(typeof(RobotMatcen), "Start")]
    public class MatcenStart_HpCap
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
        {
            // replace instances of
            //   LevelInfo.Level.LevelNum
            // with
            //   Min(LevelInfo.Level.LevelNum, 15)
            // ** note that level numbers start at 0, so this caps HP at level 16 values (i.e., the last Cronus Frontier level)

            foreach(var i in code)
            {
                yield return i;
                if (i.opcode == OpCodes.Callvirt
                    && ((MethodInfo)i.operand).DeclaringType == typeof(Overload.LevelInfo)
                    && ((MethodInfo)i.operand).Name == "get_LevelNum")
                {
                    yield return new CodeInstruction(OpCodes.Ldc_I4, 15);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(UnityEngine.Mathf), "Min", new Type[] { typeof(int), typeof(int) }));
                }
            }

            UnityEngine.Debug.Log("MatcenHpCap: Patched Matcen.Start()");
        }

        static void Postfix(RobotMatcen __instance)
        {
            UnityEngine.Debug.Log("Matcen HP cap: matcen HP is " + __instance.m_destroyable.m_hp);
        }

    }
}
