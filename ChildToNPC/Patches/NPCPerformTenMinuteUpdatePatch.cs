using Harmony;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Locations;
using System.Linq;

namespace ChildToNPC
{
    /* Prefix for performTenMinuteUpdate
     * Normally, performTenMinuteUpdate just handles the dialogue bubble while walking.
     * I've combined this with code from Child.tenMinuteUpdate to imitate Child behavior.
     */

    [HarmonyPatch(typeof(NPC))]
    [HarmonyPatch("performTenMinuteUpdate")]
    class NPCPerformTenMinuteUpdatePatch
    {
        public static bool Prefix(NPC __instance)
        {
            if (!ModEntry.IsChildNPC(__instance) || !Game1.IsMasterGame)
                return true;

            if (__instance.controller == null && Game1.timeOfDay % 100 == 0 && Game1.timeOfDay < 1900)
            {
                if (!__instance.currentLocation.Equals(Utility.getHomeOfFarmer(Game1.player)))
                    return true;
                
                __instance.IsWalkingInSquare = false;
                __instance.Halt();
                FarmHouse farmHouse = Utility.getHomeOfFarmer(Game1.player);

                if (farmHouse.characters.Contains(__instance))
                {
                    //If I'm going to prevent them from wandering into doorways, I need to do it here.
                    //__instance.controller = new PathFindController(__instance, farmHouse, farmHouse.getRandomOpenPointInHouse(Game1.random, 0, 30), 2);
                    __instance.temporaryController = new PathFindController(__instance, farmHouse, farmHouse.getRandomOpenPointInHouse(Game1.random, 0, 30), 2);
                    if (__instance.temporaryController.pathToEndPoint == null || !farmHouse.isTileOnMap(__instance.temporaryController.pathToEndPoint.Last().X, __instance.temporaryController.pathToEndPoint.Last().Y))
                        __instance.temporaryController = null;
                }
            }
            else if(Game1.timeOfDay == 1900)
            {
                __instance.IsWalkingInSquare = false;
                __instance.Halt();
                FarmHouse farmHouse = Utility.getHomeOfFarmer(Game1.player);

                //If the character is already at home, pathfind to bed (default position?)
                if (farmHouse.characters.Contains(__instance))
                {
                    __instance.controller = new PathFindController(__instance, farmHouse, Utility.Vector2ToPoint(__instance.DefaultPosition), 2);
                    if (__instance.controller.pathToEndPoint == null || !farmHouse.isTileOnMap(__instance.controller.pathToEndPoint.Last().X, __instance.controller.pathToEndPoint.Last().Y))
                        __instance.controller = null;
                }
                else
                {
                    //When not at home, walk to the warp point that leads home.
                    //(NPCArriveAtFarmHousePatch will take over after this)
                    __instance.controller = new PathFindController(__instance, Game1.getLocationFromName("BusStop"), new Point(-1, 23), 3);

                    if (__instance.controller.pathToEndPoint == null)
                        __instance.controller = null;
                }
            }

            return true;
        }
    }
}