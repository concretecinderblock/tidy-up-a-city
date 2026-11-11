using TidyUp.Core;
using UnityEngine;

namespace TidyUp.Locations
{
    /// <summary>A shop door on the street. Shows the location's progress and loads it on interact.</summary>
    public sealed class LocationDoor : Interactable
    {
        [SerializeField] private LocationDefinition _location;
        [Tooltip("Where the player stands when coming back out.")]
        [SerializeField] private Transform _returnPoint;

        public LocationDefinition Location => _location;
        public Transform ReturnPoint => _returnPoint;

        public override string FocusLabel
        {
            get
            {
                if (_location == null) return "Door";
                int placed = LocationStateStore.PlacedCount(_location.Id);
                int total = _location.TotalSlots;
                if (LocationStateStore.Get(_location.Id) == null) return $"Enter the {_location.DisplayName}";
                return placed >= total && total > 0
                    ? $"Enter the {_location.DisplayName}  ✓"
                    : $"Enter the {_location.DisplayName}  ·  {placed}/{total}";
            }
        }

        public override bool CanInteract => _location != null && GameServices.Loader != null && !GameServices.Loader.IsBusy;

        public override void Interact(Interactor interactor) => GameServices.Loader.Enter(_location, _returnPoint);

        internal void EditorConfigure(LocationDefinition location, Transform returnPoint)
        {
            _location = location;
            _returnPoint = returnPoint;
        }
    }
}
