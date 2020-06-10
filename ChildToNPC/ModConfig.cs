using System.Collections.Generic;

namespace ChildToNPC
{
    /* ModConfig Options:
     * AgeWhenKidsAreModified: the age at which the CP mod will replace the child.
     *      The Default is currently 83, which is 14 newborn/14 baby/28 crawler/28 toddler,
     *      on the assumption that CP mods will make them a child.
     * DoChildrenWander: when true, children wander around the house every hour (unless scheduled.)
     * DoChildrenHaveCurfew: when true, children will head home at curfew time (unless already walking somewhere.)
     * CurfewTime: the time of curfew (when DoChildrenHaveCurfew is true).
     * ChildParentPairs: A pair of names, the name of a child and the name of their NPC parent, to be used by CP mods.
     *      This allows you to have children with parents who aren't your spouse.
     * ModdingCommands: when true, adds commands which make it easier to generate new test children.
     */

    class ModConfig
    {
        /* 
         * AgeWhenKidsAreModified: the age at which the CP mod will replace the child.
         * The default is currently 83 days old, which is 14 newborn/14 baby/28 crawler/28 toddler,
         * on the assumption that CP mods will make them appear older.
         */
        public int AgeWhenKidsAreModified { get; set; }

        /*
         * DoChildrenWander: when true, children will wander around the house every hour,
         * unless they have a scheduled event to follow.
         */
        public bool DoChildrenWander { get; set; }
        
        /*
         * DoChildrenHaveCurfew: when true, children will head home at curfew time,
         * unless they are currently walking somewhere, then they will arrive there before heading home.
         */
        public bool DoChildrenHaveCurfew { get; set; }
        
        /*
         * CurfewTime: when DoChildrenHaveCurfew is true, this is the time of curfew.
         * The default is 2000 (20:00 or 8:00 pm).
         */ 
        public int CurfewTime { get; set; }

        /*
         * ChildParentPairs: A list of name pairs, name of child and name of their NPC parent, to be used by CP mods.
         * This allows you to have children with parents who aren't your spouse.
         * 
         * Example: "ChildParentPairs": { "Violet": "Shane" }
         * Even if you divorced Shane and he is no longer your spouse,
         * CP mods which support alternate parents can treat Violet as Shane's child instead of your spouse's child.
         */
        public Dictionary<string, string> ChildParentPairs { get; set; }
        
        /*
         * ModdingCommands: when true, adds console commands to make mod testing easier.
         */
        public bool ModdingCommands { get; set; }

        /* Temporary bool for testing purposes: enable commands but disables everything else */
        public bool ModdingDisable { get; set; }

        public ModConfig()
        {
            AgeWhenKidsAreModified = 83;
            DoChildrenWander = true;
            DoChildrenHaveCurfew = true;
            CurfewTime = 2000; // 20:00 == 8:00 pm
            ChildParentPairs = new Dictionary<string, string>();
            ModdingCommands = false;
            ModdingDisable = false;
        }
    }
}