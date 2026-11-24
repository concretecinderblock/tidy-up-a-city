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
        [Tooltip("Locations up to this many items load every room behind the entry fade and keep them (no load or " +
                 "unload hitches while walking). Bigger ones stream rooms by distance.")]
        [SerializeField] private int _keepAllLoadedUpTo = 30000;
        [Tooltip("Outline of the floor for the map (world XZ).")]
        [SerializeField] private Rect _footprint;
        [Tooltip("Doorways between rooms (world XZ centre), drawn on the map.")]
        [SerializeField] private Vector2[] _doorways = Array.Empty<Vector2>();

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

        /// <summary>The zone containing a world position, or -1.</summary>
        public int ZoneAt(Vector3 position)
        {
            var p = new Vector2(position.x, position.z);
            for (int i = 0; i < _zones.Length; i++)
                if (_zones[i].Area.Contains(p)) return i;
            return -1;
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

        /// <summary>Loads rooms near the player, unloads rooms far away (but never one whose items are carried).</summary>
        private void Stream(Vector3 position)
        {
            var p = new Vector2(position.x, position.z);
            for (int i = 0; i < _runtime.Length; i++)
            {
                Zone z = _runtime[i];
                float distance = DistanceToRect(z.Info.Area, p);
                if (distance <= _loadDistance && z.State == ZoneState.Unloaded) StartCoroutine(LoadZone(z));
                else if (!KeepAllLoaded && distance > _loadDistance + _unloadHysteresis && z.State == ZoneState.Loaded && i != _activeZone && !CarriesFrom(z))
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

        /// <summary>Small enough to hold every room at once (see _keepAllLoadedUpTo).</summary>
        public bool KeepAllLoaded => Total <= _keepAllLoadedUpTo;

        /// <summary>
        /// Waits until the rooms are in (the loader keeps the screen faded until then): every room when the location
        /// is small enough to keep them all, otherwise the room at the entrance and its neighbours.
        /// </summary>
        public IEnumerator WaitUntilReady(Vector3 playerPosition, float timeout = 60f)
        {
            if (KeepAllLoaded)
            {
                foreach (Zone z in _runtime)
                    if (z.State == ZoneState.Unloaded) StartCoroutine(LoadZone(z));
            }
            else Stream(playerPosition);
            int zone = ZoneAt(playerPosition);
            float until = Time.unscaledTime + timeout;
            while (Time.unscaledTime < until && !Ready(zone)) yield return null;
        }

        private bool Ready(int entranceZone)
        {
            if (KeepAllLoaded)
            {
                foreach (Zone z in _runtime) if (z.State != ZoneState.Loaded || z.Runtime == null) return false;
                return true;
            }
            return entranceZone < 0 || (_runtime[entranceZone].State == ZoneState.Loaded && _runtime[entranceZone].Runtime != null);
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
            Rect footprint, Vector2[] doorways)
        {
            _location = location;
            _zones = zones ?? Array.Empty<ZoneInfo>();
            _entryPoint = entryPoint;
            _dropPoint = dropPoint;
            _footprint = footprint;
            _doorways = doorways ?? Array.Empty<Vector2>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Current = null;
            CurrentChanged = null;
        }
    }
}
