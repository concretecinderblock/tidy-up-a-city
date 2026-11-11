using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TidyUp.Items;
using UnityEditor;
using UnityEngine;

namespace TidyUp.Editor
{
    /// <summary>
    /// How to build a stand-in prefab for an item without art. From the CSV greybox* columns.
    /// Shape: cube, sphere, cylinder, capsule, capsule_x (lying along X). Size: bounding box w;h;d in metres.
    /// </summary>
    public sealed class GreyboxSpec
    {
        public string Shape = "cube";
        public Vector3 Size = Vector3.one * 0.15f;
        public Color Color = Color.gray;
    }

    /// <summary>
    /// CSV -> ItemDefinition assets. CSVs in Data/Items are the source of truth.
    /// Columns: id, displayName, homeLocationId, slotTypes (';'-separated), mechanicFamily (A-J or name),
    /// rarity, isStray, prefab (optional asset path; empty = generated grey-box prefab), sortKey,
    /// greyboxShape, greyboxSize (w;h;d), greyboxColor (#RRGGBB), living (hop/float/shy/disguise), disguiseName.
    /// </summary>
    public static class ItemImporter
    {
        public const string CsvFolder = "Assets/_Project/Data/Items";
        public const string OutputFolder = "Assets/_Project/Data/Items/Generated";

        [MenuItem("Tools/Tidy Up/Import Item CSVs")]
        public static void ImportAllMenu()
        {
            Dictionary<string, ItemDefinition> result = ImportAll();
            Debug.Log($"[Tidy Up] Imported {result.Count} item definitions into {OutputFolder}.");
        }

        /// <summary>Imports every CSV in the item folder. Returns definitions by id.</summary>
        public static Dictionary<string, ItemDefinition> ImportAll() => ImportAll(out _);

        /// <summary>Imports every CSV; also returns grey-box specs for items without an explicit prefab.</summary>
        public static Dictionary<string, ItemDefinition> ImportAll(out Dictionary<string, GreyboxSpec> greybox)
        {
            EditorFolders.Ensure(OutputFolder);
            var byId = new Dictionary<string, ItemDefinition>();
            greybox = new Dictionary<string, GreyboxSpec>();

            string[] files = Directory.GetFiles(CsvFolder, "*.csv", SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.Ordinal);
            foreach (string file in files)
            {
                ImportFile(file.Replace('\\', '/'), byId, greybox);
            }

            AssetDatabase.SaveAssets();
            return byId;
        }

        private static void ImportFile(string path, Dictionary<string, ItemDefinition> byId, Dictionary<string, GreyboxSpec> greybox)
        {
            string[] lines = File.ReadAllLines(path);
            Dictionary<string, int> columns = null;

            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex].Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;

                List<string> fields = ParseCsvLine(line);
                if (columns == null)
                {
                    columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < fields.Count; i++) columns[fields[i].Trim()] = i;
                    if (!columns.ContainsKey("id"))
                    {
                        Debug.LogError($"[Tidy Up] {path}: header has no 'id' column.");
                        return;
                    }
                    continue;
                }

                string id = Get(fields, columns, "id");
                if (string.IsNullOrEmpty(id))
                {
                    Debug.LogWarning($"[Tidy Up] {path}:{lineIndex + 1}: row without id skipped.");
                    continue;
                }
                if (byId.ContainsKey(id))
                {
                    Debug.LogError($"[Tidy Up] {path}:{lineIndex + 1}: duplicate item id '{id}'.");
                    continue;
                }

                ItemDefinition definition = LoadOrCreate(id);
                definition.Configure(
                    id,
                    Get(fields, columns, "displayName"),
                    Get(fields, columns, "homeLocationId"),
                    SplitList(Get(fields, columns, "slotTypes")),
                    ParseFamily(Get(fields, columns, "mechanicFamily"), path, lineIndex),
                    ParseEnum(Get(fields, columns, "rarity"), ItemRarity.Common),
                    string.Equals(Get(fields, columns, "isStray"), "true", StringComparison.OrdinalIgnoreCase));

                definition.SetSortKey(Get(fields, columns, "sortKey"));
                definition.SetLiving(ParseLiving(Get(fields, columns, "living"), path, lineIndex), Get(fields, columns, "disguiseName"));

