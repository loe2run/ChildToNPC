using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Harmony;
using StardewValley;
using Netcode;

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
        public static bool Prefix(NPC __instance, int timeOfDay, ref int ___scheduleTimeToTry, ref Point ___previousEndPoint, ref string ___extraDialogueMessageToAddThisMorning, ref SchedulePathDescription ___directionsToNewLocation, ref Rectangle ___lastCrossroad, ref NetString ___endOfRouteBehaviorName)
        {
            if (!ModEntry.IsChildNPC(__instance))
                return true;

            __instance.updatedDialogueYet = false;
            ___extraDialogueMessageToAddThisMorning = null;
            if (__instance.ignoreScheduleToday || __instance.Schedule == null)
                return false;

            if (ModEntry.Config.DoChildrenHaveCurfew && timeOfDay == ModEntry.Config.CurfewTime && __instance.currentLocation.Equals(Game1.getLocationFromName("FarmHouse")))
            {
                //Should set the curfew pathing instead here?
                //Currently pathing from the performTenMinuteUpdate
            }
            
            ModEntry.monitor.Log("Checking the schedule, trying " + ___scheduleTimeToTry + " at time " + timeOfDay + ".");

            __instance.Schedule.TryGetValue(___scheduleTimeToTry == 9999999 ? timeOfDay : ___scheduleTimeToTry, out SchedulePathDescription schedulePathDescription);
            if (schedulePathDescription == null)
                return false;

            ModEntry.monitor.Log("Found a schedulePathDescription for that time.");

            //Normally I would see a IsMarried check here, but FarmHouse may be better?
            //(I think this section is meant for handling when the character is still walking)
            if (!__instance.currentLocation.Equals(Game1.getLocationFromName("FarmHouse")) || !__instance.IsWalkingInSquare || ___lastCrossroad.Center.X / 64 != ___previousEndPoint.X && ___lastCrossroad.Y / 64 != ___previousEndPoint.Y)
            {
                if (!___previousEndPoint.Equals(Point.Zero) && !___previousEndPoint.Equals(__instance.getTileLocationPoint()))
                {
                    if (___scheduleTimeToTry == 9999999)
                        ___scheduleTimeToTry = timeOfDay;

                    ModEntry.monitor.Log("Returning false and exiting before preparing to Disembark.");
                    return false;
                }
            }

            ModEntry.monitor.Log("Setting directionsToNewLocation.");
            ___directionsToNewLocation = schedulePathDescription;

            ModEntry.monitor.Log("Preparing to disembark through reflection.");
            //__instance.prepareToDisembarkOnNewSchedulePath();
            ModEntry.helper.Reflection.GetMethod(__instance, "prepareToDisembarkOnNewSchedulePath", true).Invoke(null);
            ModEntry.monitor.Log("Finishing the preparations.");

            if (__instance.Schedule == null)
            {
                ModEntry.monitor.Log("After preparing to disembark, schedule is null, failure.");
                return false;
            }

            if (___directionsToNewLocation != null && ___directionsToNewLocation.route != null && ___directionsToNewLocation.route.Count > 0 && (Math.Abs(__instance.getTileLocationPoint().X - ___directionsToNewLocation.route.Peek().X) > 1 || Math.Abs(__instance.getTileLocationPoint().Y - ___directionsToNewLocation.route.Peek().Y) > 1) && __instance.temporaryController == null)
            {
                ___scheduleTimeToTry = 9999999;
                ModEntry.monitor.Log("Failing to try this time after preparing to Disembark.");
                return false;
            }

            ModEntry.monitor.Log("Trying to set the controller, using reflection.");
            __instance.controller = new PathFindController(___directionsToNewLocation.route, __instance, Utility.getGameLocationOfCharacter(__instance))
            {
                finalFacingDirection = ___directionsToNewLocation.facingDirection,
                //endBehaviorFunction = this.getRouteEndBehaviorFunction(this.directionsToNewLocation.endOfRouteBehavior, this.directionsToNewLocation.endOfRouteMessage)
                endBehaviorFunction = ModEntry.helper.Reflection.GetMethod(__instance, "getRouteEndBehaviorFunction", true).Invoke<PathFindController.endBehavior>(new object[] { __instance.DirectionsToNewLocation.endOfRouteBehavior, __instance.DirectionsToNewLocation.endOfRouteMessage })
            };
            ___scheduleTimeToTry = 9999999;
            if (___directionsToNewLocation == null || ___directionsToNewLocation.route == null)
                return false;

            ___previousEndPoint = ___directionsToNewLocation.route.Count > 0 ? ___directionsToNewLocation.route.Last() : Point.Zero;
            return false;
        }
    }
}