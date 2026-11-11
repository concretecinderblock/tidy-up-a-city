using UnityEngine;
using UnityEngine.UI;

namespace TidyUp.Mechanics.Cleaning
{
    /// <summary>
    /// The wipe minigame's bar: a gold sparkly zone, a sliding marker and a hit counter, just under the reticle.
    /// Builds its own small overlay canvas the first time it's shown. No per-frame allocations.
    /// </summary>
    public sealed class WipeBarView : MonoBehaviour
    {
        private const float Width = 420f;
        private const float Height = 26f;

        private RectTransform _zone;
        private RectTransform _marker;
        private Image _markerImage;
        private Text _label;
        private CanvasGroup _group;
        private int _shownHits = -1;
        private int _shownNeeded = -1;
        private string _shownName;
        private float _flashUntil;

        public static WipeBarView Create(Transform parent)
        {
            var go = new GameObject("Wipe Bar", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<WipeBarView>();
            view.Build();
            view.Hide();
            return view;
        }

        private void Build()
        {
            var canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            _group = GetComponent<CanvasGroup>();
            _group.interactable = false;
            _group.blocksRaycasts = false;

            RectTransform bar = Box("Bar", transform, new Vector2(Width + 8f, Height + 8f), new Color(0.24f, 0.17f, 0.12f, 0.92f));
            bar.anchoredPosition = new Vector2(0f, -120f);
            RectTransform track = Box("Track", bar, new Vector2(Width, Height), new Color(0.98f, 0.93f, 0.82f, 1f));
            _zone = Box("Zone", track, new Vector2(Width * TimingBar.ZoneWidth, Height), new Color(1f, 0.78f, 0.25f, 1f));
            _zone.pivot = new Vector2(0f, 0.5f);
            _marker = Box("Marker", track, new Vector2(8f, Height + 14f), new Color(0.3f, 0.2f, 0.14f, 1f));
            _markerImage = _marker.GetComponent<Image>();

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(bar, false);
            var labelRect = (RectTransform)labelGo.transform;
            labelRect.sizeDelta = new Vector2(Width + 200f, 34f);
            labelRect.anchoredPosition = new Vector2(0f, 34f);
            _label = labelGo.GetComponent<Text>();
            _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _label.fontSize = 24;
            _label.alignment = TextAnchor.MiddleCenter;
            _label.color = new Color(1f, 0.96f, 0.86f);
            _label.raycastTarget = false;
            var shadow = labelGo.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        }

        private static RectTransform Box(string name, Transform parent, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = size;
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        public void Show() => _group.alpha = 1f;

        public void Hide()
        {
            _group.alpha = 0f;
            _shownHits = -1;
        }

        /// <summary>Call once per frame while the game runs.</summary>
        public void Refresh(TimingBar bar, string thingName)
        {
            _zone.anchoredPosition = new Vector2(-Width * 0.5f + bar.ZoneStart * Width, 0f);
            _marker.anchoredPosition = new Vector2(-Width * 0.5f + bar.Marker * Width, 0f);
            _markerImage.color = Time.unscaledTime < _flashUntil ? new Color(1f, 1f, 1f, 1f)
                : bar.InZone ? new Color(0.95f, 0.55f, 0.2f, 1f) : new Color(0.3f, 0.2f, 0.14f, 1f);

            if (bar.Hits == _shownHits && bar.Needed == _shownNeeded && ReferenceEquals(thingName, _shownName)) return;
            _shownHits = bar.Hits;
            _shownNeeded = bar.Needed;
            _shownName = thingName;
            _label.text = $"Wipe the {thingName}!  Tap [E] in the gold  ·  {bar.Hits}/{bar.Needed}";
        }

        /// <summary>A quick white flash of the marker on a good wipe.</summary>
        public void Flash() => _flashUntil = Time.unscaledTime + 0.15f;
    }
}
