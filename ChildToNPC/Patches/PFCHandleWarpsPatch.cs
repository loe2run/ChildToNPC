using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Locations;
using StardewValley.Network;

namespace ChildToNPC.Patches
{
    /* Postfix for handleWarps
     * Most of this code is directly taken from the original method.
     * It handles the issue where non-married NPCs would be kicked out.
     */
    class PFCHandleWarpsPatch
    {
        public static bool Prefix(Rectangle position, ref PathFindController __instance)
        {
            NPC npc;
            try
            {
                var character = ModEntry.helper.Reflection.GetField<Character>(__instance, "character", true);
                Character c = character.GetValue();
                if (!(c is NPC))
                    return true;
                npc = c as NPC;
            }
            catch (Exception ex)
            {
                ModEntry.monitor.Log("Failed to load PathFindController character from reflection.");
                ModEntry.monitor.Log("Exception ex: " + ex.Message);
                return true;
            }

            if (!ModEntry.IsChildNPC(npc))
                return true;

            /* Code from PathFindController.handleWarps */
            Warp warp = __instance.location.isCollidingWithWarpOrDoor(position);
            if (warp == null)
                return false;

            if (warp.TargetName == "Trailer" && Game1.MasterPlayer.mailReceived.Contains("pamHouseUpgrade"))
                warp = new Warp(warp.X, warp.Y, "Trailer_Big", 13, 24, false);

            // This is normally only for married NPCs
            if (npc.followSchedule)
            {
                if (__instance.location is FarmHouse)
                    warp = new Warp(warp.X, warp.Y, "BusStop", 0, 23, false);
                if (__instance.location is BusStop && warp.X <= 0)
                    warp = new Warp(warp.X, warp.Y, npc.getHome().Name, (npc.getHome() as FarmHouse).getEntryLocation().X, (npc.getHome() as FarmHouse).getEntryLocation().Y, false);
                if (npc.temporaryController != null && npc.controller != null)
                    npc.controller.location = Game1.getLocationFromName(warp.TargetName);
            }

            __instance.location = Game1.getLocationFromName(warp.TargetName);
            // This is normally only for married NPCs
            if (warp.TargetName == "FarmHouse" || warp.TargetName == "Cabin")
            {
                __instance.location = Utility.getHomeOfFarmer(Game1.getFarmer(GetFarmerParentId(npc)));
                warp = new Warp(warp.X, warp.Y, __instance.location.Name, (__instance.location as FarmHouse).getEntryLocation().X, (__instance.location as FarmHouse).getEntryLocation().Y, false);
                if (npc.temporaryController != null && npc.controller != null)
                    npc.controller.location = __instance.location;
            }
            Game1.warpCharacter(npc, __instance.location, new Vector2(warp.TargetX, warp.TargetY));

            if (__instance.isPlayerPresent() && __instance.location.doors.ContainsKey(new Point(warp.X, warp.Y)))
                __instance.location.playSoundAt("doorClose", new Vector2(warp.X, warp.Y), NetAudio.SoundContext.NPC);
            if (__instance.isPlayerPresent() && __instance.location.doors.ContainsKey(new Point(warp.TargetX, warp.TargetY - 1)))
                __instance.location.playSoundAt("doorClose", new Vector2(warp.TargetX, warp.TargetY), NetAudio.SoundContext.NPC);
            if (__instance.pathToEndPoint.Count > 0)
                __instance.pathToEndPoint.Pop();
            while (__instance.pathToEndPoint.Count > 0 && (Math.Abs(__instance.pathToEndPoint.Peek().X - npc.getTileX()) > 1 || Math.Abs(__instance.pathToEndPoint.Peek().Y - npc.getTileY()) > 1))
                __instance.pathToEndPoint.Pop();

            return false;
        }

        /* GetFarmerParentId
         * Returns the parentId from the child given their NPC copy
         */
        public static long GetFarmerParentId(NPC npc)
        {
            List<Child> children = ModEntry.allChildren;
            if (children != null)
            {
                foreach (Child child in children)
                {
                    if (child.Name.Equals(npc.displayName))
                        return child.idOfParent.Value;
                }
            }

            return 0L;
        }
    }
}