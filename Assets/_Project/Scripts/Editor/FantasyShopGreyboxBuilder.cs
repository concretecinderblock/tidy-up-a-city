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
    /// The Fantasy shop (about 3,300 items). Template for the Magic Quarter (family I, living
    /// items, combined with A, shelf sort): potion walls, spellbooks, herbs, crystal and wand cases, candle racks,
    /// scroll crates and curio stands, plus the two original fixtures where the living items belong. Items and
    /// their behaviours come from Data/Items/fantasy_shop.csv
    /// ("living" column). Safe to re-run.
    /// </summary>
    public static class FantasyShopGreyboxBuilder
    {
        public const string LocationAddress = "Location_FantasyShop";
        public const string LocationAssetPath = "Assets/_Project/Data/Locations/FantasyShop.asset";
        public const string LocationScenePath = "Assets/_Project/Scenes/Locations/FantasyShop.unity";
        private const string TestScenePath = "Assets/_Project/Scenes/Test/FantasyShop_Greybox.unity";
        private const string MemoryAssetPath = "Assets/_Project/Data/Story/FantasyShop_Memory.asset";
        private const float WallHeight = 3.8f;
        private static readonly Vector2 RoomSize = new Vector2(18f, 14f);

        [MenuItem("Tools/Tidy Up/Test Scenes/Build Fantasy Shop Test Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildScene(standalone: true);
        }

        /// <summary>Content-only scene for the City (called by CityGreyboxBuilder).</summary>
        public static void BuildLocationScene() => BuildScene(standalone: false);

        private static void BuildScene(bool standalone)
        {
            // 1. Data
            Dictionary<string, ItemDefinition> allItems = LocationKit.ImportItems();
            List<ItemDefinition> items = LocationKit.ItemsForLocation(allItems, "fantasy_shop");
            if (items.Count == 0)
            {
                Debug.LogError("[Tidy Up] No items with homeLocationId 'fantasy_shop' in Data/Items/*.csv.");
                return;
            }

            MemoryDefinition memory = LocationKit.LoadOrCreateMemory(MemoryAssetPath, "memory_fantasy_shop", "The Day Before",
                "Zephyrine's Curiosities, Magic Quarter", new[]
                {
                    new MemoryLine("", "Midnight in the Magic Quarter. The candles in the window burn green, then gold, then green again.", 5f),
                    new MemoryLine("Madame Zephyrine", "Potions on top, crystals in the middle, candles below. Everything in here likes to wander, so everything needs a home.", 6.5f),
                    new MemoryLine("", "The crystal ball on the counter clouded over. In the mist she saw a street she had never walked: a thirteenth lane, tucked behind the clock on the square.", 7f),
                    new MemoryLine("Madame Zephyrine", "Something small is knocking on the city's door from that side. It sounds… lonely.", 5.5f),
                    new MemoryLine("", "She left a sugared bun on the doorstep, just in case, and went up to bed.", 5f),
                    new MemoryLine("", "By morning the bun was gone, a trail of crumbs led toward the square, and every shelf in the city had spilled its secrets.", 6f),
                }, "The crystal ball showed a thirteenth lane behind the clock on the square, and something small and lonely knocking from the other side.");
            LocationKit.PrepareLocation(LocationAssetPath, "fantasy_shop", "Fantasy Shop", "Magic Quarter", LocationAddress,
                new[] { MechanicFamily.LivingItems, MechanicFamily.ShelfSort }, 1313, items, memory);

            LocationPalette p = LocationPalette.Create("Fantasy",
                floor: new Color(0.55f, 0.36f, 0.24f), wall: new Color(0.78f, 0.68f, 0.88f),
                wood: new Color(0.38f, 0.24f, 0.2f), accent: new Color(0.16f, 0.45f, 0.5f));
            AssetDatabase.SaveAssets();

            // 2. Scene
            Scene scene = LocationKit.NewScene(withSun: standalone);
            LocationDefinition location = AssetDatabase.LoadAssetAtPath<LocationDefinition>(LocationAssetPath);

            Transform room = LocationKit.BuildRoom("Shop Room", RoomSize, WallHeight, p);
            BuildFurniture(room, p);
            Material caseGlass = GreyboxAssets.TransparentUnlit("Case_Glass", new Color(0.86f, 0.95f, 1f, 0.12f));
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
            Quaternion facingEast = Quaternion.Euler(0f, -90f, 0f);  // fixtures on the west wall
            Quaternion facingWest = Quaternion.Euler(0f, 90f, 0f);   // fixtures on the east wall
            float NorthZ(ShelfSettings s) => halfZ - s.Depth * 0.5f - 0.02f;
            float Width(ShelfSettings s) => s.InnerWidth + 2f * s.PanelThickness;

            string[] potionKinds = { "potion_rose", "potion_sleep", "potion_sun", "potion_sea", "potion_ember", "potion_frost", "potion_moss", "potion_star" };
            string[] potionNames = { "Rose elixir", "Sleepy draught", "Sunshine tonic", "Sea-salt brew", "Ember draught", "Frost tonic", "Moss mixture", "Starlight syrup" };

            // ── North wall: potion wall, spellbook case, the two featured fixtures (living items), potion wall, herb shelf ──
            float x = -halfX + 0.25f;
            ShelfSettings PotionWall(string name) => new ShelfSettings
            {
                Name = name, Label = "Potion wall",
                Shelves = 8, SlotsPerShelf = 36, SlotsDeep = 2, SlotSize = new Vector3(0.08f, 0.19f, 0.09f),
                RowSlotTypes = potionKinds, RowLabels = potionNames,
                FrameMaterial = p.Wood, MarkerMaterial = p.Marker,
            };
            ShelfSettings potionA = PotionWall("Potion Wall West");
            Add(ShelfGenerator.Generate(potionA, null, new Vector3(x + Width(potionA) * 0.5f, 0f, NorthZ(potionA)), Quaternion.identity));
            x += Width(potionA) + 0.3f;

            var spellbooks = new ShelfSettings
            {
                Name = "Spellbook Case", Label = "Spellbook case",
                Shelves = 6, SlotsPerShelf = 48, SlotSize = new Vector3(0.055f, 0.28f, 0.2f),
                RowSlotTypes = new[] { "spellbook_fire", "spellbook_growth", "spellbook_tides", "spellbook_dreams", "spellbook_fire", "spellbook_tides" },
                RowLabels = new[] { "Fire", "Growth", "Tides", "Dreams", "Fire", "Tides" },
                FrameMaterial = p.Wood, MarkerMaterial = p.Marker,
            };
            Add(ShelfGenerator.Generate(spellbooks, null, new Vector3(x + Width(spellbooks) * 0.5f, 0f, NorthZ(spellbooks)), Quaternion.identity));

            // The original fixtures stay: four mixed rows where the living items (hoppers, floaters, shy and disguised) live.
            var featuredPotions = new ShelfSettings
            {
                Name = "Potion Shelf", Label = "Potion shelf",
                Shelves = 3, SlotsPerShelf = 4, SlotSize = new Vector3(0.3f, 0.3f, 0.3f),
                RowSlotTypes = new[] { "potion", "crystal", "candle" }, RowLabels = new[] { "Potions", "Crystals", "Candles" },
                FrameMaterial = p.Wood, MarkerMaterial = p.Marker,
            };
            Add(ShelfGenerator.Generate(featuredPotions, null, new Vector3(-0.95f, 0f, NorthZ(featuredPotions)), Quaternion.identity));
            var cabinet = new ShelfSettings
            {
                Name = "Curiosity Cabinet", Label = "Curiosity cabinet",
                Shelves = 2, SlotsPerShelf = 4, SlotSize = new Vector3(0.4f, 0.3f, 0.32f),
                RowSlotTypes = new[] { "wand", "curiosity" }, RowLabels = new[] { "Wands", "Curiosities" },
                FrameMaterial = p.Wood, MarkerMaterial = p.Marker,
            };
            Add(ShelfGenerator.Generate(cabinet, null, new Vector3(0.85f, 0f, NorthZ(cabinet)), Quaternion.identity));

            x = 2.0f;
            ShelfSettings potionB = PotionWall("Potion Wall East");
            Add(ShelfGenerator.Generate(potionB, null, new Vector3(x + Width(potionB) * 0.5f, 0f, NorthZ(potionB)), Quaternion.identity));
            x += Width(potionB) + 0.3f;
            var herbs = new ShelfSettings
            {
                Name = "Herb Shelf", Label = "Herb shelf",
                Shelves = 6, SlotsPerShelf = 32, SlotsDeep = 2, SlotSize = new Vector3(0.08f, 0.11f, 0.08f),
                RowSlotTypes = new[] { "herb_lavender", "herb_pepper", "herb_moonflower", "herb_toadstool", "herb_lavender", "herb_pepper" },
                RowLabels = new[] { "Lavender", "Dragon pepper", "Moonflower", "Toadstool", "Lavender", "Dragon pepper" },
                FrameMaterial = p.Wood, MarkerMaterial = p.Marker,
            };
            Add(ShelfGenerator.Generate(herbs, null, new Vector3(x + Width(herbs) * 0.5f, 0f, NorthZ(herbs)), Quaternion.identity));

            // ── West wall (low, under the windows): two glass crystal cabinets and two candle racks ──
            float z = halfZ - 0.9f;
            string[][] crystalRows = { new[] { "crystal_rosequartz", "crystal_citrine", "crystal_sapphire" }, new[] { "crystal_emerald", "crystal_onyx", "crystal_opal" } };
            string[][] crystalNames = { new[] { "Rose quartz", "Citrine", "Sapphire" }, new[] { "Emerald", "Onyx", "Opal" } };
            for (int i = 0; i < 2; i++)
            {
                var s = new ShelfSettings
                {
                    Name = $"Crystal Cabinet {i + 1}", Label = "Crystal cabinet", Style = ShelfStyle.Case,
                    Shelves = 3, SlotsPerShelf = 26, SlotsDeep = 3, SlotSize = new Vector3(0.1f, 0.13f, 0.1f), BaseHeight = 0.4f,
                    RowSlotTypes = crystalRows[i], RowLabels = crystalNames[i],
                    FrameMaterial = p.Wood, BodyMaterial = p.Accent, GlassMaterial = caseGlass, MarkerMaterial = p.Marker,
                };
                Add(ShelfGenerator.Generate(s, null, new Vector3(-halfX + s.Depth * 0.5f + 0.05f, 0f, z - Width(s) * 0.5f), facingEast));
                z -= Width(s) + 0.35f;
            }
            string[][] candleRows = { new[] { "candle_beeswax", "candle_rose", "candle_sage" }, new[] { "candle_midnight", "candle_beeswax", "candle_rose" } };
            string[][] candleNames = { new[] { "Beeswax", "Rose", "Sage" }, new[] { "Midnight", "Beeswax", "Rose" } };
            for (int i = 0; i < 2; i++)
            {
                var s = new ShelfSettings
                {
                    Name = $"Candle Rack {i + 1}", Label = "Candle rack", Style = ShelfStyle.OpenRack,
                    Shelves = 3, SlotsPerShelf = 36, SlotsDeep = 2, SlotSize = new Vector3(0.07f, 0.17f, 0.07f),
                    RowSlotTypes = candleRows[i], RowLabels = candleNames[i],
                    FrameMaterial = p.Wood, MarkerMaterial = p.Marker,
                };
                Add(ShelfGenerator.Generate(s, null, new Vector3(-halfX + s.Depth * 0.5f + 0.05f, 0f, z - Width(s) * 0.5f), facingEast));
                z -= Width(s) + 0.35f;
            }

            // ── East wall (low): two wand cases, two scroll crates; the counter by the door ──
            z = halfZ - 0.9f;
            for (int i = 0; i < 2; i++)
            {
                var s = new ShelfSettings
                {
                    Name = $"Wand Case {i + 1}", Label = "Wand case", Style = ShelfStyle.Case,
                    Shelves = 4, SlotsPerShelf = 8, SlotsDeep = 6, SlotSize = new Vector3(0.32f, 0.05f, 0.05f), BaseHeight = 0.45f,
                    RowSlotTypes = i == 0 ? new[] { "wand_oak", "wand_birch", "wand_oak", "wand_birch" } : new[] { "wand_ebony", "wand_holly", "wand_ebony", "wand_holly" },
                    RowLabels = i == 0 ? new[] { "Oak", "Birch", "Oak", "Birch" } : new[] { "Ebony", "Holly", "Ebony", "Holly" },
                    FrameMaterial = p.Wood, BodyMaterial = p.Accent, GlassMaterial = caseGlass, MarkerMaterial = p.Marker,
                };
                Add(ShelfGenerator.Generate(s, null, new Vector3(halfX - s.Depth * 0.5f - 0.05f, 0f, z - Width(s) * 0.5f), facingWest));
                z -= Width(s) + 0.35f;
            }
            for (int i = 0; i < 2; i++)
            {
                var s = new ShelfSettings
                {
                    Name = $"Scroll Crate {i + 1}", Label = "Scroll crate", Style = ShelfStyle.Crate,
                    Shelves = 1, SlotsPerShelf = 5, SlotsDeep = 10, SlotSize = new Vector3(0.27f, 0.08f, 0.06f), BaseHeight = 0.35f,
                    RowSlotTypes = new[] { i == 0 ? "scroll" : "scroll_sealed" }, RowLabels = new[] { i == 0 ? "Scrolls" : "Sealed scrolls" },
                    FrameMaterial = p.Wood, MarkerMaterial = p.Marker,
                };
                Add(ShelfGenerator.Generate(s, null, new Vector3(halfX - s.Depth * 0.5f - 0.1f, 0f, z - Width(s) * 0.5f), facingWest));
                z -= Width(s) + 0.35f;
            }

            // ── By the door: six round stands of snow globes, teacups and rune stones ──
            float[] standX = { -6.2f, -4.0f, -1.8f, 1.8f, 4.0f, 6.0f };
            string[] standKinds = { "stand_globe", "stand_cup", "stand_rune", "stand_rune", "stand_cup", "stand_globe" };
            string[] standNames = { "Snow globes", "Teacups", "Rune stones", "Rune stones", "Teacups", "Snow globes" };
            for (int i = 0; i < standX.Length; i++)
            {
                Add(StandGenerator.Generate(new StandSettings
                {
                    Name = $"Curio Stand {i + 1}", Label = "Curio stand",
                    SlotsPerTier = new[] { 8, 6, 4 }, SlotSize = new Vector3(0.17f, 0.17f, 0.17f),
                    TierSlotTypes = new[] { standKinds[i] }, TierLabels = new[] { standNames[i] },
                    FrameMaterial = p.Wood, MarkerMaterial = p.Marker,
                }, null, new Vector3(standX[i], 0f, -halfZ + 3.6f), Quaternion.identity));
            }

            var decor = new GameObject("Decor").transform;
            int lamp = 0;
            foreach (float lx in new[] { -6f, -2f, 2f, 6f })
            foreach (float lz in new[] { -3.5f, 1.1f, 4.8f })
            {
                LocationKit.BuildLamp($"Lamp {++lamp}", decor, new Vector3(lx, 0f, lz), WallHeight, p);
            }
            LocationKit.BuildWallSign(decor, "Shop Sign", "POTIONS & CURIOSITIES", new Vector3(0f, 2.6f, halfZ - 0.04f), Quaternion.identity,
                new Vector2(2.6f, 0.36f), p.Wood, new Color(0.75f, 0.6f, 1f), new Color(0.2f, 0.08f, 0.3f));

            // Items land anywhere on the floor, rug, counter and display table (never on fixtures). Living items roam it too.
            BoxCollider scatter = LocationKit.BuildScatterArea(new Vector3(0f, 0.6f, 0f), new Vector3(RoomSize.x - 1f, 1.2f, RoomSize.y - 1f));
            SlotGroup[] groupArray = groups.ToArray();
            LocationRig rig = LocationKit.BuildLocationSystems(location, groupArray, new[] { scatter }, RoomSize, p);
            if (standalone) LocationKit.BuildPlayerRig(rig.EntryPosition, 0f, p);

            // 3. At 100%: Madame Zephyrine returns to the counter and her crystal ball glows with the memory.
            GameObject keeper = LocationKit.BuildNpc("Shopkeeper NPC", new Vector3(halfX - 0.6f, 0f, -halfZ + 2.2f), -90f,
                body: new Color(0.2f, 0.22f, 0.5f), accent: new Color(0.95f, 0.75f, 0.3f), skin: new Color(0.78f, 0.58f, 0.45f),
                NpcHeadwear.WizardHat, new Color(0.35f, 0.2f, 0.55f));
            Material glass = GreyboxAssets.Emissive("Memory_CrystalBall", new Color(0.75f, 0.85f, 1f), new Color(0.6f, 0.7f, 1f), 2f);
            GameObject fragment = LocationKit.BuildMemoryFragment(location.Memory,
                "The Fantasy Shop is tidy again! The crystal ball on the counter is glowing…",
                new Vector3(halfX - 1.5f, 0.94f, -halfZ + 1.4f), Quaternion.Euler(0f, 90f, 0f), new Vector3(0.35f, 0.35f, 0.35f), visual =>
                {
                    visual.localPosition = Vector3.zero;
                    LocationKit.AddPrimitive(PrimitiveType.Cylinder, "Stand", visual, new Vector3(0f, 0.03f, 0f), new Vector3(0.14f, 0.03f, 0.14f), p.Wood);
                    LocationKit.AddPrimitive(PrimitiveType.Sphere, "Ball", visual, new Vector3(0f, 0.14f, 0f), Vector3.one * 0.18f, glass);
                });
            LocationKit.BuildCompletionStage(rig, keeper, fragment);

            LocationKit.RecordSlotCount(LocationAssetPath, groupArray);
            string path = standalone ? TestScenePath : LocationScenePath;
            LocationKit.SaveScene(scene, path);
            Debug.Log($"[Tidy Up] Built {path}.");
        }

        private static void BuildFurniture(Transform room, LocationPalette p)
        {
            // Counter by the door (east), big rug, round display table, cauldron and giant mushrooms in the open middle.
            LocationKit.Furniture("Counter", room, new Vector3(7.4f, 0.45f, -4.7f), new Vector3(0.8f, 0.9f, 2.6f), p.Accent);
            LocationKit.MakeCleanable(LocationKit.Furniture("Counter Top", room, new Vector3(7.4f, 0.92f, -4.7f), new Vector3(0.86f, 0.04f, 2.66f), p.Wood), "Counter");
            LocationKit.Furniture("Rug", room, new Vector3(0f, 0.005f, 0.8f), new Vector3(8f, 0.01f, 5f),
                GreyboxAssets.Textured("Fantasy_Rug", new Color(0.3f, 0.2f, 0.5f), Art.TextureSet.Fabric, 0.5f, 0.02f));

            Material cloth = GreyboxAssets.Textured("Fantasy_TableCloth", new Color(0.85f, 0.65f, 0.25f), Art.TextureSet.Fabric, 0.4f, 0.05f);
            GameObject round = GameObject.CreatePrimitive(PrimitiveType.Cylinder); // keeps its collider: items land on it
            round.name = "Display Table";
            round.transform.SetParent(room, false);
            round.transform.localPosition = new Vector3(-3.6f, 0.36f, -0.2f); // off the entrance sightline
            round.transform.localScale = new Vector3(1.2f, 0.36f, 1.2f);
            round.GetComponent<MeshRenderer>().sharedMaterial = cloth;
            GreyboxAssets.MarkStatic(round);

            Material iron = GreyboxAssets.Lit("Fantasy_CauldronIron", new Color(0.16f, 0.15f, 0.17f));
            Material brew = GreyboxAssets.Emissive("Fantasy_CauldronBrew", new Color(0.4f, 0.95f, 0.5f), new Color(0.3f, 0.9f, 0.4f), 1.5f);
            var cauldron = new GameObject("Cauldron").transform;
            cauldron.SetParent(room, false);
            cauldron.position = new Vector3(3.6f, 0f, 0.6f);
            LocationKit.AddPrimitive(PrimitiveType.Sphere, "Pot", cauldron, new Vector3(0f, 0.35f, 0f), new Vector3(0.8f, 0.7f, 0.8f), iron);
            LocationKit.AddPrimitive(PrimitiveType.Cylinder, "Brew", cauldron, new Vector3(0f, 0.62f, 0f), new Vector3(0.6f, 0.02f, 0.6f), brew);
            var potCollider = cauldron.gameObject.AddComponent<CapsuleCollider>();
            potCollider.center = new Vector3(0f, 0.35f, 0f);
            potCollider.radius = 0.4f;
            potCollider.height = 0.7f;

            Material mushroomCap = GreyboxAssets.Lit("Fantasy_MushroomCap", new Color(0.85f, 0.25f, 0.3f));
            Material mushroomStem = GreyboxAssets.Lit("Fantasy_MushroomStem", new Color(0.95f, 0.9f, 0.8f));
            var mushroom = new GameObject("Giant Mushroom").transform;
            mushroom.SetParent(room, false);
            mushroom.position = new Vector3(-6.4f, 0f, -1.4f);
            LocationKit.AddPrimitive(PrimitiveType.Cylinder, "Stem", mushroom, new Vector3(0f, 0.35f, 0f), new Vector3(0.18f, 0.35f, 0.18f), mushroomStem);
            LocationKit.AddPrimitive(PrimitiveType.Sphere, "Cap", mushroom, new Vector3(0f, 0.75f, 0f), new Vector3(0.7f, 0.35f, 0.7f), mushroomCap);
        }
    }
}
