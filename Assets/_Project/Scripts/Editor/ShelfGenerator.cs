using System;
using System.Collections.Generic;
using TidyUp.Items;
using TidyUp.Mechanics.ShelfSort;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TidyUp.Editor
{
    /// <summary>Fixture looks: bookcase (sides + back), open rack (posts only), glass case (cabinet + glass), crate (one low tray).</summary>
    public enum ShelfStyle { Bookcase, OpenRack, Case, Crate }

    [Serializable]
    public sealed class ShelfSettings
    {
        public string Name = "Shelf";
        public string Label = "Shelf";
        [Min(1)] public int Shelves = 3;
        [Min(1)] public int SlotsPerShelf = 4;
        [Tooltip("Slots one behind the other on each shelf (deep shelves hold far more). Back rows fill through run placement.")]
        [Min(1)] public int SlotsDeep = 1;
        public ShelfStyle Style = ShelfStyle.Bookcase;
        [Tooltip("Case style: the glass front and top. Crate style: unused.")]
        public Material GlassMaterial;
        [Tooltip("Case style: the solid cabinet under the glass (defaults to the frame material).")]
        public Material BodyMaterial;
        [Tooltip("Crate style: height of the low walls round the single level.")]
        public float CrateWallHeight = 0.12f;
        [Tooltip("Width, height clearance and depth of one slot (metres).")]
        public Vector3 SlotSize = new Vector3(0.55f, 0.35f, 0.4f);
        public float BoardThickness = 0.03f;
        public float PanelThickness = 0.04f;
        public float BaseHeight = 0.15f;
        [Tooltip("Slot type per shelf, listed top to bottom. The last entry repeats.")]
        public string[] RowSlotTypes = { "shelf" };
        [Tooltip("Label per shelf (shown on empty slots), top to bottom. The last entry repeats.")]
        public string[] RowLabels = { "Shelf" };
        public Material FrameMaterial;
        public Material MarkerMaterial;
        [Tooltip("Adds a label strip under each shelf showing its row label (the 'signs' hint).")]
        public bool AddSigns = true;
        [Tooltip("Ordered sort (family B): one exact item id per slot, in reading order (top row first, left to right). " +
                 "Use OrderedSortRules.Sort on the items' sort keys to fill it.")]
        public string[] SlotExactItemIds;
        [Tooltip("Decorative shelves above the slot rows, filled with static filler books (no items, no colliders). " +
                 "Makes bookcases read as full height while keeping the puzzle rows at a comfortable height.")]
        [Min(0)] public int FillerShelvesAbove;
        [Tooltip("Colours for filler books (shared materials).")]
        public Material[] FillerMaterials;
        public int FillerSeed = 7;

        public float FrameHeight => TotalHeight + FillerShelvesAbove * RowHeight;

        public float InnerWidth => SlotsPerShelf * SlotSize.x;
        public float Depth => SlotsDeep * SlotSize.z;
        public float RowHeight => SlotSize.y + BoardThickness;
        public float TotalHeight => BaseHeight + Shelves * RowHeight + BoardThickness;
    }

    /// <summary>
    /// Builds shelves, racks and display cases with slots from parameters (CLAUDE.md section 4).
    /// Local space: pivot bottom-centre, open front facing -Z, back panel at +Z.
    /// </summary>
    public static class ShelfGenerator
    {
        public static SlotGroup Generate(ShelfSettings s, Transform parent, Vector3 position, Quaternion rotation)
        {
            var root = new GameObject(s.Name);
            Undo.RegisterCreatedObjectUndo(root, "Generate Shelf");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, rotation);

            BuildFrame(s, root.transform);
            ItemSlot[] slots = BuildSlots(s, root.transform);
            if (s.AddSigns) BuildSigns(s, root.transform);

            SlotGroup group = root.AddComponent<SlotGroup>();
            bool ordered = s.SlotExactItemIds != null && s.SlotExactItemIds.Length > 0;
            group.EditorConfigure(s.Label, slots, ordered, s.SlotsPerShelf);
            return group;
        }

        private static void BuildFrame(ShelfSettings s, Transform root)
        {
            var frame = new GameObject("Frame").transform;
            frame.SetParent(root, false);

            float innerW = s.InnerWidth;
            float totalW = innerW + 2f * s.PanelThickness;
            float depth = s.Depth;
            float height = s.FrameHeight;
            float sideX = innerW * 0.5f + s.PanelThickness * 0.5f;
            Material body = s.Style == ShelfStyle.Case && s.BodyMaterial != null ? s.BodyMaterial : s.FrameMaterial;

            // Base: a solid plinth (bookcase, case cabinet, crate tray); an open rack stands on its posts.
            if (s.Style != ShelfStyle.OpenRack)
            {
                Mark(GreyboxAssets.Box("Kick", frame, new Vector3(0f, s.BaseHeight * 0.5f, 0f), new Vector3(totalW, s.BaseHeight, depth), body));
            }

            // Boards: the crate has only its floor; the case's top is glass.
            int lastBoard = s.Style == ShelfStyle.Crate ? 0 : s.Shelves + s.FillerShelvesAbove - (s.Style == ShelfStyle.Case ? 1 : 0);
            for (int i = 0; i <= lastBoard; i++)
            {
                float y = s.BaseHeight + i * s.RowHeight + s.BoardThickness * 0.5f;
                Mark(GreyboxAssets.Box($"Board {i}", frame, new Vector3(0f, y, 0f), new Vector3(innerW, s.BoardThickness, depth), s.FrameMaterial));
            }
            if (s.FillerShelvesAbove > 0) BuildFiller(s, frame);

            switch (s.Style)
            {
                case ShelfStyle.OpenRack:
                {
                    // Four slim corner posts, light and airy.
                    const float post = 0.04f;
                    foreach (float px in new[] { -sideX, sideX })
                    foreach (float pz in new[] { -depth * 0.5f + post * 0.5f, depth * 0.5f - post * 0.5f })
                    {
                        Mark(GreyboxAssets.Box("Post", frame, new Vector3(px, height * 0.5f, pz), new Vector3(post, height, post), s.FrameMaterial));
                    }
                    break;
                }
                case ShelfStyle.Crate:
                {
                    float wallH = s.CrateWallHeight;
                    float y = s.BaseHeight + wallH * 0.5f;
                    Mark(GreyboxAssets.Box("Wall L", frame, new Vector3(-sideX, y, 0f), new Vector3(s.PanelThickness, wallH, depth), s.FrameMaterial));
                    Mark(GreyboxAssets.Box("Wall R", frame, new Vector3(sideX, y, 0f), new Vector3(s.PanelThickness, wallH, depth), s.FrameMaterial));
                    Mark(GreyboxAssets.Box("Wall Front", frame, new Vector3(0f, y, -depth * 0.5f - s.PanelThickness * 0.5f), new Vector3(totalW, wallH, s.PanelThickness), s.FrameMaterial));
                    Mark(GreyboxAssets.Box("Wall Back", frame, new Vector3(0f, y, depth * 0.5f + s.PanelThickness * 0.5f), new Vector3(totalW, wallH, s.PanelThickness), s.FrameMaterial));
                    break;
                }
                default:
                {
                    Mark(GreyboxAssets.Box("Side L", frame, new Vector3(-sideX, height * 0.5f, 0f), new Vector3(s.PanelThickness, height, depth), body));
                    Mark(GreyboxAssets.Box("Side R", frame, new Vector3(sideX, height * 0.5f, 0f), new Vector3(s.PanelThickness, height, depth), body));
                    Mark(GreyboxAssets.Box("Back", frame, new Vector3(0f, height * 0.5f, depth * 0.5f + 0.01f), new Vector3(totalW, height, 0.02f), body));
                    if (s.Style == ShelfStyle.Case && s.GlassMaterial != null)
                    {
                        // Glass front and top: no colliders, so the reticle reaches the slots inside.
                        float glassH = height - s.BaseHeight;
                        Mark(GreyboxAssets.Box("Glass Front", frame, new Vector3(0f, s.BaseHeight + glassH * 0.5f, -depth * 0.5f - 0.008f), new Vector3(innerW, glassH, 0.012f), s.GlassMaterial, keepCollider: false));
                        Mark(GreyboxAssets.Box("Glass Top", frame, new Vector3(0f, height - 0.006f, 0f), new Vector3(totalW, 0.012f, depth + 0.02f), s.GlassMaterial, keepCollider: false));
                        Mark(GreyboxAssets.Box("Top Rail", frame, new Vector3(0f, height - 0.015f, -depth * 0.5f - 0.01f), new Vector3(totalW, 0.03f, 0.03f), body));
                    }
                    break;
                }
            }
        }

        /// <summary>Static filler books on the decorative shelves: seeded widths, heights and colours, no colliders.</summary>
        private static void BuildFiller(ShelfSettings s, Transform frame)
        {
            if (s.FillerMaterials == null || s.FillerMaterials.Length == 0) return;
            var random = new System.Random(s.FillerSeed);
            var filler = new GameObject("Filler Books").transform;
            filler.SetParent(frame, false);

            for (int shelf = 0; shelf < s.FillerShelvesAbove; shelf++)
            {
                float floorY = s.BaseHeight + (s.Shelves + shelf) * s.RowHeight + s.BoardThickness;
                float x = -s.InnerWidth * 0.5f + 0.005f;
                float end = s.InnerWidth * 0.5f - 0.005f;
                while (true)
                {
                    float width = Mathf.Lerp(0.025f, 0.05f, (float)random.NextDouble());
                    if (x + width > end) break;
                    float bookHeight = s.SlotSize.y * Mathf.Lerp(0.6f, 0.92f, (float)random.NextDouble());
                    Material material = s.FillerMaterials[random.Next(s.FillerMaterials.Length)];
                    GameObject book = GreyboxAssets.Box("Book", filler,
                        new Vector3(x + width * 0.5f, floorY + bookHeight * 0.5f, 0.01f),
                        new Vector3(width * 0.95f, bookHeight, s.SlotSize.z * 0.8f), material, keepCollider: false);
                    Mark(book);
                    x += width;
                    if (random.NextDouble() < 0.06) x += 0.04f; // the odd gap, like a real shelf
                }
            }
        }

        private static ItemSlot[] BuildSlots(ShelfSettings s, Transform root)
        {
            var slotsRoot = new GameObject("Slots").transform;
            slotsRoot.SetParent(root, false);
            var slots = new List<ItemSlot>(s.Shelves * s.SlotsPerShelf * s.SlotsDeep);

            // Top shelf first so slot order reads like a page (useful for ordered sorts later).
            for (int row = 0; row < s.Shelves; row++)
            {
                int boardIndex = s.Shelves - 1 - row;
                float floorY = s.BaseHeight + boardIndex * s.RowHeight + s.BoardThickness;
                string slotType = Pick(s.RowSlotTypes, row, "shelf");
                string label = Pick(s.RowLabels, row, s.Label);

                for (int col = 0; col < s.SlotsPerShelf; col++)
                for (int deep = 0; deep < s.SlotsDeep; deep++)
                {
                    float x = -s.InnerWidth * 0.5f + (col + 0.5f) * s.SlotSize.x;
                    float z = -s.Depth * 0.5f + (deep + 0.5f) * s.SlotSize.z; // front row first
                    var slotGo = new GameObject(s.SlotsDeep > 1 ? $"Slot r{row} c{col} d{deep}" : $"Slot r{row} c{col}");
                    slotGo.transform.SetParent(slotsRoot, false);
                    slotGo.transform.localPosition = new Vector3(x, floorY + s.SlotSize.y * 0.5f, z);

                    BoxCollider trigger = slotGo.AddComponent<BoxCollider>();
                    trigger.isTrigger = true;
                    trigger.size = new Vector3(s.SlotSize.x * 0.95f, s.SlotSize.y, s.SlotSize.z * 0.95f);

                    var snap = new GameObject("Snap").transform;
                    snap.SetParent(slotGo.transform, false);
                    snap.localPosition = new Vector3(0f, -s.SlotSize.y * 0.5f, 0f);

                    GameObject marker = GreyboxAssets.Box("Empty Marker", slotGo.transform,
                        new Vector3(0f, -s.SlotSize.y * 0.5f + 0.002f, 0f),
                        new Vector3(s.SlotSize.x * 0.7f, 0.004f, s.SlotSize.z * 0.6f),
                        s.MarkerMaterial, keepCollider: false);
                    marker.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // thousands of these

                    int order = (row * s.SlotsPerShelf + col) * s.SlotsDeep + deep;
                    string exactId = s.SlotExactItemIds != null && order < s.SlotExactItemIds.Length ? s.SlotExactItemIds[order] : string.Empty;
                    ItemSlot slot = slotGo.AddComponent<ItemSlot>();
                    slot.EditorConfigure(
                        new SlotDefinition(new[] { slotType }, exactItemId: exactId, orderIndex: order, label: label),
                        snap, marker);
                    slots.Add(slot);
                }
            }
            return slots.ToArray();
        }

        /// <summary>
        /// One world-space canvas per fixture with a label strip on the front edge of each shelf board.
        /// 1 canvas unit = 1 mm.
        /// </summary>
        private static void BuildSigns(ShelfSettings s, Transform root)
        {
            const float unitsPerMetre = 1000f;
            const float stripHeight = 0.06f;

            var canvasGo = new GameObject("Signs", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(root, false);
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasGo.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 4f;
            var canvasRect = (RectTransform)canvasGo.transform;
            canvasRect.sizeDelta = new Vector2(s.InnerWidth, s.TotalHeight) * unitsPerMetre;
            canvasRect.localScale = Vector3.one / unitsPerMetre;
            // Just in front of the shelf, facing the player (-Z). Canvas pivot is its centre.
            canvasRect.localPosition = new Vector3(0f, s.TotalHeight * 0.5f, -s.Depth * 0.5f - (s.Style == ShelfStyle.Case ? 0.02f : 0.004f));

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            for (int row = 0; row < s.Shelves; row++)
            {
                int boardIndex = s.Shelves - 1 - row;
                float boardTopY = s.BaseHeight + boardIndex * s.RowHeight + s.BoardThickness;
                // Hang the strip from the board top so it covers the board edge.
                float centreY = boardTopY - stripHeight * 0.5f + 0.01f;

                var strip = new GameObject($"Sign r{row}", typeof(RectTransform), typeof(Image));
                strip.transform.SetParent(canvasGo.transform, false);
                var stripRect = (RectTransform)strip.transform;
                float stripWidth = s.InnerWidth < 0.9f ? s.InnerWidth * 0.95f : Mathf.Min(0.5f, s.InnerWidth * 0.6f);
                stripRect.sizeDelta = new Vector2(stripWidth, stripHeight) * unitsPerMetre;
                stripRect.anchoredPosition = new Vector2(0f, (centreY - s.TotalHeight * 0.5f) * unitsPerMetre);
                Image background = strip.GetComponent<Image>();
                background.color = new Color(0.16f, 0.13f, 0.11f, 1f);
                background.raycastTarget = false;

                var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
                textGo.transform.SetParent(strip.transform, false);
                var textRect = (RectTransform)textGo.transform;
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;
                Text text = textGo.GetComponent<Text>();
                text.font = font;
                text.fontSize = 40;
                text.resizeTextForBestFit = true; // long labels shrink to fit narrow bookcases
                text.resizeTextMinSize = 18;
                text.resizeTextMaxSize = 40;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = new Color(1f, 0.94f, 0.8f);
                text.raycastTarget = false;
                text.text = Pick(s.RowLabels, row, s.Label);
            }
        }

        private static string Pick(string[] values, int index, string fallback)
        {
            if (values == null || values.Length == 0) return fallback;
            string value = values[Mathf.Min(index, values.Length - 1)];
            return string.IsNullOrEmpty(value) ? fallback : value;
        }

        private static void Mark(GameObject go) => GreyboxAssets.MarkStatic(go);
    }

    /// <summary>Tools/Tidy Up/Shelf Generator: build a shelf at the Scene view pivot.</summary>
    public sealed class ShelfGeneratorWindow : EditorWindow
    {
        [SerializeField] private ShelfSettings _settings = new ShelfSettings();
        private SerializedObject _serialized;

        [MenuItem("Tools/Tidy Up/Shelf Generator")]
        private static void Open() => GetWindow<ShelfGeneratorWindow>("Shelf Generator");

        private void OnEnable() => _serialized = new SerializedObject(this);

        private void OnGUI()
        {
            _serialized.Update();
            EditorGUILayout.PropertyField(_serialized.FindProperty(nameof(_settings)), true);
            _serialized.ApplyModifiedProperties();

            EditorGUILayout.Space();
            if (GUILayout.Button("Generate at Scene View Pivot", GUILayout.Height(28)))
            {
                if (_settings.FrameMaterial == null) _settings.FrameMaterial = GreyboxAssets.Lit("Greybox_Wood", new Color(0.55f, 0.45f, 0.35f));
                if (_settings.MarkerMaterial == null) _settings.MarkerMaterial = GreyboxAssets.Lit("Greybox_SlotMarker", new Color(1f, 0.93f, 0.7f));

                SceneView view = SceneView.lastActiveSceneView;
                Vector3 position = view != null ? view.pivot : Vector3.zero;
                position.y = 0f;
                Transform parent = Selection.activeTransform;
                SlotGroup group = ShelfGenerator.Generate(_settings, parent, position, Quaternion.identity);
                Selection.activeGameObject = group.gameObject;
            }
        }
    }
}
