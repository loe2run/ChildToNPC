using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Characters;

namespace ChildToNPC
{
    public interface IContentPatcherAPI
    {
        /*********
        ** Methods
        *********/
        /// <summary>Register a token.</summary>
        /// <param name="mod">The manifest of the mod defining the token (see <see cref="Mod.ModManifest"/> on your entry class).</param>
        /// <param name="name">The token name. This only needs to be unique for your mod; Content Patcher will prefix it with your mod ID automatically, like <c>Pathoschild.ExampleMod/SomeTokenName</c>.</param>
        /// <param name="getValue">A function which returns the current token value. If this returns a null or empty list, the token is considered unavailable in the current context and any patches or dynamic tokens using it are disabled.</param>
        void RegisterToken(IManifest mod, string name, Func<IEnumerable<string>> getValue);
    }

    /* ChildToken keeps track of the child's information for Content Patcher tokens.
     */
    internal class ChildToken
    {
        /* Used to track an individual child token */
        private readonly int ChildNumber;
        /* Has this token's values been successfully initialized? 
         * Tokens are registered before a save file is loaded,
         * so this signals whether the token holds any values yet. */
        private bool Initialized;

        /* Token values which are initialized once */
        private string NameNPC;
        private string Name;
        private string Birthday;
        private string Gender;
        private string Parent;

        /* Token values which need to be updated */
        private string DaysOld;
        private string Bed;

        /* Constructor - sets the ChildNumber, isn't initialized yet */ 
        public ChildToken(int childNumberIn)
        {
            ChildNumber = childNumberIn;
            Initialized = false;
        }

        /* InitializeChildToken - initialize token fields from game data
         * 
         * Attempts to initialize the token's data from child information.
         * Returns false on failure to initialize, Initialized bool remains false.
         * Otherwise, initializes all token fields and Initialized bool becomes true.
         */
        public bool InitializeChildToken()
        {
            // Initialize the token only if the child information is available
            Child child = ModEntry.GetChild(ChildNumber);
            if (child == null)
                return false;

            // Get the name of the child (npc name)
            NameNPC = ModEntry.tokens[ChildNumber-1];

            // Get the name of the child (display name) without added white space
            Name = child.displayName.Trim();

            // Get the gender of the child
            Gender = (child.Gender == 0) ? "male" : "female";

            // Get the parent's name for the child
            if (ModEntry.parents.TryGetValue(child.Name, out string parentName))
                Parent = parentName;
            else if (Game1.player.spouse != null && Game1.player.spouse.Length > 0)
                Parent = Game1.player.spouse;
            else
                Parent = "Abigail";
            
            // Get the birthday of the child
            SDate today = new SDate(Game1.dayOfMonth, Game1.currentSeason, Game1.year);
            SDate birthday = new SDate(1, "spring");
            try
            {
                birthday = today.AddDays(-child.daysOld.Value);
            }
            catch (ArithmeticException) { }
            Birthday = birthday.Season + " " + birthday.Day;

            // Get the current age (in days old) of the child
            DaysOld = child.daysOld.Value.ToString();

            // Get the current bed position for the child
            Bed = GetBed(child);

            // Successfully initialized the child data
            Initialized = true;
            return true;
        }

        /* Returns true if token was successfully initialized */
        public bool IsInitialized()
        {
            return Initialized;
        }

        /* UpdateChildToken - updates token fields from game data
         * 
         * Attempts to update the token's data from child information.
         * Returns false on failure to update.
         * Otherwise, updates changeable token fields (age, bed spot).
         */
        public void UpdateChildToken()
        {
            // Update the token only if the child information is available
            Child child = ModEntry.GetChild(ChildNumber);
            if (child == null)
                return;

            // Update the number of days old
            string prevDaysOld = DaysOld;
            DaysOld = child.daysOld.Value.ToString();

            if (prevDaysOld.Equals(DaysOld))
                ModEntry.monitor.Log("UpdateChildToken was called before any updates occurred.", LogLevel.Trace);

            // Update the bed position based on new siblings
            Bed = GetBed(child);
        }

        /* ClearToken - deletes cached token fields
         * 
         * Resets all token fields to null in preparation to re-initialize with a new save file.
         * Also sets the Initialized bool to false, token is no longer initialized.
         */
        public void ClearToken()
        {
            // Remove all data from this save file
            Initialized = false;
            NameNPC = null;
            Name = null;
            Birthday = null;
            Gender = null;
            Parent = null;
            DaysOld = null;
            Bed = null;
        }

        /* GetBedPoint
         * Returns Bed information in the form of a Point (instead of string).
         * Requires that Context.IsWorldReady and child is not null.
         * Returns Point.Zero on failure, a non-zero point on success.
         */
        public Point GetBedPoint(Child child)
        {
            if (child.Age < 3)
                return Point.Zero;

            int toddler = 0;

            //Children above the age check + children below the age check
            List<Child> children = ModEntry.allChildren;
            if (children == null)
                return Point.Zero;

            // Get a count of who is sleeping in beds, who is in crib
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

        /* GetBed
         * Converts the Point value from GetBedPoint to a string "FarmHouse X Y"
         */
        private string GetBed(Child child)
        {
            Point bedPoint = GetBedPoint(child);
            if (bedPoint.Equals(Point.Zero))
                return null;
            return "FarmHouse " + bedPoint.X + " " + bedPoint.Y;
        }

        /* GetChildNPC:
         * Returns the NPC's name of the numbered child
         */ 
        public IEnumerable<string> GetChild()
        {
            if (NameNPC != null)
                return new[] { NameNPC };
            return null;
        }

        /* GetChildName:
         * Returns the name of the numbered child
         */
        public IEnumerable<string> GetChildName()
        {
            if (Name != null)
                return new[] { Name };
            return null;
        }

        /* GetChildBirthday:
         * Returns the birthday of the numbered child (Spring 1 if out of bounds)
         */
        public IEnumerable<string> GetChildBirthday()
        {
            if (Birthday != null)
                return new[] { Birthday };
            return null;
        }

        /* GetChildGender:
         * Returns the gender "male" or "female" of the numbered child
         */
        public IEnumerable<string> GetChildGender()
        {
            if (Gender != null)
                return new[] { Gender };
            return null;
        }

        /* GetChildParent:
         * Returns the name of the parent, by config or by default
         */
        public IEnumerable<string> GetChildParent()
        {
            if (Parent != null)
                return new[] { Parent };
            return null;
        }

        /* GetChildBed: used to get child's bed location
         * Returns the age of the child in days old.
         */
        public IEnumerable<string> GetChildDaysOld()
        {
            if (DaysOld != null)
                return new[] { DaysOld };
            return null;
        }

        /* GetChildBed:
         * Returns the bed location in the form of "FarmHouse x y"
         * I'm not entirely sure how this will go for non-toddlers, I'll return null for now.
         */
        public IEnumerable<string> GetChildBed()
        {
            if (Bed != null)
                return new[] { Bed };
            return null;
        }
    }
}