using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Harmony;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Locations;
using StardewValley.Buildings;

namespace ChildToNPC
{
    /* To Do:
     * Let NPCs pathfind around the FarmHouse?
     * Let NPCs teleport to the spouse area, like spouses do?
     * Make gifts/talking configurable (how many points to talk, how many gifts per week) 
     * Multiple Dialogues in a day, like your spouse.
     * Fix children walking through walls, if possible.
     */

    /* ChildToNPC is a modding tool which converts a Child to an NPC 
     * for the purposes of creating Content Patcher mods.
     * 
     * ChildToNPC creates an NPC which is outwardly identical to your child
     * and removes your child from the farmhouse during the day, effectively replacing them.
     * 
     * This version of Child To NPC is compatible with SMAPI 3.0/Stadew Valley 1.4.
     */

    /* This mod makes use of IContentPatcherAPI
     * The Content Patcher API allows mods to make their own tokens
     * to be used in the Content Patcher content packs.
     * In this case, my custom tokens are for the identities of the children being patched.
     * This allows modders to get access to child data.
     * Because the tokens return null when that child isn't available,
     * the patches will not be applied at all when the child isn't present,
     * which prevents new NPCs from being wrongfully generated.
     */

    /* This mod makes use of Harmony and patches the following methods:
     * NPC.arriveAtFarmHouse
     * NPC.checkSchedule
     * NPC.parseMasterSchedule
     * NPC.performTenMinuteUpdate
     * NPC.prepareToDisembarkOnNewSchedulePath
     * PathfindController.handleWarps
     * (These methods should only trigger for custom NPCs)
     * 
     * FarmHouse.getChildren
     * (This method is always replaced)
     */
    
    class ModEntry : Mod
    {
        /* SMAPI provided */
        public static IMonitor monitor;
        public static IModHelper helper;

        /* The global config for this mod */
        public static ModConfig Config;
        /* The (configurable) age at which children become NPC's */
        public static int ageForCP;

        /* Data structures used by this mod */
        /* A list of all children in the household */
        public static List<Child> allChildren;
        /* A list of children in the household which become NPCs */
        public static Dictionary<string, Child> npcChildren;
        /* A dictionary of the NPC token info for modded children */
        public static List<ChildToken> npcTokens;
        /* A dictionary of the NPC copies of modded children */
        public static Dictionary<string, NPC> npcCopies;
        /* A dictionary of child name, parent name strings for each child */
        public static Dictionary<string, string> parents;

        /* Used for loading child data */
        public bool firstDay = true;
        /* Used for loading child CP patches */
        public bool updateNeeded = false;

        /* NPC name strings */
        /* Used to create the CP custom child tokens */
        private static readonly int MaxTokens = 4;
        public static readonly string[] tokens = { "FirstChild", "SecondChild", "ThirdChild", "FourthChild" };

