using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TidyUp.Editor.Art
{
    /// <summary>
    /// Procedural meshes for the storybook look: rounded boxes (UVs in metres, so every texture has the same
    /// scale everywhere), lathe shapes (bottles, candles, cups) and soft organic items (loaves, croissants,
    /// baguettes, eggs, crystals). Pure geometry; MeshAssets saves and caches the results.
    /// </summary>
    public static class MeshGen
    {
        // ───────────── Rounded box ─────────────

        /// <summary>Box of the given size centred on the origin, edges rounded by radius. UVs are planar in metres.</summary>
        public static Mesh RoundedBox(Vector3 size, float radius, int segments = 3)
        {
            Vector3 h = size * 0.5f;
            radius = Mathf.Clamp(radius, 0f, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * 0.95f);
            segments = Mathf.Max(1, segments);
            float[] xs = AxisSamples(h.x, radius, segments);
            float[] ys = AxisSamples(h.y, radius, segments);
            float[] zs = AxisSamples(h.z, radius, segments);
            Vector3 inner = new Vector3(h.x - radius, h.y - radius, h.z - radius);

            var builder = new Builder();
            // Six faces: (fixed axis, sign, the two varying axes' samples).
            AddFace(builder, 0, +1, ys, zs, h, inner, radius);
            AddFace(builder, 0, -1, ys, zs, h, inner, radius);
            AddFace(builder, 1, +1, xs, zs, h, inner, radius);
            AddFace(builder, 1, -1, xs, zs, h, inner, radius);
            AddFace(builder, 2, +1, xs, ys, h, inner, radius);
            AddFace(builder, 2, -1, xs, ys, h, inner, radius);
            return builder.ToMesh("RoundedBox", recalculateNormals: false);
        }

        /// <summary>Coordinates along one axis: dense across the rounded edges, two points across the flat middle.</summary>
        private static float[] AxisSamples(float half, float radius, int segments)
        {
            var list = new List<float>();
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments; // -half .. -half+radius, eased like an arc
                list.Add(-half + radius * (1f - Mathf.Cos(t * Mathf.PI * 0.5f)));
            }
            for (int i = segments; i >= 0; i--)
            {
                float t = (float)i / segments;
                float value = half - radius * (1f - Mathf.Cos(t * Mathf.PI * 0.5f));
                if (value > list[list.Count - 1] + 1e-5f) list.Add(value);
            }
            return list.ToArray();
        }

        private static void AddFace(Builder b, int axis, int sign, float[] aSamples, float[] bSamples, Vector3 h, Vector3 inner, float radius)
        {
            int start = b.Vertices.Count;
            int na = aSamples.Length;
            int nb = bSamples.Length;
            int a1 = (axis + 1) % 3;
            int a2 = (axis + 2) % 3;
            // For each face the first varying axis is the lower index of the two others, for planar UVs.
            int ua = Mathf.Min(a1, a2);
            int ub = Mathf.Max(a1, a2);
            Vector3 outward = Vector3.zero;
            outward[axis] = sign;

            for (int j = 0; j < nb; j++)
            {
                for (int i = 0; i < na; i++)
                {
                    var p = new Vector3();
                    p[axis] = sign * h[axis];
                    p[ua] = aSamples[i];
                    p[ub] = bSamples[j];
                    // Round: push the point out from the inner box by the radius.
                    var core = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                    Vector3 n = p - core;
                    n = n.sqrMagnitude > 1e-10f ? n.normalized : outward;
                    Vector3 position = radius > 0f ? core + n * radius : p;
                    b.Vertices.Add(position);
                    b.Normals.Add(n);
                    b.Uvs.Add(new Vector2(position[ua], position[ub]));
                }
            }
            for (int j = 0; j < nb - 1; j++)
            {
                for (int i = 0; i < na - 1; i++)
                {
                    int v00 = start + j * na + i;
                    int v10 = v00 + 1;
                    int v01 = v00 + na;
                    int v11 = v01 + 1;
                    b.Triangle(v00, v01, v11, outward);
                    b.Triangle(v00, v11, v10, outward);
                }
            }
        }

        // ───────────── Lathe (surfaces of revolution) ─────────────

        /// <summary>
        /// Revolves a profile of (radius, height) points around Y. Order the profile from the bottom centre
        /// (radius 0) round the outside to the top centre (radius 0) so both ends are capped and faces point out.
        /// </summary>
        public static Mesh Lathe(IList<Vector2> profile, int segments = 24, string name = "Lathe")
        {
            var b = new Builder();
            int rings = profile.Count;
            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s <= segments; s++)
                {
                    float a = (float)s / segments * Mathf.PI * 2f;
                    b.Vertices.Add(new Vector3(Mathf.Cos(a) * profile[r].x, profile[r].y, Mathf.Sin(a) * profile[r].x));
                    b.Uvs.Add(new Vector2((float)s / segments, profile[r].y));
                }
            }
            int stride = segments + 1;
            for (int r = 0; r < rings - 1; r++)
            {
                for (int s = 0; s < segments; s++)
                {
                    int a = r * stride + s;
                    int c = a + stride;
                    if (profile[r].x < 1e-5f && profile[r + 1].x < 1e-5f) continue;
                    // Outward normal of the profile segment in (radius, height): (dh, -dr), revolved to this angle.
                    Vector2 d = profile[r + 1] - profile[r];
                    float angle = (s + 0.5f) / segments * Mathf.PI * 2f;
                    var outward = new Vector3(Mathf.Cos(angle) * d.y, -d.x, Mathf.Sin(angle) * d.y);
                    b.Triangle(a, c, c + 1, outward);
                    b.Triangle(a, c + 1, a + 1, outward);
                }
            }
            return b.ToMesh(name, recalculateNormals: true);
        }

        // ───────────── Deformed spheres (organic shapes) ─────────────

        /// <summary>Unit sphere (radius 0.5) with each vertex passed through deform, then normals recomputed.</summary>
        public static Mesh DeformedSphere(Func<Vector3, Vector3> deform, int latitude = 16, int longitude = 24, string name = "Sphere")
        {
            var b = new Builder();
            for (int lat = 0; lat <= latitude; lat++)
            {
                float theta = (float)lat / latitude * Mathf.PI;
                for (int lon = 0; lon <= longitude; lon++)
                {
                    float phi = (float)lon / longitude * Mathf.PI * 2f;
                    var p = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), -Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi)) * 0.5f;
                    b.Vertices.Add(deform(p));
                    b.Uvs.Add(new Vector2((float)lon / longitude, (float)lat / latitude));
                }
            }
            int stride = longitude + 1;
            for (int lat = 0; lat < latitude; lat++)
            {
                for (int lon = 0; lon < longitude; lon++)
                {
                    int a = lat * stride + lon;
                    int c = a + stride;
                    Vector3 centre = (b.Vertices[a] + b.Vertices[c + 1]) * 0.5f;
                    b.Triangle(a, c, c + 1, centre);
                    b.Triangle(a, c + 1, a + 1, centre);
                }
            }
            return b.ToMesh(name, recalculateNormals: true);
        }

        /// <summary>Bread loaf: domed top, flat soft bottom.</summary>
        public static Mesh Loaf() => DeformedSphere(p =>
        {
            float y = p.y < 0f ? p.y * 0.35f : p.y * (1f - 0.15f * Mathf.Abs(p.x * 2f));
            return new Vector3(p.x, y, p.z);
        }, 14, 28, "Loaf");

        /// <summary>Baguette: long, slightly flattened, with a row of diagonal score bumps along the top.</summary>
        public static Mesh Baguette() => DeformedSphere(p =>
        {
            float x = p.x * 2f; // stretched below by the fit; bumps along X
            float score = p.y > 0.15f ? Mathf.Abs(Mathf.Sin((x * 5f + p.z * 3f) * Mathf.PI)) * 0.03f : 0f;
            float y = p.y < 0f ? p.y * 0.55f : p.y - score;
            float taper = 1f - 0.25f * Mathf.Pow(Mathf.Abs(x), 4f);
            return new Vector3(p.x, y * taper, p.z * taper);
        }, 12, 40, "Baguette");

        /// <summary>Egg: narrower at the top.</summary>
        public static Mesh Egg() => DeformedSphere(p =>
        {
            float squeeze = 1f - (p.y + 0.5f) * 0.28f;
            return new Vector3(p.x * squeeze, p.y * 1.25f, p.z * squeeze);
        }, 16, 24, "Egg");

        /// <summary>Croissant: a crescent tube, thick in the middle, tapered tips, ridged segments, flat bottom.</summary>
        public static Mesh Croissant(int along = 40, int around = 16)
        {
            var b = new Builder();
            const float arc = 150f * Mathf.Deg2Rad;
            const float bend = 0.5f;
            for (int i = 0; i <= along; i++)
            {
                float t = (float)i / along;
                float angle = (t - 0.5f) * arc;
                var centre = new Vector3(Mathf.Sin(angle) * bend, 0f, Mathf.Cos(angle) * bend - bend);
                var tangent = new Vector3(Mathf.Cos(angle), 0f, -Mathf.Sin(angle));
                Vector3 side = Vector3.Cross(Vector3.up, tangent).normalized;
                // Max(0, …): Sin(π) is a tiny negative number, and Pow(negative, 0.8) is NaN.
                float thickness = 0.06f + 0.22f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * Mathf.PI)), 0.8f);
                thickness *= 1f + 0.12f * Mathf.Abs(Mathf.Sin(t * Mathf.PI * 6f)); // rolled ridges
                for (int j = 0; j <= around; j++)
                {
                    float a = (float)j / around * Mathf.PI * 2f;
                    float y = Mathf.Sin(a) * thickness;
                    if (y < 0f) y *= 0.4f; // flat underside
                    b.Vertices.Add(centre + side * (Mathf.Cos(a) * thickness) + Vector3.up * y);
                    b.Uvs.Add(new Vector2(t, (float)j / around));
                }
            }
            int stride = around + 1;
            for (int i = 0; i < along; i++)
            {
                for (int j = 0; j < around; j++)
                {
                    int a = i * stride + j;
                    int c = a + stride;
                    float t = (i + 0.5f) / along;
                    float angle = (t - 0.5f) * arc;
                    var centre = new Vector3(Mathf.Sin(angle) * bend, 0f, Mathf.Cos(angle) * bend - bend);
                    Vector3 mid = (b.Vertices[a] + b.Vertices[c + 1]) * 0.5f;
                    b.Triangle(a, c, c + 1, mid - centre);
                    b.Triangle(a, c + 1, a + 1, mid - centre);
                }
            }
            return b.ToMesh("Croissant", recalculateNormals: true);
        }

        /// <summary>Faceted crystal: six-sided prism with a pointed top and a short point below. Flat-shaded.</summary>
        public static Mesh Crystal()
        {
            var ring = new List<Vector3>();
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2f;
                ring.Add(new Vector3(Mathf.Cos(a) * 0.5f, 0f, Mathf.Sin(a) * 0.5f));
            }
            var b = new Builder();
            var top = new Vector3(0f, 1f, 0f);
            var bottom = new Vector3(0f, -0.15f, 0f);
            const float shoulder = 0.62f;
            for (int i = 0; i < 6; i++)
            {
                Vector3 r0 = ring[i];
                Vector3 r1 = ring[(i + 1) % 6];
                Vector3 s0 = r0 + Vector3.up * shoulder;
                Vector3 s1 = r1 + Vector3.up * shoulder;
                Vector3 outward = (r0 + r1).normalized;
                b.FlatQuad(r0, s0, s1, r1, outward);
                b.FlatTriangle(s0, top, s1, outward + Vector3.up);
                b.FlatTriangle(r0, r1, bottom, outward + Vector3.down);
            }
            return b.ToMesh("Crystal", recalculateNormals: false);
        }

        /// <summary>Scales and moves a mesh so its bounds become exactly size, bottom-centred at the origin.</summary>
        public static void FitToBottomCentre(Mesh mesh, Vector3 size)
        {
            mesh.RecalculateBounds();
            Bounds b = mesh.bounds;
            var vertices = new List<Vector3>();
            mesh.GetVertices(vertices);
            Vector3 scale = new Vector3(
                b.size.x > 1e-6f ? size.x / b.size.x : 1f,
                b.size.y > 1e-6f ? size.y / b.size.y : 1f,
                b.size.z > 1e-6f ? size.z / b.size.z : 1f);
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 v = vertices[i] - new Vector3(b.center.x, b.min.y, b.center.z);
                vertices[i] = Vector3.Scale(v, scale);
            }
            mesh.SetVertices(vertices);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
        }

        /// <summary>Rotates all vertices (e.g. lay a vertical lathe shape along X).</summary>
        public static void Rotate(Mesh mesh, Quaternion rotation)
        {
            var vertices = new List<Vector3>();
            mesh.GetVertices(vertices);
            for (int i = 0; i < vertices.Count; i++) vertices[i] = rotation * vertices[i];
            mesh.SetVertices(vertices);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        // ───────────── Extrusion (mouldings, rails, cornices) ─────────────

        /// <summary>
        /// Sweeps a moulding profile along X, centred, length metres long. Profile points are (out, up): "out" is
        /// the distance from the wall into the room, which becomes -Z. Order them from the bottom at the wall,
        /// round the outside, to the top at the wall, so faces point into the room. Smooth shading; UVs in metres
        /// (x along the run, v along the profile). Ends are open: runs meet walls or each other in corners.
        /// </summary>
        public static Mesh Extrude(IList<Vector2> profile, float length, string name = "Extrusion")
        {
            var b = new Builder();
            float half = length * 0.5f;
            float run = 0f;
            for (int i = 0; i < profile.Count; i++)
            {
                if (i > 0) run += Vector2.Distance(profile[i], profile[i - 1]);
                foreach (float x in new[] { -half, half })
                {
                    b.Vertices.Add(new Vector3(x, profile[i].y, -profile[i].x));
                    b.Uvs.Add(new Vector2(x, run));
                }
            }
            for (int i = 0; i < profile.Count - 1; i++)
            {
                Vector2 t = profile[i + 1] - profile[i];
                var outward = new Vector3(0f, -t.x, -t.y); // (out, up) normal (t.y, -t.x), with out mapped to -Z
                int a = i * 2;
                b.Triangle(a, a + 1, a + 3, outward);
                b.Triangle(a, a + 3, a + 2, outward);
            }
            return b.ToMesh(name, recalculateNormals: true);
        }

        // ───────────── Builder ─────────────

        private sealed class Builder
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<Vector2> Uvs = new List<Vector2>();
            private readonly List<int> _triangles = new List<int>();

            /// <summary>
            /// Adds a triangle wound so its front faces the outward direction (Unity: the cross product of
            /// (b-a, c-a) points to the viewer of the front face). Degenerate triangles are kept as-is.
            /// </summary>
            public void Triangle(int a, int b, int c, Vector3 outward)
            {
                Vector3 pa = Vertices[a];
                Vector3 n = Vector3.Cross(Vertices[b] - pa, Vertices[c] - pa);
                if (Vector3.Dot(n, outward) < 0f)
                {
                    int t = b;
                    b = c;
                    c = t;
                }
                _triangles.Add(a);
                _triangles.Add(b);
                _triangles.Add(c);
            }

            public void FlatTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
            {
                int i = Vertices.Count;
                Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                if (Vector3.Dot(n, outward) < 0f)
                {
                    Vector3 t = b;
                    b = c;
                    c = t;
                    n = -n;
                }
                Vertices.Add(a);
                Vertices.Add(b);
                Vertices.Add(c);
                Normals.Add(n);
                Normals.Add(n);
                Normals.Add(n);
                Uvs.Add(new Vector2(a.x + a.z, a.y));
                Uvs.Add(new Vector2(b.x + b.z, b.y));
                Uvs.Add(new Vector2(c.x + c.z, c.y));
                _triangles.Add(i);
                _triangles.Add(i + 1);
                _triangles.Add(i + 2);
            }

            public void FlatQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward)
            {
                FlatTriangle(a, b, c, outward);
                FlatTriangle(a, c, d, outward);
            }

            public Mesh ToMesh(string name, bool recalculateNormals)
            {
                var mesh = new Mesh { name = name };
                if (Vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(Vertices);
                mesh.SetUVs(0, Uvs);
                mesh.SetTriangles(_triangles, 0);
                if (recalculateNormals || Normals.Count != Vertices.Count) mesh.RecalculateNormals();
                else mesh.SetNormals(Normals);
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();
                return mesh;
            }
        }
    }
}
