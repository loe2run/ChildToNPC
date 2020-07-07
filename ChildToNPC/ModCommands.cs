using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Locations;
using StardewModdingAPI;

namespace ChildToNPC
{
    class ModCommands
    {
        /* Given by the ModEntry class to access the console */
        public static IMonitor Monitor;

        /* The description and usage messages for the mod commands */
        private readonly string[] descripts = 
        { 
            "Adds a child to your home immediately.\n",
            "Removes the named child from the farm.\n",
            "Changes the age of the named child.\n"
        };
        private readonly string[] usage = 
        {
            "Usage: AddChild <name> <is male?> <is dark?>\n"
                + "- name: the name of the child to be generated.\n"
                + "- is male?: true for a male child, false otherwise.\n"
                + "- is dark?: true for a dark skinned sprite, false otherwise.",
            "Usage: RemoveChild <name>\n"
                + "- name: the name of the child to be removed.",
            "Usage: AgeChild <name> <days old>\n"
                + "- name: the name of the child to be aged.\n"
                + "- days old: the new age in days old;\n"
                + "  newborn is 0 to 13 days, baby is 14 to 27 days, "
                + "crawler is 28 to 55 days, and toddler is 56+ days."
        };

        /* Constructor - makes a copy of the mod's IMonitor */
        public ModCommands(IMonitor monitorIn)
        {
            Monitor = monitorIn;
        }

        /* RegisterCommands - registers the mod commands */
        public void RegisterCommands(IModHelper helper)
        {
            helper.ConsoleCommands.Add("AddChild", descripts[0] + usage[0], AddChild);
            helper.ConsoleCommands.Add("RemoveChild", descripts[1] + usage[1], RemoveChild);
            helper.ConsoleCommands.Add("AgeChild", descripts[2] + usage[2], AgeChild);
        }

        /* AddChild - adds a child to your household, independent of marriage status
         * 
         * A modding command which makes it easier to generate children.
         * Automatically adds a child to the household without marriage/pregnancy involved,
         * skipping straight to the child's birth.
         * 
         * The command uses the format "AddChild <string: Name> <bool: Is male?> <bool: Is dark?>".
         */
        public void AddChild(string command, string[] args)
        {
            // Check if it's safe to execute this command
            if (!IsCommandSafe())
                return;

            // Parse the command arguments input for creating the child
            if (args == null || args.Length < 3 || args[0].Length > 100
                || !bool.TryParse(args[1], out bool male) || !bool.TryParse(args[2], out bool dark))
            {
                Monitor.Log(usage[0], LogLevel.Info);
                return;
            }
            string name = args[0];

            // Check the name of the child doesn't match another child
            FarmHouse farmHouse = Utility.getHomeOfFarmer(Game1.player);
            List<Child> children = farmHouse.getChildren();
            foreach (Child child in children)
            {
                if (child.Name.Equals(name))
                {
                    Monitor.Log("You cannot give two children the same name.", LogLevel.Info);
                    return;
                }
            }

            /* The code below is based on the NamingEvent in Stardew Valley */
            // Begin the child creation event
            Game1.player.CanMove = false;
            Game1.fadeToBlackAlpha = 1f;

            // Verify the name of the child
            DisposableList<NPC> allCharacters = Utility.getAllCharacters();

            /* TODO: Guarantee the name won't match any other characters */
            foreach (Character character in allCharacters)
            {
                if (character.name.Equals(name))
                {
                    name += " ";
                    break;
                }
            }

            // Create the child
            Child baby = new Child(name, male, dark, Game1.player);
            baby.Age = 0;

            // Add child to home
            baby.Position = new Vector2(16f, 4f) * 64f + new Vector2(0.0f, -24f);
            farmHouse.addCharacter(baby);

            // End the event
            Game1.playSound("smallSelect");
            Game1.globalFadeToClear(null, 0.02f);
            Game1.player.CanMove = true;
        }