        /* Entry - entry method for this SMAPI mod
         * 
         * Initializes variables and the config file, adds event handlers and harmony patches.
         */
        public override void Entry(IModHelper helper)
        {
            // initialize variables
            monitor = Monitor;
            ModEntry.helper = helper;

            allChildren = new List<Child>();
            npcChildren = new Dictionary<string, Child>(MaxTokens);
            npcTokens = new List<ChildToken>(MaxTokens);
            npcCopies = new Dictionary<string, NPC>(MaxTokens);
            parents = new Dictionary<string, string>();

            // load the config file (method will create default config if doesn't exist)
            Config = helper.ReadConfig<ModConfig>();
            ageForCP = Config.AgeWhenKidsAreModified;

            // add the modding commands if enabled by config
            if (Config.ModdingCommands)
            {
                /* The ModCommands class will handle registering and executing commands
                 * This allows me to put all the code for these commands in one place. */
                ModCommands commander = new ModCommands(monitor);
                commander.RegisterCommands(helper);
            }

            // disable everything but modding commands if enabled by config
            if (Config.ModdingDisable)
                return;

            // Event handlers
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.DayStarted += OnDayStarted;
            helper.Events.GameLoop.Saving += OnSaving;
            helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
            helper.Events.GameLoop.OneSecondUpdateTicked += OnOneSecondUpdateTicked;
            
            // Harmony
            HarmonyInstance harmony = HarmonyInstance.Create("Loe2run.ChildToNPC");
            harmony.Patch(
                original: AccessTools.Method(typeof(FarmHouse), nameof(FarmHouse.getChildren)),
                postfix: new HarmonyMethod(typeof(Patches.FarmHouseGetChildrenPatch), nameof(Patches.FarmHouseGetChildrenPatch.Postfix))
            );
            harmony.Patch(
                original: AccessTools.Method(typeof(NPC), nameof(NPC.performTenMinuteUpdate)),
                prefix: new HarmonyMethod(typeof(Patches.NPCPerformTenMinuteUpdatePatch), nameof(Patches.NPCPerformTenMinuteUpdatePatch.Prefix))
            );
            harmony.Patch(
                original: AccessTools.Method(typeof(NPC), nameof(NPC.arriveAtFarmHouse)),
                postfix: new HarmonyMethod(typeof(Patches.NPCArriveAtFarmHousePatch), nameof(Patches.NPCArriveAtFarmHousePatch.Postfix))
            );
            harmony.Patch(
                original: AccessTools.Method(typeof(NPC), nameof(NPC.checkSchedule)),
                prefix: new HarmonyMethod(typeof(Patches.NPCCheckSchedulePatch), nameof(Patches.NPCCheckSchedulePatch.Prefix))
            );
            harmony.Patch(
                original: AccessTools.Method(typeof(NPC), "parseMasterSchedule"),
                prefix: new HarmonyMethod(typeof(Patches.NPCParseMasterSchedulePatch), nameof(Patches.NPCParseMasterSchedulePatch.Prefix))
            );
            
            harmony.Patch(
                original: AccessTools.Method(typeof(NPC), "prepareToDisembarkOnNewSchedulePath"),
                postfix: new HarmonyMethod(typeof(Patches.NPCPrepareToDisembarkOnNewSchedulePathPatch), nameof(Patches.NPCPrepareToDisembarkOnNewSchedulePathPatch.Postfix))
            );
            harmony.Patch(
                original: AccessTools.Method(typeof(PathFindController), nameof(PathFindController.handleWarps)),
                prefix: new HarmonyMethod(typeof(Patches.PFCHandleWarpsPatch), nameof(Patches.PFCHandleWarpsPatch.Prefix))
            );
        }

        /* OnGameLaunched
         * This is where I set up the IContentPatcherAPI tokens.
         * Tokens are in the format of (Child Order)Child(Field)
         * I.e. The first child's name is FirstChildName,
         *      the third child's birthday is ThirdChildBirthday
         */
        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            // Get the Content Patcher API
            IContentPatcherAPI api = Helper.ModRegistry.GetApi<IContentPatcherAPI>("Pathoschild.ContentPatcher");
            if (api == null)
                return;

            // Register token for number of total children
            api.RegisterToken(ModManifest, "NumberTotalChildren", TokenGetTotalChildren);
            // Register token for the config "AgeWhenKidsAreModified" value
            api.RegisterToken(ModManifest, "ConfigAge", TokenGetConfigAge);
            // Register token for the config "DoChildrenWander" value
            api.RegisterToken(ModManifest, "ConfigWander", TokenGetConfigWander);
            // Register token for the config "DoChildrenHaveCurfew" value
            api.RegisterToken(ModManifest, "ConfigCurfew", TokenGetConfigCurfew);
            // Register token for the config "ConfigCurfewTime" value
            api.RegisterToken(ModManifest, "ConfigCurfewTime", TokenGetConfigCurfewTime);

