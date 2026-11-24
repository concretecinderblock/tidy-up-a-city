using System.Collections.Generic;
using TidyUp.Core;
using TidyUp.Items;
using TidyUp.Locations;
using TidyUp.Mechanics.Cleaning;
using TidyUp.Mechanics.ShelfSort;
using UnityEngine;
using UnityEngine.UI;

namespace TidyUp.UI
{
    /// <summary>
    /// The building map inside a mega location (Jesse 2026-11-13, updated 2026-11-24):
    /// - a corner minimap: a parchment floor plan in a wooden frame with rounded corners that turns with the player
    ///   (your view always points up), with an N that circles the edge, and the room you're in plus its progress
    ///   under it;
    /// - the whole floor on M (Back on a gamepad), north up, with every room's progress.
    /// Both show every fixture (shelves, cases, stands) of the loaded rooms, warm wood when clean and dusty grey
    /// while they still need cleaning. Rooms come from the MegaLocation's zone rectangles and fixtures from the
    /// rooms' slot groups, so any mega location gets a map for free. Lives on the HUD canvas; built when the player
    /// enters a mega location. Text and tints only change when the numbers do (no per-frame allocations).
    /// </summary>
    public sealed class MapView : MonoBehaviour
    {
        [SerializeField] private float _miniSize = 270f;
        [SerializeField] private float _miniScale = 6.5f;   // pixels per metre
        [SerializeField] private Vector2 _fullSize = new Vector2(1100f, 760f);

        private static readonly Color Wood = new Color(0.36f, 0.22f, 0.13f, 0.96f);
        private static readonly Color Paper = new Color(0.96f, 0.9f, 0.76f, 0.97f);
        private static readonly Color Ink = new Color(0.27f, 0.17f, 0.1f, 1f);
        private static readonly Color Done = new Color(0.45f, 0.75f, 0.45f, 1f);
        private static readonly Color FixtureClean = new Color(0.5f, 0.32f, 0.18f, 1f);
        private static readonly Color FixtureDirty = new Color(0.66f, 0.63f, 0.58f, 1f);
        private static readonly Color Marker = new Color(0.86f, 0.24f, 0.2f, 1f);

        private sealed class Plan
        {
            public RectTransform Content;
            public RectTransform LabelLayer; // above the fixtures, so names stay readable
            public Image[] Rooms;
            public Text[] Labels;
            public RectTransform Player;
            public float Scale;
            public int[] ShownPlaced;
        }

        private sealed class Fixture
        {
            public Cleanable Cleanable;
            public Image Mini, Full;
            public bool ShownClean;
        }

        private MegaLocation _mega;
        private Plan _mini, _full;
        private RectTransform _miniFrame, _fullFrame, _miniPivot, _compass;
        private Text _fullTitle, _caption;
        private Font _font;
        private bool _fullOpen;
        private int _shownActive = -2;
        private int _shownTotal = -1;
        private int _shownActivePlaced = -1;
        private bool[] _fixturesDrawn = System.Array.Empty<bool>();
        private readonly List<Fixture> _fixtures = new List<Fixture>();
        private float _nextFixtureCheck;

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
            _fixtures.Clear();
            _mega = mega;
            _fullOpen = false;
            _shownActive = -2;
            _shownTotal = -1;
            _shownActivePlaced = -1;
            if (_mega == null || _mega.Zones.Count == 0) return;
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _fixturesDrawn = new bool[_mega.Zones.Count];

