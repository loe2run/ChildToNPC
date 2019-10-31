using Harmony;
using Microsoft.Xna.Framework;
using StardewValley;
using System;

namespace ChildToNPC.Patches
{
    /* Prefix for checkSchedule
     * This is a mix of code from the original method and my own.
     * I perform most the code, but there's a section at the end that I can't reflect.
     */

    [HarmonyPatch(typeof(NPC))]
    [HarmonyPatch("checkSchedule")]
    class NPCCheckSchedulePatch
    {
        public static bool Prefix(NPC __instance, int timeOfDay, ref int ___scheduleTimeToTry, ref Point ___previousEndPoint, ref string ___extraDialogueMessageToAddThisMorning, ref SchedulePathDescription ___directionsToNewLocation, ref Rectangle ___lastCrossroad)
        {
            if (!ModEntry.IsChildNPC(__instance))
                return true;

            __instance.updatedDialogueYet = false;
            ___extraDialogueMessageToAddThisMorning = null;
            if (__instance.ignoreScheduleToday || __instance.Schedule == null)
                return false;
            
            __instance.Schedule.TryGetValue(___scheduleTimeToTry == 9999999 ? timeOfDay : ___scheduleTimeToTry, out SchedulePathDescription schedulePathDescription);
            if (schedulePathDescription == null)
                return false;

            //Normally I would see a IsMarried check here, but FarmHouse may be better?
            //(I think this section is meant for handling when the character is still walking)
            if (!__instance.currentLocation.Equals(Game1.getLocationFromName("FarmHouse")) || !__instance.IsWalkingInSquare || ___lastCrossroad.Center.X / 64 != ___previousEndPoint.X && ___lastCrossroad.Y / 64 != ___previousEndPoint.Y)
            {
                if (!___previousEndPoint.Equals(Point.Zero) && !___previousEndPoint.Equals(__instance.getTileLocationPoint()))
                {
                    if (___scheduleTimeToTry == 9999999)
                        ___scheduleTimeToTry = timeOfDay;
                    return false;
                }
            }

            ___directionsToNewLocation = schedulePathDescription;
            //__instance.prepareToDisembarkOnNewSchedulePath();
            ModEntry.helper.Reflection.GetMethod(__instance, "prepareToDisembarkOnNewSchedulePath", true).Invoke(null);

            if (__instance.Schedule == null)
                return false;

            if (___directionsToNewLocation != null && ___directionsToNewLocation.route != null && ___directionsToNewLocation.route.Count > 0 && (Math.Abs(__instance.getTileLocationPoint().X - ___directionsToNewLocation.route.Peek().X) > 1 || Math.Abs(__instance.getTileLocationPoint().Y - ___directionsToNewLocation.route.Peek().Y) > 1) && __instance.temporaryController == null)
            {
                ___scheduleTimeToTry = 9999999;
                return false;
            }
            //normally there's an else here when the final bit of pathing is done.
            //I can't repeat that code here, so I'm returning true.
            //(Hopefully there isn't an issue where it gets stuck in the earlier code of the original.)

            return true;
        }
    }
}