        /* RemoveChild - removes the named child from your household
         * 
         * A modding command which makes it easier to remove individual children.
         */
        public void RemoveChild(string command, string[] args)
        {
            // Check if it's safe to execute this command
            if (!IsCommandSafe())
                return;

            // Parse the input to the command
            if (args == null || args.Length < 1)
            {
                Monitor.Log(usage[1], LogLevel.Info);
                return;
            }
            string name = args[0];

            // Find the named child
            FarmHouse home = Utility.getHomeOfFarmer(Game1.player);
            NPC character = home.getCharacterFromName(name);
            if (character == null || !(character is Child))
            {
                Monitor.Log("Failed to find a child named " + name + ".", LogLevel.Info);
                return;
            }
            Child child = character as Child;

            // Begin the child removal event
            Game1.player.CanMove = false;
            Game1.fadeToBlackAlpha = 1f;

            /* The code below is based on GameLocation (evilShrineLeft) in Stardew Valley */
            // Remove the child from the farmhouse
            home.getCharacters().Remove(child);
            Monitor.Log(name + " has been removed.", LogLevel.Info);

            Game1.player.currentLocation.playSound("fireball");
            string message = Game1.content.LoadString("Strings\\Locations:WitchHut_Goodbye", name);
            Game1.showGlobalMessage(message);

            // End the event
            Game1.globalFadeToClear(null, 0.02f);
            Game1.player.CanMove = true;
        }

        /* AgeChild - controls the age of the named child
         * 
         * Changes the age of the named child to the given number of days old.
         * Appropriately updates the child sprite/tile location as well,
         * though I try to avoid changing location when updating age if possible.
         */
        public void AgeChild(string command, string[] args)
        {
            // Check if it's safe to execute this command
            if (!IsCommandSafe())
                return;

            // Parse the command arguments input for creating the child
            if (args == null || args.Length < 2 || (args[0].Length > 100) 
                || !int.TryParse(args[1], out int daysOld) || daysOld < 0)
            {
                Monitor.Log(usage[2], LogLevel.Info);
                return;
            }
            string name = args[0];

            /* The code below is based on Child (dayUpdate) in Stardew Valley */
            FarmHouse home = Utility.getHomeOfFarmer(Game1.player);
            NPC character = home.getCharacterFromName(name);
            if (character == null || !(character is Child))
            {
                Monitor.Log("Failed to find a child named " + name + ".", LogLevel.Info);
                return;
            }
            Child child = character as Child;

            // Begin the child update event
            Game1.player.CanMove = false;
            Game1.fadeToBlackAlpha = 1f;

            // Update the days old
            child.daysOld.Value = daysOld;

            // Update age and speed based on days old
            child.Speed = 1;
            if (daysOld < 13)
                child.Age = 0;
            else if (daysOld < 27)
                child.Age = 1;
            else if (daysOld < 55)
                child.Age = 2;
            else
            {
                child.Age = 3;
                child.Speed = 4;
            }

            // Update child position in house
            child.resetForNewDay(Game1.dayOfMonth);
            child.Sprite.CurrentAnimation = null;

            Vector2 cribPosition = new Vector2(16f, 4f) * 64f + new Vector2(0.0f, -24f);
            Point bedPoint = home.getChildBed(child.Gender);

            if (daysOld < 27)
                child.Position = cribPosition;
            // Update position to a random point if not already randomized
            else if (child.isInCrib() || child.getTileLocationPoint() == bedPoint)
            {
                /* From Child.dayUpdate method */
                Random random = new Random(Game1.Date.TotalDays
                                           + (int)Game1.uniqueIDForThisGame / 2
                                           + ((int)Game1.player.uniqueMultiplayerID * 2));
                // Try to put at random point
                Point openPoint = home.getRandomOpenPointInHouse(random, 1, 60);
                if (!openPoint.Equals(Point.Zero))
                    child.setTilePosition(openPoint);
                // The default point for age 2 and age 3 is different
                else if (daysOld < 55)
                    child.Position = cribPosition;
                else
                    child.setTilePosition(bedPoint);
            }

            // Reload the sprite to apply the age chance to appearance
            child.reloadSprite();
            child.resetForPlayerEntry(home);

            // End the event
            Game1.playSound("smallSelect");
            Game1.globalFadeToClear(null, 0.02f);
            Game1.player.CanMove = true;
        }

        /* IsCommandSafe - checks if it's safe to use the given command right now
         * 
         * Many of these commands have similar requirements, so this method will check for them.
         */
        private bool IsCommandSafe()
        {
            if (!Context.IsWorldReady)
            {
                Monitor.Log("The save must be fully loaded to use this command, please wait.", LogLevel.Info);
                return false;
            }
            if (Game1.farmEvent != null)
            {
                Monitor.Log("An event is in progress, please wait until it is finished to use this command.", LogLevel.Info);
                return false;
            }
            FarmHouse home = Utility.getHomeOfFarmer(Game1.player);
            if (Game1.player.currentLocation != home)
            {
                Monitor.Log("You must be at home to use this command, please wait.", LogLevel.Info);
                return false;
            }
            if (home.upgradeLevel < 2)
            {
                Monitor.Log("You need the appropriate house upgrade in order to use this command.", LogLevel.Info);
                return false;
            }

            return true;
        }
    }
}