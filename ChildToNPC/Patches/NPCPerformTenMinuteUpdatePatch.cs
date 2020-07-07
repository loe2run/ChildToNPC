using System.Linq;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Locations;

namespace ChildToNPC.Patches
{
    /* Prefix for performTenMinuteUpdate
     * 
     * Normally, performTenMinuteUpdate just handles the dialogue bubble while walking.
     * I've combined this with code from FarmHouse.performTenMinuteUpdate to handle NPC's pathfinding to be at night.
     */
    public class NPCPerformTenMinuteUpdatePatch
    {
        public static void Postfix(ref NPC __instance, int timeOfDay, GameLocation l)
        {
            if (!ModEntry.IsChildNPC(__instance) || !Game1.IsMasterGame)
                return;

            if (!(l is FarmHouse))
                return;

            FarmHouse farmHouse = l as FarmHouse;
            if (!farmHouse.characters.Contains(__instance))
                return;

            Point bedPoint = new Point((int)(__instance.DefaultPosition.X / 64), (int)(__instance.DefaultPosition.Y / 64));
            if (__instance.getTileLocationPoint() == bedPoint)
                return;

            // If children are out of bed past a certain time, pathfind them past curfew (2200 for spouse)
            int curfewTime = ModEntry.Config.CurfewTime;
            if (Game1.timeOfDay >= curfewTime && (timeOfDay == curfewTime || (timeOfDay % 100 % 30 == 0 && __instance.controller == null)))
            {
                __instance.controller = null;
                __instance.controller = new PathFindController(__instance, farmHouse, bedPoint, 0, new PathFindController.endBehavior(FarmHouse.spouseSleepEndFunction));
                if (__instance.controller.pathToEndPoint == null || (!farmHouse.isTileOnMap(__instance.controller.pathToEndPoint.Last<Point>().X, __instance.controller.pathToEndPoint.Last<Point>().Y)))
                    __instance.controller = null;
            }
        }
    }
}