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

        /// <summary>The front room: glass cases of little cakes in islands, wall cases, pie stands by the street door.</summary>
        private static void BuildShopHall(Room r)
        {
            ShelfSettings IslandCase(int k, int side) => Make(r, $"Case {SmallNames[k]} {side}", "Cake case", ShelfStyle.Case, 3, 22, 3,
                new Vector3(0.11f, 0.13f, 0.11f), new[] { SmallKinds[k] }, new[] { SmallNames[k] }, 0.45f);
            int island = 0;
            foreach (float z in new[] { 3.5f, -2.5f })
            foreach (float x in new[] { -5.5f, 5.5f })
            {
                r.Island(x, z, IslandCase(island, 1), IslandCase(island, 2));
                island++;
            }
            ShelfSettings WallCase(int i) => Make(r, $"Wall Case {SmallNames[i % 4]}", "Cake case", ShelfStyle.Case, 3, 16, 2,
                new Vector3(0.11f, 0.13f, 0.11f), new[] { SmallKinds[i % 4] }, new[] { SmallNames[i % 4] }, 0.45f);
            r.Row(Wall.W, -9.2f, 0.6f, 3, WallCase);
            r.Row(Wall.E, -0.4f, 8.8f, 4, i => Tall(Make(r, $"Cake Shelf {SmallNames[(i + 2) % 4]}", "Cake shelf", ShelfStyle.Bookcase, 5, 12, 1,
                new Vector3(0.14f, 0.16f, 0.14f), new[] { SmallKinds[(i + 2) % 4] }, new[] { SmallNames[(i + 2) % 4] })));

            float[] standX = { -8f, -5.5f, -3f, 3f, 5.5f, 8f };
            for (int i = 0; i < standX.Length; i++)
                r.Stand(new StandSettings
                {
                    Name = $"Pie Stand {i + 1}", Label = "Pie stand", SlotsPerTier = new[] { 8, 6, 4 }, SlotSize = new Vector3(0.26f, 0.1f, 0.26f),
                    TierSlotTypes = new[] { "big_pie" }, TierLabels = new[] { "Apple pies" }, FrameMaterial = r.P.Wood, MarkerMaterial = r.P.Marker,
                }, standX[i], -7f);
            foreach (float x in new[] { -5.5f, 5.5f })
                LocationKit.BuildFlowers(r.Decor, r.World(x, 0.5f) + new Vector3(0f, 0.92f, 0f));
        }

        private static readonly string[] LoafKinds = { "loaf_sourdough", "loaf_rye", "loaf_seeded", "loaf_brioche" };
        private static readonly string[] LoafNames = { "Sourdough", "Rye", "Seeded", "Brioche" };

        /// <summary>Towering loaf walls all round, and islands of baguette and baton racks down the middle.</summary>
        private static void BuildBreadHall(Room r)
        {
            int n = 0;
            ShelfSettings Loaf(int i)
            {
                int k = n++ % 4;
                return Tall(Make(r, $"Loaf Wall {LoafNames[k]}", $"{LoafNames[k]} wall", ShelfStyle.Bookcase, 6, 10, 2,
                    new Vector3(0.3f, 0.2f, 0.22f), new[] { LoafKinds[k] }, new[] { LoafNames[k] }));
            }
            r.Row(Wall.N, -9.2f, 9.2f, 5, Loaf);
            r.Row(Wall.W, -9.2f, 0.6f, 3, Loaf);
            r.Row(Wall.W, 5.4f, 8.8f, 1, Loaf);
            r.Row(Wall.E, -9.2f, -5.4f, 1, Loaf);
            r.Row(Wall.E, -0.6f, 8.8f, 3, Loaf);

            string[] longKinds = { "long_baguette", "long_baton" };
            string[] longNames = { "Baguettes", "Rustic batons" };
            ShelfSettings Rack(int k) => Make(r, $"Bread Rack {longNames[k]}", "Bread rack", ShelfStyle.OpenRack, 5, 6, 3,
                new Vector3(0.46f, 0.12f, 0.12f), new[] { longKinds[k] }, new[] { longNames[k] });
            int rack = 0;
            foreach (float z in new[] { 2f, -4f })
            foreach (float x in new[] { -5f, 5f })
            {
                r.Island(x, z, Rack(rack % 2), Rack((rack + 1) % 2));
                rack++;
            }
        }

        private static readonly string[] PastryKinds = { "pastry_croissant", "pastry_chocolat", "pastry_danish", "pastry_swirl" };
        private static readonly string[] PastryNames = { "Croissants", "Pains au chocolat", "Danishes", "Cinnamon swirls" };

        /// <summary>A salon of pastry cases with racks along the long wall, and café tables to sit at.</summary>
        private static void BuildPastrySalon(Room r)
        {
            ShelfSettings Case(int k, int i) => Make(r, $"Case {PastryNames[k]} {i}", "Pastry case", ShelfStyle.Case, 3, 11, 3,
                new Vector3(0.22f, 0.12f, 0.16f), new[] { PastryKinds[k] }, new[] { PastryNames[k] }, 0.45f);
            int c = 0;
            foreach (float z in new[] { 2.5f, -3.5f })
            foreach (float x in new[] { -5.5f, 0f, 5.5f })
            {
                r.Island(x, z, Case(c % 4, c), Case((c + 1) % 4, c));
                c++;
            }
            r.Row(Wall.W, -9.2f, 9.2f, 7, i => Make(r, $"Pastry Rack {PastryNames[i % 4]}", "Pastry rack", ShelfStyle.OpenRack, 5, 10, 2,
                new Vector3(0.22f, 0.12f, 0.16f), new[] { PastryKinds[i % 4] }, new[] { PastryNames[i % 4] }));

            // Café corner along the front wall.
            Material cloth = GreyboxAssets.Lit("Bakery_TableCloth", new Color(0.97f, 0.94f, 0.88f));
            foreach (float x in new[] { -5f, -1.5f, 2f, 5.5f })
            {
                Vector3 at = r.World(x, -8.4f);
                LocationKit.AddPrimitive(PrimitiveType.Cylinder, "Café Table", r.Decor, at + new Vector3(0f, 0.74f, 0f), new Vector3(0.8f, 0.02f, 0.8f), cloth);
                LocationKit.AddPrimitive(PrimitiveType.Cylinder, "Table Leg", r.Decor, at + new Vector3(0f, 0.37f, 0f), new Vector3(0.07f, 0.37f, 0.07f), r.P.Wood);
                foreach (int side in new[] { -1, 1 })
                {
                    LocationKit.Furniture("Chair Seat", r.Decor, at + new Vector3(side * 0.65f, 0.45f, 0f), new Vector3(0.42f, 0.05f, 0.42f), r.P.Wood);
                    LocationKit.Furniture("Chair Back", r.Decor, at + new Vector3(side * 0.85f, 0.72f, 0f), new Vector3(0.05f, 0.5f, 0.42f), r.P.Wood);
                }
                LocationKit.BuildFlowers(r.Decor, at + new Vector3(0f, 0.76f, 0f));
            }
        }

        private static readonly string[] CookieKinds = { "cookie_choc", "cookie_ginger", "cookie_shortbread" };
        private static readonly string[] CookieNames = { "Chocolate chip", "Gingerbread", "Shortbread" };

        /// <summary>Long cookie counters under the windows and a field of cake stands.</summary>
        private static void BuildCakeCookie(Room r)
        {
            ShelfSettings Counter(int i) => Make(r, $"Cookie Counter {CookieNames[i % 3]}", "Cookie counter", ShelfStyle.Bookcase, 4, 20, 3,
                new Vector3(0.1f, 0.06f, 0.1f), new[] { CookieKinds[i % 3] }, new[] { CookieNames[i % 3] });
            r.Row(Wall.E, -9.2f, 9.2f, 6, Counter);
            int s = 0;
            foreach (float z in new[] { 4f, 0f, -4f })
            foreach (float x in new[] { -6f, -2.5f, 1f })
            {
                if (s >= 8) break;
                r.Stand(new StandSettings
                {
                    Name = $"Cake Stand {++s}", Label = "Cake stand", SlotsPerTier = new[] { 8, 6, 4 }, SlotSize = new Vector3(0.26f, 0.18f, 0.26f),
                    TierSlotTypes = new[] { "big_cake" }, TierLabels = new[] { "Lemon cakes" }, FrameMaterial = r.P.Wood, MarkerMaterial = r.P.Marker,
                }, x, z);
            }
        }

        /// <summary>Jars of jam and honey on tall shelves, sacks of flour and sugar along the back wall.</summary>
        private static void BuildPantry(Room r)
        {
            ShelfSettings Jars(int i) => Tall(Make(r, $"Jar Shelf {i + 1}", "Jar shelf", ShelfStyle.Bookcase, 6, 26, 1,
                new Vector3(0.1f, 0.13f, 0.11f), i % 2 == 0 ? new[] { "jar_jam", "jar_jam", "jar_honey" } : new[] { "jar_honey", "jar_jam", "jar_honey" },
                i % 2 == 0 ? new[] { "Jam", "Jam", "Honey" } : new[] { "Honey", "Jam", "Honey" }), 3.2f);
            r.Row(Wall.N, -9.2f, 9.2f, 6, Jars);
            int j = 10;
            foreach (float z in new[] { 2.5f, -3.5f })
            foreach (float x in new[] { -4.5f, 3.5f })
            {
                if (z < 0f) break; // the front half stays open, by the door to the Pastry Salon
                r.Island(x, z, Jars(j++), Jars(j++));
            }
            string[] sackKinds = { "sack_flour", "sack_sugar" };
            string[] sackNames = { "Flour", "Sugar" };
            r.Row(Wall.W, -9.2f, 8.6f, 8, i => Make(r, $"Sack Rack {sackNames[i % 2]}", "Sack rack", ShelfStyle.OpenRack, 2, 6, 2,
                new Vector3(0.32f, 0.4f, 0.26f), new[] { sackKinds[i % 2] }, new[] { sackNames[i % 2] }, 0.08f));
        }

        private static readonly string[] RollKinds = { "roll_white", "roll_bun", "roll_bagel" };
        private static readonly string[] RollNames = { "Soft rolls", "Sweet buns", "Bagels" };

        /// <summary>Where the baking happens: roll racks fresh from the ovens (the ovens are part of the shell), and workbenches.</summary>
        private static void BuildBakehouse(Room r)
        {
            r.Row(Wall.N, -9.2f, 9.2f, 8, i => Make(r, $"Roll Rack {RollNames[i % 3]}", "Roll rack", ShelfStyle.OpenRack, 4, 10, 5,
                new Vector3(0.12f, 0.1f, 0.12f), new[] { RollKinds[i % 3] }, new[] { RollNames[i % 3] }), 0.35f);
            Material flour = GreyboxAssets.Lit("Bakery_Flour", new Color(0.97f, 0.95f, 0.9f));
            foreach (float x in new[] { -4f, 2f })
            {
                Vector3 at = r.World(x, 0.5f);
                LocationKit.MakeCleanable(LocationKit.Furniture("Workbench", r.Decor, at + new Vector3(0f, 0.45f, 0f), new Vector3(3.2f, 0.9f, 1.2f), r.P.Wood), "Workbench");
                LocationKit.AddPrimitive(PrimitiveType.Sphere, "Dough", r.Decor, at + new Vector3(-0.6f, 0.97f, 0f), new Vector3(0.45f, 0.18f, 0.4f), flour);
                LocationKit.AddPrimitive(PrimitiveType.Cylinder, "Rolling Pin", r.Decor, at + new Vector3(0.5f, 0.94f, 0.1f), new Vector3(0.07f, 0.25f, 0.07f), r.P.Wood)
                    .transform.rotation = Quaternion.Euler(0f, 30f, 90f);
            }
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