            // Corner minimap: top right under the controls (the held stack has the bottom right). A wooden frame,
            // a parchment plan masked to its rounded inside, turning with the player around the centre.
            _miniFrame = Panel("Minimap", transform, new Vector2(1f, 1f), new Vector2(-30f - _miniSize * 0.5f, -190f - _miniSize * 0.5f),
                new Vector2(_miniSize, _miniSize), Wood, Rounded(18));
            RectTransform inner = Panel("Parchment", _miniFrame, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(_miniSize - 14f, _miniSize - 14f), Paper, Rounded(14));
            inner.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            _miniPivot = Rect("Turns With You", inner, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _mini = BuildPlan(_miniPivot, _miniScale, labels: false, playerMarker: false);
            // You: an arrow in the middle, always pointing up (the map turns instead).
            RectTransform you = Rect("You", inner, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22f, 22f));
            Image arrow = you.gameObject.AddComponent<Image>();
            arrow.sprite = ArrowSprite();
            arrow.color = Marker;
            arrow.raycastTarget = false;
            // N on the rim, circling as you turn.
            _compass = Rect("North", inner, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26f, 26f));
            Image disc = _compass.gameObject.AddComponent<Image>();
            disc.sprite = Rounded(13);
            disc.color = Wood;
            disc.raycastTarget = false;
            Text n = Label(_compass, "N", "N", 17, TextAnchor.MiddleCenter, Paper);
            n.rectTransform.sizeDelta = new Vector2(26f, 26f);
            n.fontStyle = FontStyle.Bold;
            // Under the frame: the room you're in and its progress, and the key for the big map.
            _caption = Label(_miniFrame, "Caption", string.Empty, 19, TextAnchor.UpperCenter, Color.white);
            _caption.supportRichText = true;
            _caption.rectTransform.anchorMin = _caption.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            _caption.rectTransform.sizeDelta = new Vector2(_miniSize + 60f, 52f);
            _caption.rectTransform.anchoredPosition = new Vector2(0f, -32f);

