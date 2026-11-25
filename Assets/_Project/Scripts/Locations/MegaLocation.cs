using System;
using System.Collections;
using System.Collections.Generic;
using TidyUp.Core;
using TidyUp.Items;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace TidyUp.Locations
{
    /// <summary>One room of a mega location: its own additive scene, streamed in and out as the player moves.</summary>
    [Serializable]
    public sealed class ZoneInfo
    {
        public string Id;
        public string DisplayName;
        [Tooltip("Addressables address of the zone's scene.")]
        public string Address;
        [Tooltip("Floor rectangle in world XZ (x, z, width, depth), for streaming and the map.")]
        public Rect Area;
        [Tooltip("Slots in the zone (recorded by the builder), so the grand total is known before a room is visited.")]
        public int TotalSlots;
        public Color MapColor = new Color(0.9f, 0.8f, 0.65f);
        [Tooltip("Which floor the room is on (0 = ground floor).")]
        public int Floor;
    }

    /// <summary>
    /// The shell of a mega location (Jesse 2026-11-13): the building itself (walls, floors, doorways, light) loads
    /// with the location, and each room (zone) is a separate scene that streams in when the player comes near
    /// and out when they're far away, so a location can be far bigger than what's in memory at once.
    /// Every zone's slots count towards the location's grand total. The zone the player stands in is the
    /// active location (HUD checklist, Tidy Sense, hints follow it). A zone is never unloaded while the player
    /// carries one of its items (items can't leave their scene).
    /// </summary>
    public sealed class MegaLocation : MonoBehaviour
    {
        [SerializeField] private LocationDefinition _location;
        [SerializeField] private ZoneInfo[] _zones = Array.Empty<ZoneInfo>();
        [SerializeField] private Transform _entryPoint;
        [SerializeField] private Transform _dropPoint;
        [Tooltip("Rooms load when the player is this close to them (metres from the room's edge).")]
        [SerializeField] private float _loadDistance = 20f;
        [Tooltip("Extra distance before a loaded room unloads again (so it doesn't flicker at the edge).")]
        [SerializeField] private float _unloadHysteresis = 8f;
        [Tooltip("A floor with up to this many items loads every room at once and keeps them (no load or " +
                 "unload hitches while walking). Bigger floors stream rooms by distance. Other floors load with the lift.")]
        [SerializeField] private int _keepAllLoadedUpTo = 30000;
        [Tooltip("Outline of the floor for the map (world XZ).")]
        [SerializeField] private Rect _footprint;
        [Tooltip("Doorways between rooms (world XZ centre), drawn on the map.")]
        [SerializeField] private Vector2[] _doorways = Array.Empty<Vector2>();
        [Tooltip("Height of each floor's walking surface (index = floor), lowest first.")]
        [SerializeField] private float[] _floorHeights = { 0f };
        [Tooltip("Floor names for the map, lowest first.")]
        [SerializeField] private string[] _floorNames = { "Ground floor" };
        [Tooltip("Lift shafts (world XZ), drawn on the map on every floor.")]
        [SerializeField] private Rect[] _lifts = Array.Empty<Rect>();

        public IReadOnlyList<Rect> Lifts => _lifts;

        private enum ZoneState { Unloaded, Loading, Loaded, Unloading }

        private sealed class Zone
        {
            public ZoneInfo Info;
            public ZoneState State;
            public AsyncOperationHandle<SceneInstance> Handle;
            public LocationRuntime Runtime;
        }

        private Zone[] _runtime = Array.Empty<Zone>();
        private int _activeZone = -1;
        private float _nextStreamCheck;
        private bool _wasComplete;
        private bool _shuttingDown;

        /// <summary>The mega location the player is in, or null.</summary>
        public static MegaLocation Current { get; private set; }
        public static event Action<MegaLocation> CurrentChanged;

        public LocationDefinition Location => _location;
        public IReadOnlyList<ZoneInfo> Zones => _zones;
        public Transform EntryPoint => _entryPoint;
        public Rect Footprint => _footprint;
        public IReadOnlyList<Vector2> Doorways => _doorways;
        /// <summary>Index of the zone the player stands in (-1 before they're in one).</summary>
        public int ActiveZone => _activeZone;

        /// <summary>Raised when the player walks into another room.</summary>
        public event Action<MegaLocation> ActiveZoneChanged;
        /// <summary>Raised (at most a few times a second) when the grand total may have changed.</summary>
        public event Action<MegaLocation> TotalsChanged;
        /// <summary>Raised once when every room is tidy.</summary>
        public event Action<MegaLocation> Completed;

        public int Total
        {
            get
            {
                int total = 0;
                for (int i = 0; i < _zones.Length; i++) total += _zones[i].TotalSlots;
                return total;
            }
        }

        public int Placed
        {
            get
            {
                int placed = 0;
                for (int i = 0; i < _zones.Length; i++) placed += ZonePlaced(i);
                return placed;
            }
        }

        public float CompletionRatio => Total == 0 ? 0f : (float)Placed / Total;
        public bool IsComplete => Total > 0 && Placed >= Total;

        public string StateKey(int zone) => ZoneKey(_location != null ? _location.Id : name, _zones[zone].Id);
        public static string ZoneKey(string locationId, string zoneId) => locationId + "/" + zoneId;

        /// <summary>Placed items in a zone: live when it's loaded, otherwise from the session store.</summary>
        public int ZonePlaced(int zone)
        {
            Zone z = _runtime.Length > zone ? _runtime[zone] : null;
            if (z != null && z.Runtime != null && z.Runtime.Progress != null && z.Runtime.Progress.Total > 0) return z.Runtime.Progress.Placed;
            return LocationStateStore.PlacedCountExact(StateKey(zone));
        }

        public bool ZoneVisited(int zone) => LocationStateStore.Get(StateKey(zone)) != null;
        public bool ZoneLoaded(int zone) => _runtime.Length > zone && _runtime[zone].State == ZoneState.Loaded;

        /// <summary>The loaded room's runtime (its fixtures, progress), or null while it isn't loaded.</summary>
        public LocationRuntime ZoneRuntime(int zone) => _runtime.Length > zone && _runtime[zone].State == ZoneState.Loaded ? _runtime[zone].Runtime : null;

        /// <summary>The zone containing a world position, or -1.</summary>
        public int ZoneAt(Vector3 position)
        {
            var p = new Vector2(position.x, position.z);
            int floor = FloorAt(position.y);
            for (int i = 0; i < _zones.Length; i++)
                if (_zones[i].Floor == floor && _zones[i].Area.Contains(p)) return i;
            return -1;
        }

        public int FloorCount => _floorHeights.Length;
        public float FloorHeight(int floor) => _floorHeights[Mathf.Clamp(floor, 0, _floorHeights.Length - 1)];
        public string FloorName(int floor) => floor >= 0 && floor < _floorNames.Length ? _floorNames[floor] : $"Floor {floor + 1}";

        /// <summary>The floor a height belongs to: the highest floor whose surface is at most half a metre above it.</summary>
        public int FloorAt(float y)
        {
            int floor = 0;
            for (int i = 1; i < _floorHeights.Length; i++)
                if (y >= _floorHeights[i] - 0.5f) floor = i;
            return floor;
        }

        private void Awake()
        {
            _runtime = new Zone[_zones.Length];
            for (int i = 0; i < _zones.Length; i++) _runtime[i] = new Zone { Info = _zones[i] };
        }

        private void OnEnable()
        {
            Current = this;
            CurrentChanged?.Invoke(this);
        }

        private void OnDisable()
        {
            if (Current != this) return;
            Current = null;
            CurrentChanged?.Invoke(null);
        }

        private void Start()
        {
            _wasComplete = IsComplete;
            TotalsChanged?.Invoke(this);
        }

        private void Update()
        {
            if (_shuttingDown) return;
            Transform player = GameServices.Player;
            if (player == null) return;

            // Which room is the player in? (Doorways belong to one side; keep the last room in between.)
            int zone = ZoneAt(player.position);
            if (zone >= 0 && zone != _activeZone)
            {
                _activeZone = zone;
                Activate(zone);
                ActiveZoneChanged?.Invoke(this);
            }

            if (Time.unscaledTime < _nextStreamCheck) return;
            _nextStreamCheck = Time.unscaledTime + 0.25f;
            Stream(player.position);

            TotalsChanged?.Invoke(this);
            bool complete = IsComplete;
            if (complete && !_wasComplete) Completed?.Invoke(this);
            _wasComplete = complete;
        }

        /// <summary>
        /// Loads rooms near the player, unloads rooms far away (but never one whose items are carried). Only rooms on
        /// the current floor: another floor loads when the lift takes the player there (SwitchFloor). A floor small
        /// enough to keep whole stays loaded as a whole.
        /// </summary>
        private void Stream(Vector3 position)
        {
            if (KeepFloorLoaded(_currentFloor)) return; // the whole floor is already in
            var p = new Vector2(position.x, position.z);
            for (int i = 0; i < _runtime.Length; i++)
            {
                Zone z = _runtime[i];
                if (z.Info.Floor != _currentFloor) continue;
                float distance = DistanceToRect(z.Info.Area, p);
                if (distance <= _loadDistance && z.State == ZoneState.Unloaded) StartCoroutine(LoadZone(z));
                else if (distance > _loadDistance + _unloadHysteresis && z.State == ZoneState.Loaded && i != _activeZone && !CarriesFrom(z))
                    StartCoroutine(UnloadZone(z));
            }
        }

        private static float DistanceToRect(Rect r, Vector2 p)
        {
            float dx = Mathf.Max(r.xMin - p.x, 0f, p.x - r.xMax);
            float dy = Mathf.Max(r.yMin - p.y, 0f, p.y - r.yMax);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        private static bool CarriesFrom(Zone z)
        {
            PlayerCarry carry = GameServices.Carry;
            if (carry == null || !z.Handle.IsValid()) return false;
            Scene scene = z.Handle.Result.Scene;
            IReadOnlyList<TidyItem> items = carry.Inventory.Items;
            for (int i = 0; i < items.Count; i++)
                if (items[i] != null && items[i].gameObject.scene == scene) return true;
            return false;
        }

        private IEnumerator LoadZone(Zone z)
        {
            z.State = ZoneState.Loading;
            float started = Time.realtimeSinceStartup;
            AsyncOperationHandle<SceneInstance> handle = Addressables.LoadSceneAsync(z.Info.Address, LoadSceneMode.Additive);
            yield return handle;
            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError($"[Tidy Up] Could not load zone '{z.Info.Address}': {handle.OperationException}", this);
                z.State = ZoneState.Unloaded;
                yield break;
            }
            z.Handle = handle;
            SceneBatcher.Batch(handle.Result.Scene); // shelves and decor into a few draw calls
            Debug.Log($"[Tidy Up][Perf] Zone {z.Info.Id} loaded in {(Time.realtimeSinceStartup - started) * 1000f:0} ms.", this);
            z.State = ZoneState.Loaded;
            if (_shuttingDown) yield break;
            int index = Array.IndexOf(_runtime, z);
            if (index == _activeZone) Activate(index);
        }

        private IEnumerator UnloadZone(Zone z)
        {
            z.State = ZoneState.Unloading;
            z.Runtime = null;
            AsyncOperationHandle<SceneInstance> unload = Addressables.UnloadSceneAsync(z.Handle);
            yield return unload;
            z.State = ZoneState.Unloaded;
        }

        private void Activate(int zone)
        {
            LocationRuntime runtime = _runtime[zone].Runtime;
            if (runtime != null) GameServices.SetActiveLocation(runtime);
        }

        /// <summary>Called by a zone's LocationRuntime when its scene comes alive.</summary>
        public void RegisterZone(LocationRuntime runtime)
        {
            for (int i = 0; i < _runtime.Length; i++)
            {
                if (_runtime[i].Info.Id != runtime.ZoneId) continue;
                _runtime[i].Runtime = runtime;
                if (i == _activeZone || (_activeZone < 0 && GameServices.Player != null && ZoneAt(GameServices.Player.position) == i))
                {
                    _activeZone = i;
                    Activate(i);
                    ActiveZoneChanged?.Invoke(this);
                }
                return;
            }
        }

        public void UnregisterZone(LocationRuntime runtime)
        {
            for (int i = 0; i < _runtime.Length; i++)
                if (_runtime[i].Runtime == runtime) _runtime[i].Runtime = null;
        }

        private int _currentFloor;

        /// <summary>The floor whose rooms are loaded (the player's floor once a lift ride is over).</summary>
        public int CurrentFloor => _currentFloor;

        public int FloorTotal(int floor)
        {
            int total = 0;
            for (int i = 0; i < _zones.Length; i++) if (_zones[i].Floor == floor) total += _zones[i].TotalSlots;
            return total;
        }

        /// <summary>Small enough to hold every room of this floor at once (see _keepAllLoadedUpTo).</summary>
        public bool KeepFloorLoaded(int floor) => FloorTotal(floor) <= _keepAllLoadedUpTo;

        /// <summary>
        /// Waits until the floor at the entrance is in (the loader keeps the screen faded until then): all its rooms when
        /// the floor is small enough to keep whole, otherwise the room at the entrance and its neighbours. Other floors
        /// stay unloaded until the lift goes there.
        /// </summary>
        public IEnumerator WaitUntilReady(Vector3 playerPosition, float timeout = 60f)
        {
            _currentFloor = FloorAt(playerPosition.y);
            LoadFloor(_currentFloor, playerPosition);
            float until = Time.unscaledTime + timeout;
            while (Time.unscaledTime < until && !FloorReady(_currentFloor, playerPosition)) yield return null;
        }

        /// <summary>
        /// Moving to another floor (the lift): loads that floor's rooms, waits for them, then unloads the floor left
        /// behind (except rooms whose items the player carries: items never leave their scene).
        /// </summary>
        public IEnumerator SwitchFloor(int floor, Vector3 arrival, float timeout = 60f)
        {
            LoadFloor(floor, arrival);
            float until = Time.unscaledTime + timeout;
            while (Time.unscaledTime < until && !FloorReady(floor, arrival)) yield return null;
            _currentFloor = floor;
            foreach (Zone z in _runtime)
            {
                if (z.Info.Floor == floor || z.State != ZoneState.Loaded || CarriesFrom(z)) continue;
                yield return UnloadZone(z);
            }
        }

        private void LoadFloor(int floor, Vector3 near)
        {
            bool whole = KeepFloorLoaded(floor);
            var p = new Vector2(near.x, near.z);
            foreach (Zone z in _runtime)
            {
                if (z.Info.Floor != floor || z.State != ZoneState.Unloaded) continue;
                if (whole || DistanceToRect(z.Info.Area, p) <= _loadDistance) StartCoroutine(LoadZone(z));
            }
        }

        /// <summary>Are the rooms of a floor in: all of them when the floor is kept whole, else the one at the given spot.</summary>
        public bool FloorReady(int floor, Vector3 at)
        {
            if (KeepFloorLoaded(floor))
            {
                foreach (Zone z in _runtime)
                    if (z.Info.Floor == floor && (z.State != ZoneState.Loaded || z.Runtime == null)) return false;
                return true;
            }
            int zone = ZoneAt(new Vector3(at.x, FloorHeight(floor), at.z));
            return zone < 0 || (_runtime[zone].State == ZoneState.Loaded && _runtime[zone].Runtime != null);
        }

        /// <summary>Sets carried items down by the entrance. Returns how many.</summary>
        public int ReturnCarriedItems()
        {
            PlayerCarry carry = GameServices.Carry;
            if (carry == null || carry.Inventory.IsEmpty) return 0;
            var items = new List<TidyItem>(carry.Inventory.Items);
            Vector3 origin = _dropPoint != null ? _dropPoint.position : transform.position;
            for (int i = 0; i < items.Count; i++)
            {
                carry.Inventory.Remove(items[i]);
                var offset = new Vector3((i % 5 - 2) * 0.25f, 0.3f + (i / 5) * 0.2f, 0f);
                items[i].SetLoose(origin + offset, Quaternion.identity);
            }
            return items.Count;
        }

        /// <summary>Unloads every room (leaving the location). The shell is unloaded by the LocationLoader after.</summary>
        public IEnumerator UnloadAll()
        {
            _shuttingDown = true;
            foreach (Zone z in _runtime)
            {
                while (z.State == ZoneState.Loading || z.State == ZoneState.Unloading) yield return null;
                if (z.State == ZoneState.Loaded) yield return UnloadZone(z);
            }
        }

        internal void EditorConfigure(LocationDefinition location, ZoneInfo[] zones, Transform entryPoint, Transform dropPoint,
            Rect footprint, Vector2[] doorways, float[] floorHeights = null, string[] floorNames = null, Rect[] lifts = null)
        {
            _location = location;
            _zones = zones ?? Array.Empty<ZoneInfo>();
            _entryPoint = entryPoint;
            _dropPoint = dropPoint;
            _footprint = footprint;
            _doorways = doorways ?? Array.Empty<Vector2>();
            _floorHeights = floorHeights != null && floorHeights.Length > 0 ? floorHeights : new[] { 0f };
            _floorNames = floorNames ?? new[] { "Ground floor" };
            _lifts = lifts ?? Array.Empty<Rect>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Current = null;
            CurrentChanged = null;
        }
    }
}
