using UnityEngine;
using HarmonyLib;
using Overload;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection;

namespace GameMod
{
    // Alters wind tunnel effects to only trigger for local player
    [HarmonyPatch(typeof(PlayerShip), "FixedUpdatePreAlive")]
    class PlayerShip_LocalPlayerWindTunnelFx
    {
        static bool Prefix(PlayerShip __instance)
        {
            if(!__instance.isLocalPlayer)
            {
                __instance.m_wind_tunnel_active = false;
            }
            return true;
        }
    }
}