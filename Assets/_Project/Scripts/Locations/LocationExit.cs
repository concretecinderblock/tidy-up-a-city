using TidyUp.Core;
using UnityEngine;

namespace TidyUp.Locations
{
    /// <summary>The way back out to the street, inside a location scene.</summary>
    public sealed class LocationExit : Interactable
    {
        public override string FocusLabel => "Back to the street";

        public override bool CanInteract => GameServices.Loader == null || !GameServices.Loader.IsBusy;

        public override void Interact(Interactor interactor)
        {
            if (GameServices.Loader != null && GameServices.Loader.IsInside)
            {
                GameServices.Loader.ExitCurrent();
                return;
            }
            GameServices.Announce("This is a test scene, so there's no street outside. Play City.unity for the full street.");
        }
    }
}
