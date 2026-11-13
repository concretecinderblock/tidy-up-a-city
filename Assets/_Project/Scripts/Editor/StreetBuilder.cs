using System.Collections.Generic;
using System.IO;
using TidyUp.Ambience;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TidyUp.Editor
{
    /// <summary>
    /// Makes the street part of a living town (called by CityGreyboxBuilder): townhouses filling both sides,
    /// the street running on past an archway and a square, a skyline of roofs and spires in the haze, bunting
    /// and string lights, a café terrace, a market stall, a fountain, planters, laundry in the alleys, pigeons,
    /// chimney smoke, drifting leaves, strolling townsfolk, and a golden late-afternoon light. All generated.
    /// Coordinates are relative to the street centre: X along the street, the shops' fronts at Z = +8, the
    /// opposite row's fronts at Z = -8.
    /// </summary>
    public static class StreetBuilder
    {
        private const float NorthZ = 8f, SouthZ = -8f, HalfLength = 23f;
        private static Vector3 _c;
        private static System.Random _random;

        // Materials (made once per build).
        private static Material _stone, _wood, _roof, _window, _glassCool, _iron, _leaves, _trunk, _water, _smoke;

        public static void Build(Transform street, Vector3 centre)
        {
            Init(centre);
            BuildTown(street);
        }

        private static void Init(Vector3 centre)
        {
            _c = centre;
            _random = new System.Random(2026);
            _stone = GreyboxAssets.Textured("Street_Stone", new Color(0.86f, 0.8f, 0.7f), Art.TextureSet.Stone, 1.4f, 0.1f);
            _wood = GreyboxAssets.Textured("Street_Wood", new Color(0.45f, 0.29f, 0.18f), Art.TextureSet.Wood, 1.2f, 0.45f);
            _roof = GreyboxAssets.Textured("Greybox_RoofTiles", Core.Palette.Terracotta, Art.TextureSet.RoofTiles, 2f, 0.2f);
            _window = GreyboxAssets.Emissive("City_Window_Warm", new Color(0.95f, 0.78f, 0.55f), new Color(1f, 0.72f, 0.42f), 0.7f);
            _glassCool = GreyboxAssets.Emissive("Street_Window_Evening", new Color(0.55f, 0.62f, 0.72f), new Color(0.9f, 0.7f, 0.45f), 0.35f);
            _iron = GreyboxAssets.Lit("City_LampIron", new Color(0.18f, 0.22f, 0.2f));
            _leaves = GreyboxAssets.Lit("City_Leaves", new Color(0.35f, 0.58f, 0.3f));
            _trunk = GreyboxAssets.Lit("City_Trunk", new Color(0.4f, 0.28f, 0.18f));
            _water = Water();
            _smoke = SoftParticle("Street_Smoke", new Color(0.9f, 0.88f, 0.84f, 0.35f));
        }

        private static void BuildTown(Transform street)
        {

            Transform town = new GameObject("Town").transform;
            town.SetParent(street, false);

            BuildNorthInfill(town);
            BuildSouthRow(town);
            BuildStreetEnds(town);
            BuildSkyline(town);
            BuildBunting(town);
            BuildSquare(town);
            BuildPlanters(town);
            BuildLife(town);
        }

        // ───────────── Light and atmosphere ─────────────

        /// <summary>Golden late afternoon for the street: low warm sun, warm ambient, haze down the street, the grade.</summary>
        public static void BuildMood(Transform parent)
        {
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                if (light.type != LightType.Directional) continue;
                light.transform.rotation = Quaternion.Euler(32f, -55f, 0f);
                light.color = new Color(1f, 0.84f, 0.62f);
                light.intensity = 1.5f;
                light.shadows = LightShadows.Soft;
                light.shadowStrength = 0.75f;
            }
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.78f, 0.8f, 0.86f);
            RenderSettings.ambientEquatorColor = new Color(0.82f, 0.74f, 0.64f);
            RenderSettings.ambientGroundColor = new Color(0.5f, 0.42f, 0.34f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.93f, 0.82f, 0.66f);
            RenderSettings.fogDensity = 0.016f;
            Art.MoodAssets.OutdoorVolume(parent, Art.MoodPreset.Street);
        }

        // ───────────── Townhouses ─────────────

        private enum Ground { House, Cafe, PostOffice, Flowers, Plain }

        private sealed class House
        {
            public float X, Width;
            public int Storeys;
            public Color Wall, Shutters;
            public Ground Ground;
            public string Sign;
        }

        /// <summary>The gaps beside the shops: townhouses at both ends of the north row, laundry across the alleys.</summary>
        private static void BuildNorthInfill(Transform town)
        {
            Facade(town, NorthZ, false, new House { X = -19.25f, Width = 7.5f, Storeys = 3, Wall = new Color(0.86f, 0.74f, 0.62f), Shutters = new Color(0.3f, 0.45f, 0.36f), Ground = Ground.House });
            // Where the little bakery used to be: a townhouse (the Bakery is now the Grand Bakery at the west end).
            Facade(town, NorthZ, false, new House { X = -11f, Width = 9f, Storeys = 3, Wall = new Color(0.97f, 0.86f, 0.64f), Shutters = new Color(0.2f, 0.5f, 0.5f), Ground = Ground.House });
            Facade(town, NorthZ, false, new House { X = 19.25f, Width = 7.5f, Storeys = 4, Wall = new Color(0.8f, 0.84f, 0.88f), Shutters = new Color(0.55f, 0.3f, 0.25f), Ground = Ground.House });
            foreach (float x in new[] { -5.5f, 5.5f })
            {
                // Alley: a dark back wall, a lamp, and washing on a line between the buildings.
                Box("Alley Back", town, _c + new Vector3(x, 3f, NorthZ + 6f), new Vector3(2.2f, 6f, 0.3f), _stone, collider: true);
                Laundry(town, new Vector3(x, 4.2f, NorthZ + 1.5f), 2f);
                Laundry(town, new Vector3(x, 3.4f, NorthZ + 3.5f), 2f);
                Crates(town, _c + new Vector3(x + 0.4f, 0f, NorthZ + 0.6f));
            }
        }

        /// <summary>The opposite side of the street: a row of townhouses with a café, the post office and a florist.</summary>
        private static void BuildSouthRow(Transform town)
        {
            var row = new[]
            {
                new House { X = -19.5f, Width = 7f, Storeys = 3, Wall = new Color(0.95f, 0.8f, 0.62f), Shutters = new Color(0.28f, 0.42f, 0.55f), Ground = Ground.Cafe, Sign = "CAFÉ" },
                new House { X = -12.5f, Width = 7f, Storeys = 4, Wall = new Color(0.74f, 0.82f, 0.86f), Shutters = new Color(0.9f, 0.88f, 0.8f), Ground = Ground.House },
                new House { X = -5.5f, Width = 7f, Storeys = 3, Wall = new Color(0.95f, 0.88f, 0.72f), Shutters = new Color(0.62f, 0.18f, 0.16f), Ground = Ground.PostOffice, Sign = "POST" },
                new House { X = 1.5f, Width = 7f, Storeys = 4, Wall = new Color(0.86f, 0.64f, 0.56f), Shutters = new Color(0.25f, 0.35f, 0.3f), Ground = Ground.House },
                new House { X = 8.5f, Width = 7f, Storeys = 3, Wall = new Color(0.8f, 0.88f, 0.74f), Shutters = new Color(0.95f, 0.92f, 0.85f), Ground = Ground.Flowers, Sign = "FLOWERS" },
                new House { X = 15.5f, Width = 7f, Storeys = 4, Wall = new Color(0.96f, 0.84f, 0.7f), Shutters = new Color(0.45f, 0.3f, 0.5f), Ground = Ground.House },
                new House { X = 21f, Width = 4f, Storeys = 3, Wall = new Color(0.88f, 0.76f, 0.7f), Shutters = new Color(0.3f, 0.4f, 0.45f), Ground = Ground.Plain },
            };
            foreach (House h in row) Facade(town, SouthZ, true, h);
        }

        /// <summary>
        /// A townhouse facing the street: plinth, string courses, cornice, pitched roof with a chimney (smoking now
        /// and then), shuttered windows with flower boxes, a door with a lantern, and a ground floor by type.
        /// </summary>
        private static Transform Facade(Transform town, float frontZ, bool southSide, House h)
        {
            var root = new GameObject($"House {h.Ground} {h.X:0}").transform;
            root.SetParent(town, false);
            root.position = _c + new Vector3(h.X, 0f, frontZ);
            root.rotation = southSide ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity; // local -Z faces the street
            const float groundH = 3.4f, storeyH = 3f, depth = 8f;
            float height = groundH + (h.Storeys - 1) * storeyH;
            Material wall = GreyboxAssets.Textured($"Street_Wall_{ColorUtility.ToHtmlStringRGB(h.Wall)}", h.Wall, Art.TextureSet.Plaster, 2.5f, 0.05f, 0.7f);
            Material shutter = GreyboxAssets.Textured($"Street_Shutter_{ColorUtility.ToHtmlStringRGB(h.Shutters)}", h.Shutters, Art.TextureSet.Wood, 0.8f, 0.35f);

            Box("Building", root, new Vector3(0f, height * 0.5f, depth * 0.5f), new Vector3(h.Width, height, depth), wall, collider: true);
            Box("Plinth", root, new Vector3(0f, 0.3f, -0.03f), new Vector3(h.Width, 0.6f, 0.08f), _stone);
            for (int s = 1; s < h.Storeys; s++)
                Box("String Course", root, new Vector3(0f, groundH + (s - 1) * storeyH - 0.05f, -0.04f), new Vector3(h.Width, 0.12f, 0.1f), _stone);
            Art.ArchitectureKit.CrownRun(root, root.TransformPoint(new Vector3(0f, height - 0.24f, 0f)), root.rotation, h.Width, _stone);
            Roof(root, h.Width, height, depth);
            if (h.Width >= 5f) Chimney(root, new Vector3(h.Width * 0.3f * (_random.NextDouble() < 0.5 ? -1f : 1f), height, depth * 0.55f), smoke: _random.NextDouble() < 0.6);

            // Upper floors: shuttered windows, flower boxes on some, a little balcony now and then.
            int perFloor = Mathf.Max(1, Mathf.FloorToInt(h.Width / 2.3f));
            for (int s = 1; s < h.Storeys; s++)
            {
                float y = groundH + (s - 1) * storeyH + 1.45f;
                bool balcony = s == 1 && h.Width >= 6f && _random.NextDouble() < 0.4;
                for (int i = 0; i < perFloor; i++)
                {
                    float x = Mathf.Lerp(-h.Width * 0.5f, h.Width * 0.5f, (i + 0.5f) / perFloor);
                    Window(root, new Vector3(x, y, 0f), new Vector2(0.9f, 1.35f), shutter, lit: _random.NextDouble() < 0.55);
                    if (!balcony && _random.NextDouble() < 0.6) FlowerBox(root, new Vector3(x, y - 0.75f, -0.18f), 1f);
                }
                if (balcony) Balcony(root, new Vector3(0f, y - 0.8f, 0f), h.Width - 1.2f);
            }

            // Ground floor.
            switch (h.Ground)
            {
                case Ground.Cafe:
                case Ground.PostOffice:
                case Ground.Flowers:
                    ShopGround(root, h, shutter);
                    break;
                case Ground.Plain:
                    Window(root, new Vector3(0f, 1.7f, 0f), new Vector2(0.9f, 1.3f), shutter, lit: true);
                    break;
                default:
                    Door(root, new Vector3(-h.Width * 0.25f, 0f, 0f));
                    Window(root, new Vector3(h.Width * 0.22f, 1.6f, 0f), new Vector2(1.1f, 1.3f), shutter, lit: true);
                    FlowerBox(root, new Vector3(h.Width * 0.22f, 0.82f, -0.18f), 1.2f);
                    break;
            }
            return root;
        }

        private static void ShopGround(Transform root, House h, Material shutter)
        {
            Color awningColor = h.Ground == Ground.Cafe ? new Color(0.75f, 0.2f, 0.18f) : h.Ground == Ground.Flowers ? new Color(0.3f, 0.55f, 0.35f) : new Color(0.2f, 0.32f, 0.55f);
            Door(root, new Vector3(-h.Width * 0.32f, 0f, 0f));
            // Wide shop window with a striped awning.
            var glass = Box("Shop Window", root, new Vector3(h.Width * 0.12f, 1.5f, -0.02f), new Vector3(h.Width * 0.55f, 1.6f, 0.04f), _window);
            Frame(root, new Vector3(h.Width * 0.12f, 1.5f, 0f), new Vector2(h.Width * 0.55f, 1.6f));
            GameObject awning = Box("Awning", root, new Vector3(0f, 2.85f, -0.75f), new Vector3(h.Width - 0.6f, 0.05f, 1.6f), Stripes(awningColor));
            awning.transform.localRotation = Quaternion.Euler(-16f, 0f, 0f);
            Box("Awning Valance", root, new Vector3(0f, 2.62f, -1.52f), new Vector3(h.Width - 0.6f, 0.22f, 0.03f), Stripes(awningColor));
            // A sign over the door on the wall, and a hanging blade sign.
            LocationKit.BuildWallSign(root, $"{h.Sign} Sign", h.Sign, root.TransformPoint(new Vector3(0f, 3.15f, -0.08f)), root.rotation,
                new Vector2(2.2f, 0.4f), _wood, Color.Lerp(h.Wall, Color.white, 0.5f), new Color(0.25f, 0.15f, 0.08f));
            BladeSign(root, new Vector3(h.Width * 0.5f - 0.3f, 3.0f, -0.6f), awningColor);

            Transform props = new GameObject("Shop Props").transform;
            props.SetParent(root, false);
            if (h.Ground == Ground.Cafe)
            {
                // A terrace: three tables with chairs and parasols on the pavement.
                for (int i = 0; i < 3; i++) CafeTable(props, new Vector3(-h.Width * 0.3f + i * 2.2f, 0f, -2f), awningColor);
            }
            else if (h.Ground == Ground.Flowers)
            {
                // Buckets of flowers along the shopfront.
                for (int i = 0; i < 5; i++)
                {
                    Vector3 at = new Vector3(-0.5f + i * 0.55f, 0f, -0.45f);
                    Box("Bucket", props, at + new Vector3(0f, 0.18f, 0f), new Vector3(0.36f, 0.36f, 0.36f), _iron);
                    GameObject flowers = LocationKit.BuildFlowers(props, props.TransformPoint(at + new Vector3(0f, 0.36f, 0f)));
                    flowers.transform.localScale = Vector3.one * 1.3f;
                }
            }
            else
            {
                // A red postbox by the door (the City Post, coming later).
                Material red = GreyboxAssets.Lit("Street_PostRed", new Color(0.75f, 0.12f, 0.1f));
                GameObject box = LocationKit.AddPrimitive(PrimitiveType.Cylinder, "Postbox", props, new Vector3(-h.Width * 0.5f + 0.5f, 0.6f, -0.6f), new Vector3(0.45f, 0.6f, 0.45f), red);
                LocationKit.AddPrimitive(PrimitiveType.Sphere, "Postbox Cap", props, new Vector3(-h.Width * 0.5f + 0.5f, 1.2f, -0.6f), new Vector3(0.48f, 0.2f, 0.48f), red);
                Box("Slot", props, new Vector3(-h.Width * 0.5f + 0.5f, 1.0f, -0.83f), new Vector3(0.22f, 0.04f, 0.02f), _iron);
            }
        }

        private static void Roof(Transform root, float width, float height, float depth)
        {
            const float pitch = 32f;
            float half = depth * 0.5f + 0.4f;
            float rise = half * Mathf.Tan(pitch * Mathf.Deg2Rad);
            var size = new Vector3(width + 0.4f, 0.15f, half / Mathf.Cos(pitch * Mathf.Deg2Rad));
            GameObject front = Box("Roof Front", root, new Vector3(0f, height + rise * 0.5f, depth * 0.5f - half * 0.5f), size, _roof);
            front.transform.localRotation = Quaternion.Euler(-pitch, 0f, 0f);
            GameObject back = Box("Roof Back", root, new Vector3(0f, height + rise * 0.5f, depth * 0.5f + half * 0.5f), size, _roof);
            back.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private static void Chimney(Transform root, Vector3 basePosition, bool smoke)
        {
            Box("Chimney", root, basePosition + new Vector3(0f, 1.6f, 0f), new Vector3(0.6f, 3.2f, 0.6f), _stone);
            Box("Chimney Cap", root, basePosition + new Vector3(0f, 3.25f, 0f), new Vector3(0.75f, 0.12f, 0.75f), _stone);
            if (smoke) Smoke(root, basePosition + new Vector3(0f, 3.4f, 0f));
        }

        private static void Window(Transform root, Vector3 centre, Vector2 size, Material shutter, bool lit)
        {
            Box("Glass", root, centre + new Vector3(0f, 0f, -0.02f), new Vector3(size.x, size.y, 0.03f), lit ? _window : _glassCool);
            Frame(root, centre, size);
            // Open shutters either side, turned out a little.
            foreach (int side in new[] { -1, 1 })
            {
                GameObject s = Box("Shutter", root, centre + new Vector3(side * (size.x * 0.5f + size.x * 0.27f), 0f, -0.08f), new Vector3(size.x * 0.5f, size.y + 0.05f, 0.04f), shutter);
                s.transform.localRotation = Quaternion.Euler(0f, side * 12f, 0f);
                for (int slat = 0; slat < 5; slat++)
                    Box("Slat", s.transform, new Vector3(0f, -size.y * 0.4f + slat * size.y * 0.2f, -0.025f), new Vector3(size.x * 0.42f, 0.02f, 0.015f), shutter);
            }
        }

        private static void Frame(Transform root, Vector3 centre, Vector2 size)
        {
            const float f = 0.07f;
            Box("Frame Top", root, centre + new Vector3(0f, size.y * 0.5f + f * 0.5f, -0.04f), new Vector3(size.x + f * 2f, f, 0.08f), _stone);
            Box("Sill", root, centre + new Vector3(0f, -size.y * 0.5f - 0.04f, -0.09f), new Vector3(size.x + f * 3f, 0.07f, 0.18f), _stone);
            Box("Frame L", root, centre + new Vector3(-size.x * 0.5f - f * 0.5f, 0f, -0.04f), new Vector3(f, size.y, 0.08f), _stone);
            Box("Frame R", root, centre + new Vector3(size.x * 0.5f + f * 0.5f, 0f, -0.04f), new Vector3(f, size.y, 0.08f), _stone);
            Box("Mullion", root, centre + new Vector3(0f, 0f, -0.035f), new Vector3(0.04f, size.y, 0.05f), _wood);
        }

        private static void FlowerBox(Transform root, Vector3 centre, float width)
        {
            Box("Flower Box", root, centre, new Vector3(width, 0.2f, 0.22f), _wood);
            Material[] petals =
            {
                GreyboxAssets.Lit("Street_Petal_Red", new Color(0.86f, 0.25f, 0.3f)), GreyboxAssets.Lit("Street_Petal_Yellow", new Color(0.98f, 0.82f, 0.3f)),
                GreyboxAssets.Lit("Street_Petal_Pink", new Color(0.95f, 0.6f, 0.75f)), GreyboxAssets.Lit("Street_Petal_White", new Color(0.96f, 0.95f, 0.9f)),
            };
            int count = Mathf.RoundToInt(width / 0.12f);
            for (int i = 0; i < count; i++)
            {
                float x = -width * 0.45f + width * 0.9f * i / Mathf.Max(1, count - 1);
                LocationKit.AddPrimitive(PrimitiveType.Sphere, "Leaf", root, centre + new Vector3(x, 0.14f, 0f), new Vector3(0.14f, 0.1f, 0.14f), _leaves);
                if (_random.NextDouble() < 0.7)
                    LocationKit.AddPrimitive(PrimitiveType.Sphere, "Bloom", root, centre + new Vector3(x + 0.02f, 0.21f, -0.03f), Vector3.one * 0.075f, petals[_random.Next(petals.Length)]);
            }
        }

        private static void Balcony(Transform root, Vector3 centre, float width)
        {
            Box("Balcony Slab", root, centre + new Vector3(0f, 0f, -0.45f), new Vector3(width, 0.12f, 0.9f), _stone);
            Box("Balcony Rail", root, centre + new Vector3(0f, 0.95f, -0.88f), new Vector3(width, 0.05f, 0.05f), _iron);
            int bars = Mathf.RoundToInt(width / 0.14f);
            for (int i = 0; i <= bars; i++)
                Box("Bar", root, centre + new Vector3(-width * 0.5f + width * i / bars, 0.5f, -0.88f), new Vector3(0.025f, 0.9f, 0.025f), _iron);
            FlowerBox(root, centre + new Vector3(-width * 0.3f, 0.12f, -0.6f), 0.8f);
        }

        private static void Door(Transform root, Vector3 basePosition)
        {
            Box("Door", root, basePosition + new Vector3(0f, 1.1f, -0.03f), new Vector3(1.1f, 2.2f, 0.06f), _wood);
            Box("Door Frame Top", root, basePosition + new Vector3(0f, 2.28f, -0.06f), new Vector3(1.35f, 0.16f, 0.12f), _stone);
            Box("Door Frame L", root, basePosition + new Vector3(-0.62f, 1.1f, -0.06f), new Vector3(0.12f, 2.2f, 0.12f), _stone);
            Box("Door Frame R", root, basePosition + new Vector3(0.62f, 1.1f, -0.06f), new Vector3(0.12f, 2.2f, 0.12f), _stone);
            Box("Step", root, basePosition + new Vector3(0f, 0.06f, -0.25f), new Vector3(1.4f, 0.12f, 0.45f), _stone);
            Art.ArchitectureKit.Sconce(root, root.TransformPoint(basePosition + new Vector3(0.85f, 2.0f, 0f)), root.rotation);
        }

        private static void BladeSign(Transform root, Vector3 at, Color color)
        {
            Box("Sign Bracket", root, at + new Vector3(0f, 0.35f, 0.3f), new Vector3(0.04f, 0.04f, 0.7f), _iron);
            Material board = GreyboxAssets.Lit($"Street_Blade_{ColorUtility.ToHtmlStringRGB(color)}", color);
            Box("Blade Sign", root, at, new Vector3(0.05f, 0.6f, 0.55f), board);
            Box("Blade Trim", root, at, new Vector3(0.03f, 0.66f, 0.61f), _wood); // thinner than the board: a frame round it
        }

        private static void CafeTable(Transform parent, Vector3 at, Color parasolColor)
        {
            Material cloth = GreyboxAssets.Lit("Street_TableCloth", new Color(0.95f, 0.93f, 0.86f));
            LocationKit.AddPrimitive(PrimitiveType.Cylinder, "Table Top", parent, at + new Vector3(0f, 0.74f, 0f), new Vector3(0.75f, 0.02f, 0.75f), cloth);
            LocationKit.AddPrimitive(PrimitiveType.Cylinder, "Table Leg", parent, at + new Vector3(0f, 0.37f, 0f), new Vector3(0.06f, 0.37f, 0.06f), _iron);
            foreach (int side in new[] { -1, 1 })
            {
                Vector3 chair = at + new Vector3(side * 0.6f, 0f, 0f);
                Box("Chair Seat", parent, chair + new Vector3(0f, 0.45f, 0f), new Vector3(0.4f, 0.05f, 0.4f), _wood);
                Box("Chair Back", parent, chair + new Vector3(side * 0.2f, 0.72f, 0f), new Vector3(0.04f, 0.5f, 0.4f), _wood);
                Box("Chair Leg", parent, chair + new Vector3(0f, 0.22f, 0f), new Vector3(0.05f, 0.44f, 0.05f), _iron);
            }
            // Parasol: pole and a shallow cone of stripes.
            LocationKit.AddPrimitive(PrimitiveType.Cylinder, "Parasol Pole", parent, at + new Vector3(0f, 1.2f, 0f), new Vector3(0.04f, 1.2f, 0.04f), _wood);
            Mesh canopy = Art.MeshAssets.GetOrCreate("Parasol_v1", () => Art.MeshGen.Lathe(new[]
            {
                new Vector2(0f, 0.32f), new Vector2(1.1f, 0f), new Vector2(1.12f, -0.06f), new Vector2(1.08f, -0.04f), new Vector2(0f, 0.28f),
            }, 16, "Parasol"), rebuild: false);
            var go = new GameObject("Parasol");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at + new Vector3(0f, 2.25f, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = canopy;
            go.AddComponent<MeshRenderer>().sharedMaterial = Stripes(parasolColor);
            GreyboxAssets.MarkStatic(go);
        }

        // ───────────── The Grand Bakery ─────────────

        /// <summary>
        /// The Bakery as it looks from outside now that it's a mega location (Jesse 2026-11-13): a 32 m wide, three
        /// storey building closing the west end of the street, with a stone ground floor, arched shop windows under
        /// striped awnings, a projecting entrance pavilion with a pediment and a grand arched door up a flight of
        /// steps, the BAKERY sign, chimneys and flower boxes. frontCentre is the middle of the main front wall at
        /// street level; facing turns local -Z toward the street. Returns where the door is (local -Z out),
        /// for the City builder to make enterable.
        /// </summary>
        public static Transform BuildGrandBakery(Transform street, Vector3 centre, Vector3 frontCentre, Quaternion facing, Color wallColor, Color awningColor)
        {
            Init(centre);
            var root = new GameObject("Grand Bakery").transform;
            root.SetParent(street, false);
            root.SetPositionAndRotation(frontCentre, facing);
            const float width = 32f, depth = 24f, groundH = 6f, storeyH = 4.5f, height = groundH + storeyH * 3f;
            const float pavilionW = 9f, pavilionD = 1.6f, pavilionH = height + 2.5f;
            Material wall = GreyboxAssets.Textured("Grand_Bakery_Wall", wallColor, Art.TextureSet.Plaster, 2.5f, 0.05f, 0.7f);
            Material shutter = GreyboxAssets.Textured("Grand_Bakery_Shutter", awningColor, Art.TextureSet.Wood, 0.8f, 0.35f);
            Material gilt = Art.ArchitectureKit.Gilt();
            Material stripes = Stripes(awningColor);
            Material shopGlass = GreyboxAssets.Emissive("Grand_Bakery_ShopGlass", new Color(0.7f, 0.55f, 0.38f), new Color(1f, 0.7f, 0.4f), 0.45f);

            // Body, stone ground floor, string courses, cornice and roof.
            Box("Building", root, new Vector3(0f, height * 0.5f, depth * 0.5f), new Vector3(width, height, depth), wall, collider: true);
            Box("Rusticated Ground Floor", root, new Vector3(0f, groundH * 0.5f, -0.06f), new Vector3(width, groundH, 0.12f), _stone);
            for (int c = 0; c < 6; c++) // rustication joints
                Box("Joint", root, new Vector3(0f, 0.9f + c * 0.85f, -0.125f), new Vector3(width, 0.04f, 0.02f), _wood);
            Box("String Course", root, new Vector3(0f, groundH + 0.08f, -0.1f), new Vector3(width + 0.1f, 0.25f, 0.25f), _stone);
            Box("String Course", root, new Vector3(0f, groundH + storeyH + 0.05f, -0.06f), new Vector3(width, 0.14f, 0.12f), _stone);
            Art.ArchitectureKit.CrownRun(root, root.TransformPoint(new Vector3(0f, height - 0.3f, 0f)), root.rotation, width, _stone);
            Roof(root, width, height, depth);
            foreach (float x in new[] { -11f, -3.5f, 10f })
                Chimney(root, new Vector3(x, height, depth * 0.6f), smoke: true); // the ovens are always going
            // Dormers in the roof.
            foreach (float x in new[] { -12f, -7f, 7f, 12f })
            {
                Box("Dormer", root, new Vector3(x, height + 1.2f, 2.6f), new Vector3(1.8f, 2.2f, 1.6f), wall);
                Box("Dormer Glass", root, new Vector3(x, height + 1.1f, 1.78f), new Vector3(1f, 1.3f, 0.03f), _window);
                GameObject cap = Box("Dormer Roof", root, new Vector3(x, height + 2.45f, 2.6f), new Vector3(1.3f, 0.12f, 1.9f), _roof);
                cap.transform.localRotation = Quaternion.Euler(0f, 0f, 35f);
                GameObject cap2 = Box("Dormer Roof", root, new Vector3(x, height + 2.45f, 2.6f), new Vector3(1.3f, 0.12f, 1.9f), _roof);
                cap2.transform.localRotation = Quaternion.Euler(0f, 0f, -35f);
            }

            // Ground floor: three tall arched shop windows each side of the pavilion, each with an awning and flowers.
            foreach (int side in new[] { -1, 1 })
            for (int i = 0; i < 3; i++)
            {
                float x = side * (pavilionW * 0.5f + 1.9f + i * 3.6f);
                var size = new Vector2(2.4f, 3.4f);
                Box("Shop Window", root, new Vector3(x, 2.5f, -0.14f), new Vector3(size.x, size.y, 0.04f), shopGlass);
                for (int bar = 1; bar < 4; bar++) // glazing bars
                    Box("Glazing Bar", root, new Vector3(x, 2.5f - size.y * 0.5f + bar * size.y / 4f, -0.17f), new Vector3(size.x, 0.05f, 0.03f), _wood);
                foreach (float bx in new[] { -size.x / 6f, size.x / 6f })
                    Box("Glazing Bar", root, new Vector3(x + bx * 2f, 2.5f, -0.17f), new Vector3(0.05f, size.y, 0.03f), _wood);
                Frame(root, new Vector3(x, 2.5f, -0.1f), size);
                LocationKit.AddPrimitive(PrimitiveType.Cylinder, "Fanlight", root, new Vector3(x, 4.2f, -0.13f), new Vector3(size.x, 0.02f, size.x), _window)
                    .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                GameObject awning = Box("Awning", root, new Vector3(x, 4.7f, -0.9f), new Vector3(size.x + 0.5f, 0.05f, 1.7f), stripes);
                awning.transform.localRotation = Quaternion.Euler(-18f, 0f, 0f);
                Box("Awning Valance", root, new Vector3(x, 4.45f, -1.72f), new Vector3(size.x + 0.5f, 0.24f, 0.03f), stripes);
                FlowerBox(root, new Vector3(x, 0.72f, -0.3f), size.x);
            }

            // Upper floors: shuttered windows with flower boxes, skipping the pavilion.
            for (int floor = 0; floor < 3; floor++)
            {
                float y = groundH + floor * storeyH + 2.2f;
                for (float x = -width * 0.5f + 2f; x <= width * 0.5f - 1.9f; x += 3.1f)
                {
                    if (Mathf.Abs(x) < pavilionW * 0.5f + 0.6f) continue;
                    Window(root, new Vector3(x, y, -0.02f), new Vector2(1.2f, 2.1f), shutter, lit: _random.NextDouble() < 0.7);
                    FlowerBox(root, new Vector3(x, y - 1.2f, -0.2f), 1.3f);
                }
            }

            // The entrance pavilion: taller, in stone, with a pediment, a balcony and the sign.
            var pav = new GameObject("Entrance Pavilion").transform;
            pav.SetParent(root, false);
            pav.localPosition = new Vector3(0f, 0f, -pavilionD);
            Box("Pavilion", pav, new Vector3(0f, pavilionH * 0.5f, pavilionD * 0.5f), new Vector3(pavilionW, pavilionH, pavilionD + 0.02f), _stone, collider: true);
            foreach (int side in new[] { -1, 1 })
                Box("Column", pav, new Vector3(side * (pavilionW * 0.5f - 0.45f), pavilionH * 0.5f, -0.25f), new Vector3(0.7f, pavilionH, 0.5f), _stone);
            Art.ArchitectureKit.CrownRun(pav, pav.TransformPoint(new Vector3(0f, pavilionH - 0.3f, -0.5f)), pav.rotation, pavilionW + 0.4f, _stone);
            foreach (int side in new[] { -1, 1 })
            {
                GameObject rake = Box("Pediment", pav, new Vector3(side * pavilionW * 0.25f, pavilionH + 1.1f, 0.2f), new Vector3(pavilionW * 0.56f, 0.4f, 1.4f), _stone);
                rake.transform.localRotation = Quaternion.Euler(0f, 0f, -side * 24f);
            }
            Box("Pediment Field", pav, new Vector3(0f, pavilionH + 0.6f, 0.35f), new Vector3(pavilionW * 0.8f, 1.2f, 0.9f), wall);
            LocationKit.AddPrimitive(PrimitiveType.Cylinder, "Oculus", pav, new Vector3(0f, pavilionH + 0.8f, -0.12f), new Vector3(1.1f, 0.03f, 1.1f), _window)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            // The grand door: an arch up two steps, with lanterns.
            const float doorW = 3.2f, doorH = 4.6f;
            Box("Door Recess", pav, new Vector3(0f, doorH * 0.5f, -0.03f), new Vector3(doorW + 0.3f, doorH, 0.06f), _wood);
            foreach (int side in new[] { -1, 1 })
            {
                Box("Door Leaf", pav, new Vector3(side * doorW * 0.25f, doorH * 0.42f, -0.08f), new Vector3(doorW * 0.48f, doorH * 0.84f, 0.06f), _wood);
                Box("Door Glass", pav, new Vector3(side * doorW * 0.25f, doorH * 0.6f, -0.115f), new Vector3(doorW * 0.22f, doorH * 0.3f, 0.02f), _window);
                Box("Door Jamb", pav, new Vector3(side * (doorW * 0.5f + 0.2f), doorH * 0.5f, -0.12f), new Vector3(0.4f, doorH, 0.2f), _stone);
                Art.ArchitectureKit.Sconce(pav, pav.TransformPoint(new Vector3(side * (doorW * 0.5f + 0.8f), 2.8f, -0.22f)), pav.rotation);
            }
            Box("Step 1", pav, new Vector3(0f, 0.075f, -1.1f), new Vector3(doorW + 3f, 0.15f, 1.0f), _stone, collider: true);
            Box("Step 2", pav, new Vector3(0f, 0.2f, -0.35f), new Vector3(doorW + 2f, 0.4f, 0.6f), _stone, collider: true);

            // Balcony over the door, and the sign: big gilded letters on a dark board.
            Balcony(pav, new Vector3(0f, groundH + 0.3f, -0.05f), pavilionW - 1.6f);
            Window(pav, new Vector3(0f, groundH + 2.2f, -0.02f), new Vector2(1.6f, 2.3f), shutter, lit: true);
            Window(pav, new Vector3(0f, groundH + storeyH + 2.2f, -0.02f), new Vector2(1.6f, 2.1f), shutter, lit: true);
            Window(pav, new Vector3(0f, groundH + storeyH * 2f + 2.2f, -0.02f), new Vector2(1.6f, 2.1f), shutter, lit: true);
            // Built at half size and scaled up: the sign's text renders at a size the font can handle.
            GameObject sign = LocationKit.BuildWallSign(pav, "Bakery Sign", "BAKERY", pav.TransformPoint(new Vector3(0f, groundH - 0.75f, -0.12f)), pav.rotation,
                new Vector2(3.25f, 0.55f), _wood, new Color(0.98f, 0.92f, 0.78f), new Color(0.3f, 0.16f, 0.06f));
            sign.transform.localScale = Vector3.one * 2f;
            Box("Sign Gilt Edge", pav, new Vector3(0f, groundH - 0.75f, -0.08f), new Vector3(6.8f, 1.35f, 0.04f), gilt);
            BladeSign(root, new Vector3(-width * 0.5f + 0.6f, 4.8f, -0.7f), awningColor);
            BladeSign(root, new Vector3(width * 0.5f - 0.6f, 4.8f, -0.7f), awningColor);

            // Bay trees in planters either side of the steps.
            foreach (int side in new[] { -1, 1 })
            {
                Vector3 at = new Vector3(side * (doorW * 0.5f + 1.9f), 0f, -pavilionD - 1.3f);
                Box("Planter", root, at + new Vector3(0f, 0.4f, 0f), new Vector3(0.9f, 0.8f, 0.9f), _stone, collider: true);
                LocationKit.AddPrimitive(PrimitiveType.Cylinder, "Trunk", root, at + new Vector3(0f, 1.3f, 0f), new Vector3(0.12f, 0.6f, 0.12f), _trunk);
                LocationKit.AddPrimitive(PrimitiveType.Sphere, "Bay", root, at + new Vector3(0f, 2.3f, 0f), new Vector3(1.3f, 1.4f, 1.3f), _leaves);
            }

            var door = new GameObject("Door").transform;
            door.SetParent(pav, false);
            door.localPosition = new Vector3(0f, 0f, -0.1f);
            return door;
        }

        // ───────────── Street ends and skyline ─────────────

        /// <summary>
        /// The street doesn't stop: at the east end an archway, with the street running on past it into the haze;
        /// at the west end the square with the clock tower, and more houses beyond.
        /// </summary>
        private static void BuildStreetEnds(Transform town)
        {
            // East archway (the boundary runs under it, so the player stays on our street).
            var arch = new GameObject("East Archway").transform;
            arch.SetParent(town, false);
            arch.position = _c + new Vector3(HalfLength + 0.6f, 0f, 0f);
            foreach (int side in new[] { -1, 1 })
            {
                Box("Pier", arch, new Vector3(0f, 3.6f, side * 6.4f), new Vector3(1.4f, 7.2f, 1.6f), _stone, collider: true);
                Box("Pier Cap", arch, new Vector3(0f, 7.3f, side * 6.4f), new Vector3(1.7f, 0.25f, 1.9f), _stone);
            }
            Box("Lintel", arch, new Vector3(0f, 8.1f, 0f), new Vector3(1.4f, 1.4f, 14.4f), _stone);
            Box("Parapet", arch, new Vector3(0f, 9.05f, 0f), new Vector3(1.6f, 0.5f, 14.8f), _stone);
            Mesh archMesh = Art.MeshAssets.GetOrCreate("StreetArch_v1", ArchCurve, rebuild: true);
            var curve = new GameObject("Arch Curve");
            curve.transform.SetParent(arch, false);
            curve.transform.localPosition = new Vector3(0f, 5.1f, 0f);
            curve.AddComponent<MeshFilter>().sharedMesh = archMesh;
            curve.AddComponent<MeshRenderer>().sharedMaterial = _stone;
            GreyboxAssets.MarkStatic(curve.gameObject);
            Material banner = GreyboxAssets.Lit("Street_Banner", new Color(0.55f, 0.2f, 0.22f));
            foreach (int side in new[] { -1, 1 })
                Box("Banner", arch, new Vector3(-0.75f, 5.4f, side * 6.4f), new Vector3(0.04f, 2.6f, 0.9f), banner);

            // Beyond both ends: more street, lined with simpler houses that fade into the haze.
            foreach (int dir in new[] { -1, 1 })
            {
                Box("Road Beyond", town, new Vector3(dir * (HalfLength + 12f), -0.1f, 0f) + _c, new Vector3(24f, 0.2f, 16f),
                    GreyboxAssets.Textured("City_Cobbles", Core.Palette.WarmStone, Art.TextureSet.Cobbles, 2.2f, 0.2f, 1.2f));
                for (int i = 0; i < (dir < 0 ? 1 : 4); i++) // west: one house, then the square in front of the Grand Bakery
                {
                    float x = dir * (HalfLength + 3.5f + i * 6.2f);
                    foreach (bool south in new[] { false, true })
                    {
                        var h = new House
                        {
                            X = x, Width = 6f, Storeys = 3 + _random.Next(2),
                            Wall = Color.Lerp(new Color(0.92f, 0.82f, 0.68f), new Color(0.8f, 0.82f, 0.86f), (float)_random.NextDouble()),
                            Shutters = new Color(0.32f, 0.42f, 0.4f), Ground = Ground.House,
                        };
                        Facade(town, south ? SouthZ : NorthZ, south, h);
                    }
                }
            }

            // The square in front of the Grand Bakery, wide enough to see the whole front.
            Box("Bakery Square", town, _c + new Vector3(-34.75f, -0.1f, 0f), new Vector3(10.5f, 0.2f, 32f),
                GreyboxAssets.Textured("City_Cobbles", Core.Palette.WarmStone, Art.TextureSet.Cobbles, 2.2f, 0.2f, 1.2f), collider: true, world: true);
            foreach (int side in new[] { -1, 1 })
            {
                Vector3 at = _c + new Vector3(-31f, 0f, side * 12.5f);
                Box("Square Bench", town, at + new Vector3(0f, 0.45f, 0f), new Vector3(0.5f, 0.08f, 1.8f), _wood, collider: true, world: true);
                Box("Square Bench Back", town, at + new Vector3(0.22f, 0.75f, 0f), new Vector3(0.06f, 0.5f, 1.8f), _wood, world: true);
                Box("Square Planter", town, at + new Vector3(-3.5f, 0.3f, 0f), new Vector3(1.1f, 0.6f, 1.1f), _stone, collider: true, world: true);
                LocationKit.AddPrimitive(PrimitiveType.Sphere, "Square Shrub", town, at + new Vector3(-3.5f, 1f, 0f) - town.position, new Vector3(1.2f, 1f, 1.2f), _leaves);
            }

            // Close the view at the east end with the town hall (the Grand Bakery closes the west end),
            // so the street leads somewhere instead of ending at the horizon.
            foreach (int dir in new[] { -1, 1 })
            {
                if (dir < 0) continue;
                Transform closer = Facade(town, 0f, false, new House
                {
                    X = 0f, Width = 18f, Storeys = dir > 0 ? 4 : 5,
                    Wall = dir > 0 ? new Color(0.9f, 0.84f, 0.74f) : new Color(0.86f, 0.76f, 0.66f),
                    Shutters = dir > 0 ? new Color(0.3f, 0.36f, 0.5f) : new Color(0.45f, 0.3f, 0.25f), Ground = Ground.House,
                });
                closer.position = _c + new Vector3(dir * (HalfLength + 27f), 0f, 0f);
                closer.rotation = Quaternion.Euler(0f, dir > 0 ? 90f : -90f, 0f); // front (-Z) faces back down the street
                if (dir > 0)
                {
                    // The town hall's little clock turret over the middle.
                    Box("Turret", closer, new Vector3(0f, 16.5f, 3f), new Vector3(3f, 5f, 3f), _stone);
                    GameObject cap = Box("Turret Roof", closer, new Vector3(0f, 19.6f, 3f), new Vector3(2.4f, 2.4f, 2.4f), _roof);
                    cap.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                }
            }

            // Ground that runs on under the whole town, so nothing ends in a hard edge.
            Box("Town Ground", town, _c + new Vector3(0f, -0.25f, 0f), new Vector3(320f, 0.2f, 320f),
                GreyboxAssets.Textured("Town_Ground", new Color(0.72f, 0.66f, 0.56f), Art.TextureSet.Cobbles, 3f, 0.15f, 1f));
        }

        /// <summary>A half-ring of stone under the lintel: the opening of the archway.</summary>
        private static Mesh ArchCurve()
        {
            var parts = new List<CombineInstance>();
            const int steps = 12;
            const float radius = 5.6f;
            Mesh block = Art.MeshGen.RoundedBox(new Vector3(1.3f, 0.5f, 1.4f), 0.05f, 1);
            for (int i = 0; i <= steps; i++)
            {
                float a = Mathf.PI * i / steps;
                var p = new Vector3(0f, Mathf.Sin(a) * radius * 0.42f, Mathf.Cos(a) * radius);
                Quaternion r = Quaternion.Euler(-Mathf.Rad2Deg * (a - Mathf.PI * 0.5f) * 0.6f, 0f, 0f);
                parts.Add(new CombineInstance { mesh = block, transform = Matrix4x4.TRS(p, r, Vector3.one) });
            }
            var mesh = new Mesh { name = "StreetArch" };
            mesh.CombineMeshes(parts.ToArray(), true, true);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>The rest of the city over the rooftops: house blocks, a church spire, a dome, softened by the haze.</summary>
        private static void BuildSkyline(Transform town)
        {
            var sky = new GameObject("Skyline").transform;
            sky.SetParent(town, false);
            Material[] walls =
            {
                GreyboxAssets.Lit("Skyline_Wall_A", new Color(0.86f, 0.78f, 0.68f)), GreyboxAssets.Lit("Skyline_Wall_B", new Color(0.78f, 0.74f, 0.72f)),
                GreyboxAssets.Lit("Skyline_Wall_C", new Color(0.9f, 0.84f, 0.74f)),
            };
            foreach (int side in new[] { -1, 1 })
            {
                for (int i = 0; i < 9; i++)
                {
                    float x = -40f + i * 10f + (float)_random.NextDouble() * 4f;
                    float z = side * (24f + (float)_random.NextDouble() * 14f);
                    float h = 9f + (float)_random.NextDouble() * 8f;
                    var at = _c + new Vector3(x, 0f, z);
                    Box("Block", sky, at + new Vector3(0f, h * 0.5f, 0f), new Vector3(7f, h, 7f), walls[_random.Next(walls.Length)]);
                    GameObject roof = Box("Block Roof", sky, at + new Vector3(0f, h + 1.2f, 0f), new Vector3(5.2f, 2.4f, 5.2f), _roof);
                    roof.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
                }
            }
            // A church spire and a dome, landmarks above the roofs.
            Vector3 church = _c + new Vector3(14f, 0f, 34f);
            Box("Church Tower", sky, church + new Vector3(0f, 11f, 0f), new Vector3(4f, 22f, 4f), _stone);
            Mesh spire = Art.MeshAssets.GetOrCreate("Spire_v1", () => Art.MeshGen.Lathe(new[] { new Vector2(0f, 0f), new Vector2(2.6f, 0f), new Vector2(0f, 9f) }, 4, "Spire"), rebuild: false);
            var spireGo = new GameObject("Church Spire");
            spireGo.transform.SetParent(sky, false);
            spireGo.transform.SetPositionAndRotation(church + new Vector3(0f, 22f, 0f), Quaternion.Euler(0f, 45f, 0f));
            spireGo.AddComponent<MeshFilter>().sharedMesh = spire;
            spireGo.AddComponent<MeshRenderer>().sharedMaterial = GreyboxAssets.Lit("Skyline_Slate", new Color(0.42f, 0.46f, 0.52f));
            Vector3 dome = _c + new Vector3(-24f, 0f, -36f);
            Box("Dome Drum", sky, dome + new Vector3(0f, 9f, 0f), new Vector3(8f, 18f, 8f), walls[0]);
            LocationKit.AddPrimitive(PrimitiveType.Sphere, "Dome", sky, dome + new Vector3(0f, 18f, 0f), new Vector3(8.4f, 7f, 8.4f),
                GreyboxAssets.Lit("Skyline_Copper", new Color(0.42f, 0.66f, 0.58f)));
        }

        // ───────────── Bunting and string lights ─────────────

        private static void BuildBunting(Transform town)
        {
            Material[] flags =
            {
                GreyboxAssets.Lit("Bunting_Red", new Color(0.86f, 0.28f, 0.25f)), GreyboxAssets.Lit("Bunting_Yellow", new Color(0.98f, 0.8f, 0.3f)),
                GreyboxAssets.Lit("Bunting_Blue", new Color(0.3f, 0.5f, 0.8f)), GreyboxAssets.Lit("Bunting_Green", new Color(0.4f, 0.68f, 0.42f)),
                GreyboxAssets.Lit("Bunting_White", new Color(0.96f, 0.94f, 0.88f)),
            };
            Material bulb = GreyboxAssets.Emissive("String_Bulb", new Color(1f, 0.9f, 0.7f), new Color(1f, 0.75f, 0.45f), 3.5f);
            float[] lines = { -15f, -8f, -2f, 4f, 10f, 16f };
            for (int l = 0; l < lines.Length; l++)
            {
                bool lights = l % 2 == 1;
                Vector3 a = _c + new Vector3(lines[l] + (l % 2 == 0 ? -1.2f : 1.2f), 5.3f, NorthZ - 0.1f);
                Vector3 b = _c + new Vector3(lines[l], 5.3f, SouthZ + 0.1f);
                var root = new GameObject(lights ? "String Lights" : "Bunting").transform;
                root.SetParent(town, false);
                Vector3 prev = a;
                const int steps = 32;
                for (int i = 1; i <= steps; i++)
                {
                    float t = (float)i / steps;
                    Vector3 p = Vector3.Lerp(a, b, t) + Vector3.down * Mathf.Sin(t * Mathf.PI) * 1.1f;
                    Cord(root, prev, p);
                    if (lights && i % 2 == 0 && i < steps)
                        LocationKit.AddPrimitive(PrimitiveType.Sphere, "Bulb", root, root.InverseTransformPoint(p + Vector3.down * 0.08f), Vector3.one * 0.08f, bulb);
                    else if (!lights && i < steps)
                    {
                        // A little pennant hanging from the cord.
                        Vector3 along = (p - prev).normalized;
                        var flag = new GameObject("Pennant");
                        flag.transform.SetParent(root, false);
                        flag.transform.position = p;
                        flag.transform.rotation = Quaternion.LookRotation(Vector3.Cross(along, Vector3.up), Vector3.up);
                        flag.AddComponent<MeshFilter>().sharedMesh = Pennant();
                        var r = flag.AddComponent<MeshRenderer>();
                        r.sharedMaterial = flags[(i + l) % flags.Length];
                        r.shadowCastingMode = ShadowCastingMode.Off;
                    }
                    prev = p;
                }
            }
        }

        private static void Cord(Transform parent, Vector3 from, Vector3 to)
        {
            var go = GreyboxAssets.Box("Cord", parent, Vector3.zero, new Vector3(0.015f, 0.015f, Vector3.Distance(from, to)), _iron, keepCollider: false);
            go.transform.position = (from + to) * 0.5f;
            go.transform.rotation = Quaternion.LookRotation(to - from);
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        private static Mesh Pennant() => Art.MeshAssets.GetOrCreate("Pennant_v2", () =>
        {
            var mesh = new Mesh { name = "Pennant" };
            // Two separate faces (front and back) so each keeps its own normal and the flag is lit on both sides.
            mesh.vertices = new[] { new Vector3(-0.13f, 0f, 0f), new Vector3(0.13f, 0f, 0f), new Vector3(0f, -0.3f, 0f), new Vector3(-0.13f, 0f, 0f), new Vector3(0.13f, 0f, 0f), new Vector3(0f, -0.3f, 0f) };
            mesh.triangles = new[] { 0, 1, 2, 3, 5, 4 };
            mesh.uv = new[] { new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0f) };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }, rebuild: false);

        // ───────────── The square ─────────────

        /// <summary>By the clock tower: a fountain with splashing water, a market stall, barrels and crates.</summary>
        private static void BuildSquare(Transform town)
        {
            var square = new GameObject("Square").transform;
            square.SetParent(town, false);

            // Fountain.
            Vector3 f = _c + new Vector3(-14.5f, 0f, 0.8f);
            var fountain = new GameObject("Fountain").transform;
            fountain.SetParent(square, false);
            fountain.position = f;
            Mesh basin = Art.MeshAssets.GetOrCreate("Fountain_Basin_v1", () => Art.MeshGen.Lathe(new[]
            {
                new Vector2(0f, 0f), new Vector2(1.6f, 0f), new Vector2(1.7f, 0.1f), new Vector2(1.7f, 0.55f), new Vector2(1.55f, 0.62f),
                new Vector2(1.45f, 0.5f), new Vector2(1.45f, 0.3f), new Vector2(0f, 0.3f),
            }, 32, "Basin"), rebuild: false);
            Mesh column = Art.MeshAssets.GetOrCreate("Fountain_Column_v1", () => Art.MeshGen.Lathe(new[]
            {
                new Vector2(0f, 0f), new Vector2(0.28f, 0f), new Vector2(0.18f, 0.25f), new Vector2(0.16f, 1.1f), new Vector2(0.65f, 1.2f),
                new Vector2(0.7f, 1.32f), new Vector2(0.55f, 1.3f), new Vector2(0.12f, 1.35f), new Vector2(0.1f, 1.7f), new Vector2(0.18f, 1.78f), new Vector2(0f, 1.85f),
            }, 24, "Column"), rebuild: false);
            Mesh pool = Art.MeshAssets.GetOrCreate("Fountain_Water_v1", () => Art.MeshGen.Lathe(new[] { new Vector2(0f, 0f), new Vector2(1.47f, 0f), new Vector2(1.47f, 0.02f), new Vector2(0f, 0.02f) }, 32, "Water"), rebuild: false);
            Place(fountain, "Basin", basin, _stone, Vector3.zero, collider: true);
            Place(fountain, "Column", column, _stone, Vector3.zero, collider: false);
            Place(fountain, "Water", pool, _water, new Vector3(0f, 0.45f, 0f), collider: false);
            Splash(fountain, new Vector3(0f, 1.85f, 0f));

            // Market stall: striped canopy over a counter of fruit crates.
            Vector3 s = _c + new Vector3(-18.5f, 0f, -3.2f);
            var stall = new GameObject("Market Stall").transform;
            stall.SetParent(square, false);
            stall.position = s;
            stall.rotation = Quaternion.Euler(0f, 20f, 0f);
            Box("Counter", stall, new Vector3(0f, 0.45f, 0f), new Vector3(2.4f, 0.9f, 1f), _wood, collider: true);
            foreach (int x in new[] { -1, 1 })
            foreach (int z in new[] { -1, 1 })
                Box("Post", stall, new Vector3(x * 1.15f, 1.2f, z * 0.45f), new Vector3(0.06f, 2.4f, 0.06f), _wood);
            GameObject canopy = Box("Canopy", stall, new Vector3(0f, 2.45f, 0f), new Vector3(2.7f, 0.05f, 1.5f), Stripes(new Color(0.85f, 0.55f, 0.15f)));
            canopy.transform.localRotation = Quaternion.Euler(-10f, 0f, 0f);
            Material[] fruit =
            {
                GreyboxAssets.Lit("Fruit_Apple", new Color(0.8f, 0.15f, 0.12f)), GreyboxAssets.Lit("Fruit_Orange", new Color(0.98f, 0.55f, 0.12f)),
                GreyboxAssets.Lit("Fruit_Pear", new Color(0.7f, 0.78f, 0.25f)),
            };
            for (int c = 0; c < 3; c++)
            {
                Vector3 crate = new Vector3(-0.75f + c * 0.75f, 0.9f, 0f);
                Box("Fruit Crate", stall, crate + new Vector3(0f, 0.1f, 0f), new Vector3(0.65f, 0.2f, 0.8f), _wood);
                for (int k = 0; k < 14; k++)
                {
                    var p = crate + new Vector3(((k % 4) - 1.5f) * 0.14f, 0.24f + (k / 8) * 0.06f, ((k / 4 % 2) - 0.5f) * 0.28f + (k / 8) * 0.07f);
                    LocationKit.AddPrimitive(PrimitiveType.Sphere, "Fruit", stall, p, Vector3.one * 0.12f, fruit[c]);
                }
            }
            Barrel(square, _c + new Vector3(-17f, 0f, -4.6f));
            Barrel(square, _c + new Vector3(-16.4f, 0f, -4.9f));
            Crates(square, _c + new Vector3(-20.3f, 0f, -2.2f));
        }

        private static void Barrel(Transform parent, Vector3 at)
        {
            Mesh barrel = Art.MeshAssets.GetOrCreate("Barrel_v1", () => Art.MeshGen.Lathe(new[]
            {
                new Vector2(0f, 0f), new Vector2(0.3f, 0f), new Vector2(0.36f, 0.2f), new Vector2(0.38f, 0.45f), new Vector2(0.36f, 0.7f),
                new Vector2(0.3f, 0.9f), new Vector2(0f, 0.9f),
            }, 18, "Barrel"), rebuild: false);
            var go = Place(parent, "Barrel", barrel, _wood, Vector3.zero, collider: true);
            go.transform.position = at;
            foreach (float y in new[] { 0.15f, 0.75f })
                LocationKit.AddPrimitive(PrimitiveType.Cylinder, "Hoop", go.transform, new Vector3(0f, y, 0f), new Vector3(0.74f, 0.02f, 0.74f), _iron);
        }

        private static void Crates(Transform parent, Vector3 at)
        {
            Box("Crate", parent, at + new Vector3(0f, 0.25f, 0f) - parent.position + parent.position, new Vector3(0.5f, 0.5f, 0.5f), _wood, collider: true, world: true);
            Box("Crate", parent, at + new Vector3(0.55f, 0.25f, 0.1f), new Vector3(0.5f, 0.5f, 0.5f), _wood, collider: true, world: true);
            Box("Crate", parent, at + new Vector3(0.25f, 0.72f, 0.05f), new Vector3(0.45f, 0.45f, 0.45f), _wood, world: true)
                .transform.rotation = Quaternion.Euler(0f, 18f, 0f);
        }

        private static void Laundry(Transform parent, Vector3 localCentre, float width)
        {
            Vector3 a = _c + localCentre + new Vector3(-width * 0.5f, 0f, 0f), b = _c + localCentre + new Vector3(width * 0.5f, 0f, 0f);
            var root = new GameObject("Laundry").transform;
            root.SetParent(parent, false);
            Vector3 prev = a;
            for (int i = 1; i <= 8; i++)
            {
                float t = i / 8f;
                Vector3 p = Vector3.Lerp(a, b, t) + Vector3.down * Mathf.Sin(t * Mathf.PI) * 0.25f;
                Cord(root, prev, p);
                prev = p;
            }
            Color[] cloth = { new Color(0.95f, 0.95f, 0.92f), new Color(0.55f, 0.7f, 0.9f), new Color(0.95f, 0.75f, 0.8f), new Color(0.98f, 0.85f, 0.45f) };
            for (int i = 0; i < 4; i++)
            {
                float t = 0.15f + i * 0.23f;
                Vector3 p = Vector3.Lerp(a, b, t) + Vector3.down * (Mathf.Sin(t * Mathf.PI) * 0.25f + 0.28f);
                Material m = GreyboxAssets.Lit("Laundry_" + i, cloth[i]);
                Box("Sheet", root, p, new Vector3(0.38f, 0.5f, 0.02f), m, world: true);
            }
        }

        // ───────────── Planters along the south pavement ─────────────

        private static void BuildPlanters(Transform town)
        {
            foreach (float x in new[] { -12f, -4f, 4f, 12f })
            {
                Vector3 at = _c + new Vector3(x, 0f, -5.2f);
                Box("Planter", town, at + new Vector3(0f, 0.3f, 0f), new Vector3(1.1f, 0.6f, 1.1f), _stone, collider: true, world: true);
                LocationKit.AddPrimitive(PrimitiveType.Cylinder, "Trunk", town, at + new Vector3(0f, 1.45f, 0f) - town.position, new Vector3(0.16f, 0.9f, 0.16f), _trunk);
                foreach (Vector3 o in new[] { new Vector3(0f, 2.9f, 0f), new Vector3(0.35f, 2.6f, 0.2f), new Vector3(-0.3f, 2.7f, -0.25f) })
                    LocationKit.AddPrimitive(PrimitiveType.Sphere, "Crown", town, at + o - town.position, new Vector3(1.5f, 1.3f, 1.5f), _leaves);
            }
            Leaves(town, _c + new Vector3(0f, 4f, -4.5f));
        }

        // ───────────── Life ─────────────

        private static void BuildLife(Transform town)
        {
            var life = new GameObject("Street Life").transform;
            life.SetParent(town, false);
            Pigeons(life, _c + new Vector3(-12.6f, 0f, -0.8f), 6);
            Pigeons(life, _c + new Vector3(-17.5f, 0f, -5.6f), 4);
            Pigeons(life, _c + new Vector3(6.5f, 0f, -6.3f), 3);
            Strolling(life, "Townsperson A", _c + new Vector3(-15f, 0f, -6.4f), _c + new Vector3(14f, 0f, -6.4f), 1.0f,
                new Color(0.35f, 0.45f, 0.6f), new Color(0.9f, 0.85f, 0.7f), new Color(0.85f, 0.66f, 0.52f), NpcHeadwear.None);
            Strolling(life, "Townsperson B", _c + new Vector3(14f, 0f, 6.3f), _c + new Vector3(-14f, 0f, 6.3f), 0.85f,
                new Color(0.6f, 0.3f, 0.32f), new Color(0.95f, 0.9f, 0.8f), new Color(0.7f, 0.52f, 0.4f), NpcHeadwear.HairBun);
        }

        private static void Pigeons(Transform parent, Vector3 at, int count)
        {
            var flock = new GameObject("Pigeons");
            flock.transform.SetParent(parent, false);
            flock.transform.position = at;
            Material feathers = GreyboxAssets.Lit("Pigeon_Feathers", new Color(0.55f, 0.58f, 0.64f));
            Material neck = GreyboxAssets.Lit("Pigeon_Neck", new Color(0.36f, 0.5f, 0.44f));
            Material beak = GreyboxAssets.Lit("Pigeon_Beak", new Color(0.85f, 0.6f, 0.5f));
            for (int i = 0; i < count; i++)
            {
                var bird = new GameObject("Pigeon").transform;
                bird.SetParent(flock.transform, false);
                bird.position = at + new Vector3((float)_random.NextDouble() * 3f - 1.5f, 0f, (float)_random.NextDouble() * 2f - 1f);
                bird.rotation = Quaternion.Euler(0f, (float)_random.NextDouble() * 360f, 0f);
                LocationKit.AddPrimitive(PrimitiveType.Sphere, "Body", bird, new Vector3(0f, 0.11f, 0f), new Vector3(0.14f, 0.13f, 0.24f), feathers);
                LocationKit.AddPrimitive(PrimitiveType.Sphere, "Neck", bird, new Vector3(0f, 0.18f, 0.08f), new Vector3(0.09f, 0.1f, 0.09f), neck);
                LocationKit.AddPrimitive(PrimitiveType.Sphere, "Head", bird, new Vector3(0f, 0.23f, 0.12f), Vector3.one * 0.075f, feathers);
                LocationKit.AddPrimitive(PrimitiveType.Sphere, "Beak", bird, new Vector3(0f, 0.22f, 0.165f), new Vector3(0.02f, 0.02f, 0.04f), beak);
                LocationKit.AddPrimitive(PrimitiveType.Sphere, "Tail", bird, new Vector3(0f, 0.12f, -0.15f), new Vector3(0.1f, 0.03f, 0.12f), feathers);
                foreach (Renderer r in bird.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
            }
            flock.AddComponent<PigeonFlock>();
        }

        private static void Strolling(Transform parent, string name, Vector3 a, Vector3 b, float speed, Color body, Color accent, Color skin, NpcHeadwear headwear)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = a;
            GameObject npc = LocationKit.BuildNpc(name + " Body", a, 0f, body, accent, skin, headwear, new Color(0.35f, 0.24f, 0.16f));
            npc.transform.SetParent(root.transform, false);
            npc.transform.localPosition = Vector3.zero;
            npc.transform.localRotation = Quaternion.identity;
            root.AddComponent<Stroller>().EditorConfigure(a, b, npc.transform, speed);
        }

        // ───────────── Particles ─────────────

        private static void Smoke(Transform parent, Vector3 localPosition)
        {
            var go = new GameObject("Chimney Smoke");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            main.startColor = new Color(0.95f, 0.93f, 0.9f, 0.35f);
            main.maxParticles = 40;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 3f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8f;
            shape.radius = 0.15f;
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0.25f, 0.45f); // a light breeze down the street
            vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.2f));
            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.5f, 0.15f), new GradientAlphaKey(0f, 1f) });
            colour.color = g;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = _smoke;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        private static void Splash(Transform parent, Vector3 localPosition)
        {
            var go = new GameObject("Fountain Splash");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 1.1f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.6f, 2.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
            main.startColor = new Color(0.85f, 0.93f, 1f, 0.6f);
            main.gravityModifier = 1f;
            main.maxParticles = 200;
            var emission = ps.emission;
            emission.rateOverTime = 70f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 28f;
            shape.radius = 0.05f;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = SoftParticle("Street_Spray", new Color(0.9f, 0.96f, 1f, 0.7f));
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        private static void Leaves(Transform parent, Vector3 position)
        {
            var go = new GameObject("Drifting Leaves");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 9f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.1f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.9f, 0.6f, 0.25f, 1f), new Color(0.75f, 0.35f, 0.2f, 1f));
            main.gravityModifier = 0.03f;
            main.maxParticles = 60;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 4f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(40f, 1f, 3f);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
            vel.y = new ParticleSystem.MinMaxCurve(-0.5f, -0.3f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.4f;
            noise.frequency = 0.3f;
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-2f, 2f);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            Material leaf = SoftParticle("Street_Leaf", Color.white);
            renderer.sharedMaterial = leaf;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        // ───────────── Materials and helpers ─────────────

        private static Material SoftParticle(string name, Color color)
        {
            Material m = GreyboxAssets.AdditiveParticle(name, color);
            m.SetFloat("_Blend", 0f); // alpha blend, not additive: smoke and leaves have body
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetTexture("_BaseMap", Art.DustAssets.SmudgeTexture());
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material Water()
        {
            Material m = GreyboxAssets.Lit("Street_Water", new Color(0.35f, 0.55f, 0.6f, 0.75f));
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Smoothness", 0.95f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Awning stripes: cream and a colour, generated once per colour.</summary>
        private static Material Stripes(Color color)
        {
            string hex = ColorUtility.ToHtmlStringRGB(color);
            string path = $"{Art.ProceduralTextures.Folder}/Stripes_{hex}.png";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                const int size = 64;
                var pixels = new Color32[size * size];
                Color cream = new Color(0.97f, 0.94f, 0.86f);
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = (x / (size / 8)) % 2 == 0 ? color : cream;
                var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
                t.SetPixels32(pixels);
                t.Apply();
                Directory.CreateDirectory(Art.ProceduralTextures.Folder);
                File.WriteAllBytes(path, t.EncodeToPNG());
                Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(path);
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            Material m = GreyboxAssets.Lit($"Stripes_{hex}", Color.white);
            m.SetTexture("_BaseMap", texture);
            m.SetTextureScale("_BaseMap", new Vector2(1.2f, 1.2f));
            m.SetFloat("_Smoothness", 0.1f);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>A rounded box: local position under parent (or world position when world is set).</summary>
        private static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size, Material material, bool collider = false, bool world = false)
        {
            GameObject go = GreyboxAssets.Box(name, parent, world ? parent.InverseTransformPoint(position) : position, size, material, keepCollider: collider);
            if (world) go.transform.rotation = parent.rotation;
            GreyboxAssets.MarkStatic(go);
            return go;
        }

        private static GameObject Place(Transform parent, string name, Mesh mesh, Material material, Vector3 localPosition, bool collider)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            GreyboxAssets.MarkStatic(go);
            return go;
        }
    }
}
