using System.Collections.Generic;
using TidyUp.Items;
using TidyUp.Locations;
using TidyUp.Mechanics.OrderedSort;
using TidyUp.Mechanics.ShelfSort;
using TidyUp.Story;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TidyUp.Editor
{
    /// <summary>
    /// The Library (about 4,800 books). Template for the Culture Quarter (family B, ordered sort). Books are sorted
    /// into sections in order: novels and mysteries by author range in the stacks, non-fiction by subject number on
    /// the low wall shelves, encyclopaedias A–M / N–Z, picture books by size in the children's bins. Two featured
    /// cases keep the exact-order puzzle from library.csv sort keys (novels by author, City Atlas by volume, picture
    /// books tallest first). Safe to re-run.
    /// </summary>
    public static class LibraryGreyboxBuilder
    {
        public const string LocationAddress = "Location_Library";
        public const string LocationAssetPath = "Assets/_Project/Data/Locations/Library.asset";
        public const string LocationScenePath = "Assets/_Project/Scenes/Locations/Library.unity";
        private const string TestScenePath = "Assets/_Project/Scenes/Test/Library_Greybox.unity";
        private const string MemoryAssetPath = "Assets/_Project/Data/Story/Library_Memory.asset";
        private const float WallHeight = 6.4f;   // two storeys: a gallery runs round three walls at GalleryHeight
        private const float GalleryHeight = 3.0f;
        private const int BooksPerShelf = 10;
        private static readonly Vector2 RoomSize = new Vector2(20f, 16f);

        [MenuItem("Tools/Tidy Up/Test Scenes/Build Library Test Scene")]
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
            List<ItemDefinition> items = LocationKit.ItemsForLocation(allItems, "library");
            List<string> novels = SortedIds(items, "novel", SortDirection.Ascending);
            List<string> atlases = SortedIds(items, "atlas", SortDirection.Ascending);
            List<string> pictureBooks = SortedIds(items, "picture_book", SortDirection.Descending);
            if (novels.Count != 2 * BooksPerShelf || atlases.Count != BooksPerShelf || pictureBooks.Count != BooksPerShelf)
            {
                Debug.LogError($"[Tidy Up] library.csv needs {2 * BooksPerShelf} novels, {BooksPerShelf} atlases and " +
                               $"{BooksPerShelf} picture books (found {novels.Count}/{atlases.Count}/{pictureBooks.Count}).");
                return;
            }
            // Read item data now: NewScene() below unloads these assets.
            string[] novelRowLabels =
            {
                $"Novels {Initial(novels[0], allItems)}–{Initial(novels[BooksPerShelf - 1], allItems)}",
                $"Novels {Initial(novels[BooksPerShelf], allItems)}–{Initial(novels[novels.Count - 1], allItems)}",
            };

            MemoryDefinition memory = LocationKit.LoadOrCreateMemory(MemoryAssetPath, "memory_library", "The Day Before",
                "The Library, Culture Quarter", new[]
                {
                    new MemoryLine("", "Half past eight in the evening. Rain on the tall windows, and one last reader asleep over a crossword.", 5.5f),
                    new MemoryLine("Ms. Penhallow, the librarian", "Novels by author, atlases by volume, picture books tallest first. A place for everything.", 5.5f),
                    new MemoryLine("", "On the returns desk lay a card for a book she had never seen: City Atlas, Volume Eleven. The Streets That Aren't There.", 6.5f),
                    new MemoryLine("Ms. Penhallow, the librarian", "We have only ever had ten volumes. And look: a tiny, floury handprint on the card.", 6f),
                    new MemoryLine("", "She tucked the card under the lamp to ask about in the morning, and locked the door.", 5f),
                    new MemoryLine("", "By morning, every book had gone wandering, and Volume Eleven was nowhere to be found.", 5.5f),
                }, "A return card for City Atlas, Volume Eleven: 'The Streets That Aren't There', a book the library never owned, signed with a tiny, floury handprint.");
            LocationKit.PrepareLocation(LocationAssetPath, "library", "Library", "Culture Quarter", LocationAddress,
                new[] { MechanicFamily.OrderedSort }, 2024, items, memory);

            LocationPalette p = LocationPalette.Create("Library",
                floor: new Color(0.5f, 0.3f, 0.18f), wall: new Color(0.3f, 0.37f, 0.24f),   // deep green walls, mahogany
                wood: new Color(0.42f, 0.19f, 0.1f), accent: new Color(0.42f, 0.19f, 0.1f));
            p.Mood = Art.MoodPreset.Library;
            p.UseRichSurfaces("Library",
                Art.TextureSet.Damask, new Color(0.32f, 0.39f, 0.25f), 0.7f,          // green damask
                Art.TextureSet.StarPanels, Color.white, 2.0f,                          // starry coffered ceiling
                Art.TextureSet.Parquet, new Color(0.55f, 0.33f, 0.2f), 1.6f,          // parquet
                new Color(0.42f, 0.19f, 0.1f));                                       // mahogany panelling
            // Filler books share the novels' materials (same "Item_<hex>" names as the generated item prefabs).
            string[] fillerHex = { "8E3B46", "2E5E4E", "3B5B92", "C9A227", "6B4E71", "B5651D", "1F3A5F" };
            var filler = new Material[fillerHex.Length];
            for (int i = 0; i < fillerHex.Length; i++)
            {
                ColorUtility.TryParseHtmlString("#" + fillerHex[i], out Color c);
                filler[i] = GreyboxAssets.Lit("Item_" + fillerHex[i], c);
            }
            AssetDatabase.SaveAssets();

            // 2. Scene
            Scene scene = LocationKit.NewScene(withSun: standalone);
            LocationDefinition location = AssetDatabase.LoadAssetAtPath<LocationDefinition>(LocationAssetPath);

            p.GalleryHeight = GalleryHeight;
            Transform room = LocationKit.BuildRoom("Library Room", RoomSize, WallHeight, p);
            Art.ArchitectureKit.Gallery(room, RoomSize, 0.2f, GalleryHeight, WallHeight, p.Wood, p.Trim, filler);
            BuildFurniture(room, p);

            float halfX = RoomSize.x * 0.5f - 0.1f; // inner wall faces
            float halfZ = RoomSize.y * 0.5f - 0.1f;
            Quaternion facingEast = Quaternion.Euler(0f, -90f, 0f);  // fixtures on the west wall
            Quaternion facingWest = Quaternion.Euler(0f, 90f, 0f);   // fixtures on the east wall
            Quaternion facingNorth = Quaternion.Euler(0f, 180f, 0f);
            var groups = new List<SlotGroup>();
            var itemsRoot = new GameObject("Items").transform;

            SlotGroup Add(SlotGroup group)
            {
                LocationKit.FillTidyState(group, location.Items, itemsRoot);
                groups.Add(group);
                return group;
            }

            // ── North wall, centre: the two featured cases keep the exact-order puzzle (one exact book per slot) ──
            var bookSlot = new Vector3(0.06f, 0.3f, 0.2f);
            Add(ShelfGenerator.Generate(new ShelfSettings
            {
                Name = "Featured Novels", Label = "Featured novels",
                Shelves = 2, SlotsPerShelf = BooksPerShelf, SlotSize = bookSlot,
                RowSlotTypes = new[] { "novel" }, RowLabels = novelRowLabels, SlotExactItemIds = novels.ToArray(),
                FillerShelvesAbove = 3, FillerMaterials = filler, FillerSeed = 11,
                FrameMaterial = p.Wood, MarkerMaterial = p.Marker,
            }, null, new Vector3(-0.6f, 0f, halfZ - bookSlot.z * 0.5f - 0.02f), Quaternion.identity));

            var reference = new List<string>(atlases);
            reference.AddRange(pictureBooks);
            Add(ShelfGenerator.Generate(new ShelfSettings
            {
                Name = "Featured Reference", Label = "Featured reference",
                Shelves = 2, SlotsPerShelf = BooksPerShelf, SlotSize = new Vector3(bookSlot.x, 0.4f, bookSlot.z), // tallest picture book is 38 cm
                RowSlotTypes = new[] { "atlas", "picture_book" }, RowLabels = new[] { "City Atlas 1–10", "Picture books · tallest first" },
                SlotExactItemIds = reference.ToArray(),
                FillerShelvesAbove = 2, FillerMaterials = filler, FillerSeed = 23,
                FrameMaterial = p.Wood, MarkerMaterial = p.Marker,
            }, null, new Vector3(0.6f, 0f, halfZ - bookSlot.z * 0.5f - 0.02f), Quaternion.identity));

            // ── North wall, either side: four tall encyclopaedia cases (A–M left, N–Z right) ──
            float[] encX = { -4.91f, -2.31f, 2.31f, 4.91f };
            for (int i = 0; i < encX.Length; i++)
            {
                bool am = encX[i] < 0f;
                var s = new ShelfSettings
                {
                    Name = $"Encyclopaedia {(am ? "A–M" : "N–Z")} {i % 2 + 1}", Label = "Encyclopaedia case",
                    Shelves = 5, SlotsPerShelf = 36, SlotSize = new Vector3(0.065f, 0.36f, 0.26f),
                    RowSlotTypes = new[] { am ? "enc_am" : "enc_nz" }, RowLabels = new[] { am ? "Encyclopaedia A–M" : "Encyclopaedia N–Z" },
                    FrameMaterial = p.Wood, MarkerMaterial = p.Marker,
                };
                Add(ShelfGenerator.Generate(s, null, new Vector3(encX[i], 0f, halfZ - s.Depth * 0.5f - 0.02f), Quaternion.identity));
            }

            // ── The stacks: six double-sided bookcases, novels A–Z then mysteries, in reading order ──
            string[] stackKinds = { "novel_ac", "novel_df", "novel_gi", "novel_jl", "novel_mo", "novel_pr", "novel_su", "novel_vz",
                                    "mystery_af", "mystery_gl", "mystery_mr", "mystery_sz" };
            string[] stackNames = { "Novels A–C", "Novels D–F", "Novels G–I", "Novels J–L", "Novels M–O", "Novels P–R", "Novels S–U", "Novels V–Z",
                                    "Mysteries A–F", "Mysteries G–L", "Mysteries M–R", "Mysteries S–Z" };
            float[] stackZ = { 4.4f, 2.0f, -0.4f };
            float[] stackX = { -3.4f, 3.4f };
            int side = 0;
            foreach (float sz in stackZ)
            foreach (float sx in stackX)
            {
                foreach (bool facesSouth in new[] { true, false })
                {
                    var s = new ShelfSettings
                    {
                        Name = $"Stack {stackNames[side]}", Label = "Bookcase",
                        Shelves = 5, SlotsPerShelf = 48, SlotSize = new Vector3(0.052f, 0.3f, 0.2f),
                        RowSlotTypes = new[] { stackKinds[side] }, RowLabels = new[] { stackNames[side] },
                        FrameMaterial = p.Wood, MarkerMaterial = p.Marker,
                    };
                    // Back to back, backs 1 cm apart (no z-fighting).
                    float offset = s.Depth * 0.5f + 0.025f;
                    Add(ShelfGenerator.Generate(s, null, new Vector3(sx, 0f, facesSouth ? sz - offset : sz + offset),
                        facesSouth ? Quaternion.identity : facingNorth));
                    side++;
                }
            }

            // ── West and east walls (low, under the windows): non-fiction and poetry by subject number ──
            string[] westKinds = { "nf_cookery", "nf_gardening", "nf_history", "nf_science", "nf_travel" };
            string[] westNames = { "641 Cookery", "635 Gardening", "900 History", "500 Science", "910 Travel" };
            string[] eastKinds = { "nf_art", "nf_music", "nf_nature", "nf_poetry_am", "nf_poetry_nz" };
            string[] eastNames = { "700 Art", "780 Music", "590 Nature", "821 Poetry A–M", "821 Poetry N–Z" };
            for (int wall = 0; wall < 2; wall++)
            {
                string[] kinds = wall == 0 ? westKinds : eastKinds;
                string[] names = wall == 0 ? westNames : eastNames;
                float z = halfZ - 0.9f;
                for (int i = 0; i < kinds.Length; i++)
                {
                    var s = new ShelfSettings
                    {
                        Name = $"Low Shelf {names[i]}", Label = "Low bookshelf",
                        Shelves = 2, SlotsPerShelf = 48, SlotSize = new Vector3(0.052f, 0.3f, 0.21f),
                        RowSlotTypes = new[] { kinds[i] }, RowLabels = new[] { names[i] },
                        FrameMaterial = p.Wood, MarkerMaterial = p.Marker,
                    };
                    float width = s.InnerWidth + 2f * s.PanelThickness;
                    float x = wall == 0 ? -halfX + s.Depth * 0.5f + 0.02f : halfX - s.Depth * 0.5f - 0.02f;
                    Add(ShelfGenerator.Generate(s, null, new Vector3(x, 0f, z - width * 0.5f), wall == 0 ? facingEast : facingWest));
                    z -= width + 0.3f;
                }
            }

            // ── Children's corner: four low book bins, picture books by size ──
            string[] binKinds = { "board_book", "pic_small", "pic_medium", "pic_large" };
            string[] binNames = { "Board books", "Picture books · small", "Picture books · medium", "Picture books · large" };
            float[] binX = { -7.8f, -6.4f, -5.0f, -3.6f };
            for (int i = 0; i < binKinds.Length; i++)
            {
                Add(ShelfGenerator.Generate(new ShelfSettings
                {
                    Name = $"Book Bin {binNames[i]}", Label = "Book bin", Style = ShelfStyle.Crate,
                    Shelves = 1, SlotsPerShelf = 30, SlotsDeep = 2, SlotSize = new Vector3(0.036f, 0.4f, 0.27f),
                    BaseHeight = 0.3f, CrateWallHeight = 0.16f,
                    RowSlotTypes = new[] { binKinds[i] }, RowLabels = new[] { binNames[i] },
                    FrameMaterial = p.Accent, MarkerMaterial = p.Marker,
                }, null, new Vector3(binX[i], 0f, -3.6f), Quaternion.identity));
            }

            var decor = new GameObject("Decor").transform;
            int lamp = 0;
            foreach (float lx in new[] { -6f, -2f, 2f, 6f })
            foreach (float lz in new[] { -4.5f, 0.8f, 5.6f })
            {
                LocationKit.BuildLamp($"Lamp {++lamp}", decor, new Vector3(lx, 0f, lz), WallHeight, p);
            }
            foreach (int wallSide in new[] { -1, 1 })
                Art.ArchitectureKit.Painting(decor, new Vector3(wallSide * 3.6f, 2.0f, -halfZ), Quaternion.Euler(0f, 180f, 0f), new Vector2(1.2f, 0.9f),
                    Art.ArchitectureKit.Picture.Map, wallSide < 0 ? 11 : 12);   // framed city maps, like the reference
            LocationKit.BuildWallSign(decor, "Library Sign", "LIBRARY", new Vector3(0f, 2.55f, halfZ - 0.04f), Quaternion.identity,
                new Vector2(1.6f, 0.36f), p.Wood, new Color(1f, 0.85f, 0.55f), new Color(0.25f, 0.15f, 0.08f));
            LocationKit.BuildFlowers(decor, new Vector3(5.4f, 0.75f, -3.2f));

            // Books land anywhere on the floor, rugs, desk and tables (never on the shelves).
            BoxCollider scatter = LocationKit.BuildScatterArea(new Vector3(0f, 0.6f, 0f), new Vector3(RoomSize.x - 1f, 1.2f, RoomSize.y - 1f));
            SlotGroup[] groupArray = groups.ToArray();
            LocationRig rig = LocationKit.BuildLocationSystems(location, groupArray, new[] { scatter }, RoomSize, p);
            if (standalone) LocationKit.BuildPlayerRig(rig.EntryPosition, 0f, p);

            // 3. At 100%: Ms. Penhallow returns to the desk and the return card glows on it.
            GameObject librarian = LocationKit.BuildNpc("Librarian NPC", new Vector3(-4.5f, 0f, -5.6f), 90f,
                body: new Color(0.5f, 0.32f, 0.5f), accent: new Color(0.95f, 0.78f, 0.35f), skin: new Color(0.85f, 0.66f, 0.52f),
                NpcHeadwear.HairBun, new Color(0.72f, 0.72f, 0.75f));
            Material card = GreyboxAssets.Emissive("Memory_ReturnCard", new Color(0.98f, 0.92f, 0.78f), new Color(1f, 0.78f, 0.45f), 1.6f);
            Material flour = GreyboxAssets.Lit("Memory_FlourPrint", new Color(1f, 1f, 1f));
            GameObject fragment = LocationKit.BuildMemoryFragment(location.Memory,
                "The Library is tidy again! Something glimmers on the returns desk…",
                new Vector3(-3.6f, 0.94f, -6.1f), Quaternion.Euler(0f, -90f, 0f), new Vector3(0.35f, 0.25f, 0.35f), visual =>
                {
                    visual.localPosition = new Vector3(0f, 0.03f, 0f);
                    LocationKit.AddPrimitive(PrimitiveType.Cube, "Card", visual, Vector3.zero, new Vector3(0.13f, 0.006f, 0.19f), card);
                    LocationKit.AddPrimitive(PrimitiveType.Sphere, "Handprint", visual, new Vector3(0.02f, 0.004f, -0.03f), new Vector3(0.035f, 0.004f, 0.045f), flour);
                });
            LocationKit.BuildCompletionStage(rig, librarian, fragment);

            LocationKit.RecordSlotCount(LocationAssetPath, groupArray);
            string path = standalone ? TestScenePath : LocationScenePath;
            LocationKit.SaveScene(scene, path);
            Debug.Log($"[Tidy Up] Built {path}.");
        }

        private static List<string> SortedIds(List<ItemDefinition> items, string slotType, SortDirection direction)
        {
            var matching = new List<ItemDefinition>();
            foreach (ItemDefinition item in items)
            {
                if (System.Array.IndexOf(item.SlotTypes, slotType) >= 0) matching.Add(item);
            }
            var ids = new List<string>();
            foreach (ItemDefinition item in OrderedSortRules.Sort(matching, i => i.SortKey, direction)) ids.Add(item.Id);
            return ids;
        }

        private static string Initial(string itemId, Dictionary<string, ItemDefinition> items)
        {
            string key = items[itemId].SortKey;
            return string.IsNullOrEmpty(key) ? "?" : key.Substring(0, 1).ToUpperInvariant();
        }

        private static void BuildFurniture(Transform room, LocationPalette p)
        {
            // Returns desk by the door (west of the entrance), reading corner (south-east), children's rug (south-west).
            LocationKit.Furniture("Returns Desk", room, new Vector3(-3.6f, 0.45f, -5.6f), new Vector3(0.8f, 0.9f, 2f), p.Accent);
            LocationKit.MakeCleanable(LocationKit.Furniture("Returns Desk Top", room, new Vector3(-3.6f, 0.92f, -5.6f), new Vector3(0.86f, 0.04f, 2.06f), p.Wood), "Returns desk");
            LocationKit.MakeCleanable(LocationKit.Furniture("Reading Table", room, new Vector3(5.6f, 0.375f, -3.2f), new Vector3(1.8f, 0.75f, 0.9f), p.Wood), "Reading table");
            LocationKit.Furniture("Chair N", room, new Vector3(5.6f, 0.23f, -2.45f), new Vector3(0.45f, 0.46f, 0.45f), p.Wood);
            LocationKit.Furniture("Chair S", room, new Vector3(5.6f, 0.23f, -3.95f), new Vector3(0.45f, 0.46f, 0.45f), p.Wood);
            LocationKit.Furniture("Rug", room, new Vector3(5.6f, 0.005f, -3.2f), new Vector3(3.6f, 0.01f, 2.6f),
                GreyboxAssets.Textured("Library_Rug", new Color(0.62f, 0.2f, 0.22f), Art.TextureSet.Fabric, 0.5f, 0.02f));
            LocationKit.Furniture("Kids Rug", room, new Vector3(-5.7f, 0.005f, -4.4f), new Vector3(6f, 0.01f, 2.4f),
                GreyboxAssets.Textured("Library_KidsRug", new Color(0.95f, 0.75f, 0.35f), Art.TextureSet.Fabric, 0.5f, 0.02f));

            Material upholstery = GreyboxAssets.Textured("Library_Upholstery", new Color(0.3f, 0.45f, 0.62f), Art.TextureSet.Fabric, 0.4f, 0.05f);
            LocationKit.Furniture("Armchair Seat", room, new Vector3(8.0f, 0.22f, -5.8f), new Vector3(0.8f, 0.44f, 0.75f), upholstery);
            LocationKit.Furniture("Armchair Back", room, new Vector3(8.35f, 0.6f, -5.8f), new Vector3(0.15f, 0.8f, 0.75f), upholstery);

            Material pot = GreyboxAssets.Lit("Prop_Pot", new Color(0.72f, 0.38f, 0.24f));
            Material leaves = GreyboxAssets.Lit("Prop_Leaves", new Color(0.3f, 0.58f, 0.3f));
            foreach (Vector3 at in new[] { new Vector3(8.4f, 0f, -1.4f), new Vector3(-1.4f, 0f, -6.9f), new Vector3(1.4f, 0f, -6.9f) })
            {
                var plant = new GameObject("Plant").transform;
                plant.SetParent(room, false);
                plant.position = at;
                LocationKit.AddPrimitive(PrimitiveType.Cylinder, "Pot", plant, new Vector3(0f, 0.2f, 0f), new Vector3(0.4f, 0.2f, 0.4f), pot);
                LocationKit.AddPrimitive(PrimitiveType.Sphere, "Leaves", plant, new Vector3(0f, 0.75f, 0f), new Vector3(0.75f, 0.8f, 0.75f), leaves);
            }
        }
    }
}
