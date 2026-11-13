using System;
using System.Collections.Generic;
using TidyUp.Items;
using TidyUp.Locations;
using TidyUp.Mechanics.ShelfSort;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TidyUp.Editor
{
    /// <summary>
    /// The mega Bakery (Jesse 2026-11-13): a 60 × 40 m ground floor with 6 m ceilings, split into six rooms
    /// that each stream in as their own scene. The shell scene (address Location_Bakery) holds the building:
    /// walls, doorways with room signs, floors, windows, chandeliers, ovens, the sales counter, the baker and
    /// the memory, and the MegaLocation that streams the rooms. Each room scene holds its fixtures, items and
    /// systems (progress, Muddle, hints, cleaning). Every kind of baked good lives in exactly one room.
    /// Layout (north up, the street door in the south wall):
    ///   Pantry       | Bread Hall | Bakehouse
    ///   Pastry Salon | Shop Hall  | Cake &amp; Cookie Room
    /// </summary>
    public static class MegaBakeryBuilder
    {
        private const string ShellScenePath = BakeryGreyboxBuilder.LocationScenePath;
        private const string ZoneFolder = "Assets/_Project/Scenes/Locations/Bakery";
        private const float Ceiling = 6f;
        private const float Face = 9.75f; // a room's inner wall faces, from its centre
        private static readonly Vector2 FloorSize = new Vector2(60f, 40f);

        private sealed class ZoneSpec
        {
            public string Id, Name;
            public Vector2 Centre;
            public Color MapColor;
            public Art.TextureSet Inlay;
            public Color InlayColor;
            public Action<Room> Build;
            public string ScenePath => $"{ZoneFolder}/Bakery_{Id}.unity";
            public string Address => $"{BakeryGreyboxBuilder.LocationAddress}_{Id}";
            public Rect Area => new Rect(Centre.x - 10f, Centre.y - 10f, 20f, 20f);
        }

        private static readonly ZoneSpec[] Zones =
        {
            new ZoneSpec { Id = "shop_hall", Name = "Shop Hall", Centre = new Vector2(0f, -10f), MapColor = new Color(0.95f, 0.82f, 0.6f),
                Inlay = Art.TextureSet.Tiles, InlayColor = new Color(0.78f, 0.45f, 0.32f), Build = BuildShopHall },
            new ZoneSpec { Id = "bread_hall", Name = "Bread Hall", Centre = new Vector2(0f, 10f), MapColor = new Color(0.9f, 0.7f, 0.45f),
                Inlay = Art.TextureSet.Parquet, InlayColor = new Color(0.62f, 0.42f, 0.26f), Build = BuildBreadHall },
            new ZoneSpec { Id = "pastry_salon", Name = "Pastry Salon", Centre = new Vector2(-20f, -10f), MapColor = new Color(0.96f, 0.72f, 0.72f),
                Inlay = Art.TextureSet.Parquet, InlayColor = new Color(0.72f, 0.5f, 0.36f), Build = BuildPastrySalon },
            new ZoneSpec { Id = "cake_cookie", Name = "Cake & Cookie Room", Centre = new Vector2(20f, -10f), MapColor = new Color(0.85f, 0.75f, 0.95f),
                Inlay = Art.TextureSet.Tiles, InlayColor = new Color(0.85f, 0.7f, 0.62f), Build = BuildCakeCookie },
            new ZoneSpec { Id = "pantry", Name = "Pantry", Centre = new Vector2(-20f, 10f), MapColor = new Color(0.75f, 0.85f, 0.65f),
                Inlay = Art.TextureSet.Planks, InlayColor = new Color(0.55f, 0.4f, 0.28f), Build = BuildPantry },
            new ZoneSpec { Id = "bakehouse", Name = "Bakehouse", Centre = new Vector2(20f, 10f), MapColor = new Color(0.95f, 0.62f, 0.45f),
                Inlay = Art.TextureSet.Stone, InlayColor = new Color(0.7f, 0.64f, 0.56f), Build = BuildBakehouse },
        };

        /// <summary>Doorways between rooms: world XZ centre, and whether the wall runs along X (else along Z).</summary>
        private static readonly (Vector2 at, bool alongX, string south, string north)[] Doorways =
        {
            (new Vector2(0f, 0f), true, "shop_hall", "bread_hall"),
            (new Vector2(-20f, 0f), true, "pastry_salon", "pantry"),
            (new Vector2(20f, 0f), true, "cake_cookie", "bakehouse"),
            // Along Z: "south" is the west room, "north" the east room.
            (new Vector2(-10f, -7f), false, "pastry_salon", "shop_hall"),
            (new Vector2(10f, -13f), false, "shop_hall", "cake_cookie"),
            (new Vector2(-10f, 13f), false, "pantry", "bread_hall"),
            (new Vector2(10f, 7f), false, "bread_hall", "bakehouse"),
        };
        private const float DoorWidth = 4f, DoorHeight = 3.8f, WallThickness = 0.4f;

        /// <summary>Zone scenes and their addresses (registered with Addressables by the City builder).</summary>
        public static IEnumerable<(string scenePath, string address)> ZoneScenes
        {
            get { foreach (ZoneSpec z in Zones) yield return (z.ScenePath, z.Address); }
        }

        [MenuItem("Tools/Tidy Up/Build Mega Bakery (shell + rooms)")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        public static void Build()
        {
            LocationPalette p = BakeryGreyboxBuilder.PrepareData();
            if (p == null) return;
            p.WindowY = 4.95f;                       // windows high up, over the tall shelves
            p.WindowSize = new Vector2(1.4f, 1.6f);
            p.WallPilasters = false;                 // fixtures line the walls
            Material glass = GreyboxAssets.TransparentUnlit("Case_Glass", new Color(0.86f, 0.95f, 1f, 0.12f));
            _stock = new[]
            {
                GreyboxAssets.Textured("Bakery_Wicker", new Color(0.78f, 0.6f, 0.36f), Art.TextureSet.Fabric, 0.4f, 0.1f),
                GreyboxAssets.Textured("Bakery_Crate", new Color(0.62f, 0.44f, 0.28f), Art.TextureSet.Planks, 0.6f, 0.2f),
                GreyboxAssets.Textured("Bakery_FlourSack", new Color(0.95f, 0.92f, 0.84f), Art.TextureSet.Fabric, 0.5f, 0.05f),
                GreyboxAssets.Lit("Bakery_Tin", new Color(0.25f, 0.5f, 0.5f)),
                GreyboxAssets.Lit("Bakery_TinRed", new Color(0.7f, 0.25f, 0.2f)),
            };
            AssetDatabase.SaveAssets();
            EditorFolders.Ensure(ZoneFolder);

            // 1. One scene per room (records each room's slot count).
            var infos = new List<ZoneInfo>();
            int grandTotal = 0;
            foreach (ZoneSpec spec in Zones)
            {
                Scene scene = LocationKit.NewScene(withSun: false);
                var room = new Room(spec, AssetDatabase.LoadAssetAtPath<LocationDefinition>(BakeryGreyboxBuilder.LocationAssetPath), p, glass);
                spec.Build(room);
                BoxCollider scatter = LocationKit.BuildScatterArea(new Vector3(spec.Centre.x, 0.7f, spec.Centre.y), new Vector3(18.6f, 1.4f, 18.6f));
                SlotGroup[] groups = room.Groups.ToArray();
                LocationKit.BuildZoneSystems(room.Location, spec.Id, spec.Name, groups, new[] { scatter }, spec.Area, p);
                int slots = 0;
                foreach (SlotGroup g in groups) slots += g.Slots.Length;
                grandTotal += slots;
                infos.Add(new ZoneInfo { Id = spec.Id, DisplayName = spec.Name, Address = spec.Address, Area = spec.Area, TotalSlots = slots, MapColor = spec.MapColor });
                LocationKit.SaveScene(scene, spec.ScenePath);
                Debug.Log($"[Tidy Up] Built {spec.ScenePath}: {slots} slots.");
            }

            // 2. The shell.
            Scene shell = LocationKit.NewScene(withSun: false);
            LocationDefinition location = AssetDatabase.LoadAssetAtPath<LocationDefinition>(BakeryGreyboxBuilder.LocationAssetPath);
            BuildShell(location, infos.ToArray(), p);
            location.SetTotalSlots(grandTotal);
            EditorUtility.SetDirty(location);
            LocationKit.SaveScene(shell, ShellScenePath);
            Debug.Log($"[Tidy Up] Built the mega Bakery: {Zones.Length} rooms, {grandTotal} slots.");
        }

        // ───────────── Rooms ─────────────

        private enum Wall { N, S, E, W }

        /// <summary>One room being built: places fixtures in room-local coordinates (origin at the room's centre).</summary>
        private sealed class Room
        {
            public readonly ZoneSpec Spec;
            public readonly LocationDefinition Location;
            public readonly LocationPalette P;
            public readonly Material Glass;
            public readonly List<SlotGroup> Groups = new List<SlotGroup>();
            public readonly Transform Items;
            public readonly Transform Decor;

            public Room(ZoneSpec spec, LocationDefinition location, LocationPalette p, Material glass)
            {
                Spec = spec;
                Location = location;
                P = p;
                Glass = glass;
                Items = new GameObject("Items").transform;
                Decor = new GameObject("Room Decor").transform;
            }

            public Vector3 World(float x, float z) => new Vector3(Spec.Centre.x + x, 0f, Spec.Centre.y + z);

            public SlotGroup Add(SlotGroup group)
            {
                LocationKit.FillTidyState(group, Location.Items, Items);
                Groups.Add(group);
                return group;
            }

            public SlotGroup Shelf(ShelfSettings s, float x, float z, Quaternion rotation) => Add(ShelfGenerator.Generate(s, null, World(x, z), rotation));

            /// <summary>Fixtures side by side against a wall between from and to (along the wall's axis), centred in that span.</summary>
            public void Row(Wall wall, float from, float to, int count, Func<int, ShelfSettings> make, float gap = 0.3f)
            {
                var fitting = new List<ShelfSettings>();
                float used = 0f;
                for (int i = 0; i < count; i++)
                {
                    ShelfSettings s = make(i);
                    float w = s.InnerWidth + 2f * s.PanelThickness;
                    if (used + (fitting.Count > 0 ? gap : 0f) + w > to - from) break;
                    used += (fitting.Count > 0 ? gap : 0f) + w;
                    fitting.Add(s);
                }
                float a = from + (to - from - used) * 0.5f;
                foreach (ShelfSettings s in fitting)
                {
                    float w = s.InnerWidth + 2f * s.PanelThickness, d = s.Depth, mid = a + w * 0.5f;
                    switch (wall)
                    {
                        case Wall.N: Shelf(s, mid, Face - d * 0.5f - 0.02f, Quaternion.identity); break;
                        case Wall.S: Shelf(s, mid, -Face + d * 0.5f + 0.02f, Quaternion.Euler(0f, 180f, 0f)); break;
                        case Wall.W: Shelf(s, -Face + d * 0.5f + 0.02f, mid, Quaternion.Euler(0f, -90f, 0f)); break;
                        case Wall.E: Shelf(s, Face - d * 0.5f - 0.02f, mid, Quaternion.Euler(0f, 90f, 0f)); break;
                    }
                    a += w + gap;
                }
            }

            /// <summary>Two fixtures back to back (an island you can walk round), fronts facing north and south.</summary>
            public void Island(float x, float z, ShelfSettings south, ShelfSettings north)
            {
                Shelf(south, x, z - south.Depth * 0.5f - 0.03f, Quaternion.identity);
                Shelf(north, x, z + north.Depth * 0.5f + 0.03f, Quaternion.Euler(0f, 180f, 0f));
            }

            public void Stand(StandSettings s, float x, float z) => Add(StandGenerator.Generate(s, null, World(x, z), Quaternion.identity));
        }

        private static ShelfSettings Make(Room r, string name, string label, ShelfStyle style, int shelves, int perShelf, int deep, Vector3 slot,
            string[] types, string[] labels, float baseHeight = 0.15f)
        {
            return new ShelfSettings
            {
                Name = name, Label = label, Style = style, Shelves = shelves, SlotsPerShelf = perShelf, SlotsDeep = deep, SlotSize = slot,
                BaseHeight = baseHeight, RowSlotTypes = types, RowLabels = labels,
                FrameMaterial = r.P.Wood, BodyMaterial = r.P.Accent, GlassMaterial = style == ShelfStyle.Case ? r.Glass : null, MarkerMaterial = r.P.Marker,
            };
        }


        private static Material[] _stock;

        /// <summary>Decorative stocked shelves above the reachable ones, so wall fixtures rise to about 4 m.</summary>
        private static ShelfSettings Tall(ShelfSettings s, float topY = 3.9f)
        {
            s.FillerShelvesAbove = Mathf.Max(0, Mathf.FloorToInt((topY - s.TotalHeight) / Mathf.Max(0.2f, s.RowHeight)));
            s.FillerMaterials = _stock;
            s.FillerBaskets = true;
            s.FillerSeed = s.Name.GetHashCode() & 0xffff;
            return s;
        }
        // Doorways, room-local: Shop Hall N x0, W z+3, E z-3, S street door x0. Bread Hall S x0, W z+3, E z-3.
        // Pastry Salon E z+3, N x0. Cake & Cookie W z-3, N x0. Pantry E z+3, S x0. Bakehouse W z-3, S x0.

        private static readonly string[] SmallKinds = { "small_muffin", "small_cupcake", "small_donut", "small_macaron" };
        private static readonly string[] SmallNames = { "Muffins", "Cupcakes", "Donuts", "Macarons" };

        // Fixtures are sized for 6 m halls (Jesse 2026-11-13: "the cabinets are too tiny"): glass cases stand on tall
        // counters, wall cabinets reach about 2 m of reachable shelves with stocked shelves above to about 4 m.

        /// <summary>The front room: big glass cake counters in islands, tall cake cabinets, pie stands, a wedding cake.</summary>
        private static void BuildShopHall(Room r)
        {
            ShelfSettings IslandCase(int k, int side) => Make(r, $"Case {SmallNames[k]} {side}", "Cake case", ShelfStyle.Case, 4, 26, 3,
                new Vector3(0.11f, 0.13f, 0.11f), new[] { SmallKinds[k] }, new[] { SmallNames[k] }, 0.6f);
            int island = 0;
            foreach (float z in new[] { 4.5f, -1.5f })
            foreach (float x in new[] { -5.6f, 5.6f })
            {
                r.Island(x, z, IslandCase(island % 4, 1), IslandCase(island % 4, 2));
                island++;
            }
            ShelfSettings Cabinet(int i) => Tall(Make(r, $"Cake Cabinet {SmallNames[i % 4]}", "Cake cabinet", ShelfStyle.Bookcase, 6, 14, 2,
                new Vector3(0.14f, 0.16f, 0.14f), new[] { SmallKinds[i % 4] }, new[] { SmallNames[i % 4] }, 0.35f), 4.1f);
            r.Row(Wall.W, -9.2f, 0.6f, 4, Cabinet);
            r.Row(Wall.E, -0.4f, 8.8f, 3, i => Cabinet(i + 2));

            float[] standX = { -8f, -5.2f, -2.6f, 2.6f, 5.2f, 8f };
            for (int i = 0; i < standX.Length; i++)
                r.Stand(new StandSettings
                {
                    Name = $"Pie Stand {i + 1}", Label = "Pie stand", SlotsPerTier = new[] { 12, 9, 6 }, SlotSize = new Vector3(0.26f, 0.1f, 0.26f),
                    TierSlotTypes = new[] { "big_pie" }, TierLabels = new[] { "Apple pies" }, FrameMaterial = r.P.Wood, MarkerMaterial = r.P.Marker,
                }, standX[i], -7.2f);

            WeddingCake(r, 0f, 1.5f);
            Rug(r, 0f, -4f, new Vector2(3f, 9f), new Color(0.55f, 0.18f, 0.2f));
            foreach (int side in new[] { -1, 1 })
            {
                Plant(r, side * 9f, -9f, 1.4f);
                Chalkboard(r, side * 1.9f, -8.6f, side * 15f);
                LocationKit.BuildFlowers(r.Decor, r.World(side * 5.6f, 1.5f) + new Vector3(0f, 1.42f, 0f));
            }
        }

        private static readonly string[] LoafKinds = { "loaf_sourdough", "loaf_rye", "loaf_seeded", "loaf_brioche" };
        private static readonly string[] LoafNames = { "Sourdough", "Rye", "Seeded", "Brioche" };

        /// <summary>Towering loaf walls all round, islands of tall bread racks, a harvest table in the middle.</summary>
        private static void BuildBreadHall(Room r)
        {
            int n = 0;
            ShelfSettings Loaf(int i)
            {
                int k = n++ % 4;
                return Tall(Make(r, $"Loaf Wall {LoafNames[k]}", $"{LoafNames[k]} wall", ShelfStyle.Bookcase, 8, 12, 2,
                    new Vector3(0.3f, 0.2f, 0.22f), new[] { LoafKinds[k] }, new[] { LoafNames[k] }), 4.3f);
            }
            r.Row(Wall.N, -9.2f, 9.2f, 4, Loaf);
            r.Row(Wall.W, -9.2f, 0.6f, 2, Loaf);
            r.Row(Wall.E, -9.2f, -5.4f, 1, Loaf);
            r.Row(Wall.E, -0.6f, 8.8f, 2, Loaf);

            string[] longKinds = { "long_baguette", "long_baton" };
            string[] longNames = { "Baguettes", "Rustic batons" };
            ShelfSettings Rack(int k) => Make(r, $"Bread Rack {longNames[k]}", "Bread rack", ShelfStyle.OpenRack, 8, 8, 3,
                new Vector3(0.46f, 0.12f, 0.12f), new[] { longKinds[k] }, new[] { longNames[k] }, 0.25f);
            int rack = 0;
            foreach (float z in new[] { 3.5f, -3f })
            foreach (float x in new[] { -5.2f, 5.2f })
            {
                r.Island(x, z, Rack(rack % 2), Rack((rack + 1) % 2));
                rack++;
            }

            HarvestTable(r, 0f, 0.5f);
            Rug(r, 0f, -6f, new Vector2(3.2f, 6f), new Color(0.6f, 0.45f, 0.2f));
            foreach (int side in new[] { -1, 1 })
            {
                Plant(r, side * 9f, -9f, 1.6f);
                Sacks(r, side * 3.4f, -9f);
            }
        }

        private static readonly string[] PastryKinds = { "pastry_croissant", "pastry_chocolat", "pastry_danish", "pastry_swirl" };
        private static readonly string[] PastryNames = { "Croissants", "Pains au chocolat", "Danishes", "Cinnamon swirls" };

        /// <summary>A salon of big pastry counters, tall pastry cabinets on the long wall, and café tables.</summary>
        private static void BuildPastrySalon(Room r)
        {
            ShelfSettings Case(int k, int i) => Make(r, $"Case {PastryNames[k]} {i}", "Pastry case", ShelfStyle.Case, 4, 15, 3,
                new Vector3(0.22f, 0.12f, 0.16f), new[] { PastryKinds[k] }, new[] { PastryNames[k] }, 0.6f);
            int c = 0;
            foreach (float z in new[] { 4f, -2f })
            foreach (float x in new[] { -5.4f, 0f, 5.4f })
            {
                r.Island(x, z, Case(c % 4, c), Case((c + 1) % 4, c));
                c++;
            }
            r.Row(Wall.W, -9.2f, 9.2f, 6, i => Tall(Make(r, $"Pastry Cabinet {PastryNames[i % 4]}", "Pastry cabinet", ShelfStyle.Bookcase, 7, 12, 2,
                new Vector3(0.22f, 0.15f, 0.16f), new[] { PastryKinds[i % 4] }, new[] { PastryNames[i % 4] }, 0.35f), 4.1f));

            // Café corner along the front wall.
            Material cloth = GreyboxAssets.Lit("Bakery_TableCloth", new Color(0.97f, 0.94f, 0.88f));
            foreach (float x in new[] { -5f, -1.5f, 2f, 5.5f })
            {
                Vector3 at = r.World(x, -8.2f);
                LocationKit.AddPrimitive(PrimitiveType.Cylinder, "Café Table", r.Decor, at + new Vector3(0f, 0.74f, 0f), new Vector3(0.8f, 0.02f, 0.8f), cloth);
                LocationKit.AddPrimitive(PrimitiveType.Cylinder, "Table Leg", r.Decor, at + new Vector3(0f, 0.37f, 0f), new Vector3(0.07f, 0.37f, 0.07f), r.P.Wood);
                foreach (int side in new[] { -1, 1 })
                {
                    LocationKit.Furniture("Chair Seat", r.Decor, at + new Vector3(side * 0.65f, 0.45f, 0f), new Vector3(0.42f, 0.05f, 0.42f), r.P.Wood);
                    LocationKit.Furniture("Chair Back", r.Decor, at + new Vector3(side * 0.85f, 0.72f, 0f), new Vector3(0.05f, 0.5f, 0.42f), r.P.Wood);
                }
                LocationKit.BuildFlowers(r.Decor, at + new Vector3(0f, 0.76f, 0f));
            }
            Rug(r, 0f, 1f, new Vector2(4f, 3f), new Color(0.35f, 0.5f, 0.55f));
            Plant(r, 9f, -9f, 1.6f);
            Plant(r, -8.8f, -9.1f, 1.4f);
            Chalkboard(r, 8.2f, 8.6f, -20f);
        }

        private static readonly string[] CookieKinds = { "cookie_choc", "cookie_ginger", "cookie_shortbread" };
        private static readonly string[] CookieNames = { "Chocolate chip", "Gingerbread", "Shortbread" };

        /// <summary>Tall cookie cabinets under the windows, cake cabinets, and a field of big cake stands.</summary>
        private static void BuildCakeCookie(Room r)
        {
            ShelfSettings Counter(int i) => Tall(Make(r, $"Cookie Cabinet {CookieNames[i % 3]}", "Cookie cabinet", ShelfStyle.Bookcase, 9, 22, 2,
                new Vector3(0.1f, 0.08f, 0.1f), new[] { CookieKinds[i % 3] }, new[] { CookieNames[i % 3] }, 0.45f), 3.8f);
            r.Row(Wall.E, -9.2f, 9.2f, 6, Counter);
            r.Row(Wall.N, -9.2f, -2.4f, 2, i => Counter(i + 1));
            r.Row(Wall.W, -0.6f, 8.6f, 3, i => Tall(Make(r, $"Cake Cabinet {i + 1}", "Cake cabinet", ShelfStyle.Bookcase, 4, 6, 2,
                new Vector3(0.28f, 0.22f, 0.28f), new[] { "big_cake" }, new[] { "Lemon cakes" }, 0.35f), 4.1f));
            int s = 0;
            foreach (float z in new[] { 4.5f, 0.5f, -3.5f })
            foreach (float x in new[] { -5.5f, -1.5f, 2.5f })
                r.Stand(new StandSettings
                {
                    Name = $"Cake Stand {++s}", Label = "Cake stand", SlotsPerTier = new[] { 12, 9, 6 }, SlotSize = new Vector3(0.26f, 0.18f, 0.26f),
                    TierSlotTypes = new[] { "big_cake" }, TierLabels = new[] { "Lemon cakes" }, FrameMaterial = r.P.Wood, MarkerMaterial = r.P.Marker,
                }, x, z);
            WeddingCake(r, -1.5f, -7.5f);
            Plant(r, -9f, -9f, 1.5f);
            Rug(r, -1.5f, 0.5f, new Vector2(10f, 10f), new Color(0.62f, 0.5f, 0.68f));
        }

        /// <summary>Jars of jam and honey on tall shelves, sacks along the back wall, barrels and herbs.</summary>
        private static void BuildPantry(Room r)
        {
            ShelfSettings Jars(int i) => Tall(Make(r, $"Jar Shelf {i + 1}", "Jar shelf", ShelfStyle.Bookcase, 9, 28, 1,
                new Vector3(0.1f, 0.13f, 0.11f), i % 2 == 0 ? new[] { "jar_jam", "jar_jam", "jar_honey" } : new[] { "jar_honey", "jar_jam", "jar_honey" },
                i % 2 == 0 ? new[] { "Jam", "Jam", "Honey" } : new[] { "Honey", "Jam", "Honey" }, 0.3f), 4f);
            r.Row(Wall.N, -9.2f, 9.2f, 5, Jars);
            int j = 10;
            foreach (var (x, z) in new[] { (-5f, 3.5f), (4f, 3.5f), (-5f, -3f) })
                r.Island(x, z, Jars(j++), Jars(j++));
            string[] sackKinds = { "sack_flour", "sack_sugar" };
            string[] sackNames = { "Flour", "Sugar" };
            r.Row(Wall.W, -9.2f, 8.6f, 8, i => Make(r, $"Sack Rack {sackNames[i % 2]}", "Sack rack", ShelfStyle.OpenRack, 3, 6, 2,
                new Vector3(0.32f, 0.4f, 0.26f), new[] { sackKinds[i % 2] }, new[] { sackNames[i % 2] }, 0.08f));
            foreach (var (x, z) in new[] { (8.6f, -8.6f), (7.7f, -8.9f), (8.9f, -7.6f), (4.5f, -8.8f) })
                Barrel(r, x, z);
            Crates(r, 6.5f, -6.5f);
            HerbRack(r, 0f, 9.4f);
            Rug(r, 0f, -1f, new Vector2(3f, 7f), new Color(0.45f, 0.55f, 0.35f));
        }

        private static readonly string[] RollKinds = { "roll_white", "roll_bun", "roll_bagel" };
        private static readonly string[] RollNames = { "Soft rolls", "Sweet buns", "Bagels" };

        /// <summary>Where the baking happens: tall roll trolleys fresh from the ovens (part of the shell), workbenches, pans.</summary>
        private static void BuildBakehouse(Room r)
        {
            ShelfSettings Trolley(int i) => Make(r, $"Roll Trolley {RollNames[i % 3]}", "Roll trolley", ShelfStyle.OpenRack, 13, 10, 2,
                new Vector3(0.12f, 0.12f, 0.12f), new[] { RollKinds[i % 3] }, new[] { RollNames[i % 3] }, 0.2f);
            r.Row(Wall.N, -9.2f, 9.2f, 9, Trolley, 0.45f);
            r.Row(Wall.S, 2.4f, 9.2f, 3, i => Trolley(i + 1), 0.45f);
            Material flour = GreyboxAssets.Lit("Bakery_Flour", new Color(0.97f, 0.95f, 0.9f));
            foreach (float x in new[] { -4f, 2f })
            {
                Vector3 at = r.World(x, 1f);
                LocationKit.MakeCleanable(LocationKit.Furniture("Workbench", r.Decor, at + new Vector3(0f, 0.45f, 0f), new Vector3(3.6f, 0.9f, 1.4f), r.P.Wood), "Workbench");
                LocationKit.AddPrimitive(PrimitiveType.Sphere, "Dough", r.Decor, at + new Vector3(-0.8f, 0.97f, 0f), new Vector3(0.5f, 0.2f, 0.45f), flour);
                LocationKit.AddPrimitive(PrimitiveType.Sphere, "Dough", r.Decor, at + new Vector3(0.9f, 0.96f, -0.3f), new Vector3(0.35f, 0.15f, 0.3f), flour);
                LocationKit.AddPrimitive(PrimitiveType.Cylinder, "Rolling Pin", r.Decor, at + new Vector3(0.3f, 0.94f, 0.2f), new Vector3(0.07f, 0.25f, 0.07f), r.P.Wood)
                    .transform.rotation = Quaternion.Euler(0f, 30f, 90f);
                Pans(r, x, 1f);
            }
            Sacks(r, -8.6f, -8.6f);
            Sacks(r, -7.2f, -8.9f);
            Crates(r, -8.8f, 6.2f);
            Rug(r, -1f, -5f, new Vector2(8f, 3f), new Color(0.7f, 0.6f, 0.45f));
        }

        // ───────────── Room decoration (decor only: no slots) ─────────────

        private static Material DecorMat(string name, Color color) => GreyboxAssets.Lit("Bakery_Decor_" + name, color);

        private static void Rug(Room r, float x, float z, Vector2 size, Color color)
        {
            Material m = GreyboxAssets.Textured($"Bakery_Rug_{ColorUtility.ToHtmlStringRGB(color)}", color, Art.TextureSet.Fabric, 0.8f, 0.05f);
            GameObject rug = GreyboxAssets.Box("Rug", r.Decor, r.World(x, z) + new Vector3(0f, 0.012f, 0f), new Vector3(size.x, 0.012f, size.y), m, keepCollider: false);
            GreyboxAssets.MarkStatic(rug);
            GameObject border = GreyboxAssets.Box("Rug Border", r.Decor, r.World(x, z) + new Vector3(0f, 0.009f, 0f), new Vector3(size.x + 0.25f, 0.01f, size.y + 0.25f),
                DecorMat("RugBorder", Color.Lerp(color, new Color(0.95f, 0.85f, 0.6f), 0.6f)), keepCollider: false);
            GreyboxAssets.MarkStatic(border);
        }

        private static void Plant(Room r, float x, float z, float height)
        {
            Vector3 at = r.World(x, z);
            LocationKit.Furniture("Plant Pot", r.Decor, at + new Vector3(0f, 0.3f, 0f), new Vector3(0.6f, 0.6f, 0.6f), DecorMat("Terracotta", new Color(0.72f, 0.38f, 0.25f)));
            Material leaf = DecorMat("Leaf", new Color(0.3f, 0.52f, 0.28f));
            LocationKit.AddPrimitive(PrimitiveType.Cylinder, "Stem", r.Decor, at + new Vector3(0f, 0.6f + height * 0.3f, 0f), new Vector3(0.06f, height * 0.3f, 0.06f), DecorMat("Stem", new Color(0.35f, 0.25f, 0.15f)));
            for (int i = 0; i < 4; i++)
            {
                float a = i * 1.57f;
                LocationKit.AddPrimitive(PrimitiveType.Sphere, "Foliage", r.Decor, at + new Vector3(Mathf.Cos(a) * 0.25f, 0.6f + height * (0.55f + i * 0.1f), Mathf.Sin(a) * 0.25f),
                    new Vector3(0.75f, 0.55f, 0.75f), leaf);
            }
        }

        /// <summary>A pavement chalkboard on an easel ("Fresh today").</summary>
        private static void Chalkboard(Room r, float x, float z, float yaw)
        {
            var root = new GameObject("Chalkboard").transform;
            root.SetParent(r.Decor, false);
            root.SetPositionAndRotation(r.World(x, z), Quaternion.Euler(0f, yaw, 0f));
            GameObject board = LocationKit.Furniture("Board", root, root.position + new Vector3(0f, 0.75f, 0f), new Vector3(0.75f, 1.1f, 0.05f), DecorMat("Chalk", new Color(0.12f, 0.16f, 0.14f)));
            board.transform.localRotation = Quaternion.Euler(-10f, 0f, 0f);
            LocationKit.BuildWallSign(root, "Chalk Text", "FRESH\nTODAY", root.TransformPoint(new Vector3(0f, 0.85f, -0.05f)), root.rotation * Quaternion.Euler(-10f, 0f, 0f),
                new Vector2(0.6f, 0.5f), r.P.Wood, new Color(0.14f, 0.18f, 0.16f), new Color(0.95f, 0.93f, 0.85f));
        }

        /// <summary>A five-tier wedding cake on a round table: the room's centrepiece.</summary>
        private static void WeddingCake(Room r, float x, float z)
        {
            Vector3 at = r.World(x, z);
            Material icing = DecorMat("Icing", new Color(0.98f, 0.95f, 0.92f));
            Material pink = DecorMat("IcingPink", new Color(0.96f, 0.72f, 0.78f));
            LocationKit.AddPrimitive(PrimitiveType.Cylinder, "Display Table", r.Decor, at + new Vector3(0f, 0.4f, 0f), new Vector3(1.8f, 0.4f, 1.8f), r.P.Accent);
            LocationKit.Furniture("Display Collider", r.Decor, at + new Vector3(0f, 0.4f, 0f), new Vector3(1.5f, 0.8f, 1.5f), r.P.Accent).GetComponent<MeshRenderer>().enabled = false;
            float y = 0.8f;
            for (int t = 0; t < 5; t++)
            {
                float d = 1.2f - t * 0.2f, h = 0.32f - t * 0.03f;
                LocationKit.AddPrimitive(PrimitiveType.Cylinder, "Tier", r.Decor, at + new Vector3(0f, y + h * 0.5f, 0f), new Vector3(d, h * 0.5f, d), t % 2 == 0 ? icing : pink);
                LocationKit.AddPrimitive(PrimitiveType.Cylinder, "Trim", r.Decor, at + new Vector3(0f, y + 0.02f, 0f), new Vector3(d + 0.04f, 0.02f, d + 0.04f), t % 2 == 0 ? pink : icing);
                y += h;
            }
            LocationKit.BuildFlowers(r.Decor, at + new Vector3(0f, y, 0f));
        }

        /// <summary>A long harvest table heaped with (decorative) loaves and wheat sheaves.</summary>
        private static void HarvestTable(Room r, float x, float z)
        {
            Vector3 at = r.World(x, z);
            LocationKit.Furniture("Harvest Table", r.Decor, at + new Vector3(0f, 0.42f, 0f), new Vector3(1.6f, 0.84f, 4.2f), r.P.Wood);
            Material crust = DecorMat("Crust", new Color(0.72f, 0.45f, 0.22f));
            Material wheat = DecorMat("Wheat", new Color(0.92f, 0.78f, 0.42f));
            var random = new System.Random(5);
            for (int i = 0; i < 14; i++)
            {
                var p = new Vector3(((float)random.NextDouble() - 0.5f) * 1.2f, 0.95f, ((float)random.NextDouble() - 0.5f) * 3.8f);
                LocationKit.AddPrimitive(PrimitiveType.Sphere, "Loaf", r.Decor, at + p, new Vector3(0.36f, 0.2f, 0.24f), crust)
                    .transform.rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 180f, 0f);
            }
            foreach (float dz in new[] { -1.6f, 1.6f })
                LocationKit.AddPrimitive(PrimitiveType.Cylinder, "Wheat Sheaf", r.Decor, at + new Vector3(0f, 1.3f, dz), new Vector3(0.3f, 0.45f, 0.3f), wheat);
        }

        private static void Barrel(Room r, float x, float z)
        {
            Vector3 at = r.World(x, z);
            LocationKit.Furniture("Barrel", r.Decor, at + new Vector3(0f, 0.45f, 0f), new Vector3(0.6f, 0.9f, 0.6f), r.P.Wood).GetComponent<MeshRenderer>().enabled = false;
            LocationKit.AddPrimitive(PrimitiveType.Cylinder, "Barrel Body", r.Decor, at + new Vector3(0f, 0.45f, 0f), new Vector3(0.65f, 0.45f, 0.65f), r.P.Wood);
            foreach (float y in new[] { 0.15f, 0.75f })
                LocationKit.AddPrimitive(PrimitiveType.Cylinder, "Hoop", r.Decor, at + new Vector3(0f, y, 0f), new Vector3(0.68f, 0.025f, 0.68f), DecorMat("Iron", new Color(0.2f, 0.2f, 0.2f)));
        }

        private static void Crates(Room r, float x, float z)
        {
            Vector3 at = r.World(x, z);
            Material crate = GreyboxAssets.Textured("Bakery_Crate", new Color(0.62f, 0.44f, 0.28f), Art.TextureSet.Planks, 0.6f, 0.2f);
            LocationKit.Furniture("Crate", r.Decor, at + new Vector3(0f, 0.3f, 0f), new Vector3(0.6f, 0.6f, 0.6f), crate);
            LocationKit.Furniture("Crate", r.Decor, at + new Vector3(0.7f, 0.3f, 0.1f), new Vector3(0.6f, 0.6f, 0.6f), crate);
            LocationKit.Furniture("Crate", r.Decor, at + new Vector3(0.35f, 0.85f, 0.05f), new Vector3(0.55f, 0.5f, 0.55f), crate).transform.rotation = Quaternion.Euler(0f, 15f, 0f);
        }

        private static void Sacks(Room r, float x, float z)
        {
            Vector3 at = r.World(x, z);
            Material sack = GreyboxAssets.Textured("Bakery_FlourSack", new Color(0.95f, 0.92f, 0.84f), Art.TextureSet.Fabric, 0.5f, 0.05f);
            foreach (var (dx, dy, dz) in new[] { (-0.3f, 0.25f, 0f), (0.3f, 0.25f, 0.05f), (0f, 0.62f, 0.02f) })
                LocationKit.AddPrimitive(PrimitiveType.Sphere, "Sack", r.Decor, at + new Vector3(dx, dy, dz), new Vector3(0.6f, 0.5f, 0.45f), sack);
        }

        /// <summary>Copper pans hanging from a rail over a workbench.</summary>
        private static void Pans(Room r, float x, float z)
        {
            Vector3 at = r.World(x, z);
            Material copper = DecorMat("Copper", new Color(0.78f, 0.45f, 0.28f));
            Material iron = DecorMat("Iron", new Color(0.2f, 0.2f, 0.2f));
            GreyboxAssets.MarkStatic(GreyboxAssets.Box("Pan Rail", r.Decor, at + new Vector3(0f, 2.4f, 0f), new Vector3(3f, 0.05f, 0.05f), iron, keepCollider: false));
            foreach (float dx in new[] { -1.2f, -0.6f, 0f, 0.6f, 1.2f })
            {
                LocationKit.AddPrimitive(PrimitiveType.Cylinder, "Pan", r.Decor, at + new Vector3(dx, 2.05f, 0f), new Vector3(0.38f, 0.03f, 0.38f), copper)
                    .transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                GreyboxAssets.MarkStatic(GreyboxAssets.Box("Pan Hook", r.Decor, at + new Vector3(dx, 2.3f, 0f), new Vector3(0.02f, 0.2f, 0.02f), iron, keepCollider: false));
            }
            foreach (float dx in new[] { -1.5f, 1.5f })
                GreyboxAssets.MarkStatic(GreyboxAssets.Box("Pan Rail Hanger", r.Decor, at + new Vector3(dx, 4.2f, 0f), new Vector3(0.03f, 3.6f, 0.03f), iron, keepCollider: false));
        }

        /// <summary>Bunches of dried herbs hanging along a beam on the pantry's back wall, above the jars.</summary>
        private static void HerbRack(Room r, float x, float z)
        {
            Vector3 at = r.World(x, z);
            GreyboxAssets.MarkStatic(GreyboxAssets.Box("Herb Beam", r.Decor, at + new Vector3(0f, 4.4f, 0f), new Vector3(16f, 0.12f, 0.12f), r.P.Wood, keepCollider: false));
            Material[] herbs = { DecorMat("Herb1", new Color(0.45f, 0.55f, 0.3f)), DecorMat("Herb2", new Color(0.6f, 0.55f, 0.35f)), DecorMat("Lavender", new Color(0.55f, 0.45f, 0.7f)) };
            for (int i = 0; i < 22; i++)
                LocationKit.AddPrimitive(PrimitiveType.Capsule, "Herbs", r.Decor, at + new Vector3(-7.6f + i * 0.72f, 4.1f, -0.05f), new Vector3(0.18f, 0.25f, 0.18f), herbs[i % 3]);
        }

        // ───────────── The shell: the building itself ─────────────

        private static void BuildShell(LocationDefinition location, ZoneInfo[] zones, LocationPalette p)
        {
            Transform building = LocationKit.BuildRoom("Bakery Building", FloorSize, Ceiling, p);
            var decor = new GameObject("Decor").transform;

            // Partition walls with wide doorways, and a sign over each doorway naming the room beyond.
            BuildPartitions(building, p);
            foreach (var d in Doorways)
            {
                string Name(string id) { foreach (ZoneSpec z in Zones) if (z.Id == id) return z.Name; return id; }
                Vector3 c = new Vector3(d.at.x, 0f, d.at.y);
                Vector3 n = d.alongX ? Vector3.forward : Vector3.right; // from the "south" room into the "north" room
                float y = DoorHeight + 0.55f, off = WallThickness * 0.5f + 0.03f;
                LocationKit.BuildWallSign(decor, $"Sign {Name(d.north)}", Name(d.north).ToUpperInvariant(), c - n * off + Vector3.up * y,
                    Quaternion.LookRotation(n), new Vector2(3.2f, 0.5f), p.Wood, new Color(0.98f, 0.92f, 0.78f), new Color(0.35f, 0.18f, 0.08f));
                LocationKit.BuildWallSign(decor, $"Sign {Name(d.south)}", Name(d.south).ToUpperInvariant(), c + n * off + Vector3.up * y,
                    Quaternion.LookRotation(-n), new Vector2(3.2f, 0.5f), p.Wood, new Color(0.98f, 0.92f, 0.78f), new Color(0.35f, 0.18f, 0.08f));
            }


            // Dark ceiling beams across the whole floor (the frieze runs along the partitions, see Partition), and paintings
            // either side of each doorway, above the tall shelves.
            Material beam = GreyboxAssets.Textured("Bakery_Beam", new Color(0.32f, 0.2f, 0.12f), Art.TextureSet.Wood, 1.2f, 0.3f);

            for (float bx = -27.5f; bx <= 27.6f; bx += 5f)
                GreyboxAssets.MarkStatic(GreyboxAssets.Box("Ceiling Beam", building, new Vector3(bx, Ceiling - 0.22f, 0f), new Vector3(0.32f, 0.44f, FloorSize.y - 0.2f), beam, keepCollider: false));
            int seed = 31;
            foreach (var d in Doorways)
            {
                Vector3 c = new Vector3(d.at.x, 0f, d.at.y);
                Vector3 n = d.alongX ? Vector3.forward : Vector3.right;
                Vector3 along = d.alongX ? Vector3.right : Vector3.forward;
                foreach (int face in new[] { -1, 1 })
                foreach (int side in new[] { -1, 1 })
                {
                    Vector3 at = c + n * face * (WallThickness * 0.5f + 0.02f) + along * side * 4.2f + Vector3.up * 4.85f;
                    var picture = (seed % 3) == 0 ? Art.ArchitectureKit.Picture.Map : Art.ArchitectureKit.Picture.Landscape;
                    Art.ArchitectureKit.Painting(decor, at, Quaternion.LookRotation(-n * face), new Vector2(1.5f, 0.95f), picture, seed++);
                }
            }
            // Each room gets its own floor inlay, two rows of chandeliers, and windows in the front and back walls.
            foreach (ZoneSpec z in Zones)
            {
                Material inlay = GreyboxAssets.Textured($"Bakery_Inlay_{z.Id}", z.InlayColor, z.Inlay, 1.6f, 0.35f);
                GreyboxAssets.MarkStatic(GreyboxAssets.Box($"Floor Inlay {z.Name}", building, new Vector3(z.Centre.x, 0.004f, z.Centre.y), new Vector3(17.5f, 0.008f, 17.5f), inlay, keepCollider: false));
                foreach (float lx in new[] { -5f, 5f })
                foreach (float lz in new[] { -5f, 5f })
                    LocationKit.BuildLamp($"Lamp {z.Name} {lx:0}{lz:0}", decor, new Vector3(z.Centre.x + lx, 0f, z.Centre.y + lz), Ceiling, p);
                foreach (float wx in new[] { -5f, 5f })
                {
                    bool frontRow = z.Centre.y < 0f;
                    var window = new GameObject($"Window {(frontRow ? "S" : "N")} {z.Name} {wx:0}").transform;
                    window.SetParent(building, false);
                    window.localPosition = new Vector3(z.Centre.x + wx, p.WindowY, frontRow ? -FloorSize.y * 0.5f + 0.1f : FloorSize.y * 0.5f - 0.1f);
                    window.localRotation = Quaternion.Euler(0f, frontRow ? 180f : 0f, 0f); // local -Z faces into the building
                    LocationKit.BuildWindow(window, p.WindowSize, p);
                    if (p.Mood != null) Art.MoodAssets.AddWindowLight(window, p.WindowSize, p.Mood.WindowLight, p.Mood.WindowLightStrength);
                }
            }

            // The Bakehouse ovens: brick, with a glowing mouth, along its outside wall.
            Material brick = GreyboxAssets.Textured("Bakery_OvenBrick", new Color(0.78f, 0.5f, 0.38f), Art.TextureSet.Brick, 1f, 0.1f);
            Material fire = GreyboxAssets.Emissive("Bakery_OvenFire", new Color(1f, 0.55f, 0.2f), new Color(1f, 0.45f, 0.12f), 4f);
            Material iron = GreyboxAssets.Lit("Bakery_Iron", new Color(0.16f, 0.15f, 0.14f));
            foreach (float z in new[] { 4f, 9.5f, 15f })
            {
                var oven = new GameObject("Oven").transform;
                oven.SetParent(decor, false);
                oven.SetPositionAndRotation(new Vector3(29.9f - 1f, 0f, z), Quaternion.Euler(0f, 90f, 0f)); // local -Z (the mouth) faces west
                LocationKit.Furniture("Oven Body", oven, new Vector3(0f, 1.2f, 0f), new Vector3(3.2f, 2.4f, 2f), brick);
                LocationKit.Furniture("Oven Hood", oven, new Vector3(0f, 2.7f, 0.1f), new Vector3(2.6f, 0.6f, 1.6f), brick);
                LocationKit.Furniture("Flue", oven, new Vector3(0f, 4.5f, 0.3f), new Vector3(0.8f, 3f, 0.8f), brick);
                LocationKit.AddPrimitive(PrimitiveType.Cube, "Fire", oven, new Vector3(0f, 0.95f, -1.0f), new Vector3(1.3f, 0.75f, 0.04f), fire);
                LocationKit.AddPrimitive(PrimitiveType.Cube, "Oven Door Bar", oven, new Vector3(0f, 1.42f, -1.03f), new Vector3(1.5f, 0.06f, 0.06f), iron);
                var glowGo = new GameObject("Oven Glow");
                glowGo.transform.SetParent(oven, false);
                glowGo.transform.localPosition = new Vector3(0f, 1f, -1.5f);
                Light glow = glowGo.AddComponent<Light>();
                glow.type = LightType.Point;
                glow.color = new Color(1f, 0.55f, 0.25f);
                glow.intensity = 1.6f;
                glow.range = 4f;
                glow.shadows = LightShadows.None;
            }

            // The sales counter in the Shop Hall, with the baker behind it once the whole Bakery is tidy.
            var counterAt = new Vector3(5.8f, 0f, -1.6f);
            LocationKit.Furniture("Sales Counter", decor, counterAt + new Vector3(0f, 0.5f, 0f), new Vector3(5f, 1f, 0.9f), p.Accent);
            LocationKit.Furniture("Counter Top", decor, counterAt + new Vector3(0f, 1.02f, 0f), new Vector3(5.1f, 0.05f, 1f), p.Wood);
            LocationKit.BuildFlowers(decor, counterAt + new Vector3(-2f, 1.05f, 0f));
            GameObject baker = LocationKit.BuildNpc("Baker NPC", counterAt + new Vector3(0.8f, 0f, 0.9f), 180f,
                body: new Color(0.97f, 0.97f, 0.95f), accent: new Color(0.2f, 0.55f, 0.56f), skin: new Color(0.92f, 0.72f, 0.58f),
                NpcHeadwear.ChefHat, new Color(0.97f, 0.97f, 0.95f));
            Material photo = GreyboxAssets.Emissive("Memory_Photo", new Color(0.95f, 0.8f, 0.55f), new Color(1f, 0.72f, 0.4f), 1.6f);
            GameObject fragment = LocationKit.BuildMemoryFragment(location.Memory, "The whole Bakery is tidy again! Something glimmers on the counter…",
                counterAt + new Vector3(-0.8f, 1.05f, 0f), Quaternion.Euler(0f, 180f, 0f), new Vector3(0.45f, 0.4f, 0.3f), visual =>
                {
                    visual.localPosition = new Vector3(0f, 0.15f, 0f);
                    visual.localRotation = Quaternion.Euler(-12f, 0f, 0f);
                    LocationKit.AddPrimitive(PrimitiveType.Cube, "Frame", visual, Vector3.zero, new Vector3(0.3f, 0.24f, 0.025f), p.Wood);
                    LocationKit.AddPrimitive(PrimitiveType.Cube, "Photo", visual, new Vector3(0f, 0f, -0.014f), new Vector3(0.24f, 0.18f, 0.005f), photo);
                });

            // Systems: the MegaLocation, entrance and exit, the mood, and restoration on the grand total.
            var systems = new GameObject("Bakery Systems");
            float front = -FloorSize.y * 0.5f;
            var entry = new GameObject("Entry Point").transform;
            entry.SetParent(systems.transform, false);
            entry.SetPositionAndRotation(new Vector3(0f, 0.05f, front + 1.4f), Quaternion.identity);
            var drop = new GameObject("Drop Point (items set down when leaving)").transform;
            drop.SetParent(systems.transform, false);
            drop.position = new Vector3(1.2f, 0f, front + 0.8f);
            LocationKit.BuildExitDoor(systems.transform, new Vector3(0f, 0f, front + 0.12f), p);
            if (p.Mood != null) Art.MoodAssets.Build(systems.transform, p.Mood, FloorSize, Ceiling);

            var doorways = new List<Vector2>();
            foreach (var d in Doorways) doorways.Add(d.at);
            MegaLocation mega = systems.AddComponent<MegaLocation>();
            mega.EditorConfigure(location, zones, entry, drop, new Rect(-FloorSize.x * 0.5f, -FloorSize.y * 0.5f, FloorSize.x, FloorSize.y), doorways.ToArray());

            var stagesRoot = new GameObject("Restoration Stages").transform;
            RestorationStage stage = new GameObject("Stage 100% Bakery Opens").AddComponent<RestorationStage>();
            stage.transform.SetParent(stagesRoot, false);
            baker.transform.SetParent(stagesRoot, true);
            fragment.transform.SetParent(stagesRoot, true);
            stage.EditorConfigure(1f, new[] { baker, fragment }, null, null, null, null);
            systems.AddComponent<RestorationController>().EditorConfigure(null, null, new[] { stage }, mega);
        }

        /// <summary>Interior walls on the room grid (x = ±10, z = 0), with the doorways cut out and framed.</summary>
        private static void BuildPartitions(Transform building, LocationPalette p)
        {
            var walls = new GameObject("Partitions").transform;
            walls.SetParent(building, false);
            float hx = FloorSize.x * 0.5f, hz = FloorSize.y * 0.5f;
            // z = 0 from x -30..30 (three doorways); x = ±10 from z -20..20 (two doorways each).
            Partition(walls, new Vector2(-hx, 0f), new Vector2(hx, 0f), p);
            Partition(walls, new Vector2(-10f, -hz), new Vector2(-10f, hz), p);
            Partition(walls, new Vector2(10f, -hz), new Vector2(10f, hz), p);
        }

        private static void Partition(Transform parent, Vector2 a, Vector2 b, LocationPalette p)
        {
            bool alongX = Mathf.Abs(b.x - a.x) > Mathf.Abs(b.y - a.y);
            float start = alongX ? a.x : a.y, end = alongX ? b.x : b.y;
            var cuts = new List<float>();
            foreach (var d in Doorways)
                if (d.alongX == alongX && Mathf.Approximately(alongX ? d.at.y : d.at.x, alongX ? a.y : a.x)) cuts.Add(alongX ? d.at.x : d.at.y);
            cuts.Sort();

            Vector3 Pos(float t, float y) => alongX ? new Vector3(t, y, a.y) : new Vector3(a.x, y, t);
            Vector3 Size(float length, float height, float thick) => alongX ? new Vector3(length, height, thick) : new Vector3(thick, height, length);

            float cursor = start;
            var spans = new List<(float from, float to)>();
            foreach (float c in cuts)
            {
                spans.Add((cursor, c - DoorWidth * 0.5f));
                cursor = c + DoorWidth * 0.5f;
            }
            spans.Add((cursor, end));
            foreach (var s in spans)
            {
                float len = s.to - s.from, mid = (s.from + s.to) * 0.5f;
                if (len <= 0.01f) continue;
                LocationKit.Furniture("Wall", parent, Pos(mid, Ceiling * 0.5f), Size(len, Ceiling, WallThickness), p.Wall);
                // Baseboard and chair rail on both faces.
                GreyboxAssets.MarkStatic(GreyboxAssets.Box("Baseboard", parent, Pos(mid, 0.07f), Size(len, 0.14f, WallThickness + 0.06f), p.Trim, keepCollider: false));
                GreyboxAssets.MarkStatic(GreyboxAssets.Box("Chair Rail", parent, Pos(mid, 1.03f), Size(len, 0.06f, WallThickness + 0.09f), p.Trim, keepCollider: false));
            }
            // The painted frieze runs along the partitions too, both faces, over the doorways.
            GreyboxAssets.MarkStatic(GreyboxAssets.Box("Frieze", parent, Pos((start + end) * 0.5f, 5.55f), Size(end - start, 0.36f, WallThickness + 0.08f),
                GreyboxAssets.Textured("Bakery_Frieze", new Color(0.22f, 0.42f, 0.42f), Art.TextureSet.Damask, 1.2f, 0.2f), keepCollider: false));
            foreach (float c in cuts)
            {
                // Lintel over the doorway, and a framed opening: jambs, a moulded head.
                LocationKit.Furniture("Lintel", parent, Pos(c, (DoorHeight + Ceiling) * 0.5f), Size(DoorWidth, Ceiling - DoorHeight, WallThickness), p.Wall);
                foreach (int side in new[] { -1, 1 })
                    LocationKit.Furniture("Jamb", parent, Pos(c + side * (DoorWidth * 0.5f - 0.12f), DoorHeight * 0.5f), Size(0.24f, DoorHeight, WallThickness + 0.12f), p.Wood);
                LocationKit.Furniture("Door Head", parent, Pos(c, DoorHeight + 0.1f), Size(DoorWidth + 0.3f, 0.2f, WallThickness + 0.16f), p.Wood);
            }
        }
    }
}
