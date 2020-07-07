using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Network;

namespace ChildToNPC.Patches
{
    /* Postfix for arriveAtFarmHouse
     * 
     * This code is directly translated from the original method
     * because the original method would immediately kick out non-married NPCs.
     * 
     * When NPC arrives at farmhouse, pathfinds them into the house.
     */
    class NPCArriveAtFarmHousePatch
    {
        public static void Postfix(ref NPC __instance, FarmHouse farmHouse)
        {
            if (!ModEntry.IsChildNPC(__instance))
                return;

            /* Code from NPC.arriveAtFarmHouse */
            if (Game1.newDay || Game1.timeOfDay <= 630)
                return;

            __instance.setTilePosition(farmHouse.getEntryLocation());
            __instance.ignoreScheduleToday = true;
            __instance.temporaryController = null;
            __instance.controller = null;

            // If Game1.timeOfDay is after curfew (for spouse 2130)
            if (Game1.timeOfDay >= ModEntry.Config.CurfewTime)
            {
                Point bedPoint = new Point((int)(__instance.DefaultPosition.X / 64), (int)(__instance.DefaultPosition.Y / 64));
                __instance.controller = new PathFindController(__instance, farmHouse, bedPoint, 0, new PathFindController.endBehavior(FarmHouse.spouseSleepEndFunction));
            }
            else
            {
                // Pathfind the NPC from the door to a point in the house
                PathfindToRandomPoint(__instance, farmHouse);
            }

            // If that failed, try again while allowing object destruction
            if (__instance.controller.pathToEndPoint == null)
            {
                __instance.willDestroyObjectsUnderfoot = true;
                PathfindToRandomPoint(__instance, farmHouse);
            }

            // Play audio cue for player(s)
            if (Game1.currentLocation == farmHouse)
                Game1.currentLocation.playSound("doorClose", NetAudio.SoundContext.NPC);
        }

        /* PathfindToRandomPoint - sets the NPC's controller to a random point in the FarmHouse
         * 
         * Since I need to do this exact thing twice, I've separated it out to its own method.
         * Generates a random point in the farmhouse and creates a new PathFindController to that point.
         * If that fails, creates a PathFindController to NPC's default position in farmhouse.
         */ 
        private static void PathfindToRandomPoint(NPC npc, FarmHouse farmHouse)
        {
            // Try to pathfind to random point in house
            Point destPoint = farmHouse.getRandomOpenPointInHouse(Game1.random, 0, 30);
            if (!destPoint.Equals(Point.Zero))
            {
                npc.controller = new PathFindController(npc, farmHouse, destPoint, 2);
                return;
            }

            // Otherwise, pathfind to default position in farmhouse
            destPoint = new Point((int)(npc.DefaultPosition.X / 64), (int)(npc.DefaultPosition.Y / 64));
            npc.controller = new PathFindController(npc, farmHouse, destPoint, 2);
        }
    }
}