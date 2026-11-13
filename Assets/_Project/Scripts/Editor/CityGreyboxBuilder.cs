using System.Collections.Generic;
using TidyUp.Core.Save;
using TidyUp.Locations;
using TidyUp.UI;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TidyUp.Editor
{
    /// <summary>
    /// Tools/Tidy Up/Build Vertical Slice (City + Locations). Builds the three content-only location scenes,
    /// registers them as Addressables (group "Locations"), then builds the persistent City scene: one cozy
    /// street with the Bakery, Library and Fantasy shop fronts, the player, HUD and the LocationLoader.
    /// City.unity becomes the only scene in Build Settings. Safe to re-run.
    /// </summary>
    public static class CityGreyboxBuilder
    {
        public const string CityScenePath = "Assets/_Project/Scenes/City.unity";
        private const string AddressablesGroup = "Locations";

        /// <summary>Street centre. Far from the origin, where location interiors load.</summary>
        private static readonly Vector3 StreetCentre = new Vector3(0f, 0f, -50f);

        private struct Shop
        {
            public string AssetPath;
            public string ScenePath;
            public string Address;
            public float X;
            public Color Wall;
            public Color Awning;
            public string Sign;
            /// <summary>A mega location: a grand building closing the west end of the street instead of a shopfront.</summary>
            public bool Grand;
        }

        private static readonly Shop[] Shops =
        {
            new Shop { AssetPath = BakeryGreyboxBuilder.LocationAssetPath, ScenePath = BakeryGreyboxBuilder.LocationScenePath, Address = BakeryGreyboxBuilder.LocationAddress,
                X = -11f, Wall = new Color(0.97f, 0.86f, 0.64f), Awning = new Color(0.2f, 0.55f, 0.56f), Sign = "BAKERY", Grand = true },
            new Shop { AssetPath = LibraryGreyboxBuilder.LocationAssetPath, ScenePath = LibraryGreyboxBuilder.LocationScenePath, Address = LibraryGreyboxBuilder.LocationAddress,
                X = 0f, Wall = new Color(0.85f, 0.78f, 0.66f), Awning = new Color(0.24f, 0.4f, 0.3f), Sign = "LIBRARY" },
            new Shop { AssetPath = FantasyShopGreyboxBuilder.LocationAssetPath, ScenePath = FantasyShopGreyboxBuilder.LocationScenePath, Address = FantasyShopGreyboxBuilder.LocationAddress,
                X = 11f, Wall = new Color(0.78f, 0.68f, 0.88f), Awning = new Color(0.35f, 0.2f, 0.55f), Sign = "CURIOSITIES" },
        };

        [MenuItem("Tools/Tidy Up/Build Vertical Slice (City + Locations)")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            // 1. Location scenes (content only) and their Addressables entries.
            BakeryGreyboxBuilder.BuildLocationScene();
            LibraryGreyboxBuilder.BuildLocationScene();
            FantasyShopGreyboxBuilder.BuildLocationScene();
            RegisterAddressables();

            LocationPalette p = LocationPalette.Create("City",
                floor: new Color(0.62f, 0.58f, 0.53f), wall: new Color(0.9f, 0.82f, 0.7f),
                wood: new Color(0.45f, 0.3f, 0.2f), accent: new Color(0.3f, 0.5f, 0.35f));
            AssetDatabase.SaveAssets();

            // 2. City scene
            Scene scene = LocationKit.NewScene(withSun: true);
            var street = new GameObject("Street").transform; // hidden while the player is inside a location
            BuildGround(street);
            var locations = new List<LocationDefinition>();
            foreach (Shop shop in Shops)
            {
                LocationDefinition location = AssetDatabase.LoadAssetAtPath<LocationDefinition>(shop.AssetPath);
                locations.Add(location);
                if (shop.Grand) BuildGrandEntrance(street, shop, location, p);
                else BuildShopfront(street, shop, location, p);
            }
            BuildClockTower(street, p);
            BuildStreetFurniture(street, p);
            StreetBuilder.Build(street, StreetCentre);   // the rest of the town: both rows, street ends, skyline, life
            StreetBuilder.BuildMood(street);             // golden late afternoon (interiors swap in their own mood)
            BuildBounds(street);

            PlayerRig player = LocationKit.BuildPlayerRig(StreetCentre + new Vector3(0f, 0.05f, -2f), 0f, p);
            CanvasGroup fade = BuildFade();
            var citySystems = new GameObject("City Systems");
            LocationLoader loader = citySystems.AddComponent<LocationLoader>();
            loader.EditorConfigure(fade, street.gameObject, locations.ToArray());
            SaveService saves = citySystems.AddComponent<SaveService>();
            saves.EditorConfigure(player.Memories, loader, MainMenuBuilder.SceneName);
            player.Menu.EditorConfigure(MenuMode.Pause, string.Empty, saves, player.Input);

            LocationKit.SaveScene(scene, CityScenePath);

            // 3. Main menu (first scene in the build), then reopen the City for editing.
            MainMenuBuilder.BuildScene();
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainMenuBuilder.ScenePath, true),
                new EditorBuildSettingsScene(CityScenePath, true),
            };
            EditorSceneManager.OpenScene(CityScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Tidy Up] Built {MainMenuBuilder.ScenePath} and {CityScenePath} with {Shops.Length} locations. Play MainMenu.unity to start.");
        }

        private static void RegisterAddressables()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            AddressableAssetGroup group = settings.FindGroup(AddressablesGroup);
            if (group == null)
            {
                group = settings.CreateGroup(AddressablesGroup, false, false, true, null,
                    typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            }
            foreach (Shop shop in Shops)
            {
                string guid = AssetDatabase.AssetPathToGUID(shop.ScenePath);
                AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group, false, false);
                entry.SetAddress(shop.Address, false);
            }
            // The mega Bakery's rooms stream in as their own scenes.
            foreach ((string scenePath, string address) in MegaBakeryBuilder.ZoneScenes)
            {
                AddressableAssetEntry zone = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(scenePath), group, false, false);
                zone.SetAddress(address, false);
            }
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, null, true);
            AssetDatabase.SaveAssets();
        }

        // ───────────── Street ─────────────

        private static void BuildGround(Transform street)
        {
            Material cobbles = GreyboxAssets.Textured("City_Cobbles", Core.Palette.WarmStone, Art.TextureSet.Cobbles, 2.2f, 0.2f, 1.2f);
            Material pavement = GreyboxAssets.Textured("City_Pavement", new Color(0.85f, 0.8f, 0.72f), Art.TextureSet.Tiles, 2f, 0.15f);
            Material grass = GreyboxAssets.Lit("City_Grass", new Color(0.45f, 0.65f, 0.35f));
            LocationKit.Furniture("Cobbles", street, StreetCentre + new Vector3(0f, -0.1f, 0f), new Vector3(46f, 0.2f, 18f), cobbles);
            LocationKit.Furniture("Pavement N", street, StreetCentre + new Vector3(0f, 0.01f, 6.5f), new Vector3(46f, 0.02f, 3f), pavement);
            LocationKit.Furniture("Pavement S", street, StreetCentre + new Vector3(0f, 0.01f, -6.5f), new Vector3(46f, 0.02f, 3f), pavement);
            LocationKit.Furniture("Green", street, StreetCentre + new Vector3(0f, 0.015f, -8.6f), new Vector3(46f, 0.02f, 1.2f), grass);
        }

        /// <summary>
        /// A mega location's entrance: the grand building (StreetBuilder) closing the west end of the street, its
        /// arched door made enterable. The return point is at the foot of the steps, facing back down the street.
        /// </summary>
        private static void BuildGrandEntrance(Transform street, Shop shop, LocationDefinition location, LocationPalette p)
        {
            Transform door = StreetBuilder.BuildGrandBakery(street, StreetCentre, StreetCentre + new Vector3(-40f, 0f, 0f),
                Quaternion.Euler(0f, -90f, 0f), shop.Wall, shop.Awning);
            BoxCollider target = door.gameObject.AddComponent<BoxCollider>();
            target.isTrigger = true;
            target.center = new Vector3(0f, 1.6f, -0.6f); // reaches out past the street bound, over the top step
            target.size = new Vector3(3.4f, 3.2f, 1.0f);
            var returnPoint = new GameObject("Return Point").transform;
            returnPoint.SetParent(door, false);
            returnPoint.localPosition = new Vector3(0f, 0.05f, -2.6f);
            returnPoint.localRotation = Quaternion.Euler(0f, 180f, 0f);
            door.gameObject.AddComponent<LocationDoor>().EditorConfigure(location, returnPoint);
        }

        /// <summary>A two-storey shop building facing the street, with an enterable door, windows, awning and sign.</summary>
        private static void BuildShopfront(Transform street, Shop shop, LocationDefinition location, LocationPalette p)
        {
            const float frontZ = 8f; // building front, relative to the street centre
            const float width = 9f;
            const float height = 5.5f;
            const float depth = 6f;
            var root = new GameObject($"Shopfront {location.DisplayName}").transform;
            root.SetParent(street, false);
            root.position = StreetCentre + new Vector3(shop.X, 0f, frontZ);

            Material wall = GreyboxAssets.Textured($"City_Wall_{location.Id}", shop.Wall, Art.TextureSet.Plaster, 2.5f, 0.05f, 0.7f);
            Material awning = GreyboxAssets.Lit($"City_Awning_{location.Id}", shop.Awning);
            Material window = GreyboxAssets.Emissive("City_Window_Warm", new Color(0.95f, 0.78f, 0.55f), new Color(1f, 0.72f, 0.42f), 0.7f); // lamplight inside
            Material doorGlow = GreyboxAssets.Emissive("Door_Frame_Glow", new Color(1f, 0.85f, 0.55f), new Color(1f, 0.75f, 0.4f), 1.2f);

            LocationKit.Furniture("Building", root, new Vector3(0f, height * 0.5f, depth * 0.5f), new Vector3(width, height, depth), wall);
            // Roof: pitched along X, overhanging the front.
            const float pitch = 30f;
            float half = depth * 0.5f + 0.4f;
            float rise = half * Mathf.Tan(pitch * Mathf.Deg2Rad);
            var roofSize = new Vector3(width + 0.6f, 0.15f, half / Mathf.Cos(pitch * Mathf.Deg2Rad));
            GameObject front = LocationKit.Furniture("Roof Front", root, new Vector3(0f, height + rise * 0.5f, depth * 0.5f - half * 0.5f), roofSize, p.Roof);
            front.transform.localRotation = Quaternion.Euler(-pitch, 0f, 0f);
            GameObject back = LocationKit.Furniture("Roof Back", root, new Vector3(0f, height + rise * 0.5f, depth * 0.5f + half * 0.5f), roofSize, p.Roof);
            back.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            // A carved cornice under the eaves and a lantern either side of the door, like the neighbours.
            Art.ArchitectureKit.CrownRun(root, root.position + new Vector3(0f, height - 0.24f, 0f), Quaternion.identity, width, p.Trim);
            foreach (int side in new[] { -1, 1 }) Art.ArchitectureKit.Sconce(root, root.position + new Vector3(side * 1.05f, 2.0f, 0f), Quaternion.identity);

            // Shop windows (glowing warm: someone's home) and an awning over them.
            for (int side = -1; side <= 1; side += 2)
            {
                FacadeWindow(root, side < 0 ? "Window L" : "Window R", new Vector3(side * 2.7f, 1.6f, 0f), new Vector2(2.1f, 1.4f), p, window);
                LocationKit.BuildFlowers(root, root.position + new Vector3(side * 2.7f, 0.89f, -0.1f)); // on the window's sill
                GameObject shade = LocationKit.AddPrimitive(PrimitiveType.Cube, "Awning", root, new Vector3(side * 2.7f, 2.65f, -0.45f), new Vector3(2.6f, 0.06f, 1f), awning);
                shade.transform.localRotation = Quaternion.Euler(-18f, 0f, 0f);
            }
            // Upper-floor windows.
            for (int i = -1; i <= 1; i++)
            {
                FacadeWindow(root, "Upper Window", new Vector3(i * 2.8f, 4.1f, 0f), new Vector2(0.95f, 1.1f), p, window);
            }

            LocationKit.BuildWallSign(root, $"{location.DisplayName} Sign", shop.Sign, root.position + new Vector3(0f, 3.15f, -0.08f),
                Quaternion.identity, new Vector2(2.6f, 0.45f), p.Wood, Color.Lerp(shop.Wall, Color.white, 0.5f), new Color(0.25f, 0.15f, 0.08f));

            // The door: the way in. The return point is on the pavement, facing the street.
            var door = new GameObject("Door").transform;
            door.SetParent(root, false);
            door.localPosition = new Vector3(0f, 0f, -0.05f);
            LocationKit.AddPrimitive(PrimitiveType.Cube, "Door Leaf", door, new Vector3(0f, 1.15f, 0f), new Vector3(1.3f, 2.3f, 0.08f), p.Wood);
            LocationKit.AddPrimitive(PrimitiveType.Cube, "Frame Top", door, new Vector3(0f, 2.35f, -0.02f), new Vector3(1.5f, 0.1f, 0.1f), doorGlow);
            LocationKit.AddPrimitive(PrimitiveType.Cube, "Frame L", door, new Vector3(-0.7f, 1.15f, -0.02f), new Vector3(0.1f, 2.3f, 0.1f), doorGlow);
            LocationKit.AddPrimitive(PrimitiveType.Cube, "Frame R", door, new Vector3(0.7f, 1.15f, -0.02f), new Vector3(0.1f, 2.3f, 0.1f), doorGlow);
            LocationKit.AddPrimitive(PrimitiveType.Sphere, "Knob", door, new Vector3(0.45f, 1.1f, -0.06f), Vector3.one * 0.08f, doorGlow);
            BoxCollider target = door.gameObject.AddComponent<BoxCollider>();
            target.isTrigger = true;
            target.center = new Vector3(0f, 1.2f, -0.2f);
            target.size = new Vector3(1.5f, 2.4f, 0.5f);

            var returnPoint = new GameObject("Return Point").transform;
            returnPoint.SetParent(root, false);
            returnPoint.localPosition = new Vector3(0f, 0.05f, -2f);
            returnPoint.localRotation = Quaternion.Euler(0f, 180f, 0f);
            door.gameObject.AddComponent<LocationDoor>().EditorConfigure(location, returnPoint);
        }

        /// <summary>The clock on the square, the one that struck thirteen. West end of the street.</summary>
        private static void BuildClockTower(Transform street, LocationPalette p)
        {
            Material stone = GreyboxAssets.Textured("City_Stone", Core.Palette.WarmStone, Art.TextureSet.Stone, 1.6f, 0.1f);
            Material face = GreyboxAssets.Emissive("City_ClockFace", new Color(1f, 0.97f, 0.88f), new Color(1f, 0.9f, 0.7f), 1f);
            Material hands = GreyboxAssets.Lit("City_ClockHands", new Color(0.12f, 0.1f, 0.1f));
            var tower = new GameObject("Clock Tower").transform;
            tower.SetParent(street, false);
            tower.position = StreetCentre + new Vector3(-28f, 0f, 5.4f); // at the side of the square: the street looks straight at the Grand Bakery
            LocationKit.Furniture("Tower", tower, new Vector3(0f, 5f, 0f), new Vector3(3f, 10f, 3f), stone);
            GameObject cap = LocationKit.Furniture("Spire", tower, new Vector3(0f, 10.8f, 0f), new Vector3(2.2f, 1.6f, 2.2f), p.Roof);
            cap.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            // Face toward the street (+X).
            GameObject dial = LocationKit.AddPrimitive(PrimitiveType.Cylinder, "Clock Face", tower, new Vector3(1.52f, 8f, 0f), new Vector3(1.8f, 0.03f, 1.8f), face);
            dial.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            // Hands at "one o'clock past midnight", or thirteen.
            GameObject hour = LocationKit.AddPrimitive(PrimitiveType.Cube, "Hour Hand", tower, new Vector3(1.56f, 8.22f, 0.12f), new Vector3(0.03f, 0.5f, 0.08f), hands);
            hour.transform.localRotation = Quaternion.Euler(-30f, 0f, 0f);
            LocationKit.AddPrimitive(PrimitiveType.Cube, "Minute Hand", tower, new Vector3(1.56f, 8.35f, 0f), new Vector3(0.03f, 0.75f, 0.05f), hands);
        }

        private static void BuildStreetFurniture(Transform street, LocationPalette p)
        {
            Material iron = GreyboxAssets.Lit("City_LampIron", new Color(0.18f, 0.22f, 0.2f));
            Material glow = GreyboxAssets.Emissive("City_LampGlow", new Color(1f, 0.92f, 0.75f), new Color(1f, 0.78f, 0.5f), 3f);
            Material trunk = GreyboxAssets.Lit("City_Trunk", new Color(0.4f, 0.28f, 0.18f));
            Material leaves = GreyboxAssets.Lit("City_Leaves", new Color(0.35f, 0.58f, 0.3f));
            Material bench = GreyboxAssets.Textured("City_Bench", new Color(0.55f, 0.36f, 0.22f), Art.TextureSet.Wood, 1f, 0.3f);
            var furniture = new GameObject("Street Furniture").transform;
            furniture.SetParent(street, false);

            // Lampposts along the south pavement, always lit (cozy, never dark).
            for (int i = 0; i < 5; i++)
            {
                var lamp = new GameObject($"Lamppost {i + 1}").transform;
                lamp.SetParent(furniture, false);
                lamp.position = StreetCentre + new Vector3(-16f + i * 8f, 0f, -5.4f);
                LocationKit.AddPrimitive(PrimitiveType.Cylinder, "Post", lamp, new Vector3(0f, 1.6f, 0f), new Vector3(0.12f, 1.6f, 0.12f), iron);
                LocationKit.AddPrimitive(PrimitiveType.Cube, "Lantern", lamp, new Vector3(0f, 3.35f, 0f), new Vector3(0.35f, 0.45f, 0.35f), glow);
                LocationKit.AddPrimitive(PrimitiveType.Cylinder, "Cap", lamp, new Vector3(0f, 3.62f, 0f), new Vector3(0.45f, 0.05f, 0.45f), iron);
                CapsuleCollider pole = lamp.gameObject.AddComponent<CapsuleCollider>();
                pole.center = new Vector3(0f, 1.6f, 0f);
                pole.radius = 0.12f;
                pole.height = 3.2f;
                var lightGo = new GameObject("Light");
                lightGo.transform.SetParent(lamp, false);
                lightGo.transform.localPosition = new Vector3(0f, 3.2f, 0f);
                Light light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.82f, 0.6f);
                light.intensity = 2f;
                light.range = 8f;
                light.shadows = LightShadows.None;
            }

            // Benches against the houses opposite (the trees now stand in planters, see StreetBuilder).
            for (int i = 0; i < 4; i++)
            {
                float x = -12f + i * 8f;
                var seat = new GameObject($"Bench {i + 1}").transform;
                seat.SetParent(furniture, false);
                seat.position = StreetCentre + new Vector3(x + 2.2f, 0f, -7f);
                LocationKit.Furniture("Seat", seat, new Vector3(0f, 0.45f, 0f), new Vector3(1.8f, 0.08f, 0.5f), bench);
                LocationKit.Furniture("Back", seat, new Vector3(0f, 0.75f, -0.22f), new Vector3(1.8f, 0.5f, 0.06f), bench);
                LocationKit.Furniture("Leg L", seat, new Vector3(-0.8f, 0.22f, 0f), new Vector3(0.08f, 0.44f, 0.45f), iron);
                LocationKit.Furniture("Leg R", seat, new Vector3(0.8f, 0.22f, 0f), new Vector3(0.08f, 0.44f, 0.45f), iron);
            }
        }

        /// <summary>A framed facade window (frame, mullions, sill) with warm lamplight behind the glass.</summary>
        private static void FacadeWindow(Transform building, string name, Vector3 localPosition, Vector2 size, LocationPalette p, Material glass)
        {
            var window = new GameObject(name).transform;
            window.SetParent(building, false);
            window.localPosition = localPosition;
            LocationKit.BuildWindow(window, size, p, glass);
        }

        /// <summary>Invisible walls so the player stays on the street (the rest of the city comes later).</summary>
        private static void BuildBounds(Transform street)
        {
            var bounds = new GameObject("Street Bounds").transform;
            bounds.SetParent(street, false);
            // The street runs on west into the square in front of the Grand Bakery (x -29.5 .. -37.75, z ±15).
            AddWall(bounds, "North", StreetCentre + new Vector3(-3.25f, 2f, 7.95f), new Vector3(52.5f, 4f, 0.1f));
            AddWall(bounds, "South", StreetCentre + new Vector3(-3.25f, 2f, -8.05f), new Vector3(52.5f, 4f, 0.1f));
            AddWall(bounds, "Square North", StreetCentre + new Vector3(-33.6f, 2f, 15f), new Vector3(8.3f, 4f, 0.1f));
            AddWall(bounds, "Square South", StreetCentre + new Vector3(-33.6f, 2f, -15f), new Vector3(8.3f, 4f, 0.1f));
            AddWall(bounds, "Square NE", StreetCentre + new Vector3(-29.5f, 2f, 11.5f), new Vector3(0.1f, 4f, 7f));
            AddWall(bounds, "Square SE", StreetCentre + new Vector3(-29.5f, 2f, -11.5f), new Vector3(0.1f, 4f, 7f));
            AddWall(bounds, "West", StreetCentre + new Vector3(-37.75f, 2f, 0f), new Vector3(0.1f, 4f, 30f));
            AddWall(bounds, "East", StreetCentre + new Vector3(23f, 2f, 0f), new Vector3(0.2f, 4f, 18f));
        }

        private static void AddWall(Transform parent, string name, Vector3 position, Vector3 size)
        {
            var go = new GameObject($"Bound {name}");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.AddComponent<BoxCollider>().size = size;
        }

        /// <summary>Full-screen fade used by the LocationLoader to hide scene swaps.</summary>
        private static CanvasGroup BuildFade()
        {
            var canvasGo = new GameObject("Scene Fade");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            CanvasGroup group = canvasGo.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            var image = new GameObject("Black", typeof(RectTransform), typeof(Image));
            image.transform.SetParent(canvasGo.transform, false);
            var rect = (RectTransform)image.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            image.GetComponent<Image>().color = new Color(0.08f, 0.06f, 0.05f);
            image.GetComponent<Image>().raycastTarget = false;
            return group;
        }
    }
}
