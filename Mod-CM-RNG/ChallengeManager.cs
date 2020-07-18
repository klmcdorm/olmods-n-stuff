using UnityEngine;
using Overload;
using Harmony;
using System.Reflection;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace GameMod
{
    // outside factors that influence RNG
    // - starting loadout
    // - any leftover state
    // - other processes using RNG
    // - challengemanager.addkill
    // - robotmanager item drops

    // fixed RNG seed
    [HarmonyPatch(typeof(ChallengeManager), "InitChallenge")]
    class ChallengeManager_InitChallenge
    {
        [HarmonyPriority(Priority.First)]
        static void Prefix()
        {
            //Random_Logged.enabled = false;

            FixedRngConfig.Load();

            // Reset state as much as possible
            ChallengeManager.BackupSpawnTimer = 0;
            ChallengeManager.SpawnSpacing = 0;
            ChallengeManager.SequencePatternCurrent = SequencePattern.NUM;
            ChallengeManager.PreviousSpawnType = EnemyType.NUM;
            ChallengeManager.ChosenSuperPower = 0;
            ChallengeManager.PreviousSpawnPoint = -1;
            ChallengeManager.PrevSpecialSub = -1;

            Robot.BONUS_CM_PITY = 0;
            Robot.PITY_DROPPER = 0;
            Robot.AUTO_ARMOR_COUNT = 20;

            if(FixedRngConfig.FixedSeed)
            {
                FixedRng.Reset(FixedRngConfig.Seed);

                FieldInfo fi = typeof(ChallengeManager).GetField("m_previous_spawn_segments", BindingFlags.NonPublic | BindingFlags.Static);
                fi.SetValue(null, new int[] { -1, -1, -1, -1, -1 });
            }
            else
            {
                FixedRng.Reset(Random.Range(int.MinValue, int.MaxValue));
            }
            FixedRng.UseState();
        }

        static void Postfix()
        {
            FixedRng.RestoreState();
            //Debug.Log("InitChallenge s=" + FixedRng.Status());
        }
    }

    [HarmonyPatch(typeof(ChallengeManager), "AddKill")]
    class ChallengeManager_AddKill
    {
        static void Prefix()
        {
            FixedRng.UseState();
        }

        static void Postfix()
        {
            FixedRng.RestoreState();
        }
    }

    /*
     // DEBUG logging
    [HarmonyPatch(typeof(ChallengeManager), "ChooseNextSpawnSequence")]
    class ChallengeManager_ChooseNextSpawnSequence
    {
        static void Prefix()
        {
            Random_Logged.enabled = true;
            Debug.Log(".ChooseNextSpawnSequence s1=" + Random.state.GetHashCode());
        }

        static void Postfix()
        {
            Random_Logged.enabled = false;
            Debug.Log(".ChooseNextSpawnSequence s2=" + Random.state.GetHashCode());
        }
    }
    
    [HarmonyPatch(typeof(ChallengeManager), "ChooseNextSuperGoal")]
    class ChallengeManager_ChooseNextSuperGoal
    {
        static void Postfix()
        {
            Debug.Log(".ChooseNextSuperGoal s=" + Random.state.GetHashCode());
        }
    }

    public class Random_Logged
    {
        public static bool enabled = false;
    }
    [HarmonyPatch(typeof(Random), "Range", typeof(int), typeof(int))]
    class Random_Logged2
    {
        static void Prefix(int min, int max) { if (Random_Logged.enabled) { Debug.Log(string.Format("Random.Range s1={0} s2={1} min={2} max={3}", Random.state.GetHashCode(), FixedRng.Status(), min, max)); } }
    }
    */


    [HarmonyPatch(typeof(ChallengeManager), "SpawnRobot")]
    class ChallengeManager_SpawnRobot
    {
        static void Postfix(ref float __result)
        {
            //Random_Logged.enabled = true;
            //Debug.Log(string.Format(".SpawnRobot s={0}", FixedRng.Status()));
        }
    }

    [HarmonyPatch(typeof(ChallengeManager), "Update")]
    class ChallengeManager_Update
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            bool flag = true;
            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(FixedRng), "UseState"));
            foreach (var i in instructions)
            {
                if (i.opcode == OpCodes.Ldsfld && ((FieldInfo)i.operand).DeclaringType == typeof(ChallengeManager) && ((FieldInfo)i.operand).Name == "m_random_timer")
                {
                    if (flag)
                    {
                        i.opcode = OpCodes.Call;
                        i.operand = AccessTools.Method(typeof(FixedRng), "RestoreState");
                        yield return i;
                        yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(ChallengeManager), "m_random_timer"));
                        flag = false;
                        continue;
                    }
                }
                yield return i;
            }
            if(flag)
            {
                // this hopefully won't happen but let's be prepared
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(FixedRng), "RestoreState"));
            }
            Debug.Log("Patched ChallengeManager.Update f=" + flag);
        }
    }
}