using HarmonyLib;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.AI;

namespace SpiderPositionFix.Patches
{
    [HarmonyPatch(typeof(SandSpiderAI))]
    public class SpiderPositionPatch
    {
        private static bool isInWallState = false;
        private static float returningFromWallState = 0f;
        private static bool startPatch = false;

        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        static void StartPostfix(SandSpiderAI __instance)
        {
            __instance.agent.areaMask &= ~(1 << NavMesh.GetAreaFromName("Jump"));
            startPatch = true;
        }

        [HarmonyPatch("LateUpdate")]
        [HarmonyPostfix]
        static void MeshContainerPositionFix(SandSpiderAI __instance)
        {       
            if (startPatch != true) return;

            if (!__instance.lookingForWallPosition && !__instance.gotWallPositionInLOS && !isInWallState)
            {
                if (Vector3.Distance(__instance.meshContainerPosition, __instance.transform.position) > 0.66f)
                {
                    if (__instance.agent.velocity.magnitude > 0f)
                    {
                        __instance.meshContainer.rotation = __instance.gameObject.transform.rotation;
                    }
                    __instance.meshContainerPosition = Vector3.Slerp(__instance.meshContainerPosition, __instance.transform.position, Distance(Vector3.Distance(__instance.meshContainerPosition, __instance.transform.position), 0.5f) * Time.deltaTime);
                }
            }
            if (__instance.lookingForWallPosition && __instance.gotWallPositionInLOS && !isInWallState)
            {
                isInWallState = true;
            }
            if (__instance.lookingForWallPosition && __instance.gotWallPositionInLOS && isInWallState)
            {
                returningFromWallState += Time.deltaTime;

                if (isInWallState && Vector3.Distance(__instance.meshContainerPosition, __instance.transform.position - __instance.meshContainerPosition) < 1f || returningFromWallState > 10f)
                {
                    isInWallState = false;
                    returningFromWallState = 0f;
                }
            }
        }

        static float Distance(float distance, float time)
        {
            float ratio = distance / time;
            return ratio;
        }
    }
}