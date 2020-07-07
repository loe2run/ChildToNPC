using StardewValley;
using StardewValley.Locations;

namespace ChildToNPC.Patches
{
    /* Postfix for shouldCollideWithBuildingLayer
     * Normally, this method ejects NPCs who aren't married to a player.
     * This allows NPCs who live in the FarmHouse to be considered as well.
     */
    class NPCShouldCollideWithBuildingLayerPatch
    {
        public static void Postfix(NPC __instance, GameLocation location, ref bool __result)
        {
            if (!ModEntry.IsChildNPC(__instance))
                return;

            // return this.isMarried() && (this.Schedule == null || location is FarmHouse) || base.shouldCollideWithBuildingLayer(location);
            // base.shouldCollide... -> return this.controller == null && !this.IsMonster;
            __result = (__instance.Schedule == null) || (location is FarmHouse) || (__instance.controller == null);
        }
    }
}