using UnityEngine;
using HarmonyLib;
using Overload;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection;

namespace GameMod
{
    // Alters warper HUD/sound effects to only trigger for local player
    [HarmonyPatch(typeof(TriggerWarper), "OnTriggerEnter")]
    class TriggerWarper_LocalPlayerHudFx
    {
        static void TeleportRemotePlayer(TriggerWarper warper, Vector3 rot_offset, PlayerShip playerShip)
        {
            playerShip.CreateTeleportFlash();
            GameManager.m_audio.PlayCuePos(329, playerShip.c_transform.position, 0.9f, -0.2f, 0f, 1f);
            warper.TeleportObject(playerShip.c_transform, playerShip.c_rigidbody, rot_offset, true);

            GameManager.m_audio.PlayCuePos(329, playerShip.c_transform.position, 0.7f, 0.2f, 0f, 1f);
            playerShip.MakeSpawnFlash();
            warper.dest_warper.timer = 0.1f;
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
        {
            const int BufferSize = 3;
            int state = 1;
            var buffer = new List<CodeInstruction>(BufferSize);
            var skipLabel = new Label();

            foreach(var i in code)
            {
                if(buffer.Count >= BufferSize)
                {
                    yield return buffer[0];
                    buffer.RemoveAt(0);
                }
                buffer.Add(i);

                switch (state)
                {
                    case 1:
                        // 1. Find spot where PlayerShip variable is assigned from GetComponent
                        if (i.opcode == OpCodes.Stloc_S)
                        {
                            // could be either LocalBuilder or a raw number
                            if (i.operand is LocalBuilder && ((LocalBuilder)i.operand).LocalIndex != 4)
                            {
                                break;
                            }
                            if (!(i.operand is LocalBuilder) && !4.Equals(i.operand))
                            {
                                break;
                            }

                            var prev = buffer[BufferSize - 1 - 1];
                            if(prev.opcode == OpCodes.Callvirt 
                                && ((MethodInfo)prev.operand).DeclaringType == typeof(Component) 
                                && ((MethodInfo)prev.operand).Name == "GetComponent"
                                && ((MethodInfo)prev.operand).IsGenericMethod
                                && ((MethodInfo)prev.operand).GetGenericArguments()[0] == typeof(PlayerShip) )
                            {
                                state = 2;
                            }
                        }
                        break;
                    case 2:
                        // 2. If player is not local, play 3D sounds before and after teleporting (via TeleportRemotePlayer defined above), then skip the rest
                        // TODO for some reason a remote player isn't guaranteed to observe teleportation happening... probably a larger design problem.
                        if (i.opcode == OpCodes.Brfalse)
                        {
                            var before = buffer[BufferSize - 1 - 2];
                            if (before.opcode == OpCodes.Ldloc_S)
                            {
                                if (before.operand is LocalBuilder && ((LocalBuilder)before.operand).LocalIndex != 4)
                                {
                                    break;
                                }
                                if (!(before.operand is LocalBuilder) && !4.Equals(before.operand))
                                {
                                    break;
                                }

                                // flush buffer
                                foreach (var j in buffer)
                                {
                                    yield return j;
                                }
								buffer.Clear();

                                yield return new CodeInstruction(OpCodes.Ldloc_S, 4);
                                yield return new CodeInstruction(OpCodes.Call, 
                                    AccessTools.Property(typeof(UnityEngine.Networking.NetworkBehaviour), "isLocalPlayer").GetGetMethod());
                                yield return new CodeInstruction(OpCodes.Brtrue, skipLabel);

                                yield return new CodeInstruction(OpCodes.Ldarg_0);    // this
                                yield return new CodeInstruction(OpCodes.Ldloc_2);    // rot_offset
                                yield return new CodeInstruction(OpCodes.Ldloc_S, 4); // PlayerShip
                                yield return new CodeInstruction(OpCodes.Call, 
                                    typeof(TriggerWarper_LocalPlayerHudFx).GetMethod("TeleportRemotePlayer", BindingFlags.Static | BindingFlags.NonPublic));
                                yield return new CodeInstruction(OpCodes.Ret);

                                var after = new CodeInstruction(OpCodes.Nop);
                                after.labels.Add(skipLabel);
                                yield return after;

                                state = 3;
                            }
                        }
                        break;
                    case 3:
                    default:
                        break;
                }
            }

            foreach(var i in buffer)
            {
                yield return i;
            }
            Debug.Log("Patched TriggerWarper.OnTriggerEnter state=" + state);
        }
    }
}