using System;
using System.Collections;
using TidyUp.Core;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace TidyUp.Locations
{
    /// <summary>
    /// Lives in the persistent City scene. Entering a door loads that location's scene additively through
    /// Addressables (address = LocationDefinition.SceneName) and moves the player inside; leaving sets
    /// carried items down, unloads the scene and puts the player back at the door. The street is hidden
    /// while inside (nothing to render or simulate out there). Screen fades hide the swap.
    /// </summary>
    public sealed class LocationLoader : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _fade;
        [SerializeField] private GameObject _streetRoot;
        [SerializeField] private float _fadeDuration = 0.35f;
        [Tooltip("All locations on this street, for the HUD overview.")]
        [SerializeField] private LocationDefinition[] _locations = Array.Empty<LocationDefinition>();

        private AsyncOperationHandle<SceneInstance> _loaded;
        private bool _hasLoaded;
        private Transform _returnPoint;
        private bool _busy;

        public LocationDefinition[] Locations => _locations;
        public bool IsBusy => _busy;
        public bool IsInside => _hasLoaded;

        /// <summary>Raised once the player is back on the street (location unloaded, player at the door).</summary>
        public event Action ReturnedToStreet;

        /// <summary>The street's doors (used when loading a save made inside a location).</summary>
        public LocationDoor[] GetDoors()
        {
            return _streetRoot != null ? _streetRoot.GetComponentsInChildren<LocationDoor>(true) : Array.Empty<LocationDoor>();
        }

        private void Awake()
        {
            GameServices.RegisterLoader(this);
            if (_fade != null)
            {
                _fade.alpha = 0f;
                _fade.blocksRaycasts = false;
            }
        }

        public void Enter(LocationDefinition location, Transform returnPoint)
        {
            if (_busy || _hasLoaded || location == null || string.IsNullOrEmpty(location.SceneName)) return;
            StartCoroutine(EnterRoutine(location, returnPoint));
        }

        public void ExitCurrent()
        {
            if (_busy || !_hasLoaded) return;
            StartCoroutine(ExitRoutine());
        }

        private IEnumerator EnterRoutine(LocationDefinition location, Transform returnPoint)
        {
            _busy = true;
            SetBlocked(true);
            yield return Fade(1f);

            AsyncOperationHandle<SceneInstance> handle = Addressables.LoadSceneAsync(location.SceneName, LoadSceneMode.Additive);
            yield return handle;
            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError($"[Tidy Up] Could not load location scene '{location.SceneName}': {handle.OperationException}", this);
                GameServices.Announce($"The door to the {location.DisplayName} is stuck. (Scene failed to load.)");
                yield return Fade(0f);
                SetBlocked(false);
                _busy = false;
                yield break;
            }

            _loaded = handle;
            _hasLoaded = true;
            _returnPoint = returnPoint;
            if (_streetRoot != null) _streetRoot.SetActive(false);

            LocationRuntime runtime = FindRuntime(handle.Result.Scene);
            if (runtime != null && runtime.EntryPoint != null && GameServices.PlayerController != null)
            {
                GameServices.PlayerController.Teleport(runtime.EntryPoint.position, runtime.EntryPoint.eulerAngles.y);
            }

            yield return Fade(0f);
            SetBlocked(false);
            _busy = false;
        }

        private IEnumerator ExitRoutine()
        {
            _busy = true;
            SetBlocked(true);

            LocationRuntime runtime = GameServices.ActiveLocation;
            int returned = runtime != null ? runtime.ReturnCarriedItems() : 0;
            string name = runtime != null && runtime.Location != null ? runtime.Location.DisplayName : "location";

            yield return Fade(1f);

            if (_streetRoot != null) _streetRoot.SetActive(true);
            AsyncOperationHandle<SceneInstance> unload = Addressables.UnloadSceneAsync(_loaded);
            yield return unload;
            _hasLoaded = false;

            if (_returnPoint != null && GameServices.PlayerController != null)
            {
                GameServices.PlayerController.Teleport(_returnPoint.position, _returnPoint.eulerAngles.y);
            }

            yield return Fade(0f);
            SetBlocked(false);
            _busy = false;
            ReturnedToStreet?.Invoke();
            if (returned > 0)
            {
                GameServices.Announce(returned == 1
                    ? $"You set the {name}'s item down by the door. Things stay where they belong."
                    : $"You set the {name}'s {returned} items down by the door. Things stay where they belong.");
            }
        }

        private static LocationRuntime FindRuntime(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                LocationRuntime runtime = root.GetComponentInChildren<LocationRuntime>(true);
                if (runtime != null) return runtime;
            }
            return null;
        }

        private IEnumerator Fade(float target)
        {
            if (_fade == null) yield break;
            float start = _fade.alpha;
            float time = 0f;
            while (time < _fadeDuration)
            {
                time += Time.unscaledDeltaTime;
                _fade.alpha = Mathf.Lerp(start, target, time / _fadeDuration);
                yield return null;
            }
            _fade.alpha = target;
        }

        private static void SetBlocked(bool blocked)
        {
            if (GameServices.Input != null) GameServices.Input.GameplayBlocked = blocked;
        }

        internal void EditorConfigure(CanvasGroup fade, GameObject streetRoot, LocationDefinition[] locations)
        {
            _fade = fade;
            _streetRoot = streetRoot;
            _locations = locations ?? Array.Empty<LocationDefinition>();
        }
    }
}
