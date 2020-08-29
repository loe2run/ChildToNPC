using StardewModdingAPI;

namespace ChildToNPC.Integrations.ContentPatcher
{
    /// <summary>The interface provided by the Content Patcher API.</summary>
    public interface IContentPatcherAPI
    {
        /*********
        ** Methods
        *********/
        /// <summary>Register a complex token. This is an advanced API; only use this method if you've read the documentation and are aware of the consequences.</summary>
        /// <param name="mod">The manifest of the mod defining the token (see <see cref="Mod.ModManifest"/> on your entry class).</param>
        /// <param name="name">The token name. This only needs to be unique for your mod; Content Patcher will prefix it with your mod ID automatically, like <c>YourName.ExampleMod/SomeTokenName</c>.</param>
        /// <param name="token">An arbitrary class with one or more methods matching the conventions recognized by Content Patcher.</param>
        void RegisterToken(IManifest mod, string name, object token);
    }
}
