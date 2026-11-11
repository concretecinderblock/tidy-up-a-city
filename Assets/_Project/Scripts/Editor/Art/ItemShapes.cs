using System.Collections.Generic;
using TidyUp.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace TidyUp.Editor.Art
{
    /// <summary>
    /// Builds an item's look from its CSV greybox spec: one mesh (several submeshes for multi-material items
    /// like books, bottles and candles) and its materials, fitted exactly to the spec size, bottom-centred.
    /// One renderer per item keeps thousands of items cheap.
    /// Shapes: cube, sphere, cylinder, loaf, baguette, croissant, egg, crystal, book, bottle, candle, cup,
    /// wand, globe (capsule / capsule_x map to cylinder / baguette-like rods); bakery: bun, bagel, donut, cookie,
    /// macaron, muffin, cupcake, cake, pie, jar, sack.
    /// </summary>
    public static class ItemShapes
    {
        private struct Part
        {
            public Mesh Mesh;
            public Matrix4x4 Transform;
            public int Material; // index into the material list
        }

        public static void Build(string id, GreyboxSpec spec, out Mesh mesh, out Material[] materials)
        {
            var parts = new List<Part>();
            var mats = new List<Material>();
            Material main = Lit(spec.Color, Smoothness(spec.Shape));
            mats.Add(main);
            Matrix4x4 identity = Matrix4x4.identity;

            switch (spec.Shape)
            {
                case "loaf": parts.Add(P(MeshGen.Loaf(), identity, 0)); break;
                case "baguette":
                case "capsule_x": parts.Add(P(MeshGen.Baguette(), identity, 0)); break;
                case "croissant": parts.Add(P(MeshGen.Croissant(), identity, 0)); break;
                case "egg": parts.Add(P(MeshGen.Egg(), identity, 0)); break;
                case "crystal": parts.Add(P(MeshGen.Crystal(), identity, 0)); break;
                case "sphere": parts.Add(P(MeshGen.DeformedSphere(p => p), identity, 0)); break;

                case "book":
                case "book_flat":
                {
                    // Unit book: x = thickness, y = height, z = depth; spine at -Z faces out of the shelf.
                    mats.Add(Lit(Palette.Cream, 0.05f)); // pages
                    const float board = 0.06f;
                    parts.Add(P(MeshGen.RoundedBox(new Vector3(1f, 1f, board), 0.02f, 2), Matrix4x4.Translate(new Vector3(0f, 0f, -0.5f + board * 0.5f)), 0));
                    parts.Add(P(MeshGen.RoundedBox(new Vector3(board, 1f, 1f), 0.02f, 2), Matrix4x4.Translate(new Vector3(-0.5f + board * 0.5f, 0f, 0f)), 0));
                    parts.Add(P(MeshGen.RoundedBox(new Vector3(board, 1f, 1f), 0.02f, 2), Matrix4x4.Translate(new Vector3(0.5f - board * 0.5f, 0f, 0f)), 0));
                    parts.Add(P(MeshGen.RoundedBox(new Vector3(1f - board * 2f, 0.95f, 0.95f - board), 0.01f, 1), Matrix4x4.Translate(new Vector3(0f, -0.01f, board * 0.5f)), 1));
                    break;
                }

                case "book_lite":
                {
                    // Library-scale book (thousands on screen): one bevelled cover block and a page strip showing at
                    // the fore-edge (+Z; the spine at -Z faces out of the shelf). About 100 triangles.
                    mats.Add(Lit(Palette.Cream, 0.05f)); // pages
                    parts.Add(P(MeshGen.RoundedBox(Vector3.one, 0.06f, 1), identity, 0));
                    parts.Add(P(MeshGen.RoundedBox(new Vector3(0.84f, 0.92f, 0.1f), 0.01f, 1), Matrix4x4.Translate(new Vector3(0f, 0f, 0.46f)), 1));
                    break;
                }

                case "bottle":
                {
                    mats[0] = Lit(spec.Color, 0.85f); // glassy
                    mats.Add(Lit(Palette.Walnut, 0.2f)); // cork
                    parts.Add(P(MeshGen.Lathe(new[]
                    {
                        V(0f, 0f), V(0.4f, 0f), V(0.5f, 0.06f), V(0.5f, 0.55f), V(0.42f, 0.68f), V(0.2f, 0.76f),
                        V(0.16f, 0.86f), V(0.19f, 0.9f), V(0.16f, 0.93f), V(0f, 0.93f),
                    }, 20, "Bottle"), identity, 0));
                    parts.Add(P(MeshGen.Lathe(new[] { V(0f, 0.9f), V(0.14f, 0.9f), V(0.15f, 1f), V(0f, 1f) }, 12, "Cork"), identity, 1));
                    break;
                }

                case "candle":
                {
                    mats[0] = Lit(spec.Color, 0.35f);
                    mats.Add(Lit(Palette.Ink, 0.1f)); // wick
                    mats.Add(Glow(Palette.Amber));    // flame
                    parts.Add(P(MeshGen.Lathe(new[] { V(0f, 0f), V(0.5f, 0f), V(0.5f, 0.78f), V(0.42f, 0.82f), V(0f, 0.8f) }, 20, "Candle"), identity, 0));
                    parts.Add(P(MeshGen.Lathe(new[] { V(0f, 0.8f), V(0.04f, 0.8f), V(0.04f, 0.86f), V(0f, 0.86f) }, 8, "Wick"), identity, 1));
                    Mesh flame = MeshGen.Egg();
                    parts.Add(P(flame, Matrix4x4.TRS(new Vector3(0f, 0.93f, 0f), Quaternion.identity, new Vector3(0.18f, 0.12f, 0.18f)), 2));
                    break;
                }

                case "cup":
                    parts.Add(P(MeshGen.Lathe(new[]
                    {
                        V(0f, 0f), V(0.32f, 0f), V(0.4f, 0.08f), V(0.5f, 0.75f), V(0.5f, 1f), V(0.44f, 1f), V(0.42f, 0.2f), V(0f, 0.2f),
                    }, 24, "Cup"), identity, 0));
                    break;

                case "wand":
                {
                    Mesh wand = MeshGen.Lathe(new[]
                    {
                        V(0f, 0f), V(0.07f, 0.02f), V(0.08f, 0.28f), V(0.06f, 0.33f), V(0.035f, 0.98f), V(0f, 1f),
                    }, 10, "Wand");
                    MeshGen.Rotate(wand, Quaternion.Euler(0f, 0f, -90f)); // lie along X
                    parts.Add(P(wand, identity, 0));
                    break;
                }

                case "globe":
                    mats[0] = Lit(spec.Color, 0.9f);
                    mats.Add(Lit(Palette.Walnut, 0.25f));
                    parts.Add(P(MeshGen.DeformedSphere(p => p * 0.9f + Vector3.up * 0.55f), identity, 0));
                    parts.Add(P(MeshGen.Lathe(new[] { V(0f, 0f), V(0.42f, 0f), V(0.45f, 0.08f), V(0.3f, 0.2f), V(0f, 0.2f) }, 20, "GlobeBase"), identity, 1));
                    break;

                // ───── Bakery shapes (step 4) — low segment counts: thousands of these share a room ─────

                case "bun": // rolls and buns: a soft dome
                    parts.Add(P(MeshGen.DeformedSphere(p => new Vector3(p.x, p.y < 0f ? p.y * 0.35f : p.y, p.z), 10, 16, "Bun"), identity, 0));
                    break;

                case "bagel":
                    parts.Add(P(MeshGen.Lathe(TorusProfile(0.3f, 0.2f, 10), 16, "Bagel"), identity, 0));
                    break;

                case "donut": // dough ring with icing in the item colour on top
                    mats[0] = Lit(new Color(0.85f, 0.6f, 0.33f), 0.25f);
                    mats.Add(Lit(spec.Color, 0.55f));
                    parts.Add(P(MeshGen.Lathe(TorusProfile(0.3f, 0.2f, 10), 16, "Donut"), identity, 0));
                    parts.Add(P(MeshGen.Lathe(TorusTopProfile(0.3f, 0.215f, 6), 16, "Icing"), identity, 1));
                    break;

                case "cookie":
                    parts.Add(P(MeshGen.Lathe(new[] { V(0f, 0f), V(0.46f, 0f), V(0.5f, 0.35f), V(0.46f, 0.85f), V(0.3f, 1f), V(0f, 1f) }, 16, "Cookie"), identity, 0));
                    break;

                case "macaron": // two shells and a cream filling
                    mats.Add(Lit(Palette.Cream, 0.2f));
                    parts.Add(P(MeshGen.Lathe(new[] { V(0f, 0f), V(0.45f, 0f), V(0.5f, 0.12f), V(0.48f, 0.38f), V(0f, 0.38f) }, 16, "ShellLow"), identity, 0));
                    parts.Add(P(MeshGen.Lathe(new[] { V(0f, 0.36f), V(0.44f, 0.36f), V(0.44f, 0.62f), V(0f, 0.62f) }, 16, "Filling"), identity, 1));
                    parts.Add(P(MeshGen.Lathe(new[] { V(0f, 0.6f), V(0.48f, 0.6f), V(0.5f, 0.8f), V(0.4f, 0.96f), V(0f, 1f) }, 16, "ShellTop"), identity, 0));
                    break;

                case "muffin": // paper case, golden dome in the item colour
                    mats.Add(Lit(Palette.Cream, 0.15f));
                    parts.Add(P(MeshGen.Lathe(new[] { V(0f, 0f), V(0.34f, 0f), V(0.42f, 0.55f), V(0f, 0.55f) }, 16, "Case"), identity, 1));
                    parts.Add(P(MeshGen.Lathe(new[] { V(0f, 0.5f), V(0.44f, 0.5f), V(0.5f, 0.62f), V(0.44f, 0.84f), V(0.25f, 0.97f), V(0f, 1f) }, 16, "Top"), identity, 0));
                    break;

                case "cupcake": // paper case, sponge, frosting swirl in the item colour
                    mats.Add(Lit(Palette.Cream, 0.15f));
                    mats.Add(Lit(new Color(0.86f, 0.64f, 0.38f), 0.25f));
                    parts.Add(P(MeshGen.Lathe(new[] { V(0f, 0f), V(0.32f, 0f), V(0.4f, 0.42f), V(0f, 0.42f) }, 16, "Case"), identity, 1));
                    parts.Add(P(MeshGen.Lathe(new[] { V(0f, 0.4f), V(0.42f, 0.4f), V(0.44f, 0.5f), V(0f, 0.5f) }, 16, "Sponge"), identity, 2));
                    parts.Add(P(MeshGen.Lathe(new[]
                    {
                        V(0f, 0.48f), V(0.46f, 0.48f), V(0.5f, 0.58f), V(0.38f, 0.66f), V(0.4f, 0.74f), V(0.28f, 0.82f), V(0.28f, 0.88f), V(0.12f, 0.96f), V(0f, 1f),
                    }, 16, "Frosting"), identity, 0));
                    break;

                case "cake": // whole cake: sponge in the item colour, cream top and rim
                    mats.Add(Lit(Palette.Cream, 0.3f));
                    parts.Add(P(MeshGen.Lathe(new[] { V(0f, 0f), V(0.48f, 0f), V(0.5f, 0.04f), V(0.5f, 0.8f), V(0f, 0.8f) }, 24, "Sponge"), identity, 0));
                    parts.Add(P(MeshGen.Lathe(new[] { V(0f, 0.78f), V(0.51f, 0.78f), V(0.52f, 0.86f), V(0.47f, 0.92f), V(0f, 0.92f) }, 24, "Cream"), identity, 1));
                    parts.Add(P(MeshGen.Lathe(TorusProfile(0.38f, 0.05f, 6, 0.9f), 20, "Rim"), identity, 1));
                    break;

                case "pie": // dish and a golden domed crust in the item colour
                    mats.Add(Lit(Palette.Terracotta, 0.35f));
                    parts.Add(P(MeshGen.Lathe(new[] { V(0f, 0f), V(0.4f, 0f), V(0.5f, 0.45f), V(0.47f, 0.47f), V(0f, 0.4f) }, 20, "Dish"), identity, 1));
                    parts.Add(P(MeshGen.Lathe(new[] { V(0f, 0.38f), V(0.5f, 0.45f), V(0.5f, 0.55f), V(0.4f, 0.8f), V(0.2f, 0.95f), V(0f, 1f) }, 20, "Crust"), identity, 0));
                    break;

                case "jar": // glass jar showing its contents (item colour) with a cloth-topped lid
                    mats[0] = Lit(spec.Color, 0.7f);
                    mats.Add(Lit(Palette.Terracotta, 0.2f));
                    parts.Add(P(MeshGen.Lathe(new[] { V(0f, 0f), V(0.42f, 0f), V(0.48f, 0.06f), V(0.48f, 0.72f), V(0.38f, 0.8f), V(0f, 0.8f) }, 16, "Jar"), identity, 0));
                    parts.Add(P(MeshGen.Lathe(new[] { V(0f, 0.78f), V(0.45f, 0.78f), V(0.5f, 0.84f), V(0.4f, 1f), V(0f, 1f) }, 16, "Lid"), identity, 1));
                    break;

                case "sack": // soft sack with a tied neck
                    mats.Add(Lit(Palette.Ink, 0.1f));
                    parts.Add(P(MeshGen.DeformedSphere(p =>
                    {
                        // Squarish body that narrows into a neck at the top.
                        var q = new Vector3(Mathf.Sign(p.x) * Mathf.Pow(Mathf.Abs(p.x * 2f), 0.6f) * 0.5f, p.y, Mathf.Sign(p.z) * Mathf.Pow(Mathf.Abs(p.z * 2f), 0.6f) * 0.5f);
                        float neck = p.y > 0.3f ? Mathf.Lerp(1f, 0.35f, (p.y - 0.3f) / 0.2f) : 1f;
                        return new Vector3(q.x * neck, p.y * 0.85f, q.z * neck);
                    }, 10, 16, "Sack"), identity, 0));
                    parts.Add(P(MeshGen.Lathe(TorusProfile(0.12f, 0.035f, 6), 12, "Tie"), Matrix4x4.Translate(new Vector3(0f, 0.27f, 0f)), 1));
                    break;

                case "cylinder":
                case "capsule":
                    parts.Add(P(MeshGen.Lathe(new[] { V(0f, 0f), V(0.45f, 0f), V(0.5f, 0.05f), V(0.5f, 0.95f), V(0.45f, 1f), V(0f, 1f) }, 20, "Cylinder"), identity, 0));
                    break;

                default: // cube
                    parts.Add(P(MeshGen.RoundedBox(Vector3.one, 0.12f, 3), identity, 0));
                    break;
            }

            mesh = Combine(id, parts, mats.Count);
            if (spec.Shape == "book_flat") MeshGen.Rotate(mesh, Quaternion.Euler(0f, 0f, 90f)); // lying on its side
            MeshGen.FitToBottomCentre(mesh, spec.Size);
            materials = mats.ToArray();
        }

        private static Part P(Mesh mesh, Matrix4x4 transform, int material) => new Part { Mesh = mesh, Transform = transform, Material = material };
        private static Vector2 V(float radius, float height) => new Vector2(radius, height);

        /// <summary>
        /// A circle in (radius, height), counter-clockwise from its lowest point, for lathing a torus (bagel,
        /// donut, rims). Ring radius R, tube radius r, resting on y = 0 (scaled by heightScale).
        /// </summary>
        private static Vector2[] TorusProfile(float ringRadius, float tubeRadius, int steps, float baseHeight = 0f)
        {
            var points = new Vector2[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                float a = -Mathf.PI * 0.5f + i * Mathf.PI * 2f / steps;
                points[i] = new Vector2(ringRadius + tubeRadius * Mathf.Cos(a), baseHeight + tubeRadius + tubeRadius * Mathf.Sin(a));
            }
            return points;
        }

        /// <summary>The upper half of a torus tube (icing on a donut), outer edge to inner edge over the top.</summary>
        private static Vector2[] TorusTopProfile(float ringRadius, float tubeRadius, int steps)
        {
            var points = new Vector2[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                float a = i * Mathf.PI / steps; // 0 = outer, PI = inner
                points[i] = new Vector2(ringRadius + tubeRadius * Mathf.Cos(a), 0.2f + tubeRadius * Mathf.Sin(a) + 0.005f);
            }
            return points;
        }

        private static float Smoothness(string shape)
        {
            switch (shape)
            {
                case "crystal": return 0.8f;
                case "egg": return 0.45f;
                case "loaf":
                case "baguette":
                case "croissant":
                case "bun":
                case "bagel":
                case "cookie":
                case "muffin":
                case "cupcake":
                case "cake":
                case "pie": return 0.25f;
                case "macaron": return 0.35f;
                case "sack": return 0.08f;
                default: return 0.3f;
            }
        }

        /// <summary>Merges parts into one mesh, one submesh per material.</summary>
        private static Mesh Combine(string id, List<Part> parts, int materialCount)
        {
            var perMaterial = new Mesh[materialCount];
            for (int m = 0; m < materialCount; m++)
            {
                var combine = new List<CombineInstance>();
                foreach (Part part in parts)
                {
                    if (part.Material == m) combine.Add(new CombineInstance { mesh = part.Mesh, transform = part.Transform });
                }
                var merged = new Mesh { indexFormat = IndexFormat.UInt32 };
                merged.CombineMeshes(combine.ToArray(), true, true);
                perMaterial[m] = merged;
            }

            var instances = new CombineInstance[materialCount];
            for (int m = 0; m < materialCount; m++) instances[m] = new CombineInstance { mesh = perMaterial[m], transform = Matrix4x4.identity };
            var mesh = new Mesh { name = "Item_" + id, indexFormat = IndexFormat.UInt32 };
            mesh.CombineMeshes(instances, false, false);

            foreach (Mesh m in perMaterial) Object.DestroyImmediate(m);
            foreach (Part part in parts) Object.DestroyImmediate(part.Mesh);
            return mesh;
        }

        private static Material Lit(Color color, float smoothness)
        {
            Material material = GreyboxAssets.Lit("Item_" + ColorUtility.ToHtmlStringRGB(color) + "_s" + Mathf.RoundToInt(smoothness * 100f), color);
            material.SetFloat("_Smoothness", smoothness);
            return material;
        }

        private static Material Glow(Color color) =>
            GreyboxAssets.Emissive("Item_Glow_" + ColorUtility.ToHtmlStringRGB(color), color, color, 3f);
    }
}
