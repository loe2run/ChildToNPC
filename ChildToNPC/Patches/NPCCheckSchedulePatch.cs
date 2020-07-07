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
        public static bool Prefix(ref NPC __instance, int timeOfDay)
        {
            if (!ModEntry.IsChildNPC(__instance))
                return true;

            var scheduleTimeToTry = ModEntry.helper.Reflection.GetField<int>(__instance, "scheduleTimeToTry");
            int scheduleTimeRef = scheduleTimeToTry.GetValue();

            /* Code from NPC.checkSchedule below */
            __instance.updatedDialogueYet = false;
            
            var extraDialogue = ModEntry.helper.Reflection.GetField<string>(__instance, "extraDialogueMessageToAddThisMorning");
            extraDialogue.SetValue(null);
            
            if (__instance.ignoreScheduleToday)
                return false;

            if (__instance.Schedule == null)
            {
                ModEntry.monitor.Log("Schedule for " + __instance.Name + " is null, check your patch summary.", LogLevel.Trace);
                return false;
            }

            // Try to load the schedule for the next time
            __instance.Schedule.TryGetValue(scheduleTimeRef == 9999999 ? timeOfDay : scheduleTimeRef, out SchedulePathDescription schedulePathDescription);
            
            if (schedulePathDescription == null)
                return false;
            
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
            {
                var previousEndPoint = ModEntry.helper.Reflection.GetField<Point>(__instance, "previousEndPoint");
                previousEndPoint.SetValue(__instance.DirectionsToNewLocation.route.Count > 0 ? __instance.DirectionsToNewLocation.route.Last() : Point.Zero);
            }

            return false;
        }
    }
}