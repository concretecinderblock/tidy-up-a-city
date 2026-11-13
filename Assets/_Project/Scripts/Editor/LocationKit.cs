using System;
using System.Collections.Generic;
using TidyUp.Core;
using TidyUp.Hints;
using TidyUp.Items;
using TidyUp.Locations;
using TidyUp.Mechanics.Cleaning;
using TidyUp.Mechanics.LivingItems;
using TidyUp.Mechanics.ShelfSort;
using TidyUp.Muddle;
using TidyUp.Story;
using TidyUp.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TidyUp.Editor
{
    /// <summary>Shared materials for one grey-box location.</summary>
    public sealed class LocationPalette
    {
        public Material Floor, Wall, Ceiling, Roof, Wood, Accent, Trim, Fabric, WindowGlass, Marker, Ghost, Beacon, LampShade, BulbOn;
        public ParticleSystem Sparkle;
        /// <summary>Interior atmosphere (lighting, haze, grade, window light). Set by the location builder.</summary>
        public Art.MoodPreset Mood;
        /// <summary>Set by BuildRoom.</summary>
        public float WallHeight = 3f;
        /// <summary>Art step 3: mouldings, pilasters, sconces (chandeliers come from BuildLamp).</summary>
        public bool Architecture = true;
        /// <summary>Height of a gallery deck (Library), or 0. Pilasters stop under it.</summary>
        public float GalleryHeight;
        /// <summary>Wood panelling on the lower walls (null: plain walls).</summary>
        public Material Wainscot;
        public const float WainscotHeight = 1.0f;
        /// <summary>Centre height and size of the side-wall windows (mega locations put tall windows high, over the shelves).</summary>
        public float WindowY = 1.65f;
        public Vector2 WindowSize = new Vector2(1.1f, 1.3f);
        /// <summary>Pilasters and sconces along the side walls (off where fixtures line those walls).</summary>
        public bool WallPilasters = true;

        /// <summary>
        /// Art step 2: swaps the plain plaster room for rich surfaces: a patterned wall (damask, brick), a ceiling
        /// (star panels, wooden planks), a floor (parquet, tiles) and panelled wood below the chair rail.
        /// </summary>
        public LocationPalette UseRichSurfaces(string prefix,
            Art.TextureSet wallPattern, Color wallTint, float wallTile,
            Art.TextureSet ceilingPattern, Color ceilingTint, float ceilingTile,
            Art.TextureSet floorPattern, Color floorTint, float floorTile,
            Color panelTint)
        {
            Wall = GreyboxAssets.Textured($"{prefix}_Wall", wallTint, wallPattern, wallTile, wallPattern == Art.TextureSet.Brick ? 0.1f : 0.18f, 1f);
            Ceiling = GreyboxAssets.Textured($"{prefix}_Ceiling", ceilingTint, ceilingPattern, ceilingTile,
                ceilingPattern == Art.TextureSet.StarPanels ? 0.35f : 0.3f, 1f);
            if (ceilingPattern == Art.TextureSet.StarPanels)
            {
                // A painted ceiling catches bounced lamplight: let it glow faintly so the stars read in the dark.
                Ceiling.EnableKeyword("_EMISSION");
                Ceiling.SetTexture("_EmissionMap", Art.ProceduralTextures.Albedo(ceilingPattern));
                Ceiling.SetColor("_EmissionColor", ceilingTint * 0.55f);
                Ceiling.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            Floor = GreyboxAssets.Textured($"{prefix}_Floor", floorTint, floorPattern, floorTile, 0.5f);
            Wainscot = GreyboxAssets.Textured($"{prefix}_Wainscot", panelTint, Art.TextureSet.Wainscot, WainscotHeight, 0.55f, 1f);
            // Box UVs are centred on the box: shift half a tile so one panel sits centred in the band.
            Wainscot.SetTextureOffset("_BaseMap", new Vector2(0.5f, 0.5f));
            EditorUtility.SetDirty(Wainscot);
            return this;
        }

        /// <param name="prefix">Material name prefix, e.g. "Greybox" (Bakery) or "Library".</param>
        /// <param name="floorPattern">Planks, Tiles, ... (Docs/ArtBible.md: textures are tinted by palette colours).</param>
        public static LocationPalette Create(string prefix, Color floor, Color wall, Color wood, Color accent,
            Art.TextureSet floorPattern = Art.TextureSet.Planks)
        {
            float floorTile = floorPattern == Art.TextureSet.Tiles ? 1.6f : 2.4f;
            return new LocationPalette
            {
                Floor = GreyboxAssets.Textured($"{prefix}_Floor", floor, floorPattern, floorTile, 0.42f), // polished: catches lamp reflections
                Wall = GreyboxAssets.Textured($"{prefix}_Wall", wall, Art.TextureSet.Plaster, 2.5f, 0.05f, 0.6f),
                Ceiling = GreyboxAssets.Textured($"{prefix}_Ceiling", Color.Lerp(wall, Color.white, 0.35f), Art.TextureSet.Plaster, 3f, 0.05f, 0.5f),
                Wood = GreyboxAssets.Textured($"{prefix}_Wood", wood, Art.TextureSet.Wood, 1.2f, 0.55f), // varnished
                Accent = GreyboxAssets.Textured($"{prefix}_Accent", accent, Art.TextureSet.Plaster, 1.5f, 0.3f, 0.35f), // painted wood
                Trim = GreyboxAssets.Textured($"{prefix}_Trim", Color.Lerp(wood, Color.white, 0.15f), Art.TextureSet.Wood, 1f, 0.55f),
                Fabric = GreyboxAssets.Textured($"{prefix}_Fabric", accent, Art.TextureSet.Fabric, 0.5f, 0.02f),
                WindowGlass = GreyboxAssets.Emissive("Window_Sky", new Color(0.8f, 0.86f, 0.95f), new Color(0.78f, 0.86f, 1f), 1.8f), // frosty daylight that blooms
                Roof = GreyboxAssets.Textured("Greybox_RoofTiles", Core.Palette.Terracotta, Art.TextureSet.RoofTiles, 2f, 0.2f),
                Marker = GreyboxAssets.Lit("Greybox_SlotMarker", Color.Lerp(wood, Core.Palette.Cream, 0.35f)), // a soft mat, not a white card
                Ghost = GreyboxAssets.TransparentUnlit("Hint_Ghost", new Color(0.75f, 0.9f, 1f, 0.28f)),
                Beacon = GreyboxAssets.AdditiveParticle("Hint_Beacon", new Color(1f, 0.85f, 0.45f, 0.8f)),
                LampShade = GreyboxAssets.Lit("Lamp_Shade", new Color(0.18f, 0.32f, 0.3f)),
                BulbOn = GreyboxAssets.Emissive("Lamp_Bulb_On", new Color(1f, 0.95f, 0.8f), new Color(1f, 0.8f, 0.5f), 4f),
                Sparkle = LocationKit.BuildSparklePrefab(),
            };
        }
    }

    /// <summary>The location side of a scene: systems that belong to one location and load/unload with it.</summary>
    public sealed class LocationRig
    {
        public GameObject Systems;
        public LocationProgress Progress;
        public HintService Hints;
        public LocationRuntime Runtime;
        public Vector3 EntryPosition;
    }

    /// <summary>The player side: player, feedback, memories, Tidy Sense, HUD. Lives in the City scene (or a test scene).</summary>
    public sealed class PlayerRig
    {
        public GameObject Root;
        public FeedbackService Feedback;
        public MemoryService Memories;
        public PlayerInputReader Input;
        public Interactor Interactor;
        public PlayerCarry Carry;
        public FirstPersonController Controller;
        public Camera Camera;
        public HudView Hud;
        public GameMenu Menu;
    }

    public enum NpcHeadwear
    {
        None = 0,
        ChefHat = 1,
        HairBun = 2,
        WizardHat = 3
    }

    /// <summary>
    /// Shared building blocks for grey-box location scenes: data prep, room shell with roof, player,
    /// systems, hints, HUD, memory overlay and fragment, completion stage, decor and NPCs.
    /// Location builders (Bakery, Library, …) only describe their own content.
    /// Order matters: do all asset work first, then NewScene(), then reload assets (NewScene unloads them).
    /// </summary>
    public static class LocationKit
    {
        public const int IgnoreRaycastLayer = 2;
        private const string ItemPrefabFolder = "Assets/_Project/Prefabs/Items/Greybox";
        private const string FxPrefabFolder = "Assets/_Project/Prefabs/FX";

        // ───────────────────────── Data (before NewScene) ─────────────────────────

        /// <summary>Imports all item CSVs and (re)builds grey-box prefabs for items without explicit art.</summary>
        public static Dictionary<string, ItemDefinition> ImportItems()
        {
            Dictionary<string, ItemDefinition> items = ItemImporter.ImportAll(out Dictionary<string, GreyboxSpec> greybox);
            foreach (KeyValuePair<string, GreyboxSpec> pair in greybox)
            {
                if (!items.TryGetValue(pair.Key, out ItemDefinition definition)) continue;
                definition.SetPrefab(BuildGreyboxPrefab(definition, pair.Value));
                EditorUtility.SetDirty(definition);
            }
            AssetDatabase.SaveAssets();
            return items;
        }

        public static List<ItemDefinition> ItemsForLocation(Dictionary<string, ItemDefinition> items, string locationId)
        {
            var result = new List<ItemDefinition>();
            foreach (ItemDefinition definition in items.Values)
            {
                if (definition.HomeLocationId == locationId) result.Add(definition);
            }
            result.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return result;
        }

        public static LocationDefinition PrepareLocation(string assetPath, string id, string displayName, string district,
            string sceneName, MechanicFamily[] families, int muddleSeed, IReadOnlyList<ItemDefinition> items, MemoryDefinition memory)
        {
            EditorFolders.Ensure(System.IO.Path.GetDirectoryName(assetPath)?.Replace('\\', '/'));
            var location = AssetDatabase.LoadAssetAtPath<LocationDefinition>(assetPath);
            var itemArray = new ItemDefinition[items.Count];
            for (int i = 0; i < items.Count; i++) itemArray[i] = items[i];

            if (location == null)
            {
                location = ScriptableObject.CreateInstance<LocationDefinition>();
                AssetDatabase.CreateAsset(location, assetPath);
                location.Configure(id, displayName, district, sceneName, families, itemArray, muddleSeed, 1f);
            }
            else
            {
                // Keep designer-tuned values (seed, fraction); refresh the item list from the CSVs and the
                // scene address (owned by the builder: it's the Addressables address of the location scene).
                location.Configure(location.Id, location.DisplayName, location.District, sceneName,
                    location.MechanicFamilies, itemArray, location.MuddleSeed, location.MuddleFraction);
            }
            location.SetMemory(memory);
            EditorUtility.SetDirty(location);
            AssetDatabase.SaveAssets();
            return location;
        }

        /// <summary>Creates the memory asset with this text only if it doesn't exist; afterwards the asset is the source of truth.</summary>
        public static MemoryDefinition LoadOrCreateMemory(string assetPath, string id, string title, string locationName,
            MemoryLine[] lines, string clue)
        {
            EditorFolders.Ensure(System.IO.Path.GetDirectoryName(assetPath)?.Replace('\\', '/'));
            var memory = AssetDatabase.LoadAssetAtPath<MemoryDefinition>(assetPath);
            if (memory != null) return memory;
            memory = ScriptableObject.CreateInstance<MemoryDefinition>();
            AssetDatabase.CreateAsset(memory, assetPath);
            memory.Configure(id, title, locationName, lines, clue);
            EditorUtility.SetDirty(memory);
            return memory;
        }

        private static GameObject BuildGreyboxPrefab(ItemDefinition definition, GreyboxSpec spec)
        {
            EditorFolders.Ensure(ItemPrefabFolder);
            var root = new GameObject(definition.DisplayName);
            Rigidbody body = root.AddComponent<Rigidbody>();
            body.mass = 0.3f;
            body.interpolation = RigidbodyInterpolation.None; // switched on only while an item really moves
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            root.AddComponent<TidyItem>().SetDefinition(definition);

            Vector3 size = spec.Size;

            // One generated mesh per item (bottom-centred pivot, matches slot snap points), one renderer,
            // submeshes for multi-material items. Materials are shared per colour (SRP Batcher friendly).
            Art.ItemShapes.Build(definition.Id, spec, out Mesh built, out Material[] materials);
            Mesh mesh = Art.MeshAssets.GetOrCreate("Item_" + definition.Id, () => built, rebuild: true);

            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            visual.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer itemRenderer = visual.AddComponent<MeshRenderer>();
            itemRenderer.sharedMaterials = materials;
            // Indoors under a roof the sun shadow already covers everything: item shadows would be thousands of
            // invisible shadow-map draws. Items still receive light and shadow.
            itemRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            BoxCollider collider = visual.AddComponent<BoxCollider>();
            collider.center = mesh.bounds.center;
            collider.size = mesh.bounds.size;

            if (definition.Living != LivingBehaviour.None) AddPersonality(root, visual, definition, mesh, materials.Length, size);

            // Items layer: the player walks through piles, and the Muddle never stacks piles on items.
            int itemsLayer = TidyLayersEditor.EnsureItemsLayer();
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = itemsLayer;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{ItemPrefabFolder}/{definition.Id}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>
        /// Living items get googly eyes on their front (-Z, facing the player on a shelf). Disguised items also
        /// get a stand-in look: the same silhouette in dull stone grey, no eyes, hidden until the Muddle scatters them.
        /// </summary>
        private static void AddPersonality(GameObject root, GameObject visual, ItemDefinition definition,
            Mesh mesh, int materialCount, Vector3 size)
        {
            Material white = GreyboxAssets.Lit("Eye_White", Color.white);
            Material pupil = GreyboxAssets.Lit("Eye_Pupil", new Color(0.08f, 0.06f, 0.06f));
            float eye = Mathf.Clamp(Mathf.Min(size.x, size.y) * 0.3f, 0.012f, 0.035f);
            var eyes = new GameObject("Eyes").transform;
            eyes.SetParent(root.transform, false);
            eyes.localPosition = new Vector3(0f, size.y * 0.62f, -size.z * 0.5f - eye * 0.2f);
            for (int side = -1; side <= 1; side += 2)
            {
                var x = new Vector3(side * Mathf.Max(eye * 0.65f, size.x * 0.18f), 0f, 0f);
                AddPrimitive(PrimitiveType.Sphere, side < 0 ? "Eye L" : "Eye R", eyes, x, Vector3.one * eye, white);
                AddPrimitive(PrimitiveType.Sphere, "Pupil", eyes, x + new Vector3(0f, -eye * 0.1f, -eye * 0.35f), Vector3.one * eye * 0.5f, pupil);
            }

            GameObject disguise = null;
            if (definition.Living == LivingBehaviour.Disguised)
            {
                disguise = new GameObject("Disguise Visual");
                disguise.transform.SetParent(root.transform, false);
                disguise.AddComponent<MeshFilter>().sharedMesh = mesh;
                Material stone = GreyboxAssets.Textured("Disguise_Stone", new Color(0.62f, 0.6f, 0.57f), Art.TextureSet.Stone, 0.4f, 0.1f);
                var stones = new Material[materialCount];
                for (int i = 0; i < stones.Length; i++) stones[i] = stone;
                disguise.AddComponent<MeshRenderer>().sharedMaterials = stones;
                BoxCollider c = disguise.AddComponent<BoxCollider>();
                c.center = mesh.bounds.center;
                c.size = mesh.bounds.size;
                disguise.SetActive(false);
            }

            Material shimmer = GreyboxAssets.TransparentUnlit("Living_Shimmer", new Color(0.85f, 0.75f, 1f, 0.18f));
            root.AddComponent<LivingItem>().EditorConfigure(visual, disguise, eyes, shimmer);
        }

        public static ParticleSystem BuildSparklePrefab()
        {
            EditorFolders.Ensure(FxPrefabFolder);
            var go = new GameObject("Sparkle");
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.3f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.035f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.6f), new Color(1f, 0.8f, 0.4f));
            main.gravityModifier = -0.05f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 40;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.08f;

            ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;

            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = GreyboxAssets.AdditiveParticle("FX_Sparkle", Color.white);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{FxPrefabFolder}/Sparkle.prefab");
            Object.DestroyImmediate(go);
            return prefab.GetComponent<ParticleSystem>();
        }

        // ───────────────────────── Scene ─────────────────────────

        /// <summary>
        /// New scene. With sun: warm sunlight (standalone test scenes, the City). Without: an empty scene for
        /// additively loaded location content, which uses the City's sun and lighting.
        /// </summary>
        public static Scene NewScene(bool withSun = true)
        {
            if (!withSun) return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.GetComponent<Camera>() != null)
                {
                    Object.DestroyImmediate(root);
                    continue;
                }
                Light light = root.GetComponent<Light>();
                if (light != null && light.type == LightType.Directional)
                {
                    light.color = new Color(1f, 0.93f, 0.8f);
                    light.intensity = 1.3f;
                }
            }
            return scene;
        }

        /// <summary>
        /// Floor, four walls, ceiling and a pitched tiled roof (ridge along X). The shell doesn't cast sun
        /// shadows: interiors stay evenly bright and cozy (and it saves shadow-map cost on Quest).
        /// </summary>
        public static Transform BuildRoom(string name, Vector2 size, float wallHeight, LocationPalette p)
        {
            var room = new GameObject(name).transform;
            const float wall = 0.2f;
            float hx = size.x * 0.5f;
            float hz = size.y * 0.5f;

            Shell(GreyboxAssets.Box("Floor", room, new Vector3(0f, -0.1f, 0f), new Vector3(size.x, 0.2f, size.y), p.Floor));
            Shell(GreyboxAssets.Box("Wall N", room, new Vector3(0f, wallHeight * 0.5f, hz), new Vector3(size.x, wallHeight, wall), p.Wall));
            Shell(GreyboxAssets.Box("Wall S", room, new Vector3(0f, wallHeight * 0.5f, -hz), new Vector3(size.x, wallHeight, wall), p.Wall));
            Shell(GreyboxAssets.Box("Wall E", room, new Vector3(hx, wallHeight * 0.5f, 0f), new Vector3(wall, wallHeight, size.y), p.Wall));
            Shell(GreyboxAssets.Box("Wall W", room, new Vector3(-hx, wallHeight * 0.5f, 0f), new Vector3(wall, wallHeight, size.y), p.Wall));
            Shell(GreyboxAssets.Box("Ceiling", room, new Vector3(0f, wallHeight + 0.1f, 0f), new Vector3(size.x + wall, 0.2f, size.y + wall), p.Ceiling));
            p.WallHeight = wallHeight;
            BuildTrimAndWindows(room, size, wallHeight, wall, p);
            if (p.Wainscot != null) BuildWainscot(room, size, wall, p);

            const float pitch = 30f;
            const float overhang = 0.35f;
            float halfDepth = hz + overhang;
            float rise = halfDepth * Mathf.Tan(pitch * Mathf.Deg2Rad);
            float slope = halfDepth / Mathf.Cos(pitch * Mathf.Deg2Rad);
            float baseY = wallHeight + 0.2f;
            var roofSize = new Vector3(size.x + 2f * overhang, 0.12f, slope);
            Transform north = Shell(GreyboxAssets.Box("Roof N", room, new Vector3(0f, baseY + rise * 0.5f, halfDepth * 0.5f), roofSize, p.Roof)).transform;
            north.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            Transform south = Shell(GreyboxAssets.Box("Roof S", room, new Vector3(0f, baseY + rise * 0.5f, -halfDepth * 0.5f), roofSize, p.Roof)).transform;
            south.localRotation = Quaternion.Euler(-pitch, 0f, 0f);
            return room;
        }

        /// <summary>
        /// Raised-panel wainscoting round the lower walls with a chair rail on top (art step 2). Thin, so fixtures
        /// standing against the wall cover it naturally.
        /// </summary>
        private static void BuildWainscot(Transform room, Vector2 size, float wall, LocationPalette p)
        {
            var band = new GameObject("Wainscot").transform;
            band.SetParent(room, false);
            const float depth = 0.022f, railH = 0.06f, railD = 0.045f;
            float h = LocationPalette.WainscotHeight;
            float ix = size.x * 0.5f - wall * 0.5f, iz = size.y * 0.5f - wall * 0.5f;
            foreach (int sign in new[] { -1, 1 })
            {
                Shell(GreyboxAssets.Box("Panels Z", band, new Vector3(0f, h * 0.5f, sign * (iz - depth * 0.5f)), new Vector3(size.x - wall, h, depth), p.Wainscot, keepCollider: false));
                Shell(GreyboxAssets.Box("Panels X", band, new Vector3(sign * (ix - depth * 0.5f), h * 0.5f, 0f), new Vector3(depth, h, size.y - wall), p.Wainscot, keepCollider: false));
                Shell(GreyboxAssets.Box("Chair Rail Z", band, new Vector3(0f, h + railH * 0.5f, sign * (iz - railD * 0.5f)), new Vector3(size.x - wall, railH, railD), p.Trim, keepCollider: false));
                Shell(GreyboxAssets.Box("Chair Rail X", band, new Vector3(sign * (ix - railD * 0.5f), h + railH * 0.5f, 0f), new Vector3(railD, railH, size.y - wall), p.Trim, keepCollider: false));
            }
        }

        /// <summary>Baseboards and a cornice round the room, and framed windows (glowing sky panes) on the side walls.</summary>
        private static void BuildTrimAndWindows(Transform room, Vector2 size, float wallHeight, float wall, LocationPalette p)
        {
            var trim = new GameObject("Trim").transform;
            trim.SetParent(room, false);
            float ix = size.x * 0.5f - wall * 0.5f; // inner wall faces
            float iz = size.y * 0.5f - wall * 0.5f;
            const float baseH = 0.14f, baseD = 0.03f, corniceH = 0.1f, corniceD = 0.06f;
            foreach (int sign in new[] { -1, 1 })
            {
                Shell(GreyboxAssets.Box("Baseboard Z", trim, new Vector3(0f, baseH * 0.5f, sign * (iz - baseD * 0.5f)), new Vector3(size.x - wall, baseH, baseD), p.Trim, keepCollider: false));
                Shell(GreyboxAssets.Box("Baseboard X", trim, new Vector3(sign * (ix - baseD * 0.5f), baseH * 0.5f, 0f), new Vector3(baseD, baseH, size.y - wall), p.Trim, keepCollider: false));
                if (p.Architecture)
                {
                    // Crown moulding: a proper profile instead of a plain strip.
                    Art.ArchitectureKit.CrownRun(trim, new Vector3(0f, wallHeight - 0.22f, sign * iz), Quaternion.Euler(0f, sign > 0 ? 0f : 180f, 0f), size.x - wall, p.Trim);
                    Art.ArchitectureKit.CrownRun(trim, new Vector3(sign * ix, wallHeight - 0.22f, 0f), Quaternion.Euler(0f, sign * 90f, 0f), size.y - wall, p.Trim);
                    continue;
                }
                Shell(GreyboxAssets.Box("Cornice Z", trim, new Vector3(0f, wallHeight - corniceH * 0.5f, sign * (iz - corniceD * 0.5f)), new Vector3(size.x - wall, corniceH, corniceD), p.Trim, keepCollider: false));
                Shell(GreyboxAssets.Box("Cornice X", trim, new Vector3(sign * (ix - corniceD * 0.5f), wallHeight - corniceH * 0.5f, 0f), new Vector3(corniceD, corniceH, size.y - wall), p.Trim, keepCollider: false));
            }

            // Windows: one every ~3 m along the east and west walls, between the walls' ends.
            int count = Mathf.Max(1, Mathf.FloorToInt((size.y - 1f) / Mathf.Max(3f, p.WindowSize.x + 2f)));
            for (int i = 0; i < count; i++)
            {
                float z = Mathf.Lerp(-size.y * 0.5f, size.y * 0.5f, (i + 1f) / (count + 1f));
                foreach (int sign in new[] { -1, 1 })
                {
                    var window = new GameObject($"Window {(sign < 0 ? "W" : "E")}{i + 1}").transform;
                    window.SetParent(trim, false);
                    window.localPosition = new Vector3(sign * ix, p.WindowY, z);
                    window.localRotation = Quaternion.Euler(0f, sign * 90f, 0f); // local -Z faces into the room (west wall: -90, east: +90)
                    BuildWindow(window, p.WindowSize, p);
                    if (p.Mood != null) Art.MoodAssets.AddWindowLight(window, p.WindowSize, p.Mood.WindowLight, p.Mood.WindowLightStrength);
                }
            }

            if (!p.Architecture || !p.WallPilasters) return;

            // Art step 3: pilasters with candle sconces between the windows, by the door, and sconces over the
            // north wall's tall fixtures. Pilasters stop under a gallery when there is one.
            float pilasterTop = p.GalleryHeight > 0f ? p.GalleryHeight : wallHeight - 0.22f;
            var arch = new GameObject("Architecture").transform;
            arch.SetParent(room, false);
            for (int i = 0; i <= count; i++)
            {
                float z = Mathf.Lerp(-size.y * 0.5f, size.y * 0.5f, (i + 0.5f) / (count + 1f));
                foreach (int sign in new[] { -1, 1 })
                {
                    Quaternion facing = Quaternion.Euler(0f, sign * 90f, 0f);
                    Art.ArchitectureKit.Pilaster(arch, new Vector3(sign * ix, 0f, z), facing, pilasterTop, p.Wood, p.Trim);
                    Art.ArchitectureKit.Sconce(arch, new Vector3(sign * (ix - 0.05f), 2.15f, z), facing);
                }
            }
            foreach (int side in new[] { -1, 1 })
            {
                Quaternion southFacing = Quaternion.Euler(0f, 180f, 0f);
                Art.ArchitectureKit.Pilaster(arch, new Vector3(side * 0.95f, 0f, -iz), southFacing, pilasterTop, p.Wood, p.Trim);
                Art.ArchitectureKit.Sconce(arch, new Vector3(side * 0.95f, 1.95f, -iz + 0.05f), southFacing);
            }
            if (wallHeight >= 3.4f)
            {
                int northSconces = Mathf.Max(2, Mathf.RoundToInt((size.x - 2f) / 4f));
                for (int i = 0; i < northSconces; i++)
                {
                    float x = Mathf.Lerp(-size.x * 0.5f + 1f, size.x * 0.5f - 1f, (i + 0.5f) / northSconces);
                    Art.ArchitectureKit.Sconce(arch, new Vector3(x, 2.55f, iz), Quaternion.identity);
                }
            }
        }

        /// <summary>A framed window on a wall: glowing sky pane, cross mullions, sill. Local -Z faces the room.</summary>
        public static void BuildWindow(Transform window, Vector2 size, LocationPalette p, Material glass = null)
        {
            const float frame = 0.07f;
            const float depth = 0.06f;
            float w = size.x, h = size.y;
            Shell(GreyboxAssets.Box("Glass", window, new Vector3(0f, 0f, -0.005f), new Vector3(w, h, 0.01f), glass != null ? glass : p.WindowGlass, keepCollider: false));
            Shell(GreyboxAssets.Box("Frame Top", window, new Vector3(0f, h * 0.5f, -depth * 0.5f), new Vector3(w + frame * 2f, frame, depth), p.Trim, keepCollider: false));
            Shell(GreyboxAssets.Box("Frame Bottom", window, new Vector3(0f, -h * 0.5f, -depth * 0.5f), new Vector3(w + frame * 2f, frame, depth), p.Trim, keepCollider: false));
            Shell(GreyboxAssets.Box("Frame L", window, new Vector3(-w * 0.5f, 0f, -depth * 0.5f), new Vector3(frame, h, depth), p.Trim, keepCollider: false));
            Shell(GreyboxAssets.Box("Frame R", window, new Vector3(w * 0.5f, 0f, -depth * 0.5f), new Vector3(frame, h, depth), p.Trim, keepCollider: false));
            Shell(GreyboxAssets.Box("Mullion V", window, new Vector3(0f, 0f, -depth * 0.4f), new Vector3(frame * 0.6f, h, depth * 0.8f), p.Trim, keepCollider: false));
            Shell(GreyboxAssets.Box("Mullion H", window, new Vector3(0f, h * 0.1f, -depth * 0.4f), new Vector3(w, frame * 0.6f, depth * 0.8f), p.Trim, keepCollider: false));
            Shell(GreyboxAssets.Box("Sill", window, new Vector3(0f, -h * 0.5f - frame * 0.5f, -0.09f), new Vector3(w + frame * 3f, 0.05f, 0.18f), p.Trim, keepCollider: false));
        }

        private static GameObject Shell(GameObject go)
        {
            // The shell casts shadows: the street's sun must not light the interior through walls and roof.
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;
            GreyboxAssets.MarkStatic(go);
            return go;
        }

        /// <summary>Static furniture box (casts shadows, marked static).</summary>
        public static GameObject Furniture(string name, Transform parent, Vector3 position, Vector3 size, Material material)
        {
            GameObject go = GreyboxAssets.Box(name, parent, position, size, material);
            GreyboxAssets.MarkStatic(go);
            return go;
        }

        /// <summary>
        /// Puts each slot's item in place (the tidy state). Exact-item slots get that item; type slots get the
        /// least-used matching item, so a shelf of four different potions gets each once, while a bakery row
        /// with one baguette definition gets four baguettes.
        /// </summary>
        public static void FillTidyState(SlotGroup group, IReadOnlyList<ItemDefinition> items, Transform itemsRoot)
        {
            var uses = new Dictionary<ItemDefinition, int>();
            foreach (ItemSlot slot in group.Slots)
            {
                ItemDefinition definition = FindItemFor(slot.Definition, items, uses);
                if (definition == null || definition.Prefab == null)
                {
                    Debug.LogWarning($"[Tidy Up] No item for slot '{slot.name}' in '{group.name}'.", slot);
                    continue;
                }
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(definition.Prefab);
                instance.transform.SetParent(itemsRoot, false);
                instance.transform.SetPositionAndRotation(slot.SnapPoint.position, slot.SnapPoint.rotation);
                slot.EditorSetStartOccupant(instance.GetComponent<TidyItem>());
            }
        }

        private static ItemDefinition FindItemFor(SlotDefinition slot, IReadOnlyList<ItemDefinition> items, Dictionary<ItemDefinition, int> uses)
        {
            ItemDefinition best = null;
            int bestUses = int.MaxValue;
            for (int i = 0; i < items.Count; i++)
            {
                if (!SlotRules.Accepts(slot, items[i])) continue;
                uses.TryGetValue(items[i], out int used);
                if (used < bestUses)
                {
                    best = items[i];
                    bestUses = used;
                }
            }
            if (best != null) uses[best] = bestUses + 1;
            return best;
        }

        /// <summary>Items drop from the top of this box onto whatever is below (floor, tables).</summary>
        public static BoxCollider BuildScatterArea(Vector3 center, Vector3 size)
        {
            var go = new GameObject("Muddle Scatter Area");
            go.layer = IgnoreRaycastLayer;
            go.transform.position = center;
            BoxCollider box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            return box;
        }

        /// <summary>
        /// Location-side systems: progress, Muddle, hints, living-item director, the LocationRuntime (entry point,
        /// drop point), and the exit door on the south wall. Player services are resolved at runtime through
        /// GameServices, so the same scene works additively under the City or next to a test player rig.
        /// </summary>
        public static LocationRig BuildLocationSystems(LocationDefinition location, SlotGroup[] groups, BoxCollider[] scatterAreas,
            Vector2 roomSize, LocationPalette palette)
        {
            var rig = new LocationRig { Systems = new GameObject("Location Systems") };
            rig.Progress = rig.Systems.AddComponent<LocationProgress>();
            rig.Progress.EditorConfigure(location, groups);
            MuddleScatterer muddle = rig.Systems.AddComponent<MuddleScatterer>();
            muddle.EditorConfigure(location, groups, scatterAreas);
            rig.Hints = rig.Systems.AddComponent<HintService>();
            rig.Hints.EditorConfigure(rig.Progress, palette.Ghost);

            // Living items (family I) need a roam area; the player and feedback come from GameServices.
            LivingItem[] living = Object.FindObjectsByType<LivingItem>(FindObjectsInactive.Include);
            if (living.Length > 0)
            {
                LivingItemDirector director = rig.Systems.AddComponent<LivingItemDirector>();
                director.EditorConfigure(null, scatterAreas.Length > 0 ? scatterAreas[0] : null, null);
                foreach (LivingItem item in living)
                {
                    item.EditorSetDirector(director);
                    EditorUtility.SetDirty(item);
                }
            }

            // Entrance on the south wall: the player arrives a step inside, facing the shelves.
            float southWall = -roomSize.y * 0.5f;
            rig.EntryPosition = new Vector3(0f, 0.05f, southWall + 1.1f);
            var entry = new GameObject("Entry Point").transform;
            entry.SetParent(rig.Systems.transform, false);
            entry.SetPositionAndRotation(rig.EntryPosition, Quaternion.identity);
            var drop = new GameObject("Drop Point (items set down when leaving)").transform;
            drop.SetParent(rig.Systems.transform, false);
            drop.position = new Vector3(0.9f, 0f, southWall + 0.6f);
            BuildExitDoor(rig.Systems.transform, new Vector3(0f, 0f, southWall + 0.12f), palette);

            if (palette.Mood != null) Art.MoodAssets.Build(rig.Systems.transform, palette.Mood, roomSize, palette.WallHeight);
            CleaningProgress cleaning = BuildCleaning(rig.Systems, location, groups, roomSize);
            rig.Runtime = rig.Systems.AddComponent<LocationRuntime>();
            rig.Runtime.EditorConfigure(location, rig.Progress, rig.Hints, muddle, entry, drop, cleaning);
            return rig;
        }


        /// <summary>
        /// The systems of one room of a mega location (its own streamed scene): progress, Muddle, hints and cleaning,
        /// all saved under "location/zone", and a LocationRuntime the shell's MegaLocation makes active when the
        /// player walks in. No door, entry or mood: the shell owns those.
        /// </summary>
        public static LocationRig BuildZoneSystems(LocationDefinition location, string zoneId, string zoneName, SlotGroup[] groups,
            BoxCollider[] scatterAreas, Rect area, LocationPalette palette)
        {
            string key = Locations.MegaLocation.ZoneKey(location.Id, zoneId);
            var rig = new LocationRig { Systems = new GameObject($"Zone Systems ({zoneName})") };
            rig.Progress = rig.Systems.AddComponent<LocationProgress>();
            rig.Progress.EditorConfigure(location, groups);
            MuddleScatterer muddle = rig.Systems.AddComponent<MuddleScatterer>();
            muddle.EditorConfigure(location, groups, scatterAreas, key);
            rig.Hints = rig.Systems.AddComponent<HintService>();
            rig.Hints.EditorConfigure(rig.Progress, palette.Ghost);
            var centre = new Vector3(area.center.x, 0f, area.center.y);
            CleaningProgress cleaning = BuildCleaning(rig.Systems, location, groups, area.size, centre, key);
            rig.Runtime = rig.Systems.AddComponent<LocationRuntime>();
            rig.Runtime.EditorConfigure(location, rig.Progress, rig.Hints, muddle, null, null, cleaning, zoneId, zoneName);
            rig.EntryPosition = centre;
            return rig;
        }

        // ───────────── Cleaning (family K) ─────────────

        /// <summary>Makes a piece of furniture an optional cleaning goal (dust washes its colours; scrub to clean).</summary>
        public static Cleanable MakeCleanable(GameObject go, string label)
        {
            Cleanable cleanable = GetOrAdd<Cleanable>(go);
            cleanable.EditorConfigure(label, required: false, Cleanable.DirtLook.Tint, CleanGame.Wipe, 3, null, null);
            return cleanable;
        }

        /// <summary>
        /// Every fixture with slots must be cleaned before it takes items; windows and floor smudges are optional.
        /// Builders mark extra furniture with MakeCleanable before this runs. Order is by hierarchy path, so the
        /// saved clean flags stay stable between builds.
        /// </summary>
        private static CleaningProgress BuildCleaning(GameObject systems, LocationDefinition location, SlotGroup[] groups, Vector2 roomSize,
            Vector3 centre = default, string stateKey = null)
        {
            DustBunny bunny = Art.DustAssets.DustBunnyPrefab(); // fixtures are cleaned by poofing dust bunnies
            foreach (SlotGroup group in groups)
            {
                Cleanable fixture = GetOrAdd<Cleanable>(group.gameObject);
                fixture.EditorConfigure(group.Label, required: true, Cleanable.DirtLook.Tint, CleanGame.DustBunnies, 3, group, bunny);
            }

            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (!t.name.StartsWith("Window ") || t.parent == null || t.parent.name != "Trim") continue;
                BoxCollider target = GetOrAdd<BoxCollider>(t.gameObject);
                target.isTrigger = true;
                target.center = new Vector3(0f, 0f, -0.06f);
                target.size = new Vector3(1.25f, 1.45f, 0.12f);
                Cleanable window = GetOrAdd<Cleanable>(t.gameObject);
                window.EditorConfigure("Window", required: false, Cleanable.DirtLook.Tint, CleanGame.Wipe, 3, null, null);
            }

            Material dustMaterial = Art.DustAssets.SmudgeMaterial();
            Mesh dustQuad = Art.DustAssets.FloorQuad();
            foreach (Cleanable tinted in Object.FindObjectsByType<Cleanable>(FindObjectsInactive.Include)) AddDustFilms(tinted, dustMaterial, dustQuad);
            BuildFloorSmudges(systems.transform, location, roomSize, centre, stateKey);

            var all = new List<Cleanable>(Object.FindObjectsByType<Cleanable>(FindObjectsInactive.Include));
            all.Sort((a, b) => string.CompareOrdinal(HierarchyPath(a.transform), HierarchyPath(b.transform)));
            CleaningProgress progress = systems.AddComponent<CleaningProgress>();
            progress.EditorConfigure(location, all.ToArray(), stateKey);
            return progress;
        }


        /// <summary>
        /// Soft dust layers on every flat top surface of a cleanable (shelf boards, counter and table tops, sills).
        /// The Cleanable fades them out as it's scrubbed: dust you can see, gone when it's clean.
        /// </summary>
        private static void AddDustFilms(Cleanable cleanable, Material dust, Mesh quad)
        {
            var targets = new List<MeshRenderer>();
            foreach (MeshRenderer renderer in cleanable.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.name.StartsWith(Cleanable.DustFilmName)) return; // already has films (rebuild)
                if (renderer.GetComponentInParent<ItemSlot>(true) != null || renderer.GetComponentInParent<TidyItem>(true) != null) continue;
                Bounds b = renderer.bounds;
                bool flat = b.size.y < 0.08f && b.size.x > 0.15f && b.size.z > 0.1f;
                bool ownTop = renderer.gameObject == cleanable.gameObject && b.size.x > 0.3f && b.size.z > 0.3f; // a table: dust on top
                if (flat || ownTop) targets.Add(renderer);
            }
            foreach (MeshRenderer renderer in targets)
            {
                Bounds b = renderer.bounds;
                var film = new GameObject(Cleanable.DustFilmName);
                film.transform.SetPositionAndRotation(new Vector3(b.center.x, b.max.y + 0.002f, b.center.z), Quaternion.identity);
                film.transform.localScale = new Vector3(b.size.x * 0.94f, 1f, b.size.z * 0.9f);
                film.transform.SetParent(cleanable.transform, true);
                film.AddComponent<MeshFilter>().sharedMesh = quad;
                MeshRenderer filmRenderer = film.AddComponent<MeshRenderer>();
                filmRenderer.sharedMaterial = dust;
                filmRenderer.shadowCastingMode = ShadowCastingMode.Off;
                filmRenderer.receiveShadows = false;
            }
        }
        /// <summary>Soft dust smudges on the floor (optional goals), seeded from the location id so builds match.</summary>
        private static void BuildFloorSmudges(Transform parent, LocationDefinition location, Vector2 roomSize, Vector3 centre = default, string stateKey = null)
        {
            var root = new GameObject("Dust Smudges").transform;
            root.SetParent(parent, false);
            Material material = Art.DustAssets.SmudgeMaterial();
            Mesh quad = Art.DustAssets.FloorQuad();
            int seed = 17;
            if (location != null) foreach (char c in stateKey ?? location.Id) seed = seed * 31 + c;
            var random = new System.Random(seed);
            int count = Mathf.Clamp(Mathf.RoundToInt(roomSize.x * roomSize.y / 8f), 4, 12);
            float halfX = roomSize.x * 0.5f - 1f, halfZ = roomSize.y * 0.5f - 1f;
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject($"Dust Smudge {i + 1:00}");
                go.transform.SetParent(root, false);
                float x = Mathf.Lerp(-halfX, halfX, (float)random.NextDouble());
                float z = Mathf.Lerp(-halfZ + 1f, halfZ, (float)random.NextDouble()); // keep clear of the door
                go.transform.localPosition = centre + new Vector3(x, 0.026f, z); // above rugs and their borders (no flicker)
                go.transform.localRotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
                float size = Mathf.Lerp(0.7f, 1.2f, (float)random.NextDouble());
                go.transform.localScale = new Vector3(size, 1f, size * Mathf.Lerp(0.7f, 1f, (float)random.NextDouble()));
                go.AddComponent<MeshFilter>().sharedMesh = quad;
                MeshRenderer renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                BoxCollider target = go.AddComponent<BoxCollider>();
                target.isTrigger = true;
                target.size = new Vector3(0.8f, 0.05f, 0.8f);
                go.AddComponent<Cleanable>().EditorConfigure("Dusty patch", required: false, Cleanable.DirtLook.Fade, CleanGame.Wipe, 2, null, null);
            }
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T existing = go.GetComponent<T>();
            return existing != null ? existing : go.AddComponent<T>();
        }

        private static string HierarchyPath(Transform t)
        {
            string path = t.name;
            for (Transform p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
            return path;
        }
        /// <summary>Door on the inside of the south wall, leading back to the street.</summary>
        public static void BuildExitDoor(Transform parent, Vector3 floorPosition, LocationPalette p)
        {
            var root = new GameObject("Exit Door");
            root.transform.SetParent(parent, false);
            root.transform.position = floorPosition;
            Material glow = GreyboxAssets.Emissive("Door_Frame_Glow", new Color(1f, 0.85f, 0.55f), new Color(1f, 0.75f, 0.4f), 1.2f);
            AddPrimitive(PrimitiveType.Cube, "Door", root.transform, new Vector3(0f, 1.05f, 0f), new Vector3(1.1f, 2.1f, 0.06f), p.Wood);
            AddPrimitive(PrimitiveType.Cube, "Frame Top", root.transform, new Vector3(0f, 2.15f, 0f), new Vector3(1.3f, 0.1f, 0.1f), glow);
            AddPrimitive(PrimitiveType.Cube, "Frame L", root.transform, new Vector3(-0.6f, 1.05f, 0f), new Vector3(0.1f, 2.1f, 0.1f), glow);
            AddPrimitive(PrimitiveType.Cube, "Frame R", root.transform, new Vector3(0.6f, 1.05f, 0f), new Vector3(0.1f, 2.1f, 0.1f), glow);
            AddPrimitive(PrimitiveType.Sphere, "Knob", root.transform, new Vector3(0.4f, 1.0f, 0.05f), Vector3.one * 0.07f, glow);
            BoxCollider target = root.AddComponent<BoxCollider>();
            target.isTrigger = true;
            target.center = new Vector3(0f, 1.1f, 0.15f);
            target.size = new Vector3(1.3f, 2.2f, 0.4f);
            root.AddComponent<LocationExit>();
        }

        /// <summary>Records the slot count on the location asset (for progress shown on the street).</summary>
        public static void RecordSlotCount(string locationAssetPath, SlotGroup[] groups)
        {
            int total = 0;
            foreach (SlotGroup group in groups) total += group.Slots.Length;
            var location = AssetDatabase.LoadAssetAtPath<LocationDefinition>(locationAssetPath);
            if (location == null) return;
            location.SetTotalSlots(total);
            EditorUtility.SetDirty(location);
        }

        /// <summary>At 100%: the NPC returns and the memory fragment appears (plus a chord).</summary>
        public static void BuildCompletionStage(LocationRig rig, params GameObject[] reveal)
        {
            var stagesRoot = new GameObject("Restoration Stages").transform;
            RestorationStage stage = new GameObject("Stage 100% Location Opens").AddComponent<RestorationStage>();
            stage.transform.SetParent(stagesRoot, false);
            foreach (GameObject go in reveal) go.transform.SetParent(stagesRoot, true);
            stage.EditorConfigure(1f, reveal, null, null, null, null);
            rig.Systems.AddComponent<RestorationController>().EditorConfigure(rig.Progress, null, new[] { stage });
        }

        /// <summary>
        /// The memory's interactable object. buildVisual adds meshes under the given (bobbing) transform.
        /// Starts hidden: the completion stage reveals it, and its first OnEnable announces it.
        /// </summary>
        public static GameObject BuildMemoryFragment(MemoryDefinition memory, string announcement,
            Vector3 position, Quaternion rotation, Vector3 targetSize, Action<Transform> buildVisual)
        {
            var root = new GameObject("Memory Fragment");
            root.transform.SetPositionAndRotation(position, rotation);
            BoxCollider target = root.AddComponent<BoxCollider>(); // generous trigger so it's easy to look at
            target.isTrigger = true;
            target.center = new Vector3(0f, targetSize.y * 0.5f, 0f);
            target.size = targetSize;

            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            buildVisual(visual);

            root.AddComponent<MemoryFragment>().EditorConfigure(memory, null, announcement, visual); // service resolved at runtime
            root.SetActive(false);
            return root;
        }

        /// <summary>Saves the scene. Only the City goes into Build Settings; locations load through Addressables.</summary>
        public static void SaveScene(Scene scene, string path)
        {
            EditorFolders.Ensure(System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/'));
            EditorSceneManager.SaveScene(scene, path);
            AssetDatabase.SaveAssets();
        }

        // ───────────────────────── Player, HUD, overlays ─────────────────────────

        /// <summary>
        /// The player side: feedback, player, memory overlay and service, Tidy Sense, HUD, and the PlayerServices
        /// registrar that publishes them to GameServices.
        /// </summary>
        public static PlayerRig BuildPlayerRig(Vector3 position, float yaw, LocationPalette palette)
        {
            var rig = new PlayerRig { Root = new GameObject("Player Systems") };
            rig.Feedback = rig.Root.AddComponent<FeedbackService>();
            rig.Feedback.EditorConfigure(palette.Sparkle);
            HintBeacon beacon = BuildBeacon(palette.Beacon);

            BuildPlayer(rig, position, yaw);

            MemoryView memoryView = BuildMemoryView(rig.Input);
            rig.Memories = rig.Root.AddComponent<MemoryService>();
            rig.Memories.EditorConfigure(rig.Input, memoryView, rig.Feedback);

            TidySense tidySense = rig.Interactor.gameObject.AddComponent<TidySense>();
            tidySense.EditorConfigure(rig.Input, rig.Camera.transform, rig.Carry, null, null, beacon, rig.Feedback);

            rig.Hud = BuildHud(rig, tidySense);
            // Pause menu (Esc / Start). The City wires its SaveService in; test scenes get resume/controls/quit.
            rig.Menu = new GameObject("Pause Menu").AddComponent<GameMenu>();
            rig.Menu.EditorConfigure(MenuMode.Pause, string.Empty, null, rig.Input);
            rig.Root.AddComponent<PlayerServices>().EditorConfigure(rig.Feedback, rig.Input, rig.Carry, rig.Interactor, rig.Controller, rig.Memories);
            return rig;
        }

        private static void BuildPlayer(PlayerRig rig, Vector3 position, float yaw)
        {
            var player = new GameObject("Player");
            player.layer = IgnoreRaycastLayer; // keeps the look ray and muddle drops off the player capsule
            player.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

            CharacterController controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.3f;
            controller.center = new Vector3(0f, 0.9f, 0f);

            rig.Input = player.AddComponent<PlayerInputReader>();
            rig.Carry = player.AddComponent<PlayerCarry>();
            player.AddComponent<CarryUpgrades>().EditorConfigure(rig.Carry); // hands → basket → trolley → satchel

            var cameraGo = new GameObject("Player Camera");
            cameraGo.tag = "MainCamera";
            cameraGo.transform.SetParent(player.transform, false);
            cameraGo.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            rig.Camera = cameraGo.AddComponent<Camera>();
            rig.Camera.nearClipPlane = 0.05f;
            rig.Camera.fieldOfView = 70f;
            rig.Camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var cameraData = rig.Camera.GetUniversalAdditionalCameraData();
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing; // clean edges on thousands of small items
            cameraData.antialiasingQuality = AntialiasingQuality.High;

            // What you carry, in your hands: drawn by an overlay camera on its own layer so it never clips into
            // shelves or walls, on top of the main view.
            int heldLayer = TidyLayersEditor.EnsureHeldLayer();
            rig.Camera.cullingMask &= ~(1 << heldLayer);
            var heldCameraGo = new GameObject("Held Items Camera");
            heldCameraGo.transform.SetParent(cameraGo.transform, false);
            Camera heldCamera = heldCameraGo.AddComponent<Camera>();
            heldCamera.fieldOfView = rig.Camera.fieldOfView;
            heldCamera.nearClipPlane = 0.02f;
            heldCamera.farClipPlane = 4f;
            heldCamera.cullingMask = 1 << heldLayer;
            var heldData = heldCamera.GetUniversalAdditionalCameraData();
            heldData.renderType = CameraRenderType.Overlay;
            heldData.renderShadows = false;
            heldData.renderPostProcessing = false;
            cameraData.cameraStack.Add(heldCamera);
            player.AddComponent<HeldItemsView>().EditorConfigure(rig.Carry, cameraGo.transform);
            cameraGo.AddComponent<AudioListener>();

            rig.Controller = player.AddComponent<FirstPersonController>();
            rig.Controller.EditorConfigure(rig.Input, cameraGo.transform);
            rig.Interactor = player.AddComponent<Interactor>();
            rig.Interactor.EditorConfigure(rig.Input, rig.Camera, rig.Carry, rig.Feedback);
        }

        private static HintBeacon BuildBeacon(Material material)
        {
            var root = new GameObject("Tidy Sense Beacon");
            GameObject orb = AddPrimitive(PrimitiveType.Sphere, "Orb", root.transform, new Vector3(0f, 0.12f, 0f), Vector3.one * 0.22f, material);
            GameObject column = AddPrimitive(PrimitiveType.Cylinder, "Column", root.transform, new Vector3(0f, 1.2f, 0f), new Vector3(0.05f, 1.2f, 0.05f), material);
            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            HintBeacon beacon = root.AddComponent<HintBeacon>();
            beacon.EditorConfigure(orb.transform, column.transform);
            return beacon; // left active: HintBeacon hides itself in Awake
        }

        private static HudView BuildHud(PlayerRig rig, TidySense tidySense)
        {
            Font font = BuiltinFont;
            var canvasGo = new GameObject("HUD");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            AddScaler(canvasGo);
            canvasGo.AddComponent<CanvasGroup>(); // lets the HUD step aside while a memory plays

            var reticle = new GameObject("Reticle", typeof(RectTransform), typeof(Image));
            reticle.transform.SetParent(canvasGo.transform, false);
            ((RectTransform)reticle.transform).sizeDelta = new Vector2(6f, 6f);
            reticle.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.85f);
            reticle.GetComponent<Image>().raycastTarget = false;

            Text focus = CreateText(canvasGo.transform, "Focus Label", font, 30, TextAnchor.UpperCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -28f), new Vector2(1100f, 50f), Color.white);
            Text message = CreateText(canvasGo.transform, "Message", font, 26, TextAnchor.UpperCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -90f), new Vector2(1200f, 50f), new Color(1f, 0.85f, 0.55f));
            Text inventory = CreateText(canvasGo.transform, "Inventory Strip", font, 24, TextAnchor.LowerCenter,
                new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1800f, 60f), Color.white);
            Text checklist = CreateText(canvasGo.transform, "Checklist", font, 24, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(30f, -30f), new Vector2(600f, 240f), Color.white);
            Text help = CreateText(canvasGo.transform, "Controls", font, 18, TextAnchor.UpperRight,
                new Vector2(1f, 1f), new Vector2(-30f, -30f), new Vector2(520f, 200f), new Color(1f, 1f, 1f, 0.7f));
            help.text = "WASD move · Shift sprint\nLMB / E  pick up · place\nQ / Tab / scroll  choose item\nG / RMB  drop · F  Tidy Sense\nM  map · Esc  menu";
            Text sense = CreateText(canvasGo.transform, "Tidy Sense", font, 22, TextAnchor.LowerCenter,
                new Vector2(0.5f, 0f), new Vector2(0f, 85f), new Vector2(600f, 40f), new Color(1f, 0.85f, 0.55f));

            canvasGo.AddComponent<MapView>(); // building map inside mega locations (builds itself at runtime)
            HudView hud = canvasGo.AddComponent<HudView>();
            hud.EditorConfigure(rig.Interactor, rig.Carry, tidySense, rig.Memories, focus, inventory, checklist, message, sense);
            return hud;
        }

        /// <summary>Full-screen memory overlay: warm sepia veil over the room, centred text on a soft band.</summary>
        private static MemoryView BuildMemoryView(PlayerInputReader input)
        {
            Font font = BuiltinFont;
            var canvasGo = new GameObject("Memory Overlay");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10; // above the HUD
            AddScaler(canvasGo);
            CanvasGroup group = canvasGo.AddComponent<CanvasGroup>();

            Stretch(new GameObject("Sepia Veil", typeof(RectTransform), typeof(Image)), canvasGo.transform,
                new Color(0.36f, 0.25f, 0.15f, 0.88f), Vector2.zero, Vector2.one, Vector2.zero);
            GameObject band = Stretch(new GameObject("Text Band", typeof(RectTransform), typeof(Image)), canvasGo.transform,
                new Color(0.2f, 0.13f, 0.08f, 0.45f), new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 300f));
            ((RectTransform)band.transform).anchoredPosition = new Vector2(0f, 10f);

            Text title = CreateText(canvasGo.transform, "Title", font, 56, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1400f, 200f), new Color(1f, 0.92f, 0.75f));
            title.fontStyle = FontStyle.Bold;
            Text speaker = CreateText(canvasGo.transform, "Speaker", font, 26, TextAnchor.LowerCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 110f), new Vector2(1200f, 40f), new Color(1f, 0.78f, 0.5f));
            Text line = CreateText(canvasGo.transform, "Line", font, 36, TextAnchor.UpperCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1200f, 300f), new Color(1f, 0.96f, 0.88f));
            line.horizontalOverflow = HorizontalWrapMode.Wrap;
            Text prompt = CreateText(canvasGo.transform, "Prompt", font, 20, TextAnchor.LowerCenter,
                new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(800f, 40f), new Color(1f, 0.9f, 0.75f, 0.7f));

            MemoryView view = canvasGo.AddComponent<MemoryView>();
            view.EditorConfigure(input, group, title, speaker, line, prompt);
            group.alpha = 0f;
            return view;
        }

        // ───────────────────────── Decor & NPCs ─────────────────────────

        /// <summary>Hanging ceiling lamp, always on (cozy from the start). No shadows (Quest budget).</summary>
        public static void BuildLamp(string name, Transform parent, Vector3 floorPosition, float ceiling, LocationPalette p)
        {
            // Art step 3: a brass chandelier with candles (its downlight is tuned by the location's mood).
            if (p.Architecture)
            {
                Art.ArchitectureKit.Chandelier(name, parent, floorPosition, ceiling);
                return;
            }
            float hang = ceiling - 0.65f;
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.position = new Vector3(floorPosition.x, 0f, floorPosition.z);
            AddPrimitive(PrimitiveType.Sphere, "Bulb", root, new Vector3(0f, hang - 0.08f, 0f), Vector3.one * 0.12f, p.BulbOn);
            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(root, false);
            lightGo.transform.localPosition = new Vector3(0f, hang - 0.15f, 0f);
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.82f, 0.6f);
            light.intensity = 2.5f;
            light.range = 5f;
        }

        public static GameObject BuildFlowers(Transform parent, Vector3 position)
        {
            var root = new GameObject("Flowers");
            root.transform.SetParent(parent, false);
            root.transform.position = position;

            Material vase = GreyboxAssets.Lit("Prop_Vase", new Color(0.25f, 0.45f, 0.75f));
            Material stem = GreyboxAssets.Lit("Prop_Stem", new Color(0.3f, 0.6f, 0.25f));
            Material[] petals =
            {
                GreyboxAssets.Lit("Prop_Petal_Red", new Color(0.9f, 0.25f, 0.3f)),
                GreyboxAssets.Lit("Prop_Petal_Yellow", new Color(1f, 0.82f, 0.2f)),
                GreyboxAssets.Lit("Prop_Petal_Pink", new Color(0.95f, 0.5f, 0.7f)),
            };

            AddPrimitive(PrimitiveType.Cylinder, "Vase", root.transform, new Vector3(0f, 0.1f, 0f), new Vector3(0.14f, 0.1f, 0.14f), vase);
            for (int i = 0; i < 3; i++)
            {
                float angle = i * 120f * Mathf.Deg2Rad;
                var offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.05f;
                AddPrimitive(PrimitiveType.Cylinder, "Stem", root.transform, offset + new Vector3(0f, 0.3f, 0f), new Vector3(0.012f, 0.12f, 0.012f), stem);
                AddPrimitive(PrimitiveType.Sphere, "Bloom", root.transform, offset + new Vector3(0f, 0.43f, 0f), Vector3.one * 0.08f, petals[i]);
            }
            return root;
        }

        /// <summary>Framed, softly glowing wall sign. Faces local -Z (place it on a wall facing into the room).</summary>
        public static GameObject BuildWallSign(Transform parent, string name, string text, Vector3 position, Quaternion rotation,
            Vector2 size, Material frame, Color glow, Color textColor)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, rotation);

            Material panel = GreyboxAssets.Emissive($"Sign_{name.Replace(' ', '_')}_Glow", glow, glow * 0.9f, 2.5f);
            AddPrimitive(PrimitiveType.Cube, "Frame", root.transform, Vector3.zero, new Vector3(size.x + 0.1f, size.y + 0.08f, 0.04f), frame);
            AddPrimitive(PrimitiveType.Cube, "Panel", root.transform, new Vector3(0f, 0f, -0.025f), new Vector3(size.x, size.y, 0.02f), panel);

            var canvasGo = new GameObject("Text", typeof(RectTransform), typeof(Canvas));
            canvasGo.transform.SetParent(root.transform, false);
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var rect = (RectTransform)canvasGo.transform;
            rect.sizeDelta = size * 1000f;
            rect.localScale = Vector3.one * 0.001f;
            rect.localPosition = new Vector3(0f, 0f, -0.04f);

            var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(canvasGo.transform, false);
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            Text t = label.GetComponent<Text>();
            t.font = BuiltinFont;
            t.fontSize = Mathf.RoundToInt(size.y * 1000f * 0.65f);
            t.resizeTextForBestFit = true; // long names shrink to fit the panel
            t.resizeTextMinSize = 30;
            t.resizeTextMaxSize = t.fontSize;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = textColor;
            t.raycastTarget = false;
            t.text = text;
            return root;
        }

        /// <summary>Grey-box NPC (no collider, no AI yet). Faces local +Z after yaw.</summary>
        public static GameObject BuildNpc(string name, Vector3 position, float yaw, Color body, Color accent, Color skin,
            NpcHeadwear headwear, Color headwearColor)
        {
            var root = new GameObject(name);
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

            string key = name.Replace(' ', '_');
            AddPrimitive(PrimitiveType.Capsule, "Body", root.transform, new Vector3(0f, 0.8f, 0f), new Vector3(0.5f, 0.8f, 0.4f), GreyboxAssets.Lit($"NPC_{key}_Body", body));
            AddPrimitive(PrimitiveType.Cube, "Front", root.transform, new Vector3(0f, 0.75f, 0.19f), new Vector3(0.36f, 0.6f, 0.04f), GreyboxAssets.Lit($"NPC_{key}_Accent", accent));
            AddPrimitive(PrimitiveType.Sphere, "Head", root.transform, new Vector3(0f, 1.72f, 0f), Vector3.one * 0.3f, GreyboxAssets.Lit("NPC_Skin_" + ColorUtility.ToHtmlStringRGB(skin), skin));
            Material headwearMaterial = GreyboxAssets.Lit($"NPC_{key}_Headwear", headwearColor);
            if (headwear == NpcHeadwear.ChefHat)
                AddPrimitive(PrimitiveType.Cylinder, "Hat", root.transform, new Vector3(0f, 1.95f, 0f), new Vector3(0.26f, 0.1f, 0.26f), headwearMaterial);
            else if (headwear == NpcHeadwear.HairBun)
            {
                AddPrimitive(PrimitiveType.Sphere, "Hair", root.transform, new Vector3(0f, 1.76f, -0.02f), new Vector3(0.32f, 0.26f, 0.32f), headwearMaterial);
                AddPrimitive(PrimitiveType.Sphere, "Bun", root.transform, new Vector3(0f, 1.9f, -0.1f), Vector3.one * 0.13f, headwearMaterial);
            }
            else if (headwear == NpcHeadwear.WizardHat)
            {
                // Brim plus a stack of narrowing cylinders, tip slightly bent back.
                AddPrimitive(PrimitiveType.Cylinder, "Brim", root.transform, new Vector3(0f, 1.84f, 0f), new Vector3(0.5f, 0.01f, 0.5f), headwearMaterial);
                for (int i = 0; i < 4; i++)
                {
                    float w = 0.3f - i * 0.065f;
                    GameObject part = AddPrimitive(PrimitiveType.Cylinder, $"Cone {i}", root.transform,
                        new Vector3(0f, 1.9f + i * 0.09f, -i * 0.015f), new Vector3(w, 0.05f, w), headwearMaterial);
                    part.transform.localRotation = Quaternion.Euler(-i * 4f, 0f, 0f);
                }
            }
            return root;
        }

        // ───────────────────────── Small helpers ─────────────────────────

        public static Font BuiltinFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        public static GameObject AddPrimitive(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 scale, Material material)
        {
            // Cubes become softly rounded meshes of that size (no blocky edges); other primitives stay primitives.
            if (type == PrimitiveType.Cube) return GreyboxAssets.Box(name, parent, localPosition, scale, material, keepCollider: false);

            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        public static Text CreateText(Transform parent, string name, Font font, int size, TextAnchor alignment,
            Vector2 anchor, Vector2 position, Vector2 dimensions, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Shadow));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(anchor.x, anchor.y == 0.5f ? 1f : anchor.y);
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;

            Text text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            Shadow shadow = go.GetComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
            return text;
        }

        private static void AddScaler(GameObject canvasGo)
        {
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        private static GameObject Stretch(GameObject go, Transform parent, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 sizeDelta)
        {
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.sizeDelta = sizeDelta;
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return go;
        }
    }
}
