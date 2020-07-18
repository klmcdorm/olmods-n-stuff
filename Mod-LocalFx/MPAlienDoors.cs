using UnityEngine;
using Harmony;
using Overload;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection;

namespace GameMod
{
    // Alters physics effects to apply to colliding player
    [HarmonyPatch(typeof(AlienSecretDoorTriggerStay), "OnTriggerStay")]
    class AlienSecretDoorTriggerStay_LocalPlayerPhysicsOnly
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
        {
            int state = 0;
            foreach (var i in code)
            {
                switch(state)
                {
                    case 0:
                        // 0. find
                        // stfld (AlienSecretDoor.m_effect_flash)
                        if (i.opcode == OpCodes.Stfld && ((FieldInfo)i.operand).DeclaringType == typeof(AlienSecretDoor) && ((FieldInfo)i.operand).Name == "m_effect_flash")
                        {
                            state = 1;
                        }
                        yield return i;
                        break;
                    case 1:
                        // 1. Replace layer check with player component check
                        // ** replace
                        // ldarg.1
                        // callvirt (Component.get_gameObject)
                        // callvirt (GameObject.get_layer)
                        // lds.i4.s 9
                        // bne.un -> eof
                        // ** with
                        // ldarg.1
                        // callvirt (GetComponent<Player>)
                        // brnull (aka brfalse)
                        if(i.opcode == OpCodes.Bne_Un)
                        {
                            //Debug.Log(i.ToString());
                            yield return new CodeInstruction(OpCodes.Ldarg_1);
                            yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(Component), "GetComponent", generics: new System.Type[] { typeof(Player) }));
                            yield return new CodeInstruction(i) { opcode = OpCodes.Brfalse }; // includes original destination

                            state = 2;
                        }
                        break;
                    case 2:
                        // 2. Replace global player references
                        // ** replace
                        // ldsfld (GameManager.m_player_ship)
                        //    with
                        // ldarg.1
                        // callvirt (GetComponent<Player>)
                        // ldfld (c_player_ship)
                        // ** replace
                        // ldsfld (GameManager.m_local_player)
                        //    with
                        // ldarg.1
                        // callvirt (GetComponent<Player>)
                        if(i.opcode == OpCodes.Ldsfld && ((FieldInfo) i.operand).DeclaringType == typeof(GameManager) && ((FieldInfo)i.operand).Name == "m_player_ship")
                        {
                            // in case it's a branch target, clone original with labels
                            // Debug.Log(i.ToString());
                            yield return new CodeInstruction(i) { opcode = OpCodes.Ldarg_1, operand = null };
                            yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(Component), "GetComponent", generics: new System.Type[] { typeof(Player) }));
                            yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Player), "c_player_ship"));
                        }
                        else if (i.opcode == OpCodes.Ldsfld && ((FieldInfo)i.operand).DeclaringType == typeof(GameManager) && ((FieldInfo)i.operand).Name == "m_local_player")
                        {
                            yield return new CodeInstruction(i) { opcode = OpCodes.Ldarg_1, operand = null };
                            yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(Component), "GetComponent", generics: new System.Type[] { typeof(Player) }));
                        }
                        else
                        {
                            yield return i;
                        }
                        break;
                    default:
                        yield return i;
                        break;
                }
            }
            Debug.Log("Patched AlienSecretDoorTriggerStay.OnTriggerStay state=" + state);
        }
    }

    // Alters PlayerRumble to only play for the local player
    [HarmonyPatch(typeof(AlienSecretDoorTriggerStay), "PlayerRumble")]
    class AlienSecretDoorTriggerStay_LocalPlayerRumbleOnly
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
        {
            foreach(var i in code)
            {
                if (i.opcode == OpCodes.Ldsfld && ((FieldInfo)i.operand).DeclaringType == typeof(GameManager) && ((FieldInfo)i.operand).Name == "m_shake_manager")
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                }
                else if (i.opcode == OpCodes.Callvirt && ((MethodInfo)i.operand).DeclaringType == typeof(CameraShakeManager) && ((MethodInfo)i.operand).Name == "PlayCameraShake")
                {
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Player), "PlayCameraShake", parameters: new System.Type[] { typeof(CameraShakeType), typeof(float), typeof(float) }));
                }
                else
                {
                    yield return i;
                }
            }
            Debug.Log("Patched AlienSecretDoorTriggerStay.PlayerRumble");
        }
    }
}