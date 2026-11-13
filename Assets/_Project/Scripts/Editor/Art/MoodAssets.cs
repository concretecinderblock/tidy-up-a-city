using System.Collections.Generic;
using System.IO;
using TidyUp.Locations;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TidyUp.Editor.Art
{
    /// <summary>
    /// One location's atmosphere (Docs/ArtBible.md, "warm interiors"): warm ambient with lamp downlights doing the shaping,
    /// a little haze, cool daylight through the windows, and a filmic colour grade with gentle bloom.
    /// </summary>
    public sealed class MoodPreset
    {
        public string Name;
        public Color AmbientSky, AmbientEquator, AmbientGround;
        public Color Fog;
        public float FogDensity = 0.01f;
        public float Exposure = 0.3f;
        public float Contrast = 12f;
        public float Saturation = 2f;
        public float Temperature = 5f;
        public Color Filter = Color.white;
        public float BloomIntensity = 0.6f;
        public float Vignette = 0.25f;
        public Color LampColor = new Color(1f, 0.86f, 0.7f);   // warm white, not orange
        public float LampIntensity = 6f;
        public float LampRange = 9f;
        public int ShadowedLamps = 6;
        public Color WindowLight = new Color(0.7f, 0.82f, 1f);
        public float WindowLightStrength = 0.55f;

        /// <summary>Morning bakery: bright, golden, airy.</summary>
        public static MoodPreset Bakery => new MoodPreset
        {
            Name = "Bakery",
            AmbientSky = new Color(0.92f, 0.87f, 0.8f), AmbientEquator = new Color(0.85f, 0.79f, 0.7f), AmbientGround = new Color(0.56f, 0.49f, 0.42f),
            Fog = new Color(0.7f, 0.6f, 0.48f), FogDensity = 0.006f,
            Exposure = 0.4f, Temperature = 4f,
        };

        /// <summary>Evening library: amber pools of lamplight, the far end softer in the haze.</summary>
        public static MoodPreset Library => new MoodPreset
        {
            Name = "Library",
            AmbientSky = new Color(0.84f, 0.78f, 0.7f), AmbientEquator = new Color(0.78f, 0.7f, 0.6f), AmbientGround = new Color(0.48f, 0.41f, 0.34f),
            Fog = new Color(0.55f, 0.44f, 0.32f), FogDensity = 0.012f,
            Exposure = 0.35f, Contrast = 15f, Temperature = 8f, LampColor = new Color(1f, 0.82f, 0.62f),
            BloomIntensity = 0.75f, Vignette = 0.3f,
        };

        /// <summary>The street: golden late afternoon, warm haze down the far end, a soft filmic grade.</summary>
        public static MoodPreset Street => new MoodPreset
        {
            Name = "Street",
            Exposure = 0.1f, Contrast = 10f, Saturation = 8f, Temperature = 10f, Filter = new Color(1f, 0.97f, 0.92f),
            BloomIntensity = 0.55f, Vignette = 0.22f,
        };

        /// <summary>Evening magic shop: candle-warm light against soft purple shadows.</summary>
        public static MoodPreset FantasyShop => new MoodPreset
        {
            Name = "FantasyShop",
            AmbientSky = new Color(0.8f, 0.76f, 0.88f), AmbientEquator = new Color(0.74f, 0.68f, 0.78f), AmbientGround = new Color(0.44f, 0.39f, 0.48f),
            Fog = new Color(0.42f, 0.35f, 0.5f), FogDensity = 0.012f,
            Exposure = 0.35f, Contrast = 15f, Saturation = 6f, Temperature = 3f, LampColor = new Color(1f, 0.82f, 0.64f),
            BloomIntensity = 0.85f, Vignette = 0.3f,
            WindowLight = new Color(0.6f, 0.7f, 1f), WindowLightStrength = 0.45f,
        };
    }

    public static class MoodAssets
    {
        private const string Folder = "Assets/_Project/Art/Mood";

        /// <summary>
        /// Adds the interior mood to a location: post-processing volume, a box-projected reflection probe and the
        /// InteriorMood switch, and retunes every lamp in the scene (warm, brighter, a few with soft shadows).
        /// </summary>
        /// <summary>A global post volume for an outdoor scene (lower priority than interiors, which override it).</summary>
        public static Volume OutdoorVolume(Transform parent, MoodPreset m)
        {
            var go = new GameObject($"Mood {m.Name}");
            go.transform.SetParent(parent, false);
            Volume volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = Profile(m);
            return volume;
        }

        public static InteriorMood Build(Transform parent, MoodPreset m, Vector2 roomSize, float wallHeight)
        {
            var go = new GameObject("Interior Mood");
            go.transform.SetParent(parent, false);

            Volume volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 50f;
            volume.sharedProfile = Profile(m);
            volume.enabled = false; // InteriorMood switches it on while the location is loaded

            var probeGo = new GameObject("Reflection Probe");
            probeGo.transform.SetParent(go.transform, false);
            probeGo.transform.localPosition = new Vector3(0f, wallHeight * 0.5f, 0f);
            ReflectionProbe probe = probeGo.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.boxProjection = true;
            probe.size = new Vector3(roomSize.x, wallHeight, roomSize.y);
            probe.resolution = 128;
            probe.hdr = true;

            InteriorMood mood = go.AddComponent<InteriorMood>();
            mood.EditorConfigure(m.AmbientSky, m.AmbientEquator, m.AmbientGround, m.Fog, m.FogDensity, volume, probe, 0.7f);

            TuneLamps(m);
            return mood;
        }

        /// <summary>Warm lamplight; a few lamps spread through the room cast soft shadows.</summary>
        private static void TuneLamps(MoodPreset m)
        {
            var lamps = new List<Light>();
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                // Only lamp lights ("Light"): candle sconces stay soft unshadowed point lights.
                if ((light.type == LightType.Point || light.type == LightType.Spot) && light.name == "Light" && light.GetComponentInParent<InteriorMood>() == null) lamps.Add(light);
            }
            lamps.Sort((a, b) => string.CompareOrdinal(a.transform.parent != null ? a.transform.parent.name : a.name,
                                                      b.transform.parent != null ? b.transform.parent.name : b.name));
            int every = lamps.Count <= m.ShadowedLamps ? 1 : Mathf.CeilToInt((float)lamps.Count / m.ShadowedLamps);
            for (int i = 0; i < lamps.Count; i++)
            {
                Light light = lamps[i];
                // A wide downlight: the shade throws the light onto floor and shelves, not the ceiling (no hot
                // spots up there), and a spot's shadow is one map instead of a point light's six.
                light.type = LightType.Spot;
                light.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                light.spotAngle = 150f;
                light.innerSpotAngle = 70f;
                light.color = m.LampColor;
                light.intensity = m.LampIntensity;
                light.range = m.LampRange;
                bool shadows = i % every == every / 2;
                light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
                light.shadowStrength = 0.85f;
                light.shadowNearPlane = 0.15f;
                EditorUtility.SetDirty(light);
            }
        }

        /// <summary>The colour grade (rebuilt on every build so preset tweaks always land).</summary>
        private static VolumeProfile Profile(MoodPreset m)
        {
            EditorFolders.Ensure(Folder);
            string path = $"{Folder}/Mood_{m.Name}.asset";
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(path) != null) AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            T Add<T>() where T : VolumeComponent
            {
                T c = profile.Add<T>(true);
                c.name = typeof(T).Name;
                AssetDatabase.AddObjectToAsset(c, profile);
                return c;
            }

            Add<Tonemapping>().mode.Override(TonemappingMode.ACES);
            ColorAdjustments color = Add<ColorAdjustments>();
            color.postExposure.Override(m.Exposure);
            color.contrast.Override(m.Contrast);
            color.saturation.Override(m.Saturation);
            color.colorFilter.Override(m.Filter);
            WhiteBalance balance = Add<WhiteBalance>();
            balance.temperature.Override(m.Temperature);
            ShadowsMidtonesHighlights smh = Add<ShadowsMidtonesHighlights>();
            smh.shadows.Override(new Vector4(0.95f, 0.98f, 1.05f, 0f));   // a touch of cool in the shadows
            smh.highlights.Override(new Vector4(1.04f, 1f, 0.94f, 0f));   // warm highlights
            Bloom bloom = Add<Bloom>();
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(m.BloomIntensity);
            bloom.scatter.Override(0.72f);
            bloom.tint.Override(new Color(1f, 0.88f, 0.72f));
            Vignette vignette = Add<Vignette>();
            vignette.intensity.Override(m.Vignette);
            vignette.smoothness.Override(0.45f);
            vignette.color.Override(new Color(0.12f, 0.07f, 0.04f));
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        // ───────────── Window light: a soft beam and a bright patch on the floor ─────────────

        /// <summary>
        /// Fakes daylight through a window (the walls are solid): a soft additive beam slanting down into the room
        /// and a bright patch with the mullions' cross where it lands. window: local -Z faces the room.
        /// </summary>
        public static void AddWindowLight(Transform window, Vector2 size, Color light, float strength)
        {
            float floorY = -window.position.y;              // floor height in window space (window has no tilt)
            Vector3 dir = new Vector3(0f, -0.62f, -0.78f);  // into the room and down, as from a high sun
            dir.Normalize();
            float hw = size.x * 0.5f, hh = size.y * 0.5f;
            var near = new[] { new Vector3(-hw, -hh, -0.02f), new Vector3(hw, -hh, -0.02f), new Vector3(hw, hh, -0.02f), new Vector3(-hw, hh, -0.02f) };
            var far = new Vector3[4];
            for (int i = 0; i < 4; i++)
            {
                float t = (floorY - near[i].y) / dir.y;     // walk along the beam to the floor
                far[i] = near[i] + dir * t;
                far[i].y = floorY + 0.01f;
            }

            Material beam = Additive("Window_Beam", light * strength * 0.35f, BeamTexture());
            var beamGo = new GameObject("Window Beam");
            beamGo.transform.SetParent(window, false);
            beamGo.AddComponent<MeshFilter>().sharedMesh = BeamMesh(near, far);
            var beamRenderer = beamGo.AddComponent<MeshRenderer>();
            beamRenderer.sharedMaterial = beam;
            beamRenderer.shadowCastingMode = ShadowCastingMode.Off;
            beamRenderer.receiveShadows = false;

            Material patch = Additive("Window_Patch", light * strength, PatchTexture());
            var patchGo = new GameObject("Window Patch");
            patchGo.transform.SetParent(window, false);
            patchGo.AddComponent<MeshFilter>().sharedMesh = PatchMesh(far);
            var patchRenderer = patchGo.AddComponent<MeshRenderer>();
            patchRenderer.sharedMaterial = patch;
            patchRenderer.shadowCastingMode = ShadowCastingMode.Off;
            patchRenderer.receiveShadows = false;
        }

        /// <summary>The four slanted sides of the beam; v runs from the window (0) to the floor (1).</summary>
        private static Mesh BeamMesh(Vector3[] near, Vector3[] far)
        {
            var mesh = new Mesh { name = "Window_Beam" };
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int side = 0; side < 4; side++)
            {
                int a = side, b = (side + 1) % 4, start = verts.Count;
                verts.Add(near[a]); uvs.Add(new Vector2(0f, 0f));
                verts.Add(near[b]); uvs.Add(new Vector2(1f, 0f));
                verts.Add(far[b]); uvs.Add(new Vector2(1f, 1f));
                verts.Add(far[a]); uvs.Add(new Vector2(0f, 1f));
                tris.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return SaveMesh(mesh);
        }

        private static Mesh PatchMesh(Vector3[] far)
        {
            var mesh = new Mesh { name = "Window_Patch" };
            mesh.vertices = far;
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return SaveMesh(mesh);
        }

        private static Mesh SaveMesh(Mesh mesh)
        {
            // One shape per window size and height; same key = same asset.
            Vector3 b = mesh.bounds.size;
            string key = $"{mesh.name}_v1_{Mathf.RoundToInt(b.x * 100)}_{Mathf.RoundToInt(b.y * 100)}_{Mathf.RoundToInt(b.z * 100)}";
            mesh.name = key;
            return MeshAssets.GetOrCreate(key, () => mesh, rebuild: true);
        }

        private static Material Additive(string name, Color color, Texture2D texture)
        {
            Material material = GreyboxAssets.AdditiveParticle(name, color);
            material.SetTexture("_BaseMap", texture);
            material.SetFloat("_Cull", 0f); // both sides: the beam is seen from inside and out
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Soft beam: bright at the window, fading toward the floor, feathered at the edges.</summary>
        private static Texture2D BeamTexture() => SoftTexture("Light_Beam", 64, 128, (u, v) =>
        {
            float across = Mathf.Sin(u * Mathf.PI);
            float along = Mathf.Pow(1f - v, 1.6f) * Mathf.SmoothStep(0f, 1f, v * 8f);
            float dust = 0.85f + 0.15f * Mathf.PerlinNoise(u * 6f, v * 14f);
            return across * across * along * dust;
        });

        /// <summary>Window-shaped light on the floor: four panes with the mullion cross, soft edges.</summary>
        private static Texture2D PatchTexture() => SoftTexture("Light_Patch", 128, 128, (u, v) =>
        {
            float edge = Mathf.SmoothStep(0f, 1f, Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v)) * 8f);
            float crossU = Mathf.SmoothStep(0f, 1f, Mathf.Abs(u - 0.5f) * 30f);
            float crossV = Mathf.SmoothStep(0f, 1f, Mathf.Abs(v - 0.55f) * 30f);
            return edge * Mathf.Min(crossU, crossV) * (0.75f + 0.25f * (1f - v));
        });

        private static Texture2D SoftTexture(string name, int w, int h, System.Func<float, float, float> alpha)
        {
            string path = $"{ProceduralTextures.Folder}/{name}.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;
            Directory.CreateDirectory(ProceduralTextures.Folder);
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float a = Mathf.Clamp01(alpha((x + 0.5f) / w, (y + 0.5f) / h));
                pixels[y * w + x] = new Color(1f, 1f, 1f, a);
            }
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer != null)
            {
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