            // Full map: the whole floor, fitted, north up, with names and progress.
            _fullFrame = Panel("Building Map", transform, new Vector2(0.5f, 0.5f), Vector2.zero, _fullSize, Wood, Rounded(22));
            RectTransform sheet = Panel("Parchment", _fullFrame, new Vector2(0.5f, 0.5f), Vector2.zero, _fullSize - new Vector2(20f, 20f), Paper, Rounded(16));
            Rect f = _mega.Footprint;
            float scale = Mathf.Min((_fullSize.x - 100f) / Mathf.Max(1f, f.width), (_fullSize.y - 160f) / Mathf.Max(1f, f.height));
            _full = BuildPlan(sheet, scale, labels: true, playerMarker: true);
            _full.Content.anchoredPosition = new Vector2(-f.center.x * scale, -f.center.y * scale - 20f);
            _fullTitle = Label(sheet, "Title", string.Empty, 30, TextAnchor.UpperCenter, Ink);
            _fullTitle.GetComponent<Shadow>().enabled = false;
            Stretch(_fullTitle.rectTransform, new Vector2(0f, -14f));
            Text close = Label(sheet, "Close", "M  close", 18, TextAnchor.LowerRight, Ink);
            close.GetComponent<Shadow>().enabled = false;
            Stretch(close.rectTransform, new Vector2(-16f, 8f));
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
                float yaw = player.eulerAngles.y;
                // Minimap: the plan slides under the centre and turns so the way you face points up.
                _mini.Content.anchoredPosition = -p * _mini.Scale;
                _miniPivot.localRotation = Quaternion.Euler(0f, 0f, yaw);
                float r = (_miniSize - 14f) * 0.5f - 16f;
                float a = yaw * Mathf.Deg2Rad;
                _compass.anchoredPosition = new Vector2(-Mathf.Sin(a), Mathf.Cos(a)) * r; // north, turned with the map
                // Full map: north up, the arrow turns.
                _full.Player.anchoredPosition = p * _full.Scale;
                _full.Player.localRotation = Quaternion.Euler(0f, 0f, -yaw);
            }

            if (Time.unscaledTime >= _nextFixtureCheck)
            {
                _nextFixtureCheck = Time.unscaledTime + 0.5f;
                DrawNewlyLoadedFixtures();
                RefreshFixtureTints();
            }
            RefreshTexts(force: false);
        }

        // ───────────── Fixtures ─────────────

        /// <summary>Draws the fixtures of rooms that have loaded since the last check (once per room).</summary>
        private void DrawNewlyLoadedFixtures()
        {
            for (int z = 0; z < _fixturesDrawn.Length; z++)
            {
                if (_fixturesDrawn[z]) continue;
                LocationRuntime room = _mega.ZoneRuntime(z);
                if (room == null || room.Progress == null) continue;
                _fixturesDrawn[z] = true;
                foreach (SlotGroup group in room.Progress.Groups)
                {
                    if (group == null || !TryFootprint(group, out Rect footprint)) continue;
                    var fixture = new Fixture { Cleanable = group.GetComponent<Cleanable>() };
                    fixture.ShownClean = fixture.Cleanable == null || fixture.Cleanable.IsClean;
                    Color c = fixture.ShownClean ? FixtureClean : FixtureDirty;
                    fixture.Mini = Box(_mini.Content, "Fixture", footprint.center * _mini.Scale, Vector2.Max(footprint.size * _mini.Scale, new Vector2(2f, 2f)), c);
                    fixture.Full = Box(_full.Content, "Fixture", footprint.center * _full.Scale, Vector2.Max(footprint.size * _full.Scale, new Vector2(2f, 2f)), c);
                    if (_full.LabelLayer != null) _full.LabelLayer.SetAsLastSibling(); // names above the fixtures
                    if (_full.Player != null) _full.Player.SetAsLastSibling();         // and the arrow on top of all
                    _fixtures.Add(fixture);
                }
            }
        }

        /// <summary>A fixture's floor footprint in world XZ (its own geometry, not the items on it).</summary>
        private static bool TryFootprint(SlotGroup group, out Rect footprint)
        {
            bool any = false;
            var b = new Bounds();
            foreach (Renderer renderer in group.GetComponentsInChildren<Renderer>())
            {
                if (renderer.GetComponentInParent<TidyItem>() != null) continue;
                if (!any) b = renderer.bounds; else b.Encapsulate(renderer.bounds);
                any = true;
            }
            footprint = any ? new Rect(b.min.x, b.min.z, b.size.x, b.size.z) : default;
            return any && b.size.x < 12f && b.size.z < 12f; // a stray huge bound would paint a whole room
        }

        private void RefreshFixtureTints()
        {
            for (int i = 0; i < _fixtures.Count; i++)
            {
                Fixture f = _fixtures[i];
                if (f.Cleanable == null) continue;
                bool clean = f.Cleanable.IsClean;
                if (clean == f.ShownClean) continue;
                f.ShownClean = clean;
                f.Mini.color = f.Full.color = clean ? FixtureClean : FixtureDirty;
            }
        }

        // ───────────── Rooms and text ─────────────

        /// <summary>Room colours (the one you're in is bright), labels and the caption, only when they change.</summary>
        private void RefreshTexts(bool force)
        {
            int active = _mega.ActiveZone;
            int total = _mega.Placed;
            int activePlaced = active >= 0 ? _mega.ZonePlaced(active) : -1;
            if (!force && active == _shownActive && total == _shownTotal && activePlaced == _shownActivePlaced) return;
            _shownActive = active;
            _shownTotal = total;
            _shownActivePlaced = activePlaced;
            _fullTitle.text = $"<b>{(_mega.Location != null ? _mega.Location.DisplayName : "Building")}</b>   {total:N0} / {_mega.Total:N0}";
            for (int i = 0; i < _mega.Zones.Count; i++)
            {
                ZoneInfo zone = _mega.Zones[i];
                int placed = _mega.ZonePlaced(i);
                bool complete = zone.TotalSlots > 0 && placed >= zone.TotalSlots;
                Color tint = complete ? Color.Lerp(zone.MapColor, Done, 0.5f) : zone.MapColor;
                Color fill = Color.Lerp(Paper, tint, i == active ? 0.8f : 0.4f);
                fill.a = 1f;
                _mini.Rooms[i].color = fill;
                _full.Rooms[i].color = fill;
                if (!force && _full.ShownPlaced[i] == placed) continue;
                _full.ShownPlaced[i] = placed;
                string progress = !_mega.ZoneVisited(i) && placed == 0 ? "not visited yet" : complete ? "tidy ✓" : $"{placed:N0} / {zone.TotalSlots:N0}";
                _full.Labels[i].text = $"<b>{zone.DisplayName}</b>\n<size=18>{progress}</size>";
            }
            if (active >= 0)
            {
                ZoneInfo zone = _mega.Zones[active];
                bool complete = zone.TotalSlots > 0 && activePlaced >= zone.TotalSlots;
                _caption.text = $"<b>{zone.DisplayName}</b>  {(complete ? "✓" : $"{activePlaced:N0} / {zone.TotalSlots:N0}")}\n<size=15><color=#FFFFFFB0>M  full map</color></size>";
            }
            else _caption.text = "<size=15><color=#FFFFFFB0>M  full map</color></size>";
        }

        private Plan BuildPlan(RectTransform parent, float scale, bool labels, bool playerMarker)
        {
            var plan = new Plan { Scale = scale };
            plan.Content = Rect("Plan", parent, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Rect f = _mega.Footprint;
            // Walls in ink: a rectangle a little larger than the floor; the rooms on top leave the walls showing.
            Box(plan.Content, "Walls", f.center * scale, (f.size + new Vector2(0.8f, 0.8f)) * scale, Ink);
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
                if (plan.LabelLayer == null) plan.LabelLayer = Rect("Labels", plan.Content, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                // A soft parchment card behind each name, on a layer above the fixtures.
                Image card = Box(plan.LabelLayer, "Card", a.center * scale, new Vector2(Mathf.Min(a.width * scale - 12f, 230f), 58f), new Color(Paper.r, Paper.g, Paper.b, 0.8f));
                card.sprite = Rounded(10);
                card.type = Image.Type.Sliced;
                Text label = Label(card.rectTransform, "Label", string.Empty, 22, TextAnchor.MiddleCenter, Ink);
                label.supportRichText = true;
                Stretch(label.rectTransform, Vector2.zero);
                label.GetComponent<Shadow>().enabled = false;
                plan.Labels[i] = label;
            }
            // Doorways: gaps in the walls between rooms.
            foreach (Vector2 d in _mega.Doorways)
            {
                bool onWallAlongX = Mathf.Abs(d.y - Mathf.Round(d.y / 20f) * 20f) < 0.5f;
                Vector2 size = onWallAlongX ? new Vector2(3.6f, 1.2f) : new Vector2(1.2f, 3.6f);
                Box(plan.Content, "Doorway", d * scale, size * scale, Paper);
            }
            // The street door.
            if (_mega.EntryPoint != null)
            {
                var e = new Vector2(_mega.EntryPoint.position.x, f.yMin);
                Box(plan.Content, "Street Door", e * scale, new Vector2(2.4f, 1.4f) * scale, Done);
            }
            if (playerMarker)
            {
                plan.Player = Rect("You", plan.Content, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24f, 24f));
                Image arrow = plan.Player.gameObject.AddComponent<Image>();
                arrow.sprite = ArrowSprite();
                arrow.color = Marker;
                arrow.raycastTarget = false;
            }
            return plan;
        }

        // ───────────── Generated sprites (no art assets) ─────────────

        private static readonly Dictionary<int, Sprite> RoundedCache = new Dictionary<int, Sprite>();
        private static Sprite _arrow;

        /// <summary>A white rounded rectangle, nine-sliced, with soft edges.</summary>
        private static Sprite Rounded(int radius)
        {
            if (RoundedCache.TryGetValue(radius, out Sprite cached) && cached != null) return cached;
            int size = radius * 2 + 4;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            float r = radius;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // Distance outside the rounded rectangle (negative inside).
                float dx = Mathf.Max(Mathf.Abs(x + 0.5f - size * 0.5f) - (size * 0.5f - r), 0f);
                float dy = Mathf.Max(Mathf.Abs(y + 0.5f - size * 0.5f) - (size * 0.5f - r), 0f);
                float d = Mathf.Sqrt(dx * dx + dy * dy) - r;
                byte alpha = (byte)(Mathf.Clamp01(0.5f - d) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
            t.SetPixels32(pixels);
            t.Apply(false, true);
            Sprite sprite = Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius + 1, radius + 1, radius + 1, radius + 1));
            RoundedCache[radius] = sprite;
            return sprite;
        }

        /// <summary>A white arrowhead pointing up, with a notch at the back.</summary>
        private static Sprite ArrowSprite()
        {
            if (_arrow != null) return _arrow;
            const int size = 48;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            Vector2 tip = new Vector2(24f, 45f), left = new Vector2(6f, 4f), right = new Vector2(42f, 4f), notch = new Vector2(24f, 16f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                bool inside = (InTriangle(p, tip, left, notch) || InTriangle(p, tip, notch, right));
                pixels[y * size + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
            t.SetPixels32(pixels);
            t.Apply(false, true);
            _arrow = Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return _arrow;
        }

        private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(p, a, b), d2 = Cross(p, b, c), d3 = Cross(p, c, a);
            bool neg = d1 < 0f || d2 < 0f || d3 < 0f, pos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(neg && pos);
        }

        private static float Cross(Vector2 p, Vector2 a, Vector2 b) => (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);

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

        private static RectTransform Panel(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color, Sprite sprite)
        {
            RectTransform rt = Rect(name, parent, anchor, position, size);
            Image image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
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
