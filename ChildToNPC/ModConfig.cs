using System.Collections.Generic;

namespace ChildToNPC
{
    /* ModConfig Options:
     * AgeWhenKidsAreModified: the age at which the CP mod will replace the child.
     *      The Default is currently 83, which is 14 newborn/14 baby/28 crawler/28 toddler,
     *      on the assumption that CP mods will make them a child.
     * CurfewTime: the time when children go to bed.
     * DoChildrenStartInBed: when true, children start the day in bed, otherwise at a random location in the house.
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
         * CurfewTime: the time of day when children will go to bed after returning home.
         * The default is 21:00, or 9:00 p.m., which is an hour before the time your spouse goes to bed (22:00, or 10:00 p.m.)
         */
        public int CurfewTime { get; set; }

        /*
         * DoChildrenStartInBed: when true, children start the day in bed. 
         * Otherwise, they are placed at a random location in the house at the start of the day.
         */
        public bool DoChildrenStartInBed { get; set; }

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
            CurfewTime = 2100; // 21:00 or 9:00 pm
            DoChildrenStartInBed = true;
            ChildParentPairs = new Dictionary<string, string>();
            ModdingCommands = false;
            ModdingDisable = false;
        }
    }
}