            // Register Content Patcher custom tokens for children
            ChildToken token;
            for (int i = 0; i < MaxTokens; i++)
            {
                // Create token instance and add to the list
                token = new ChildToken(i + 1);
                npcTokens.Add(token);

                // Register CP tokens
                api.RegisterToken(ModManifest, tokens[i], token.GetChild);
                api.RegisterToken(ModManifest, tokens[i] + "Name", token.GetChildName);
                api.RegisterToken(ModManifest, tokens[i] + "Birthday", token.GetChildBirthday);
                api.RegisterToken(ModManifest, tokens[i] + "Gender", token.GetChildGender);
                api.RegisterToken(ModManifest, tokens[i] + "Parent", token.GetChildParent);
                api.RegisterToken(ModManifest, tokens[i] + "DaysOld", token.GetChildDaysOld);
                /* I intend for the bed token to be customizable, but I'm leaving it as no-input right now */
                api.RegisterToken(ModManifest, tokens[i] + "Bed", token.GetChildBed);
            }
        }

        /* OnDayStarted
         * 
         * Every morning, I check if there are children in the FarmHouse and remove them,
         * and I add their NPC dopplegangers to the FarmHouse.
         * This needs to be redone each day because NPC's are swapped back to child for saving.
         */
        private void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            // The farmhouse will be used for manipulating characters
            FarmHouse farmHouse = Utility.getHomeOfFarmer(Game1.player);
            
            // Initialize data structures on the first day this save file has been loaded
            if (firstDay)
            {
                InitializeDataStructures(farmHouse);
                firstDay = false;
            }
            // Update the lists based on newly born children or newly aged children
            else
                UpdateDataStructures(farmHouse);

            // Remove all children initially
            List<NPC> tempRemovedChildren = new List<NPC>();
            List<NPC> npcInHouse = farmHouse.characters.ToList();

            // Remove all children, to maintain list order
            foreach (NPC npc in npcInHouse)
            {
                if (npc is Child)
                {
                    farmHouse.getCharacters().Remove(npc);
                    // Add children who need to be returned to list
                    if (!npcChildren.ContainsValue(npc as Child))
                        tempRemovedChildren.Add(npc);
                }
            }

            // Replace children above the age threshold with an NPC copy
            foreach (Child child in npcChildren.Values)
            {
                // Generate a new NPC copy if one hasn't been made yet
                if (!npcCopies.ContainsKey(child.Name))
                {
                    // Create a new NPC copy
                    NPC newChildCopy = CreateChildNPC(child, farmHouse);

                    // Add child copy to the list of NPCs
                    npcCopies.Add(child.Name, newChildCopy);
                }

                // Get friendship for child before removal
                Friendship childFriendship = new Friendship(250);
                if (Game1.player.friendshipData.ContainsKey(child.Name))
                    childFriendship = Game1.player.friendshipData[child.Name];

                // Add copy to the farmhouse
                if (!npcCopies.TryGetValue(child.Name, out NPC childCopy))
                    Monitor.Log("Failed to find " + child.Name + " copy from npcCopies.", LogLevel.Debug);

                farmHouse.addCharacter(childCopy);

                // Add the friendship data
                if (!Game1.player.friendshipData.ContainsKey(childCopy.Name))
                    Game1.player.friendshipData.Add(childCopy.Name, childFriendship);

                // If children wander, have them start out of bed
                if (Config.DoChildrenWander)
                {
                    Point openPoint = farmHouse.getRandomOpenPointInHouse(Game1.random, 0, 60);
                    if (!openPoint.Equals(Point.Zero))
                        childCopy.setTilePosition(openPoint);
                    else
                        childCopy.Position = childCopy.DefaultPosition;
                }
                // Otherwise, they begin the day at the default position
                else
                    childCopy.Position = childCopy.DefaultPosition;
            }

            // Return the temporarily removed children to the end of the list, maintaining order
            foreach (NPC npc in tempRemovedChildren)
                farmHouse.addCharacter(npc);

