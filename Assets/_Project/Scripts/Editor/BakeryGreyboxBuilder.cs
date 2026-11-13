using System.Collections.Generic;
using TidyUp.Items;
using TidyUp.Locations;
using TidyUp.Mechanics.ShelfSort;
using TidyUp.Story;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TidyUp.Editor
{
    /// <summary>
    /// The Bakery. Template for Old Town Market (family A, shelf sort). Items come from Data/Items/bakery.csv.
    /// Builds either the standalone test scene (location + its own player) or the content-only location
    /// scene that the City loads through Addressables. Safe to re-run.
    /// </summary>
    public static class BakeryGreyboxBuilder
    {
        public const string LocationAddress = "Location_Bakery";
        public const string LocationAssetPath = "Assets/_Project/Data/Locations/Bakery.asset";
        public const string LocationScenePath = "Assets/_Project/Scenes/Locations/Bakery.unity";
        private const string TestScenePath = "Assets/_Project/Scenes/Test/Bakery_Greybox.unity";
        private const string MemoryAssetPath = "Assets/_Project/Data/Story/Bakery_Memory.asset";
        private const float WallHeight = 3.6f;
        private static readonly Vector2 RoomSize = new Vector2(18f, 14f);

        [MenuItem("Tools/Tidy Up/Test Scenes/Build Bakery Test Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildScene(standalone: true);
        }

        /// <summary>Content-only scene for the City (called by CityGreyboxBuilder).</summary>
        public static void BuildLocationScene() => MegaBakeryBuilder.Build(); // the mega Bakery (shell + streamed rooms)

        /// <summary>Items, memory and location asset for the Bakery, plus its palette (shared with MegaBakeryBuilder).</summary>
        internal static LocationPalette PrepareData()
        {
            Dictionary<string, ItemDefinition> allItems = LocationKit.ImportItems();
            List<ItemDefinition> items = LocationKit.ItemsForLocation(allItems, "bakery");
            if (items.Count == 0)
            {
                Debug.LogError("[Tidy Up] No items with homeLocationId 'bakery' in Data/Items/*.csv.");
                return null;
            }
            MemoryDefinition memory = LocationKit.LoadOrCreateMemory(MemoryAssetPath, "memory_bakery", "The Day Before",
                "The Bakery, Old Town Market", new[]
                {
                    new MemoryLine("", "Five o'clock in the morning. The ovens hum, and the whole street smells of crust and butter.", 5f),
                    new MemoryLine("Hugo the baker", "Baguettes on top, loaves in the middle, croissants low, where the little ones can reach. Same as every morning.", 6f),
                    new MemoryLine("", "A soft knock at the back door. Nobody there. Only a trail of tiny footprints in the spilled flour, skipping out into the street.", 6f),
                    new MemoryLine("Hugo the baker", "Odd. And last night the clock on the square struck thirteen. I counted twice.", 5f),
                    new MemoryLine("", "Hugo laughed, dusted off his apron, and set tomorrow's dough to rise.", 4.5f),
                    new MemoryLine("", "By morning, every loaf in the city had forgotten where it belonged.", 5f),
                }, "Tiny footprints in the flour, and the clock on the square striking thirteen, the night before the Muddle.");
            LocationKit.PrepareLocation(LocationAssetPath, "bakery", "Bakery", "Old Town Market", LocationAddress,
                new[] { MechanicFamily.ShelfSort }, 1234, items, memory);

            LocationPalette p = LocationPalette.Create("Greybox",
                floor: new Color(0.7f, 0.38f, 0.26f), wall: new Color(0.93f, 0.8f, 0.6f),
                wood: new Color(0.5f, 0.3f, 0.17f), accent: new Color(0.17f, 0.45f, 0.46f), floorPattern: Art.TextureSet.Tiles);
            p.Mood = Art.MoodPreset.Bakery;
            p.UseRichSurfaces("Bakery",
                Art.TextureSet.Brick, new Color(0.88f, 0.62f, 0.48f), 1.0f,           // warm brick
                Art.TextureSet.Planks, new Color(0.56f, 0.37f, 0.22f), 2.4f,          // plank ceiling
                Art.TextureSet.Tiles, new Color(0.7f, 0.38f, 0.26f), 1.6f,            // terracotta tiles
                new Color(0.2f, 0.44f, 0.44f));                                       // painted teal panelling
            AssetDatabase.SaveAssets();
            return p;
        }

        private static void BuildScene(bool standalone)
        {
            // 1. Data (all asset work happens before NewScene, which unloads unreferenced assets)
            LocationPalette p = PrepareData();
            if (p == null) return;

            // 2. Scene
            Scene scene = LocationKit.NewScene(withSun: standalone);
            LocationDefinition location = AssetDatabase.LoadAssetAtPath<LocationDefinition>(LocationAssetPath);

            Transform room = LocationKit.BuildRoom("Bakery Room", RoomSize, WallHeight, p);
            Material glass = GreyboxAssets.TransparentUnlit("Case_Glass", new Color(0.86f, 0.95f, 1f, 0.12f));
            var groups = new List<SlotGroup>();
            var itemsRoot = new GameObject("Items").transform;

            SlotGroup Add(SlotGroup group)
            {
                LocationKit.FillTidyState(group, location.Items, itemsRoot);
                groups.Add(group);
                return group;
            }

            float halfX = RoomSize.x * 0.5f - 0.1f; // inner wall faces
            float halfZ = RoomSize.y * 0.5f - 0.1f;
            Quaternion facingSouth = Quaternion.identity;               // fixtures on the north wall
            Quaternion facingEast = Quaternion.Euler(0f, -90f, 0f);     // fixtures on the west wall
            Quaternion facingWest = Quaternion.Euler(0f, 90f, 0f);      // fixtures on the east wall

            // ── North wall: four loaf walls (one kind each) and the jam and honey shelf ──
            float x = -halfX + 0.25f;
            string[] loafKinds = { "loaf_sourdough", "loaf_rye", "loaf_seeded", "loaf_brioche" };
            string[] loafNames = { "Sourdough", "Rye", "Seeded", "Brioche" };
            for (int i = 0; i < loafKinds.Length; i++)
            {
                var s = new ShelfSettings
                {
                    Name = $"Loaf Wall {loafNames[i]}", Label = $"{loafNames[i]} wall",
                    Shelves = 7, SlotsPerShelf = 10, SlotsDeep = 2, SlotSize = new Vector3(0.3f, 0.2f, 0.22f),
                    RowSlotTypes = new[] { loafKinds[i] }, RowLabels = new[] { loafNames[i] },
                    FrameMaterial = p.Wood, MarkerMaterial = p.Marker,
                };
                float width = s.InnerWidth + 2f * s.PanelThickness;
                Add(ShelfGenerator.Generate(s, null, new Vector3(x + width * 0.5f, 0f, halfZ - s.Depth * 0.5f - 0.02f), facingSouth));
                x += width + 0.3f;
            }
            {
                var s = new ShelfSettings
                {
                    Name = "Jar Shelf", Label = "Jar shelf",
                    Shelves = 6, SlotsPerShelf = 26, SlotsDeep = 1, SlotSize = new Vector3(0.1f, 0.13f, 0.11f),
                    RowSlotTypes = new[] { "jar_jam", "jar_jam", "jar_jam", "jar_honey" }, RowLabels = new[] { "Jam", "Jam", "Jam", "Honey" },
                    FrameMaterial = p.Wood, MarkerMaterial = p.Marker,
                };
                float width = s.InnerWidth + 2f * s.PanelThickness;
                Add(ShelfGenerator.Generate(s, null, new Vector3(x + width * 0.5f, 0f, halfZ - s.Depth * 0.5f - 0.02f), facingSouth));
            }

            // ── West wall (low, under the windows): the cookie counter and three bread racks ──
            float z = halfZ - 0.9f;
            {
                var s = new ShelfSettings
                {
                    Name = "Cookie Counter", Label = "Cookie counter",
                    Shelves = 8, SlotsPerShelf = 28, SlotsDeep = 3, SlotSize = new Vector3(0.1f, 0.06f, 0.1f),
                    RowSlotTypes = new[] { "cookie_choc", "cookie_choc", "cookie_choc", "cookie_ginger", "cookie_ginger", "cookie_ginger", "cookie_shortbread" },
                    RowLabels = new[] { "Chocolate chip", "Chocolate chip", "Chocolate chip", "Gingerbread", "Gingerbread", "Gingerbread", "Shortbread" },
                    FrameMaterial = p.Wood, MarkerMaterial = p.Marker,
                };
                float width = s.InnerWidth + 2f * s.PanelThickness;
                Add(ShelfGenerator.Generate(s, null, new Vector3(-halfX + s.Depth * 0.5f + 0.02f, 0f, z - width * 0.5f), facingEast));
                z -= width + 0.4f;
            }
            string[] longKinds = { "long_baguette", "long_baguette", "long_baton" };
            string[] longNames = { "Baguettes", "Baguettes", "Rustic batons" };
            for (int i = 0; i < longKinds.Length; i++)
            {
                var s = new ShelfSettings
                {
                    Name = $"Bread Rack {i + 1}", Label = "Bread rack", Style = ShelfStyle.OpenRack,
                    Shelves = 5, SlotsPerShelf = 6, SlotsDeep = 3, SlotSize = new Vector3(0.46f, 0.12f, 0.12f),
                    RowSlotTypes = new[] { longKinds[i] }, RowLabels = new[] { longNames[i] },
                    FrameMaterial = p.Wood, MarkerMaterial = p.Marker,
                };
                float width = s.InnerWidth + 2f * s.PanelThickness;
                Add(ShelfGenerator.Generate(s, null, new Vector3(-halfX + s.Depth * 0.5f + 0.02f, 0f, z - width * 0.5f), facingEast));
                z -= width + 0.4f;
            }

            // ── East wall (low): three deep roll racks and two sack racks; the sales counter by the door ──
            z = halfZ - 0.9f;
            string[] rollKinds = { "roll_white", "roll_bun", "roll_bagel" };
            string[] rollNames = { "Soft rolls", "Sweet buns", "Bagels" };
            for (int i = 0; i < rollKinds.Length; i++)
            {
                var s = new ShelfSettings
                {
                    Name = $"Roll Rack {rollNames[i]}", Label = "Roll rack", Style = ShelfStyle.OpenRack,
                    Shelves = 4, SlotsPerShelf = 10, SlotsDeep = 5, SlotSize = new Vector3(0.12f, 0.1f, 0.12f),
                    RowSlotTypes = new[] { rollKinds[i] }, RowLabels = new[] { rollNames[i] },
                    FrameMaterial = p.Wood, MarkerMaterial = p.Marker,
                };
                float width = s.InnerWidth + 2f * s.PanelThickness;
                Add(ShelfGenerator.Generate(s, null, new Vector3(halfX - s.Depth * 0.5f - 0.02f, 0f, z - width * 0.5f), facingWest));
                z -= width + 0.35f;
            }
            string[] sackKinds = { "sack_flour", "sack_sugar" };
            string[] sackNames = { "Flour", "Sugar" };
            for (int i = 0; i < sackKinds.Length; i++)
            {
                var s = new ShelfSettings
                {
                    Name = $"Sack Rack {sackNames[i]}", Label = "Sack rack", Style = ShelfStyle.OpenRack,
                    Shelves = 2, SlotsPerShelf = 6, SlotsDeep = 2, SlotSize = new Vector3(0.32f, 0.4f, 0.26f), BaseHeight = 0.08f,
                    RowSlotTypes = new[] { sackKinds[i] }, RowLabels = new[] { sackNames[i] },
                    FrameMaterial = p.Wood, MarkerMaterial = p.Marker,
                };
                float width = s.InnerWidth + 2f * s.PanelThickness;
                Add(ShelfGenerator.Generate(s, null, new Vector3(halfX - s.Depth * 0.5f - 0.02f, 0f, z - width * 0.5f), facingWest));
                z -= width + 0.35f;
            }
            LocationKit.Furniture("Counter", room, new Vector3(halfX - 1.5f, 0.45f, -halfZ + 2.2f), new Vector3(0.8f, 0.9f, 2.6f), p.Accent);
            LocationKit.MakeCleanable(LocationKit.Furniture("Counter Top", room, new Vector3(halfX - 1.5f, 0.92f, -halfZ + 2.2f), new Vector3(0.86f, 0.04f, 2.66f), p.Wood), "Counter");

            // ── Middle: six islands of two glass cases back to back (pastries and little cakes) ──
            string[] caseKinds = { "pastry_croissant", "pastry_chocolat", "pastry_danish", "pastry_swirl", "pastry_croissant", "pastry_chocolat",
                                   "pastry_danish", "pastry_swirl", "small_muffin", "small_cupcake", "small_donut", "small_macaron" };
            string[] caseNames = { "Croissants", "Pains au chocolat", "Danishes", "Cinnamon swirls", "Croissants", "Pains au chocolat",
                                   "Danishes", "Cinnamon swirls", "Muffins", "Cupcakes", "Donuts", "Macarons" };
            float[] islandX = { -4.6f, 0f, 4.6f };
            float[] islandZ = { 2.8f, -0.6f };
            int caseIndex = 0;
            foreach (float iz in islandZ)
            foreach (float ix in islandX)
            {
                foreach (int side in new[] { -1, 1 }) // -1: faces south, +1: faces north
                {
                    bool small = caseKinds[caseIndex].StartsWith("small_");
                    var s = new ShelfSettings
                    {
                        Name = $"Case {caseIndex + 1} {caseNames[caseIndex]}", Label = small ? "Cake case" : "Pastry case", Style = ShelfStyle.Case,
                        Shelves = 3, SlotsPerShelf = small ? 22 : 11, SlotsDeep = 3,
                        SlotSize = small ? new Vector3(0.11f, 0.13f, 0.11f) : new Vector3(0.22f, 0.12f, 0.16f), BaseHeight = 0.45f,
                        RowSlotTypes = new[] { caseKinds[caseIndex] }, RowLabels = new[] { caseNames[caseIndex] },
                        FrameMaterial = p.Wood, BodyMaterial = p.Accent, GlassMaterial = glass, MarkerMaterial = p.Marker,
                    };
                    float offset = s.Depth * 0.5f + 0.03f;
                    Add(ShelfGenerator.Generate(s, null, new Vector3(ix, 0f, iz + (side < 0 ? -offset : offset)),
                        side < 0 ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f)));
                    caseIndex++;
                }
            }

            // ── Near the door: six tiered stands, cakes on the left, pies on the right ──
            float[] standX = { -6.2f, -4.0f, -1.8f, 1.8f, 4.0f, 6.2f };
            for (int i = 0; i < standX.Length; i++)
            {
                bool cake = standX[i] < 0f;
                Add(StandGenerator.Generate(new StandSettings
                {
                    Name = $"{(cake ? "Cake" : "Pie")} Stand {i % 3 + 1}", Label = cake ? "Cake stand" : "Pie stand",
                    SlotsPerTier = new[] { 8, 6, 4 }, SlotSize = new Vector3(0.26f, cake ? 0.18f : 0.1f, 0.26f),
                    TierSlotTypes = new[] { cake ? "big_cake" : "big_pie" }, TierLabels = new[] { cake ? "Lemon cakes" : "Apple pies" },
                    FrameMaterial = p.Wood, MarkerMaterial = p.Marker,
                }, null, new Vector3(standX[i], 0f, -halfZ + 3.6f), Quaternion.identity));
            }

            // Cozy from the start: lamps on, flowers, OPEN sign above the loaf walls.
            var decor = new GameObject("Decor").transform;
            int lamp = 0;
            foreach (float lx in new[] { -6f, -2f, 2f, 6f })
            foreach (float lz in new[] { -3.5f, 1.1f, 4.8f })
            {
                LocationKit.BuildLamp($"Lamp {++lamp}", decor, new Vector3(lx, 0f, lz), WallHeight, p);
            }
            LocationKit.MakeCleanable(LocationKit.Furniture("Table", room, new Vector3(-halfX + 1.6f, 0.375f, -halfZ + 1.4f), new Vector3(1.2f, 0.75f, 0.8f), p.Wood), "Table");
            LocationKit.BuildFlowers(decor, new Vector3(-halfX + 1.6f, 0.75f, -halfZ + 1.4f));
            foreach (int side in new[] { -1, 1 })
                Art.ArchitectureKit.Painting(decor, new Vector3(side * 3.6f, 2.0f, -halfZ), Quaternion.Euler(0f, 180f, 0f), new Vector2(1.2f, 0.9f),
                    Art.ArchitectureKit.Picture.Landscape, side < 0 ? 21 : 22);   // golden-hour landscapes
            LocationKit.BuildWallSign(decor, "OPEN Sign", "OPEN", new Vector3(0f, 2.75f, halfZ - 0.04f), Quaternion.identity,
                new Vector2(1.2f, 0.4f), p.Wood, new Color(1f, 0.55f, 0.35f), new Color(0.35f, 0.1f, 0.05f));

            // The Muddle drops piles anywhere on the floor, counter and table (never on fixtures).
            BoxCollider scatter = LocationKit.BuildScatterArea(new Vector3(0f, 0.7f, 0f), new Vector3(RoomSize.x - 1f, 1.4f, RoomSize.y - 1f));
            SlotGroup[] groupArray = groups.ToArray();
            LocationRig rig = LocationKit.BuildLocationSystems(location, groupArray, new[] { scatter }, RoomSize, p);
            if (standalone) LocationKit.BuildPlayerRig(rig.EntryPosition, 0f, p);

            // 3. At 100%: Hugo the baker returns and the memory photo appears on the counter.
            GameObject baker = LocationKit.BuildNpc("Baker NPC", new Vector3(halfX - 0.6f, 0f, -halfZ + 2.2f), -90f,
                body: new Color(0.97f, 0.97f, 0.95f), accent: new Color(0.2f, 0.55f, 0.56f), skin: new Color(0.92f, 0.72f, 0.58f),
                NpcHeadwear.ChefHat, new Color(0.97f, 0.97f, 0.95f));
            Material photo = GreyboxAssets.Emissive("Memory_Photo", new Color(0.95f, 0.8f, 0.55f), new Color(1f, 0.72f, 0.4f), 1.6f);
            GameObject fragment = LocationKit.BuildMemoryFragment(location.Memory,
                "The Bakery is tidy again! Something glimmers on the counter…",
                new Vector3(halfX - 1.5f, 0.94f, -halfZ + 1.4f), Quaternion.Euler(0f, 90f, 0f), new Vector3(0.45f, 0.4f, 0.3f), visual =>
                {
                    visual.localPosition = new Vector3(0f, 0.15f, 0f);
                    visual.localRotation = Quaternion.Euler(-12f, 0f, 0f); // leaning back on its stand
                    LocationKit.AddPrimitive(PrimitiveType.Cube, "Frame", visual, Vector3.zero, new Vector3(0.3f, 0.24f, 0.025f), p.Wood);
                    LocationKit.AddPrimitive(PrimitiveType.Cube, "Photo", visual, new Vector3(0f, 0f, -0.014f), new Vector3(0.24f, 0.18f, 0.005f), photo);
                });
            LocationKit.BuildCompletionStage(rig, baker, fragment);

            LocationKit.RecordSlotCount(LocationAssetPath, groupArray);
            string path = standalone ? TestScenePath : LocationScenePath;
            LocationKit.SaveScene(scene, path);
            Debug.Log($"[Tidy Up] Built {path}.");
        }
    }
}
