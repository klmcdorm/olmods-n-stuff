using UnityEngine;
using Harmony;
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

    // Try to fix object rotation coming out of a warper
    [HarmonyPatch(typeof(TriggerWarper), "TeleportObject")]
    class TriggerWarper_RotationFix
    {
        static bool Prefix(TriggerWarper __instance, Transform obj_transform, Rigidbody obj_rigidbody, Vector3 rot_offset, bool push_away)
        {
            Debug.Log(
                "TriggerWarper::TeleportObject this.c_transform.rotation=" + __instance.c_transform.rotation.ToString() 
                + " arg1.rotation=" + obj_transform.rotation.ToString()
                + " dest_warper.c_transform.rotation=" + __instance.dest_warper.c_transform.rotation.ToString());
            Debug.Log(
                "TriggerWarper::TeleportObject this.c_transform.rotation=" + __instance.c_transform.rotation.eulerAngles.ToString()
                + " arg1.rotation=" + obj_transform.rotation.eulerAngles.ToString()
                + " dest_warper.c_transform.rotation=" + __instance.dest_warper.c_transform.rotation.eulerAngles.ToString());

            var inverted0 = Quaternion.Inverse(__instance.c_transform.rotation);
            var inverted2 = Quaternion.Inverse(__instance.c_transform.rotation * Quaternion.AngleAxis(180.0f, Vector3.up));
            Debug.Log(
                "TriggerWarper::TeleportObject "
                + " i0=" + inverted0.ToString()
                + " i2=" + inverted2.ToString());
            Debug.Log(
                "TriggerWarper::TeleportObject "
                + " i0.euler=" + inverted0.eulerAngles.ToString()
                + " i2.euler=" + inverted2.eulerAngles.ToString());

            var rel1 = obj_transform.rotation * inverted0 * Quaternion.AngleAxis(180.0f, Vector3.up);
            var rel2 = obj_transform.rotation * inverted2;
            Debug.Log(
               "TriggerWarper::TeleportObject r1=" + rel1.ToString()
               + " r1.euler=" + rel1.eulerAngles.ToString());
            Debug.Log(
                "TriggerWarper::TeleportObject r2=" + rel2.ToString()
                + " r2.euler=" + rel2.eulerAngles.ToString());
            return true;
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
        {
            const int BufferSize = 3;
            int state = 1;
            var buffer = new List<CodeInstruction>(BufferSize);

            foreach (var i in code)
            {
                if (buffer.Count >= BufferSize)
                {
                    if(state != 2)
                    {
                        yield return buffer[0];
                    }
                    buffer.RemoveAt(0);
                }
                buffer.Add(i);

                switch (state)
                {
                    case 1:
                        // 1. Find spot where Transform::get_rotation is called on arg 1
                        if (i.opcode == OpCodes.Callvirt
                            && ((MethodInfo)i.operand).DeclaringType == typeof(Transform)
                            && ((MethodInfo)i.operand).Name == "get_rotation")
                        {
                            var prev = buffer[BufferSize - 1 - 1];
                            if(prev.opcode != OpCodes.Ldarg_1)
                            {
                                break;
                            }

                            // flush buffer
                            foreach (var j in buffer)
                            {
                                yield return j;
                            }
                            buffer.Clear();
                            state = 2;
                        }
                        break;
                    case 2:
                        // 2. Delete old rotation code (continues until set_rotation with arg 1), then insert new rotation code
                        if (i.opcode == OpCodes.Callvirt
                            && ((MethodInfo)i.operand).DeclaringType == typeof(Transform)
                            && ((MethodInfo)i.operand).Name == "set_rotation")
                        {
                            var prev = buffer[BufferSize - 1 - 2];
                            if (prev.opcode != OpCodes.Ldarg_1)
                            {
                                break;
                            }

                            // stack starts with arg1.rotation
                            //Debug.Log("TriggerWarper_RotationFix: Vector3.up=" + typeof(UnityEngine.Vector3).GetProperty("up", BindingFlags.Static | BindingFlags.Public).GetGetMethod().Name);
                            //Debug.Log("TriggerWarper_RotationFix: op_Multiply=" + typeof(UnityEngine.Quaternion).GetMethod("op_Multiply", new System.Type[] { typeof(UnityEngine.Quaternion), typeof(UnityEngine.Quaternion), }).Name);

                            // arg1.rotation = this.dest_warper.c_transform.rotation * Quaternion.Inverse( this.c_transform.rotation * Quaternion.AngleAxis( 180, Vector3.up ) ) * arg1.rotation

                            // save arg1.rotation
                            yield return new CodeInstruction(OpCodes.Stloc_2);
                            // this.dest_warper.c_transform.rotation
                            yield return new CodeInstruction(OpCodes.Ldarg_0);
                            yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(TriggerWarper), "dest_warper"));
                            yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(TriggerWarper), "c_transform"));
                            yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Property(typeof(UnityEngine.Transform), "rotation").GetGetMethod());
                            // this.c_transform.rotation
                            yield return new CodeInstruction(OpCodes.Ldarg_0);
                            yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(TriggerWarper), "c_transform"));
                            yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Property(typeof(UnityEngine.Transform), "rotation").GetGetMethod());
                            // Quaternion.AngleAxis(180, Vector3.up)
                            yield return new CodeInstruction(OpCodes.Ldc_R4, 180.0f);
                            yield return new CodeInstruction(OpCodes.Call, typeof(UnityEngine.Vector3).GetProperty("up", BindingFlags.Static | BindingFlags.Public).GetGetMethod());
                            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(UnityEngine.Quaternion), "AngleAxis"));
                            // Quaternion.Inverse(this.c_transform.rotation * Quaternion.AngleAxis(180, Vector3.up))
                            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(UnityEngine.Quaternion), "op_Multiply", new System.Type[] { typeof(UnityEngine.Quaternion), typeof(UnityEngine.Quaternion), }));
                            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(UnityEngine.Quaternion), "Inverse"));
                            // multiply with this.dest_warper.c_transform.rotation
                            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(UnityEngine.Quaternion), "op_Multiply", new System.Type[] { typeof(UnityEngine.Quaternion), typeof(UnityEngine.Quaternion), }));
                            // multiply with arg1.rotation
                            yield return new CodeInstruction(OpCodes.Ldloc_2);
                            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(UnityEngine.Quaternion), "op_Multiply", new System.Type[] { typeof(UnityEngine.Quaternion), typeof(UnityEngine.Quaternion), }));
                            // save into arg1.rotation
                            yield return new CodeInstruction(OpCodes.Stloc_2);
                            yield return new CodeInstruction(OpCodes.Ldarg_1);
                            yield return new CodeInstruction(OpCodes.Ldloc_2);
                            yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Property(typeof(UnityEngine.Transform), "rotation").GetSetMethod());

                            buffer.Clear();
                            state = 3;
                        }
                        break;
                    case 3:
                    default:
                        break;
                }
            }

            foreach (var i in buffer)
            {
                yield return i;
            }
            Debug.Log("Patched TriggerWarper.TeleportObject state=" + state);
        }
    }
}