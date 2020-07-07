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
    /* ChildToNPC is a modding tool which converts a Child to an NPC 
     * for the purposes of creating Content Patcher mods.
     * 
     * ChildToNPC creates an NPC which is outwardly identical to your child
     * and removes your child from the farmhouse during the day, effectively replacing them.
     * 
     * This version of Child To NPC was built with SMAPI 3.6/Stardew Valley 1.4.
     * (It may be compatible with other versions, I haven't checked yet.)
     */

    /* This mod makes use of IContentPatcherAPI
     * The Content Patcher API allows mods to make their own tokens for Content Patcher content packs.
     * In this case, my custom tokens are for the identities of the children being patched.
     * This allows modders to get access to child data.
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

    /* TODO: Fix clipping into crib somehow?
     * TODO: Make children more like spouse, with multiple dialogues and daily gifts and wandering around the house?
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
        /* A dictionary of the NPC token info for modded children */
        public static List<ChildToken> npcTokens;
        /* A list of all children in the household */
        public static List<Child> allChildren;
        /* A dictionary of child name, parent name strings for each child */
        public static Dictionary<string, string> parents;
        /* A dictionary of the NPC copies of modded children */
        public static Dictionary<string, NPC> npcCopies;

        /* Used for loading child CP patches */
        public bool updateNeeded = false;

        /* NPC name strings */
        /* Used to create the CP custom child tokens */
        private static readonly int MaxTokens = 4;
        public static readonly string[] tokens = { "FirstChild", "SecondChild", "ThirdChild", "FourthChild" };

        /* Entry - entry method for this SMAPI mod
         * 
         * Initializes variables, creates the config file, adds console commands by config,
         * adds event handlers and harmony patches by config.
         */
        public override void Entry(IModHelper helper)
        {
            // initialize variables
            monitor = Monitor;
            ModEntry.helper = helper;

            npcTokens = new List<ChildToken>(MaxTokens);
            allChildren = new List<Child>();
            parents = new Dictionary<string, string>();
            npcCopies = new Dictionary<string, NPC>(MaxTokens);

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
            HarmonyInstance harmony = HarmonyInstance.Create(ModManifest.UniqueID);
            // FarmHouse.getChildren patch (postfix)
            harmony.Patch(
                original: AccessTools.Method(typeof(FarmHouse), nameof(FarmHouse.getChildren)),
                postfix: new HarmonyMethod(typeof(Patches.FarmHouseGetChildrenPatch), nameof(Patches.FarmHouseGetChildrenPatch.Postfix))
            );
            // NPC.performTenMinuteUpdate patch (postfix)
            harmony.Patch(
                original: AccessTools.Method(typeof(NPC), nameof(NPC.performTenMinuteUpdate)),
                postfix: new HarmonyMethod(typeof(Patches.NPCPerformTenMinuteUpdatePatch), nameof(Patches.NPCPerformTenMinuteUpdatePatch.Postfix))
            );
            // NPC.arriveAtFarmHouse (postfix)
            harmony.Patch(
                original: AccessTools.Method(typeof(NPC), nameof(NPC.arriveAtFarmHouse)),
                postfix: new HarmonyMethod(typeof(Patches.NPCArriveAtFarmHousePatch), nameof(Patches.NPCArriveAtFarmHousePatch.Postfix))
            );
            // TODO: Make decision whether to keep checkSchedule patch
            /*
            harmony.Patch(
                original: AccessTools.Method(typeof(NPC), nameof(NPC.checkSchedule)),
                prefix: new HarmonyMethod(typeof(Patches.NPCCheckSchedulePatch), nameof(Patches.NPCCheckSchedulePatch.Prefix))
            );
            */
            // NPC.parseMasterSchedule patch (prefix)
            harmony.Patch(
                original: AccessTools.Method(typeof(NPC), "parseMasterSchedule"),
                prefix: new HarmonyMethod(typeof(Patches.NPCParseMasterSchedulePatch), nameof(Patches.NPCParseMasterSchedulePatch.Prefix))
            );
            // NPC.prepareToDisembarkOnNewSchedulePath patch (postfix)
            harmony.Patch(
                original: AccessTools.Method(typeof(NPC), "prepareToDisembarkOnNewSchedulePath"),
                postfix: new HarmonyMethod(typeof(Patches.NPCPrepareToDisembarkOnNewSchedulePathPatch), nameof(Patches.NPCPrepareToDisembarkOnNewSchedulePathPatch.Postfix))
            );
            // PathFindController.handleWarps patch (prefix)
            harmony.Patch(
                original: AccessTools.Method(typeof(PathFindController), nameof(PathFindController.handleWarps)),
                prefix: new HarmonyMethod(typeof(Patches.PFCHandleWarpsPatch), nameof(Patches.PFCHandleWarpsPatch.Prefix))
            );
            // NPC.shouldCollideWithBuildingLayer patch (postfix)
            harmony.Patch(
                original: AccessTools.Method(typeof(NPC), "shouldCollideWithBuildingLayer"),
                postfix: new HarmonyMethod(typeof(Patches.NPCShouldCollideWithBuildingLayerPatch), nameof(Patches.NPCShouldCollideWithBuildingLayerPatch.Postfix))
            );
        }

        /* OnGameLaunched - Handles mod integrations with Content Patcher.
         * 
         * This is where I set up the custom Content Patcher tokens for this mod.
         * 
         * Default Tokens: NumberTotalChildren, ConfigAge, ConfigCurfewTime
         * - NumberTotalChildren gives the number of children the current player has.
         * - The Config tokens give CP packs access to ChildToNPC's config.json info.
         * Child Tokens: Child, ChildName, ChildBirthday, ChildGender, ChildParent, ChildDaysOld, ChildBed
         * - These tokens have a copy created for each child: FirstChildName, SecondChildName, etc.
         * - Details on what these values hold are in the IContentPatcherAPI internal ChildToken class.
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
            // Register token for the config "CurfewTime" value
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
                api.RegisterToken(ModManifest, tokens[i] + "Bed", token.GetChildBed);
            }
        }

        /* OnDayStarted - Updates data structures, replaces Child with NPC daily.
         * 
         * First, updates data structures to look for new children, check new child ages, etc.
         * Then children with NPC copies have their copies added to the farmhouse.
         * 
         * NPCs are removed every day to avoid save data corruption, so this is repeated every day.
         */
        private void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            // The farmhouse will be used for manipulating characters
            FarmHouse farmHouse = Utility.getHomeOfFarmer(Game1.player);

            // Update data structures from world data
            UpdateDataStructures(farmHouse);

            // If there are any children above age threshold, replace with NPC copy
            if (npcCopies != null && npcCopies.Count > 0)
            {
                ReplaceChildNPC(farmHouse);

                // Tells the OnOneSecondUpdate method to try and load CP data
                updateNeeded = true;
            }
        }

        /* UpdateDataStructures
         * Updates the values for data structures used by the mod, run every day after the save is loaded.
         * The data structures updated for this save: allChildren, parents, tnpcCopies, tokens in npcTokens.
         */
        private void UpdateDataStructures(FarmHouse farmHouse)
        {
            // Add children to the general child list
            List<NPC> npcInHouse = farmHouse.getCharacters().ToList();
            foreach (NPC npc in npcInHouse)
            {
                if ((npc is Child) && (!allChildren.Contains(npc as Child)))
                {
                    // Add to the list of all children
                    Child child = npc as Child;
                    allChildren.Add(child);

                    // Child is new, so add parent information, from config or default
                    if (Config.ChildParentPairs.TryGetValue(child.Name, out string parentName))
                        parents.Add(child.Name, parentName);
                    else if (Game1.player.spouse != null && Game1.player.spouse.Length > 0)
                        parents.Add(child.Name, Game1.player.spouse);
                    else
                        parents.Add(child.Name, "Abigail");
                }
            }

            // Update token values for children
            for (int i = 0; i < MaxTokens; i++)
            {
                // Update token values for children
                if (npcTokens[i].IsInitialized())
                    npcTokens[i].UpdateChildToken();
            }

            // Generate new NPC copies and manage children above age limit
            foreach (Child child in allChildren)
            {
                // Check for new children above the age limit, if there's space for them
                if ((npcCopies.Count < MaxTokens) && (child.daysOld.Value >= ageForCP) && (!npcCopies.ContainsKey(child.Name)))
                {
                    // First NPC added to the list is 0, second is 1, etc.
                    int npcIndex = npcCopies.Count;

                    // Initialize the token values for this child
                    npcTokens[npcIndex].InitializeChildToken();

                    // Generate their NPC copy and add to the list of NPCs
                    npcCopies.Add(child.Name, CreateChildNPC(child, farmHouse, npcIndex));
                }

                // Children who are replaced won't be updated by the game, update for new day manually
                if (npcCopies.ContainsKey(child.Name))
                {
                    // This adds the child to the farmhouse, removal must happen after this
                    child.dayUpdate(Game1.dayOfMonth);
                }
            }
        }

        /* ReplaceChildNPC
         * Removes children over the age limit and replaces with their NPC copy.
         * Assumes that there is at least one child to be replaced.
         * 
         * Temporarily remove all children in order to maintain order for FarmHouse.characters list,
         * then adds normal children to the house again before finishing.
         * Copies child's friendship information to their NPC copy when replacing in farmhouse.
         */
        private void ReplaceChildNPC(FarmHouse farmHouse)
        {
            // To keep track of children under the age limit and add again at the end
            List<Child> tempRemovedChildren = new List<Child>();

            // Remove all children
            foreach (Child child in allChildren)
            {
                // Children without NPC are just temporarily removed
                if (!npcCopies.TryGetValue(child.Name, out NPC childCopy))
                {
                    // Save to be added again at the end
                    tempRemovedChildren.Add(child);

                    // Remove child from farmhouse
                    if (!farmHouse.getCharacters().Remove((NPC)child))
                    {
                        monitor.Log("Child " + child.Name + " wasn't on farmHouse.getCharacters() collection.", LogLevel.Error);
                        // TODO: Not sure if this is appropriate error handling?
                        tempRemovedChildren.Remove(child);
                    }
                }
                // Replace children with their NPC copy, including friendship info
                else
                {
                    // Save friendship from child before removal
                    Friendship childFriendship = new Friendship(250);

                    // If doesn't contain key, then player hasn't spoken to child before
                    if (Game1.player.friendshipData.ContainsKey(child.Name))
                    {
                        childFriendship = Game1.player.friendshipData[child.Name];
                        Game1.player.friendshipData.Remove(child.Name);
                    }

                    // Remove original child from farmhouse
                    if (!farmHouse.getCharacters().Remove((NPC)child))
                    {
                        monitor.Log("Child \"" + child.Name + "\" wasn't in FarmHouse, skipping NPC replacement.", LogLevel.Error);
                        // TODO: Not sure if it's appropriate error handling not to re-add friendship and child?
                    }
                    else
                    {
                        // Add copy to the farmhouse
                        farmHouse.addCharacter(childCopy);

                        // Add the friendship data
                        if (!Game1.player.friendshipData.ContainsKey(childCopy.Name))
                        {
                            Game1.player.friendshipData.Add(childCopy.Name, childFriendship);
                        }
                        // If copy friendship already in data, then corruption has occurred
                        else
                        {
                            monitor.Log("NPC Friendship found in data, removing & re-adding friendship for " + childCopy.Name, LogLevel.Error);
                            Game1.player.friendshipData.Remove(childCopy.Name);
                            Game1.player.friendshipData.Add(childCopy.Name, childFriendship);
                        }

                        // Children begin the day at their default position
                        if (Config.DoChildrenStartInBed)
                            childCopy.Position = childCopy.DefaultPosition;
                        else
                        {
                            // Children begin the day at a random point in the house
                            Point randomPoint = farmHouse.getRandomOpenPointInHouse(Game1.random, 0, 60);
                            if (!randomPoint.Equals(Point.Zero))
                            {
                                Vector2 randomPosition = new Vector2(randomPoint.X * 64f, randomPoint.Y * 64f);
                                childCopy.Position = randomPosition;
                            }
                            else
                                childCopy.Position = childCopy.DefaultPosition;
                        }
                    }
                }
            }

            // Return the temporarily removed children to the end of the list, maintaining order
            foreach (Child child in tempRemovedChildren)
                farmHouse.addCharacter(child);
        }

        /* CreateChildNPC
         * Creates a new NPC for the given Child.
         * The NPC is based purely on the Child's appearance and data, without referencing any CP assets at this point.
         */
        private NPC CreateChildNPC(Child child, FarmHouse farmHouse, int npcIndex)
        {
            // First NPC added to the list is 0, second is 1, etc.
            string name = tokens[npcIndex];

            /* Based on StardewValley code from Game1 loadForNewGame */
            // Default position is the child's bed point, either from token or game default
            Point bedPoint = GetBedPoint(npcIndex + 1, allChildren);
            // Try the game's built-in function if mine fails?
            if (bedPoint.Equals(Point.Zero))
                bedPoint = farmHouse.getChildBed(child.Gender);

            Vector2 location = new Vector2(bedPoint.X * 64f, bedPoint.Y * 64f);

            /* new NPC(new AnimatedSprite("Characters\\George", 0, 16, 32), new Vector2(1024f, 1408f), "JoshHouse",
             * 0, "George", false, (Dictionary<int, int[]>) null, Game1.content.Load<Texture2D>("Portraits\\George")); */
            // not datable, schedule null, portrait null
            NPC newChildCopy = new NPC(child.Sprite, location, "FarmHouse", 2, name, false, null, null)
            {
                DefaultMap = Game1.player.homeLocation.Value,
                DefaultPosition = location,
                Breather = false, // This should be true for adult-sized sprites, but false for toddler sized sprites
                HideShadow = false,
                displayName = child.Name,
                Gender = child.Gender
            };

            return newChildCopy;
        }

        /* OnSaving
         * When the game saves overnight, I add the child back to the FarmHouse.characters list
         * so that if the mod is uninstalled, the child is returned properly.
         * Additionally, I remove the child copy NPC for the same reason.
         * If the mod is uninstalled, the new NPC shouldn't be in the save data.
         * 
         * I save the Friendship data for the generated NPC here.
         * Otherwise, exiting the game would reset gift data.
         * 
         * If the NPC was out of the house at the time the game was saved, the farmHouse.characters list is no longer ordered.
         * Because the npcChildren list is ordered, doing npcChildren in order and then additional children will maintain order.
         *
         * I think it's possible for NPC children to be out of the house (not in the FarmHouse map) before this method runs?
         */
        private void OnSaving(object sender, SavingEventArgs e)
        {
            // Children are always in the house, NPCs may be somewhere else?
            FarmHouse farmHouse = Utility.getHomeOfFarmer(Game1.player);

            // Save friendship information for custom NPC's
            Dictionary<string, Friendship> tempFriendships = new Dictionary<string, Friendship>();
            foreach (KeyValuePair<string, NPC> pair in npcCopies)
            {
                if (Game1.player.friendshipData.TryGetValue(pair.Value.Name, out Friendship npcFriendship))
                {
                    // Save this friendship under the Child's name value
                    tempFriendships.Add(pair.Key, npcFriendship);
                    
                    // Remove from the save data
                    Game1.player.friendshipData.Remove(pair.Value.Name);
                }
            }

            // Remove custom NPC's from the farmhouse
            // TODO: Make this more efficient
            // Check all locations for NPC
            foreach (GameLocation location in Game1.locations)
            {
                // Check based on a potential NPC name, intentionally broad criteria
                foreach (string name in tokens)
                {
                    try
                    {
                        NPC customNPC = location.getCharacterFromName(name);
                        if (customNPC != null)
                            location.getCharacters().Remove(customNPC);
                    }
                    catch (Exception ex)
                    {
                        monitor.Log("Exception when removing custom NPCs: " + ex.Message, LogLevel.Error);
                    }
                }
            }
            // Check indoor locations for NPC (?)
            foreach (BuildableGameLocation location in Game1.locations.OfType<BuildableGameLocation>())
            {
                foreach (Building building in location.buildings)
                {
                    if (building.indoors.Value != null)
                    {
                        // Check based on a potential NPC name, intentionally broad criteria
                        foreach (string name in tokens)
                        {
                            try
                            {
                                NPC customNPC = building.indoors.Value.getCharacterFromName(name);
                                if (customNPC != null)
                                    building.indoors.Value.getCharacters().Remove(customNPC);
                            }
                            catch (Exception ex)
                            {
                                monitor.Log("Exception when removing custom NPCs: " + ex.Message, LogLevel.Error);
                            }
                        }
                    }
                }
            }

            // Temporarily remove all children from farmhouse
            foreach (Child child in allChildren)
            {
                if (farmHouse.getCharacters().Contains(child))
                    farmHouse.getCharacters().Remove(child);
            }

            // Add all children to the farmhouse in order, update friendship information if shared with NPC
            foreach (Child child in allChildren)
            {
                farmHouse.addCharacter(child);

                // Swap friendship information
                if (tempFriendships.TryGetValue(child.Name, out Friendship npcFriendship))
                {
                    if (!Game1.player.friendshipData.ContainsKey(child.Name))
                        Game1.player.friendshipData.Add(child.Name, npcFriendship);
                    else
                    {
                        monitor.Log("Child friendship already in data, removing & re-adding friendship for \"" + child.Name + "\"", LogLevel.Error);
                        Game1.player.friendshipData.Remove(child.Name);
                        Game1.player.friendshipData.Add(child.Name, npcFriendship);
                    }
                }
            }
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
                    monitor.Log("Failed to load Characters\\" + copy.Name + ": " + ex.Message, LogLevel.Trace);
                }

                // Try to load DefaultPosition from dispositions
                try
                {
                    Dictionary<string, string> dispositions = Game1.content.Load<Dictionary<string, string>>("Data\\NPCDispositions");
                    if (dispositions.ContainsKey(copy.Name))
                    {
                        string[] defaultPosition = dispositions[copy.Name].Split('/')[10].Split(' ');
                        Vector2 position = new Vector2(int.Parse(defaultPosition[1]) * 64f, int.Parse(defaultPosition[2]) * 64f);

                        copy.DefaultPosition = position;

                        // Update the default position for start of day
                        if (Config.DoChildrenStartInBed)
                            copy.Position = copy.DefaultPosition;
                    }
                }
                catch (Exception ex)
                {
                    monitor.Log("Failed to load Data\\NPCDispositions for " + copy.Name + ": " + ex.Message, LogLevel.Trace);
                }
            }

            updateNeeded = false;
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
            npcCopies = new Dictionary<string, NPC>(MaxTokens);
            parents = new Dictionary<string, string>();
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
        public static bool IsChildNPC(NPC npc)
        {
            return npcCopies != null && npcCopies.ContainsValue(npc);
        }

        /* GetChild
         * Returns the child by birth order from the allChildren list
         */ 
        public static Child GetChild(int birthNumber)
        {
            if (allChildren != null && allChildren.Count >= birthNumber)
                return allChildren[birthNumber - 1];
            return null;
        }

        /* GetBedPoint
         * Returns Bed information in the form of a Point (instead of string).
         * ChildNumber is in range [1, 2, 3, 4].
         */
        public Point GetBedPoint(int ChildNumber, List<Child> children)
        {
            if (children == null || children.Count < ChildNumber)
                return Point.Zero;

            // Get a count of who is sleeping in beds, who is in crib
            int toddler = 0;
            foreach (Child c in children)
            {
                if (c.Age >= 3)
                    toddler++;
            }

            /* Overview:
             * First child gets first bed. Second child gets second bed.
             * Once third is born, tries to share with first child.
             * (Siblings try to share with same gender siblings first.)
             * If they can't share, tries to share with second child.
             * If they can't share, third shares with first child anyway.
             * Once fourth is born, fills in the last open space.
             */
            Point bed1 = new Point(23, 5); // Right side of bed 1 (left bed)
            Point share1 = new Point(22, 5); // Left side of bed 1 (left bed)
            Point bed2 = new Point(27, 5); // Right side of bed 2 (right bed)
            Point share2 = new Point(26, 5); // Left side of bed 2 (right bed)

            if (ChildNumber == 1)
                return bed1; // Child1 always gets right side of bed 1

            if (toddler == 2)
                return bed2; // Child1 gets bed 1, Child2 gets bed 2

            // More than 2 kids and first two share gender
            if (children[0].Gender == children[1].Gender)
            {
                if (ChildNumber == 2)
                    return share1; // Child1 and Child2 share bed 1
                else if (ChildNumber == 3)
                    return bed2; // Child3 and Child4 share bed 2
                else
                    return share2;
            }
            // More than 2 kids and first two don't share gender
            if (ChildNumber == 2)
                return bed2; // Child1 gets bed 1, Child2 gets bed 2

            // More than 2 kids, Child1 and Child2 can't share, Child2 and Child3 can share
            if (children[1].Gender == children[2].Gender)
            {
                if (ChildNumber == 3)
                    return share2; // Child2 and Child3 share bed 2
                else
                    return share1; // Child1 and Child4 share bed 1
            }

            // More than 2 kids, Child1 and Child2 can't share, Child2 and Child3 can't share
            if (ChildNumber == 3)
                return share1; // Child1 and Child3 share bed 1

            return share2; // Child2 and Child4 share bed 2
        }
    }
}