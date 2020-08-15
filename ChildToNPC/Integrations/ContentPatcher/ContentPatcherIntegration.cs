using System;
using System.Globalization;
using StardewModdingAPI;
using StardewValley;

namespace ChildToNPC.Integrations.ContentPatcher
{
    /// <summary>Handles integrating with the Content Patcher API.</summary>
    internal class ContentPatcherIntegration
    {
        /*********
        ** Fields
        *********/
        /// <summary>The current mod's manifest.</summary>
        private readonly IManifest Manifest;

        /// <summary>The Content Patcher API, or <c>null</c> if Content Patcher isn't installed.</summary>
        private readonly IContentPatcherAPI ContentPatcher;

        /// <summary>The ordinal prefixes for child token names.</summary>
        private readonly string[] Ordinals = { "First", "Second", "Third", "Fourth" }; // I'm stopping at four for now. If FamilyPlanning expands past four, I'll need to come back to this.

        /// <summary>The game tick when the child data was last updated.</summary>
        private int CacheTick = -1;

        /// <summary>A snapshot of the child data as of the last context update.</summary>
        private ChildData[] Cache;


        /*********
        ** Public methods
        *********/
        /// <summary>Construct an instance.</summary>
        /// <param name="manifest">The current mod's manifest.</param>
        /// <param name="modRegistry">The SMAPI mod registry.</param>
        public ContentPatcherIntegration(IManifest manifest, IModRegistry modRegistry)
        {
            this.Manifest = manifest;
            this.ContentPatcher = modRegistry.GetApi<IContentPatcherAPI>("Pathoschild.ContentPatcher");
        }

        /// <summary>Register custom tokens with Content Patcher.</summary>
        public void RegisterTokens()
        {
            if (this.ContentPatcher == null)
                return;

            // aggregate tokens
            this
                .AddToken("NumberTotalChildren", () => this.Cache != null, () => this.Cache.Length.ToString(CultureInfo.InvariantCulture));

            // per-child tokens
            for (int i = 0; i < this.Ordinals.Length; i++)
            {
                string ordinal = this.Ordinals[i];
                int index = i;

                this
                    .AddToken($"{ordinal}ChildName", index, child => child.Name)
                    .AddToken($"{ordinal}ChildBirthday", index, child => child.Birthday)
                    .AddToken($"{ordinal}ChildBed", index, child => child.Bed)
                    .AddToken($"{ordinal}ChildGender", index, child => child.Gender)
                    .AddToken($"{ordinal}ChildParent", index, child => child.Parent);
            }
        }


        /*********
        ** Private methods
        *********/
        /// <summary>Register a token with Content Patcher.</summary>
        /// <param name="name">The token name.</param>
        /// <param name="isReady">Get whether the token is ready as of the last context update.</param>
        /// <param name="getValue">Get the token value as of the last context update.</param>
        private ContentPatcherIntegration AddToken(string name, Func<bool> isReady, Func<string> getValue)
        {
            this.ContentPatcher.RegisterToken(
                mod: this.Manifest,
                name: name,
                token: new ChildToken(
                    updateContext: this.UpdateContextIfNeeded,
                    isReady: isReady,
                    getValue: getValue
                )
            );

            return this;
        }

        /// <summary>Register a token with Content Patcher.</summary>
        /// <param name="name">The token name.</param>
        /// <param name="childIndex">The index of the child for which to add a token.</param>
        /// <param name="getValue">Get the token value.</param>
        private ContentPatcherIntegration AddToken(string name, int childIndex, Func<ChildData, string> getValue)
        {
            return this.AddToken(
                name: name,
                isReady: () => this.IsReady(childIndex),
                getValue: () =>
                {
                    ChildData child = this.GetChild(childIndex);
                    return child != null
                        ? getValue(child)
                        : null;
                }
            );
        }

        /// <summary>Get the cached data for a child.</summary>
        /// <param name="index">The child index.</param>
        private ChildData GetChild(int index)
        {
            if (this.Cache == null || index >= this.Cache.Length)
                return null;

            return this.Cache[index];
        }

        /// <summary>Get whether tokens for a given child should be marked ready.</summary>
        /// <param name="index">The child index.</param>
        private bool IsReady(int index)
        {
            return this.GetChild(index)?.Name != null;
        }

        /// <summary>Update all tokens for the current context.</summary>
        private bool UpdateContextIfNeeded()
        {
            // already updated this tick
            if (Game1.ticks == this.CacheTick)
                return false;
            this.CacheTick = Game1.ticks;

            // update context
            ChildData[] oldData = this.Cache;
            this.Cache = this.FetchNewData();
            return this.IsChanged(oldData, this.Cache);
        }

        /// <summary>Fetch the latest child data.</summary>
        private ChildData[] FetchNewData()
        {
            int count = ModEntry.GetTotalChildren();

            var data = new ChildData[count];
            for (int i = 0; i < count; i++)
            {
                data[i] = new ChildData
                {
                    Name = ModEntry.GetChildNPCName(i),
                    Gender = ModEntry.GetChildNPCGender(i),
                    Birthday = ModEntry.GetChildNPCBirthday(i),
                    Bed = ModEntry.GetBedSpot(i),
                    Parent = ModEntry.GetChildNPCParent(i)
                };
            }

            return data;
        }

        /// <summary>Get whether the cached data changed.</summary>
        /// <param name="oldData">The previous child data.</param>
        /// <param name="newData">The current child data.</param>
        private bool IsChanged(ChildData[] oldData, ChildData[] newData)
        {
            if (oldData == null || newData == null)
                return oldData != newData;

            if (oldData.Length != newData.Length)
                return true;

            for (int i = 0; i < oldData.Length; i++)
            {
                if (!oldData[i].IsEquivalentTo(newData[i]))
                    return true;
            }

            return false;
        }
    }
}
