using Harmony;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Locations;

namespace ChildToNPC.Patches
{
    [HarmonyPatch(typeof(NPC))]
    [HarmonyPatch("arriveAtFarmHouse")]
    class NPCArriveAtFarmHousePatch
    {
        /* Postfix for arriveAtFarmHouse
         * Usually the method would kick out non-married NPCs immediately,
         * so I'm repeating the code here.
         */
        public static void Postfix(NPC __instance, FarmHouse farmHouse)
        {
            if (!ModEntry.IsChildNPC(__instance))
                return; 

            if (Game1.newDay || Game1.timeOfDay <= 630)
                return;
            //__instance.setTilePosition(farmHouse.getEntryLocation());
            __instance.setTilePosition(farmHouse.getEntryLocation());
            __instance.temporaryController = null;
            __instance.controller = null;
            //normally endPoint is Game1.timeOfDay >= 2130 ? farmHouse.getSpouseBedSpot() : farmHouse.getKitchenStandingSpot()
            //I'm going to hardcode it for now
            __instance.controller = new PathFindController(__instance, farmHouse, new Point(7, 7), 2);//facing Down
            /* This isn't currently relevant to me because I only have one place for my character to go
             * 
            if (__instance.controller.pathToEndPoint == null)
            {
                __instance.willDestroyObjectsUnderfoot = true;
                __instance.controller = new PathFindController(__instance, farmHouse, farmHouse.getKitchenStandingSpot(), 0);
            }
            */
            if (!(Game1.currentLocation is FarmHouse))
                return;
            Game1.currentLocation.playSound("doorClose");
        }
    }
}