            // Tells the OnOneSecondUpdate method to try and load CP data
            updateNeeded = true;
        }

        /* InitializeDataStructures
         * Initializes the values for data structures used by the mod,
         * run on the first day after the save is loaded.
         * The data structures initialized for this save: allChildren, parents, npcChildren, tokens in npcTokens.
         */
        private void InitializeDataStructures(FarmHouse farmHouse)
        {
            // Add children to the general child list
            foreach (Child child in farmHouse.getChildren())
                allChildren.Add(child);

            // Add children to the tracking dictionaries
            foreach (Child child in allChildren)
            {
                // Add parent information for each child, from config or default
                if (Config.ChildParentPairs.TryGetValue(child.Name, out string parentName))
                    parents.Add(child.Name, parentName);
                else if (Game1.player.spouse != null && Game1.player.spouse.Length > 0)
                    parents.Add(child.Name, Game1.player.spouse);
                else
                    parents.Add(child.Name, "Abigail");

                // Add children to the npc list if they meet the age limit
                /* I think children will be added to list in birth order? */
                if (child.daysOld >= ageForCP && npcChildren.Count < MaxTokens)
                    npcChildren.Add(child.Name, child);
            }

            // Initialize token values for children
            for (int i = 0; i < MaxTokens; i++)
            {
                npcTokens[i].InitializeChildToken();
            }
        }

        /* UpdateDataStructures
         * Updates the values for data structures used by the mod,
         * run on every day after the save is loaded except the first.
         * The data structures updated for this save: allChildren, parents, npcChildren, tokens in npcTokens.
         */
        private void UpdateDataStructures(FarmHouse farmHouse)
        {
            // This list will include all children, NPCs not yet added
            foreach (Child child in farmHouse.getChildren())
            {
                // Add new children to the children list
                if (!allChildren.Contains(child))
                {
                    allChildren.Add(child);

                    // Child is new, so add parent information, from config or default
                    if (Config.ChildParentPairs.TryGetValue(child.Name, out string parentName))
                        parents.Add(child.Name, parentName);
                    else if (Game1.player.spouse != null && Game1.player.spouse.Length > 0)
                        parents.Add(child.Name, Game1.player.spouse);
                    else
                        parents.Add(child.Name, "Abigail");
                }

                // Add children to the npc list if they meet the age limit
                if (npcChildren.Count < MaxTokens && !npcChildren.ContainsValue(child) && child.daysOld >= ageForCP)
                    npcChildren.Add(child.Name, child);

                /* TODO: Update only when a child becomes toddler, not every day */
                // Update token values for children
                for (int i = 0; i < MaxTokens; i++)
                {
                    // Re-initialize token values for children
                    if (!npcTokens[i].IsInitialized())
                        npcTokens[i].InitializeChildToken();
                    // Update token values for children
                    npcTokens[i].UpdateChildToken();
                }
            }
        }

        /* CreateChildNPC
         * Creates a new NPC for the given Child.
         * The NPC is based purely on the Child's appearance and data, without referencing any CP assets at this point.
         */
        private NPC CreateChildNPC(Child child, FarmHouse farmHouse)
        {
            // First NPC added to the list is 0, second is 1, etc.
            int npcIndex = npcCopies.Count;

            /* Based on StardewValley code from Game1 loadForNewGame */
            // Default position is the child's bed point, either from token or game default
            Point bedPoint = npcTokens[npcIndex].GetBedPoint(child);
            if (bedPoint.Equals(Point.Zero))
                bedPoint = farmHouse.getChildBed(child.Gender);
            Vector2 location = new Vector2(bedPoint.X * 64f, bedPoint.Y * 64f);

            /* new NPC(new AnimatedSprite("Characters\\George", 0, 16, 32), new Vector2(1024f, 1408f), "JoshHouse",
             * 0, "George", false, (Dictionary<int, int[]>) null, Game1.content.Load<Texture2D>("Portraits\\George")); */
            /* not datable, schedule null, portrait null */
            NPC newChildCopy = new NPC(child.Sprite, location, "FarmHouse", 2, tokens[npcIndex], false, null, null)
            {
                DefaultMap = Game1.player.homeLocation.Value,
                // DefaultPosition = location,
                Breather = false, // This should be true for adult-sized sprites, but false for toddler sized sprites
                HideShadow = false,
                displayName = child.Name,
                Gender = child.Gender
            };

            return newChildCopy;
        }

        /* OnOneSecondUpdateTicked
         * Triggers the child sprites to reload the NPC assets when the world is first loaded
         * to update them from the game's default to the custom CP patch.
         * 
         * TODO: This may be a race condition between Content Patcher and my mod, look into.
         */
        private void OnOneSecondUpdateTicked(object sender, OneSecondUpdateTickedEventArgs e)
        {
            // We only try to update the sprites after OnDayStarted finishes
            // and Content Patcher loads the patched data
            if (!updateNeeded || !Context.IsWorldReady)
                return;

            // Try to load custom assets for NPCs
            foreach (NPC copy in npcCopies.Values)
            {
                // Try to load a CP modded sprite for this NPC
                try
                {
                    string spriteName = "Characters\\" + copy.Name;
                    // helper.Content.Load<Texture2D>(spriteName, ContentSource.GameContent);
                    copy.Sprite = new AnimatedSprite(spriteName, 0, 16, 32);
                }
                catch (Exception ex)
                {
                    monitor.Log("Failed to load " + copy.Name + " sprite: " + ex.Message);
                }

                //Try to load DefaultPosition from dispositions
                try
                {
                    Dictionary<string, string> dispositions = Game1.content.Load<Dictionary<string, string>>("Data\\NPCDispositions");
                    if (dispositions.ContainsKey(copy.Name))
                    {
                        string[] defaultPosition = dispositions[copy.Name].Split('/')[10].Split(' ');
                        Vector2 position = new Vector2(int.Parse(defaultPosition[1]) * 64f, int.Parse(defaultPosition[2]) * 64f);

                        copy.DefaultPosition = position;
                    }
                }
                catch (Exception) { }
            }

            updateNeeded = false;
        }

        /* OnSaving
         * When the game saves overnight, I add the child back to the FarmHouse.characters list
         * so that if the mod is uninstalled, the child is returned properly.
         * Additionally, I remove the child copy NPC for the same reason.
         * If the mod is uninstalled, the new NPC shouldn't be in the save data.
         * 
         * I save the Friendship data for the generated NPC here.
         * Otherwise, exiting the game would reset gift data.
         */
        private void OnSaving(object sender, SavingEventArgs e)
        {
            // For adding children to the house
            FarmHouse farmHouse = Utility.getHomeOfFarmer(Game1.player);

            // Remove all children in order
            List<NPC> tempRemovedChildren = new List<NPC>();
            List<NPC> npcInHouse = farmHouse.characters.ToList();

            // Remove all children, to maintain list order
            foreach (NPC npc in npcInHouse)
            {
                if (npc is Child)
                {
                    farmHouse.getCharacters().Remove(npc);
                    if (!npcChildren.ContainsValue(npc as Child))
                        tempRemovedChildren.Add(npc);
                }
            }

            // Remove each childCopy from the world and replace with original child
            foreach (Child child in npcChildren.Values)
            {
                // An error for one NPC shouldn't prevent the others from being deleted
                try
                {
                    // Remove childcopy from save file first
                    if (npcCopies.TryGetValue(child.Name, out NPC childCopy))
                    {
                        // TODO: Utility.getGameLocationOfCharacter(NPC) ?
                        // Check all locations for NPC
                        foreach (GameLocation location in Game1.locations)
                        {
                            if (location.characters.Contains(childCopy))
                                location.getCharacters().Remove(childCopy);
                        }
                        // Check indoor locations for NPC (?)
                        foreach (BuildableGameLocation location in Game1.locations.OfType<BuildableGameLocation>())
                        {
                            foreach (Building building in location.buildings)
                            {
                                if (building.indoors.Value != null && building.indoors.Value.characters.Contains(childCopy))
                                    building.indoors.Value.getCharacters().Remove(childCopy);
                            }
                        }
                    }
                    // We don't want failure to remove NPC to prevent Child from being added to save data
                    else
                    {
                        Monitor.Log("OnSaving failed, NPC Child doesn't have a child copy: " + child.Name, LogLevel.Debug);
                    }

                    // Get friendship for NPC before removal
                    Friendship npcFriendship = new Friendship(250);
                    if (Game1.player.friendshipData.ContainsKey(childCopy.Name))
                        npcFriendship = Game1.player.friendshipData[childCopy.Name];

                    // Add the original child to the house again
                    if (!farmHouse.getCharacters().Contains(child))
                        farmHouse.addCharacter(child);
                    else
                        Monitor.Log("OnSaving failed, NPC Child already in farmhouse: " + child.Name, LogLevel.Debug);

                    // Add friendship for child
                    if (!Game1.player.friendshipData.ContainsKey(child.Name))
                        Game1.player.friendshipData.Add(child.Name, npcFriendship);
                }
                catch (Exception ex)
                {
                    Monitor.Log("OnSaving failed for child " + child.Name + ": " + ex.Message);
                }
            }

            // Return the other children to the house, adding at end of the list to maintain order
            foreach (NPC npc in tempRemovedChildren)
                farmHouse.addCharacter(npc);
        }

        /* OnReturnedToTitle
         * Returning to title and loading new save causes NPCs to load in the wrong save.
         * So this clears out the children list/copies dictionary on return to title.
         * (Children exist in the save data and NPCs don't,
         *  so this won't cause people to lose their children when reloading from save.)
         */
        private void OnReturnedToTitle(object sender, ReturnedToTitleEventArgs e)
        {
            // Delete saved token data
            foreach (ChildToken token in npcTokens)
                token.ClearToken();

            // Reset save game variables
            allChildren = new List<Child>();
            npcChildren = new Dictionary<string, Child>(MaxTokens);
            npcCopies = new Dictionary<string, NPC>(MaxTokens);
            parents = new Dictionary<string, string>();
            firstDay = true;
            updateNeeded = false;
        }

        /* TokenGetTotalChildren: used for the TotalChildren CP token
         * Returns the number of total children.
         */ 
        private IEnumerable<string> TokenGetTotalChildren()
        {
            return new[] { allChildren.Count.ToString() };
        }

        /* TokenGetConfigAge: used for the ConfigAge CP token
         * Returns the "AgeWhenKidsAreModified" value from the ChildToNPC config.json
         */ 
        private IEnumerable<string> TokenGetConfigAge()
        {
            return new[] { Config.AgeWhenKidsAreModified.ToString() };
        }

        /* TokenGetConfigWander: used for the ConfigWander CP token
         * Returns the "DoChildrenWander" value from the ChildToNPC config.json
         */
        private IEnumerable<string> TokenGetConfigWander()
        {
            return new[] { Config.DoChildrenWander ? "true" : "false" };
        }

        /* TokenGetConfigCurfew: used for the ConfigCurfew CP token
         * Returns the "DoChildrenHaveCurfew" value from the ChildToNPC config.json
         */
        private IEnumerable<string> TokenGetConfigCurfew()
        {
            return new[] { Config.DoChildrenHaveCurfew ? "true" : "false" };
        }

        /* TokenGetConfigCurfewTime: used for the ConfigCurfewTime CP token
         * Returns the "CurfewTime" value from the ChildToNPC config.json
         */
        private IEnumerable<string> TokenGetConfigCurfewTime()
        {
            return new[] { Config.CurfewTime.ToString() };
        }

        /* IsChildNPC
         * I only want to trigger Harmony patches when I'm applying the method to an NPC copy,
         * so this method verifies that the NPC in question is on my list.
         */
        public static bool IsChildNPC(Character c)
        {
            return npcCopies != null && npcCopies.ContainsValue(c as NPC);
        }

        public static bool IsChildNPC(NPC npc)
        {
            return npcCopies != null && npcCopies.ContainsValue(npc);
        }

        /* GetFarmerParentId
         * Returns the parentId from the child given their NPC/Character copy
         */
        public static long GetFarmerParentId(Character c)
        {
            foreach(Child child in allChildren)
            {
                if (child.Name.Equals(c.Name))
                    return child.idOfParent.Value;
            }
            return 0L;
        }

        /* GetChild
         * Returns the child by birth order from the npcChildren list
         */ 
        public static Child GetChild(int birthNumber)
        {
            if (allChildren != null && allChildren.Count >= birthNumber)
                return allChildren[birthNumber - 1];
            return null;
        }
    }
}

