using System.Collections;
using TidyUp.Core;
using UnityEngine;

namespace TidyUp.Locations
{
    /// <summary>
    /// A lift between the floors of a mega location (Jesse 2026-11-25): an open cage car that glides up and down
    /// its shaft, carrying the player if they're in it. Each floor has a gate across the shaft that is open only
    /// while the car stands there, so nobody walks into an empty shaft. Buttons (ElevatorButton) call the car to a
    /// floor or send it on. Movement is eased and takes a few seconds; you can still look around on the way.
    /// </summary>
    public sealed class Elevator : MonoBehaviour
    {
        [SerializeField] private Transform _car;
        [Tooltip("Car height (world Y of its floor) at each stop, lowest first.")]
        [SerializeField] private float[] _stops = { 0f };
        [Tooltip("A gate per stop (visual + collider), shown while the car is elsewhere.")]
        [SerializeField] private GameObject[] _gates = System.Array.Empty<GameObject>();
        [Tooltip("The car's floor area (local to the car) that counts as standing in it.")]
        [SerializeField] private Vector2 _carSize = new Vector2(2.4f, 2.4f);
        [SerializeField] private float _speed = 1.8f; // metres per second at full speed
        [SerializeField] private string[] _floorNames = { "Ground floor" };

        private int _floor;
        private bool _moving;

        public int Floor => _floor;
        public bool IsMoving => _moving;
        public int StopCount => _stops.Length;
        public string FloorName(int floor) => floor >= 0 && floor < _floorNames.Length ? _floorNames[floor] : $"floor {floor + 1}";

        /// <summary>The floor the car goes to next from the inside button: up until the top, then back down.</summary>
        public int NextFloor => _stops.Length < 2 ? _floor : _floor + 1 < _stops.Length ? _floor + 1 : _floor - 1;

        private void Start()
        {
            if (_car != null && _stops.Length > 0)
            {
                Vector3 p = _car.position;
                _car.position = new Vector3(p.x, _stops[0], p.z);
            }
            _floor = 0;
            RefreshGates();
        }

        /// <summary>Calls the car to a floor (and takes the player along if they're in it).</summary>
        public void GoTo(int floor)
        {
            if (_moving || _car == null || floor < 0 || floor >= _stops.Length || floor == _floor) return;
            StartCoroutine(Ride(floor));
        }

        private IEnumerator Ride(int target)
        {
            _moving = true;
            SetAllGates(closed: true);
            FirstPersonController rider = PlayerInCar() ? GameServices.PlayerController : null;
            if (rider != null) rider.BeginRide();

            float from = _car.position.y, to = _stops[target];
            float duration = Mathf.Max(1.5f, Mathf.Abs(to - from) / _speed);
            float time = 0f, lastY = from;
            // Riding to another floor of a mega location: partway up, the screen dims while that floor's rooms load
            // and the floor left behind unloads (only one floor is in memory at a time).
            MegaLocation building = MegaLocation.Current;
            bool switchFloor = rider != null && building != null && building.CurrentFloor != target;
            while (time < duration)
            {
                if (switchFloor && time >= duration * 0.35f)
                {
                    switchFloor = false;
                    Vector3 arrival = new Vector3(_car.position.x, _stops[target], _car.position.z);
                    IEnumerator work = building.SwitchFloor(target, arrival);
                    string note = target > _floor ? $"Going up to the {FloorName(target)}…" : $"Going down to the {FloorName(target)}…";
                    if (GameServices.Loader != null) yield return GameServices.Loader.Cover(work, note);
                    else yield return work;
                }
                time += Mathf.Min(Time.deltaTime, 0.05f); // no jump after a loading frame
                float y = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, time / duration));
                MoveCar(y - lastY, rider);
                lastY = y;
                yield return null;
            }
            MoveCar(to - lastY, rider);

            _floor = target;
            if (rider != null) rider.EndRide();
            _moving = false;
            RefreshGates();
            if (rider != null) GameServices.Announce(FloorName(target));
        }

        private void MoveCar(float dy, FirstPersonController rider)
        {
            if (Mathf.Approximately(dy, 0f)) return;
            _car.position += new Vector3(0f, dy, 0f);
            if (rider != null) rider.transform.position += new Vector3(0f, dy, 0f);
        }

        /// <summary>Is the player standing on the car's floor?</summary>
        public bool PlayerInCar()
        {
            Transform player = GameServices.Player;
            if (player == null || _car == null) return false;
            Vector3 local = _car.InverseTransformPoint(player.position);
            return Mathf.Abs(local.x) <= _carSize.x * 0.5f && Mathf.Abs(local.z) <= _carSize.y * 0.5f && local.y > -0.6f && local.y < 2.2f;
        }

        private void RefreshGates()
        {
            for (int i = 0; i < _gates.Length; i++)
                if (_gates[i] != null) _gates[i].SetActive(i != _floor);
        }

        private void SetAllGates(bool closed)
        {
            for (int i = 0; i < _gates.Length; i++)
                if (_gates[i] != null) _gates[i].SetActive(closed);
        }

        internal void EditorConfigure(Transform car, float[] stops, GameObject[] gates, Vector2 carSize, string[] floorNames)
        {
            _car = car;
            _stops = stops;
            _gates = gates;
            _carSize = carSize;
            _floorNames = floorNames;
        }
    }
}
