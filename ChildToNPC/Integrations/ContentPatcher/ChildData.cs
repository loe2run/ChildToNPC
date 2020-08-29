namespace ChildToNPC.Integrations.ContentPatcher
{
    /// <summary>A snapshot of the token data for a child NPC.</summary>
    internal class ChildData
    {
        /*********
        ** Accessors
        *********/
        /// <summary>The child's name.</summary>
        public string Name { get; set; }

        /// <summary>The child's gender, like "male" or "female".</summary>
        public string Gender { get; set; }

        /// <summary>The child's birthday, like "spring 4".</summary>
        public string Birthday { get; set; }

        /// <summary>The name of the child's parent.</summary>
        public string Parent { get; set; }

        /// <summary>The child's bed position, like "FarmHouse 10 15" (location name and x/y position).</summary>
        public string Bed { get; set; }


        /*********
        ** Public methods
        *********/
        /// <summary>Get whether another instance has the same values as this one.</summary>
        /// <param name="other">The child data with which to compare.</param>
        public bool IsEquivalentTo(ChildData other)
        {
            return
                other != null
                && this.Name == other.Name
                && this.Gender == other.Gender
                && this.Birthday == other.Birthday
                && this.Parent == other.Parent
                && this.Bed == other.Bed;
        }
    }
}