using System.Collections.Generic;
using TidyUp.Core;
using TidyUp.Locations;
using UnityEngine;
using UnityEngine.UI;

namespace TidyUp.UI
{
    /// <summary>
    /// The building map inside a mega location (Jesse 2026-11-13): a small floor plan in the corner that follows
    /// the player, and the whole floor with every room's progress on M (Back on a gamepad). Rooms are drawn from
    /// the MegaLocation's zone rectangles, so any mega location gets a map for free. Lives on the HUD canvas;
    /// built when the player enters a mega location, destroyed when they leave. Text only changes when the
    /// numbers do (no per-frame allocations).
    /// </summary>
    public sealed class MapView : MonoBehaviour
    {
        [SerializeField] private float _miniSize = 270f;
        [SerializeField] private float _miniScale = 5.5f;   // pixels per metre
        [SerializeField] private Vector2 _fullSize = new Vector2(1100f, 760f);

        private static readonly Color Background = new Color(0.16f, 0.11f, 0.08f, 0.82f);
        private static readonly Color Ink = new Color(0.25f, 0.16f, 0.1f, 1f);
        private static readonly Color Paper = new Color(0.98f, 0.93f, 0.82f, 1f);
        private static readonly Color Done = new Color(0.55f, 0.85f, 0.55f, 1f);

        private sealed class Plan
        {
            public RectTransform Root;
            public RectTransform Content;
            public Image[] Rooms;
            public Text[] Labels;
            public RectTransform Player;
            public float Scale;
            public int[] ShownPlaced;
        }

        private MegaLocation _mega;
        private Plan _mini, _full;
        private RectTransform _miniFrame, _fullFrame;
        private Text _fullTitle;
        private Font _font;
        private bool _fullOpen;
        private int _shownActive = -2;
        private int _shownTotal = -1;

        private void OnEnable()
        {
            MegaLocation.CurrentChanged += Bind;
            Bind(MegaLocation.Current);
        }

        private void OnDisable()
        {
            MegaLocation.CurrentChanged -= Bind;
            Bind(null);
        }

        private void Bind(MegaLocation mega)
        {
            if (_miniFrame != null) Destroy(_miniFrame.gameObject);
            if (_fullFrame != null) Destroy(_fullFrame.gameObject);
            _mini = _full = null;
            _miniFrame = _fullFrame = null;
            _mega = mega;
            _fullOpen = false;
            _shownActive = -2;
            _shownTotal = -1;
            if (_mega == null || _mega.Zones.Count == 0) return;
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Corner minimap: bottom right, masked, centred on the player.
            _miniFrame = Panel("Minimap", transform, new Vector2(1f, 0f), new Vector2(-30f - _miniSize * 0.5f, 30f + _miniSize * 0.5f), new Vector2(_miniSize, _miniSize), Background);
            _miniFrame.gameObject.AddComponent<RectMask2D>();
            _mini = BuildPlan(_miniFrame, _miniScale, labels: false);
            Text hint = Label(_miniFrame, "Hint", "M  map", 16, TextAnchor.LowerRight, new Color(1f, 1f, 1f, 0.7f));
            Stretch(hint.rectTransform, new Vector2(-8f, 4f));

            // Full map: the whole floor, fitted, with names and progress.
            _fullFrame = Panel("Building Map", transform, new Vector2(0.5f, 0.5f), Vector2.zero, _fullSize, Background);
            Rect f = _mega.Footprint;
            float scale = Mathf.Min((_fullSize.x - 80f) / Mathf.Max(1f, f.width), (_fullSize.y - 140f) / Mathf.Max(1f, f.height));
            _full = BuildPlan(_fullFrame, scale, labels: true);
            _full.Content.anchoredPosition = new Vector2(-f.center.x * scale, -f.center.y * scale - 20f);
            _fullTitle = Label(_fullFrame, "Title", string.Empty, 30, TextAnchor.UpperCenter, Paper);
            Stretch(_fullTitle.rectTransform, new Vector2(0f, -18f));
            Text close = Label(_fullFrame, "Close", "M  close", 18, TextAnchor.LowerRight, new Color(1f, 1f, 1f, 0.7f));
            Stretch(close.rectTransform, new Vector2(-16f, 10f));
            _fullFrame.gameObject.SetActive(false);
            RefreshTexts(force: true);
        }

        private void Update()
        {
            if (_mega == null || _mini == null) return;
            PlayerInputReader input = GameServices.Input;
            if (input != null && input.MapPressed)
            {
                _fullOpen = !_fullOpen;
                _fullFrame.gameObject.SetActive(_fullOpen);
                _miniFrame.gameObject.SetActive(!_fullOpen);
            }

            Transform player = GameServices.Player;
            if (player != null)
            {
                var p = new Vector2(player.position.x, player.position.z);
                Quaternion heading = Quaternion.Euler(0f, 0f, -player.eulerAngles.y);
                _mini.Content.anchoredPosition = -p * _mini.Scale;          // the minimap follows the player
                _mini.Player.anchoredPosition = p * _mini.Scale;
                _mini.Player.localRotation = heading;
                _full.Player.anchoredPosition = p * _full.Scale;
                _full.Player.localRotation = heading;
            }
            RefreshTexts(force: false);
        }

