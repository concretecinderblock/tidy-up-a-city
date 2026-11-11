using UnityEngine;

namespace TidyUp.Core
{
    /// <summary>
    /// Anything the player can use that isn't an item or a slot: memory fragments, NPCs, the Lost &amp; Found
    /// box. The Interactor shows FocusLabel under the reticle and calls Interact on the interact button.
    /// </summary>
    public abstract class Interactable : MonoBehaviour
    {
        public abstract string FocusLabel { get; }
        public virtual bool CanInteract => true;
        public abstract void Interact(Interactor interactor);
    }
}
