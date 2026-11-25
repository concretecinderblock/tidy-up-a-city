using TidyUp.Core;
using UnityEngine;

namespace TidyUp.Locations
{
    /// <summary>
    /// A brass button for the lift. In the car it sends the car on to the next floor; on a landing it calls the
    /// car to that floor.
    /// </summary>
    public sealed class ElevatorButton : Interactable
    {
        [SerializeField] private Elevator _elevator;
        [Tooltip("-1 inside the car (go to the next floor); otherwise the landing's floor (call the car here).")]
        [SerializeField] private int _floor = -1;

        public override string FocusLabel
        {
            get
            {
                if (_elevator == null) return "Lift";
                if (_elevator.IsMoving) return "The lift is moving…";
                if (_floor < 0)
                {
                    int next = _elevator.NextFloor;
                    return next == _elevator.Floor ? "Lift" : $"{(next > _elevator.Floor ? "Up" : "Down")} to the {_elevator.FloorName(next)}";
                }
                return _elevator.Floor == _floor ? "The lift is here" : "Call the lift";
            }
        }

        public override bool CanInteract => _elevator != null && !_elevator.IsMoving && (_floor < 0 || _elevator.Floor != _floor);

        public override void Interact(Interactor interactor)
        {
            if (_elevator == null) return;
            _elevator.GoTo(_floor < 0 ? _elevator.NextFloor : _floor);
        }

        internal void EditorConfigure(Elevator elevator, int floor)
        {
            _elevator = elevator;
            _floor = floor;
        }
    }
}
