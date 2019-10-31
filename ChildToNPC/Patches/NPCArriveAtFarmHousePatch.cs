using Harmony;
using StardewValley;
using StardewValley.Locations;

namespace ChildToNPC.Patches
{
    /* Postfix for arriveAtFarmHouse
     * This code is directly translated from the original method
     * because the original method would immediately kick out non-married NPCs.
     */
    [HarmonyPatch(typeof(NPC))]
    [HarmonyPatch("arriveAtFarmHouse")]
    class NPCArriveAtFarmHousePatch
    {
        public static void Postfix(NPC __instance, FarmHouse farmHouse)
        {
            if (!ModEntry.IsChildNPC(__instance))
                return; 

            if (Game1.newDay || Game1.timeOfDay <= 630)
                return;
            
            __instance.setTilePosition(farmHouse.getEntryLocation());
            __instance.temporaryController = null;
            __instance.controller = null;

            //normally endPoint is Game1.timeOfDay >= 2130 ? farmHouse.getSpouseBedSpot() : farmHouse.getKitchenStandingSpot()
            //this is normally a controller, not temporaryController (test?)
            if(Game1.timeOfDay >= 1900)//700 pm
            {
                __instance.temporaryController = new PathFindController(__instance, farmHouse, Utility.Vector2ToPoint(__instance.DefaultPosition), 2);
            }
            else
            {
                __instance.temporaryController = new PathFindController(__instance, farmHouse, farmHouse.getRandomOpenPointInHouse(Game1.random, 0, 30), 2);
            }

            if (Game1.currentLocation is FarmHouse)
                Game1.currentLocation.playSound("doorClose");
        }
    }
}