// (These are some notes for myself about pathfinding.)

/* How does pathfinding work?
 * --------------------------
 * At the very top, in the Game1 class, the game executes Game1.performTenMinuteClockUpdate()
 * Inside of this method, the game executes an important piece of code.
 * -> foreach (GameLocation location in (IEnumerable<GameLocation>) Game1.locations)
 * -> {
 * ->   location.performTenMinuteUpdate(Game1.timeOfDay);
 * ->   if (location is Farm)
 * ->     ((BuildableGameLocation) location).timeUpdate(10);
 * -> }
 * 
 * Important methods now:
 * GameLocation.performTenMinuteUpdate(Game1.timeOfDay)
 * BuildableGameLocation.timeUpdate(10);
 * 
 * GameLocation.performTenMinuteUpdate(Game1.timeOfDay)
 * ----------------------------------------------------
 * 
 * Inside of GameLocation.performTenMinuteUpdate(Game1.timeOfDay), we see this.
 * -> for (int index = 0; index < this.characters.Count; ++index)
 * -> {
 * ->   if (!this.characters[index].IsInvisible)
 * ->   {
 * ->     this.characters[index].checkSchedule(timeOfDay);
 * ->     this.characters[index].performTenMinuteUpdate(timeOfDay, this);
 * ->   }
 * -> }
 * 
 * So the GameLocation that a character is in finds that character, 
 * checks their schedule, then asks them to performTenMinuteUpdate.
 * 
 * NPC.checkSchedule(int timeOfDay)
 * ----------------------------
 * This method tries to get the schedule for this timeOfDay
 * and sets the PathfindController to the destination.
 * (It also handles what to do when a character is "running late")
 * 
 * An important method inside of checkSchedule is NPC.prepareToDisembarkOnNewSchedulePath();
 * This sets the temporary controller to escort the NPC out of the FarmHouse
 * and sets the controller and schedule to null if the character is on the Farm.
 * 
 * NPC.performTenMinuteUpdate(int timeOfDay, GameLocation l)
 * -------------------------------------
 * (I'm not super confident here, but it looks like NPC.performTenMinuteUpdate() only generates SayHi dialogue?)
 * 
 * BuildableGameLocation.timeUpdate(10)
 * ------------------------------------
 * 
 * After the GameLocations perform their tenMinuteUpdate, the Farm executes timeUpdate(10).
 * The Farm goes through every AnimalHouse is has, and executes for each animal
 * FarmAnimal.updatePerTenMinutes(Game1.timeOfDay, ...building.indoors);
 * 
 * ----------------------------------------------------------------------------------------------------------------
 * Another factor in what's going on is that when the day starts,
 * the game executes NPC.dayUpdate(int dayOfMonth) for each character.
 * 
 * In this method, the game runs NPC.getSchedule(int dayOfMonth) to set the NPC.Schedule/NPC.schedule.
 * NPC.getSchedule(int dayOfMonth)
 */
