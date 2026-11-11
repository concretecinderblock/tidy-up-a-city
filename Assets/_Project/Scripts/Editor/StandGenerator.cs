using System;
using System.Collections.Generic;
using TidyUp.Items;
using TidyUp.Mechanics.ShelfSort;
using UnityEditor;
using UnityEngine;

namespace TidyUp.Editor
{
    [Serializable]
    public sealed class StandSettings
    {
        public string Name = "Cake Stand";
        public string Label = "Cake stand";
        [Tooltip("Slots per tier, bottom tier first. Tiers get smaller as they go up.")]
        public int[] SlotsPerTier = { 8, 6, 4 };
        [Tooltip("Slot type per tier, bottom first. The last entry repeats.")]
        public string[] TierSlotTypes = { "cake" };
        [Tooltip("Label per tier, bottom first. The last entry repeats.")]
        public string[] TierLabels = { "Cakes" };
        public Vector3 SlotSize = new Vector3(0.26f, 0.2f, 0.26f);
        public float BaseHeight = 0.45f;
        public float PlateThickness = 0.03f;
        public Material FrameMaterial;
        public Material MarkerMaterial;
    }

    /// <summary>
    /// Builds round tiered stands (cake stands, display carousels): stacked plates on a centre pole, slots in
    /// a ring on each plate, items facing outwards. Pivot bottom-centre.
    /// </summary>
    public static class StandGenerator
    {
        public static SlotGroup Generate(StandSettings s, Transform parent, Vector3 position, Quaternion rotation)
        {
            var root = new GameObject(s.Name);
            Undo.RegisterCreatedObjectUndo(root, "Generate Stand");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, rotation);
            var frame = new GameObject("Frame").transform;
            frame.SetParent(root.transform, false);
            var slotsRoot = new GameObject("Slots").transform;
            slotsRoot.SetParent(root.transform, false);

            var slots = new List<ItemSlot>();
            float y = s.BaseHeight;
            float poleTop = y;
            int tiers = s.SlotsPerTier.Length;
            for (int tier = 0; tier < tiers; tier++)
            {
                int count = Mathf.Max(1, s.SlotsPerTier[tier]);
                // Ring radius so neighbouring slots just touch; one slot sits in the middle.
                float ring = count == 1 ? 0f : Mathf.Max(s.SlotSize.x * 0.6f, s.SlotSize.x / (2f * Mathf.Sin(Mathf.PI / count)));
                float plateRadius = ring + s.SlotSize.z * 0.55f;
                Mark(Plate($"Plate {tier}", frame, y, plateRadius, s.PlateThickness, s.FrameMaterial));
                float floorY = y + s.PlateThickness;
                string slotType = Pick(s.TierSlotTypes, tier, "cake");
                string label = Pick(s.TierLabels, tier, s.Label);

                for (int i = 0; i < count; i++)
                {
                    float angle = i * Mathf.PI * 2f / count;
                    var dir = new Vector3(Mathf.Sin(angle), 0f, -Mathf.Cos(angle)); // first slot faces the front (-Z)
                    var slotGo = new GameObject($"Slot t{tier} {i}");
                    slotGo.transform.SetParent(slotsRoot, false);
                    slotGo.transform.localPosition = new Vector3(dir.x * ring, floorY + s.SlotSize.y * 0.5f, dir.z * ring);
                    slotGo.transform.localRotation = Quaternion.LookRotation(-dir, Vector3.up); // item front (-Z) faces outwards

                    BoxCollider trigger = slotGo.AddComponent<BoxCollider>();
                    trigger.isTrigger = true;
                    trigger.size = new Vector3(s.SlotSize.x * 0.9f, s.SlotSize.y, s.SlotSize.z * 0.9f);
                    var snap = new GameObject("Snap").transform;
                    snap.SetParent(slotGo.transform, false);
                    snap.localPosition = new Vector3(0f, -s.SlotSize.y * 0.5f, 0f);
                    GameObject marker = GreyboxAssets.Box("Empty Marker", slotGo.transform,
                        new Vector3(0f, -s.SlotSize.y * 0.5f + 0.002f, 0f),
                        new Vector3(s.SlotSize.x * 0.6f, 0.004f, s.SlotSize.z * 0.6f), s.MarkerMaterial, keepCollider: false);
                    marker.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                    ItemSlot slot = slotGo.AddComponent<ItemSlot>();
                    slot.EditorConfigure(new SlotDefinition(new[] { slotType }, orderIndex: slots.Count, label: label), snap, marker);
                    slots.Add(slot);
                }
                poleTop = floorY + s.SlotSize.y;
                y = poleTop;
            }

            // Centre pole and a foot, with a little knob on top.
            Mark(Plate("Foot", frame, 0f, 0.18f, 0.04f, s.FrameMaterial));
            Mark(GreyboxAssets.Box("Pole", frame, new Vector3(0f, poleTop * 0.5f, 0f), new Vector3(0.04f, poleTop, 0.04f), s.FrameMaterial));
            Mark(GreyboxAssets.Box("Knob", frame, new Vector3(0f, poleTop + 0.03f, 0f), new Vector3(0.07f, 0.06f, 0.07f), s.FrameMaterial));

            SlotGroup group = root.AddComponent<SlotGroup>();
            group.EditorConfigure(s.Label, slots.ToArray());
            return group;
        }

        /// <summary>A round plate (lathe disc with a soft rim) with a matching box collider.</summary>
        private static GameObject Plate(string name, Transform parent, float y, float radius, float thickness, Material material)
        {
            int mm = Mathf.RoundToInt(radius * 1000f);
            int tmm = Mathf.RoundToInt(thickness * 1000f);
            Mesh mesh = Art.MeshAssets.GetOrCreate($"Plate_v1_{mm}_{tmm}", () =>
            {
                float r = radius, t = thickness;
                return Art.MeshGen.Lathe(new[]
                {
                    new Vector2(0f, 0f), new Vector2(r * 0.96f, 0f), new Vector2(r, t * 0.35f), new Vector2(r, t * 0.75f),
                    new Vector2(r * 0.97f, t), new Vector2(0f, t),
                }, 32, "Plate");
            }, rebuild: false);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, y, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            BoxCollider collider = go.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, thickness * 0.5f, 0f);
            collider.size = new Vector3(radius * 1.6f, thickness, radius * 1.6f);
            return go;
        }

        private static string Pick(string[] values, int index, string fallback)
        {
            if (values == null || values.Length == 0) return fallback;
            string value = values[Mathf.Min(index, values.Length - 1)];
            return string.IsNullOrEmpty(value) ? fallback : value;
        }

        private static void Mark(GameObject go) => GreyboxAssets.MarkStatic(go);
    }
}
