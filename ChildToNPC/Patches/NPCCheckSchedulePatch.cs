using System;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewModdingAPI;

namespace ChildToNPC.Patches
{
    /* Prefix for checkSchedule
     * This is a mix of code from the original method and my own.
     * I use reflection to access private methods in the NPC class.
     */
    class NPCCheckSchedulePatch
    {
        public static bool Prefix(NPC __instance, int timeOfDay, ref Point ___previousEndPoint, ref string ___extraDialogueMessageToAddThisMorning, ref Rectangle ___lastCrossroad)
        {
            if (!ModEntry.IsChildNPC(__instance))
                return true;

            var scheduleTimeToTry = ModEntry.helper.Reflection.GetField<int>(__instance, "scheduleTimeToTry");
            int scheduleTimeRef = scheduleTimeToTry.GetValue();

            /* Code from NPC.checkSchedule */

            __instance.updatedDialogueYet = false;
            ___extraDialogueMessageToAddThisMorning = null;
            if (__instance.ignoreScheduleToday)
                return false;

            if (__instance.Schedule == null)
            {
                ModEntry.monitor.Log("Schedule for " + __instance.Name + " is null, check your patch summary.", LogLevel.Debug);
                return false;
            }

            __instance.Schedule.TryGetValue(scheduleTimeRef == 9999999 ? timeOfDay : scheduleTimeRef, out SchedulePathDescription schedulePathDescription);

            // If I have curfew, override the normal behavior
            if (ModEntry.Config.DoChildrenHaveCurfew && !__instance.currentLocation.Equals(Game1.getLocationFromName("FarmHouse")))
            {
                // Send child home for curfew
                if(timeOfDay == ModEntry.Config.CurfewTime)
                {
                    object[] pathfindParams = { __instance.currentLocation.Name, __instance.getTileX(), __instance.getTileY(), "BusStop", -1, 23, 3, null, null };
                    schedulePathDescription = ModEntry.helper.Reflection.GetMethod(__instance, "pathfindToNextScheduleLocation", true).Invoke<SchedulePathDescription>(pathfindParams);
                }
                // Ignore scheduled events after curfew
                else if(timeOfDay > ModEntry.Config.CurfewTime)
                {
                    schedulePathDescription = null;
                }
            }

            if (schedulePathDescription == null)
                return false;
            
            // Normally I would see a IsMarried check here, but FarmHouse may be better?
            // (I think this section is meant for handling when the character is still walking)
            if (!__instance.currentLocation.Equals(Game1.getLocationFromName("FarmHouse")) && (!__instance.IsWalkingInSquare || ___lastCrossroad.Center.X / 64 != ___previousEndPoint.X && ___lastCrossroad.Y / 64 != ___previousEndPoint.Y))
            {
                if (!___previousEndPoint.Equals(Point.Zero) && !___previousEndPoint.Equals(__instance.getTileLocationPoint()))
                {
                    if (scheduleTimeRef == 9999999)
                        scheduleTimeToTry.SetValue(timeOfDay);
                    return false;
                }
            }

            __instance.DirectionsToNewLocation = schedulePathDescription;

            // __instance.prepareToDisembarkOnNewSchedulePath();
            ModEntry.helper.Reflection.GetMethod(__instance, "prepareToDisembarkOnNewSchedulePath", true).Invoke(null);

            if (__instance.Schedule == null)
                return false;

            if (__instance.DirectionsToNewLocation != null && __instance.DirectionsToNewLocation.route != null && __instance.DirectionsToNewLocation.route.Count > 0 
                && (Math.Abs(__instance.getTileLocationPoint().X - __instance.DirectionsToNewLocation.route.Peek().X) > 1 || Math.Abs(__instance.getTileLocationPoint().Y - __instance.DirectionsToNewLocation.route.Peek().Y) > 1) && __instance.temporaryController == null)
            {
                scheduleTimeToTry.SetValue(9999999);
                return false;
            }

            __instance.controller = new PathFindController(__instance.DirectionsToNewLocation.route, __instance, Utility.getGameLocationOfCharacter(__instance))
            {
                finalFacingDirection = __instance.DirectionsToNewLocation.facingDirection,
                // endBehaviorFunction = this.getRouteEndBehaviorFunction(this.directionsToNewLocation.endOfRouteBehavior, this.directionsToNewLocation.endOfRouteMessage)
                endBehaviorFunction = ModEntry.helper.Reflection.GetMethod(__instance, "getRouteEndBehaviorFunction", true).Invoke<PathFindController.endBehavior>(new object[] { __instance.DirectionsToNewLocation.endOfRouteBehavior, __instance.DirectionsToNewLocation.endOfRouteMessage })
            };
            scheduleTimeToTry.SetValue(9999999);

            if (__instance.DirectionsToNewLocation != null && __instance.DirectionsToNewLocation.route != null)
                ___previousEndPoint = __instance.DirectionsToNewLocation.route.Count > 0 ? __instance.DirectionsToNewLocation.route.Last() : Point.Zero;

            return false;
        }
    }
}