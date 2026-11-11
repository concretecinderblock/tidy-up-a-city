namespace TidyUp.Items
{
    /// <summary>
    /// Something that can close a slot to new items, such as a dusty shelf that must be cleaned first
    /// (family K). Slots stay closed while IsOpen is false; items already in them stay put.
    /// </summary>
    public interface ISlotGate
    {
        bool IsOpen { get; }
        /// <summary>Shown when the player tries to place into a closed slot.</summary>
        string ClosedMessage { get; }
    }
}