        /// <summary>Room colours (the one you're in is bright) and "placed/total" labels, only when they change.</summary>
        private void RefreshTexts(bool force)
        {
            int active = _mega.ActiveZone;
            int total = _mega.Placed;
            if (!force && active == _shownActive && total == _shownTotal) return;
            _shownActive = active;
            _shownTotal = total;
            _fullTitle.text = $"{(_mega.Location != null ? _mega.Location.DisplayName : "Building")}   {total:N0} / {_mega.Total:N0}";
            for (int i = 0; i < _mega.Zones.Count; i++)
            {
                ZoneInfo zone = _mega.Zones[i];
                int placed = _mega.ZonePlaced(i);
                bool complete = zone.TotalSlots > 0 && placed >= zone.TotalSlots;
                Color c = complete ? Color.Lerp(zone.MapColor, Done, 0.5f) : zone.MapColor;
                Color shaded = i == active ? c : Color.Lerp(c, Background, 0.45f);
                _mini.Rooms[i].color = shaded;
                _full.Rooms[i].color = shaded;
                if (!force && _full.ShownPlaced[i] == placed) continue;
                _full.ShownPlaced[i] = placed;
                string progress = !_mega.ZoneVisited(i) && placed == 0 ? "not visited yet" : complete ? "tidy ✓" : $"{placed:N0} / {zone.TotalSlots:N0}";
                _full.Labels[i].text = $"<b>{zone.DisplayName}</b>\n<size=18>{progress}</size>";
            }
        }

        private Plan BuildPlan(RectTransform frame, float scale, bool labels)
        {
            var plan = new Plan { Scale = scale };
            plan.Content = Rect("Plan", frame, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Rect f = _mega.Footprint;
            // Outer walls: a dark rectangle a little larger than the floor, rooms on top leave the walls showing.
            Image walls = Box(plan.Content, "Walls", f.center * scale, (f.size + new Vector2(0.8f, 0.8f)) * scale, Ink);
            walls.raycastTarget = false;
            int n = _mega.Zones.Count;
            plan.Rooms = new Image[n];
            plan.Labels = new Text[n];
            plan.ShownPlaced = new int[n];
            for (int i = 0; i < n; i++)
            {
                Rect a = _mega.Zones[i].Area;
                plan.Rooms[i] = Box(plan.Content, _mega.Zones[i].DisplayName, a.center * scale, (a.size - new Vector2(0.4f, 0.4f)) * scale, _mega.Zones[i].MapColor);
                plan.ShownPlaced[i] = -1;
                if (!labels) continue;
                Text label = Label(plan.Rooms[i].rectTransform, "Label", string.Empty, 22, TextAnchor.MiddleCenter, Ink);
                label.supportRichText = true;
                Stretch(label.rectTransform, Vector2.zero);
                label.GetComponent<Shadow>().enabled = false;
                plan.Labels[i] = label;
            }
            // Doorways: gaps in the walls between rooms.
            foreach (Vector2 d in _mega.Doorways)
            {
                bool betweenNorthSouth = Mathf.Abs(d.y) < 0.5f || Mathf.Abs(d.y - Mathf.Round(d.y / 20f) * 20f) < 0.5f; // on a wall along X
                Vector2 size = betweenNorthSouth ? new Vector2(3.6f, 1.2f) : new Vector2(1.2f, 3.6f);
                Box(plan.Content, "Doorway", d * scale, size * scale, Paper);
            }
            // The street door.
            if (_mega.EntryPoint != null)
            {
                var e = new Vector2(_mega.EntryPoint.position.x, f.yMin);
                Box(plan.Content, "Street Door", e * scale, new Vector2(2.4f, 1.4f) * scale, Done);
            }
            // The player: a dot with a nose pointing where they look.
            plan.Player = Rect("Player", plan.Content, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16f, 16f));
            Image dot = plan.Player.gameObject.AddComponent<Image>();
            dot.color = new Color(0.95f, 0.3f, 0.25f);
            dot.raycastTarget = false;
            Image nose = Box(plan.Player, "Facing", new Vector2(0f, 12f), new Vector2(6f, 14f), new Color(0.95f, 0.3f, 0.25f));
            nose.raycastTarget = false;
            return plan;
        }

        // ───────────── Small uGUI helpers ─────────────

        private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        private static RectTransform Panel(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color)
        {
            RectTransform rt = Rect(name, parent, anchor, position, size);
            Image image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rt;
        }

        private static Image Box(RectTransform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            RectTransform rt = Rect(name, parent, new Vector2(0.5f, 0.5f), position, size);
            Image image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private Text Label(RectTransform parent, string name, string text, int size, TextAnchor alignment, Color color)
        {
            RectTransform rt = Rect(name, parent, new Vector2(0.5f, 0.5f), Vector2.zero, parent.sizeDelta);
            Text label = rt.gameObject.AddComponent<Text>();
            rt.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.6f);
            label.font = _font;
            label.fontSize = size;
            label.alignment = alignment;
            label.color = color;
            label.text = text;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        private static void Stretch(RectTransform rt, Vector2 offset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = new Vector2(-24f, -16f);
            rt.anchoredPosition = offset;
        }
    }
}
