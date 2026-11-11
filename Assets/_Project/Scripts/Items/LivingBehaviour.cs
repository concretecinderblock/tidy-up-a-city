namespace TidyUp.Items
{
    /// <summary>Living item behaviours (mechanic family I). Set per item in the CSV ("living" column).</summary>
    public enum LivingBehaviour
    {
        None = 0,
        /// <summary>Hops away when the player gets close; tires after a few hops and can be caught.</summary>
        Skittish = 1,
        /// <summary>Drifts slowly through the air while loose.</summary>
        Floating = 2,
        /// <summary>Only a faint shimmer until the player comes close.</summary>
        Shy = 3,
        /// <summary>Looks and reads as something else until looked at or grabbed.</summary>
        Disguised = 4
    }

    /// <summary>
    /// Hook between a TidyItem and its living behaviour, so Core (Interactor) never depends on the
    /// behaviour implementation.
    /// </summary>
    public interface ILivingBehaviour
    {
        /// <summary>Name to show instead of the real one (disguise, shy shimmer), or null.</summary>
        string DisplayNameOverride { get; }

        /// <summary>Increments when the shown name changes, so the reticle label can refresh.</summary>
        int LabelVersion { get; }

        /// <summary>Called on pick-up attempts. Return false to slip away; message explains what happened.</summary>
        bool TryCatch(out string message);

        /// <summary>Called every frame the item is under the reticle.</summary>
        void OnGazed(float deltaTime);

        void OnStateChanged(ItemState state);
    }
}