                string prefabPath = Get(fields, columns, "prefab");
                if (!string.IsNullOrEmpty(prefabPath))
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    if (prefab != null) definition.SetPrefab(prefab);
                    else Debug.LogWarning($"[Tidy Up] {path}:{lineIndex + 1}: prefab '{prefabPath}' not found.");
                }
                else
                {
                    greybox[id] = ParseGreybox(fields, columns, path, lineIndex);
                }

                EditorUtility.SetDirty(definition);
                byId.Add(id, definition);
            }
        }

        private static ItemDefinition LoadOrCreate(string id)
        {
            string assetPath = $"{OutputFolder}/{id}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(assetPath);
            if (definition != null) return definition;
            definition = ScriptableObject.CreateInstance<ItemDefinition>();
            AssetDatabase.CreateAsset(definition, assetPath);
            return definition;
        }

        private static string Get(List<string> fields, Dictionary<string, int> columns, string name)
        {
            return columns.TryGetValue(name, out int index) && index < fields.Count ? fields[index].Trim() : string.Empty;
        }

        private static string[] SplitList(string value)
        {
            if (string.IsNullOrEmpty(value)) return Array.Empty<string>();
            string[] parts = value.Split(';');
            var result = new List<string>(parts.Length);
            foreach (string part in parts)
            {
                string trimmed = part.Trim();
                if (trimmed.Length > 0) result.Add(trimmed);
            }
            return result.ToArray();
        }

        private static MechanicFamily ParseFamily(string value, string path, int lineIndex)
        {
            if (value.Length == 1 && char.IsLetter(value[0]))
            {
                int index = char.ToUpperInvariant(value[0]) - 'A';
                if (index >= 0 && index <= (int)MechanicFamily.ShadowBoards) return (MechanicFamily)index;
            }
            if (Enum.TryParse(value, true, out MechanicFamily family)) return family;
            if (value.Length > 0) Debug.LogWarning($"[Tidy Up] {path}:{lineIndex + 1}: unknown mechanic family '{value}', using ShelfSort.");
            return MechanicFamily.ShelfSort;
        }

        /// <summary>CSV "living": hop | float | shy | disguise (or the enum names). Empty = ordinary item.</summary>
        private static LivingBehaviour ParseLiving(string value, string path, int lineIndex)
        {
            switch (value.ToLowerInvariant())
            {
                case "": return LivingBehaviour.None;
                case "hop": return LivingBehaviour.Skittish;
                case "float": return LivingBehaviour.Floating;
                case "shy": return LivingBehaviour.Shy;
                case "disguise": return LivingBehaviour.Disguised;
            }
            if (Enum.TryParse(value, true, out LivingBehaviour parsed)) return parsed;
            Debug.LogWarning($"[Tidy Up] {path}:{lineIndex + 1}: unknown living behaviour '{value}' (use hop, float, shy or disguise).");
            return LivingBehaviour.None;
        }

        private static GreyboxSpec ParseGreybox(List<string> fields, Dictionary<string, int> columns, string path, int lineIndex)
        {
            var spec = new GreyboxSpec();
            string shape = Get(fields, columns, "greyboxShape");
            if (shape.Length > 0) spec.Shape = shape.ToLowerInvariant();

            string size = Get(fields, columns, "greyboxSize");
            if (size.Length > 0)
            {
                string[] parts = size.Split(';');
                if (parts.Length == 3 &&
                    float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float w) &&
                    float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float h) &&
                    float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float d))
                {
                    spec.Size = new Vector3(w, h, d);
                }
                else Debug.LogWarning($"[Tidy Up] {path}:{lineIndex + 1}: greyboxSize '{size}' should be w;h;d in metres.");
            }

            string color = Get(fields, columns, "greyboxColor");
            if (color.Length > 0)
            {
                if (ColorUtility.TryParseHtmlString(color, out Color parsed)) spec.Color = parsed;
                else Debug.LogWarning($"[Tidy Up] {path}:{lineIndex + 1}: greyboxColor '{color}' should be #RRGGBB.");
            }
            return spec;
        }

        private static T ParseEnum<T>(string value, T fallback) where T : struct
        {
            return Enum.TryParse(value, true, out T parsed) ? parsed : fallback;
        }

        /// <summary>Minimal RFC 4180 line parser (quoted fields, doubled quotes).</summary>
        internal static List<string> ParseCsvLine(string line)
        {
            var fields = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            current.Append('"');
                            i++;
                        }
                        else inQuotes = false;
                    }
                    else current.Append(c);
                }
                else if (c == '"') inQuotes = true;
                else if (c == ',')
                {
                    fields.Add(current.ToString());
                    current.Clear();
                }
                else current.Append(c);
            }
            fields.Add(current.ToString());
            return fields;
        }
    }
}
