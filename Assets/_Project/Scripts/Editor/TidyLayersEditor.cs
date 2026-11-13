using TidyUp.Core;
using UnityEditor;
using UnityEngine;

namespace TidyUp.Editor
{
    /// <summary>Makes sure the project defines the layers the game relies on (TidyLayers).</summary>
    public static class TidyLayersEditor
    {
        /// <summary>Index of the Items layer, adding it to the first free user layer if needed.</summary>
        public static int EnsureItemsLayer() => EnsureLayer(TidyLayers.ItemsName);

        /// <summary>Index of the Held layer (the carried stack, drawn by the player's overlay camera).</summary>
        public static int EnsureHeldLayer() => EnsureLayer(TidyLayers.HeldName);

        public static int EnsureLayer(string name)
        {
            int existing = LayerMask.NameToLayer(name);
            if (existing >= 0) return existing;

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(layer.stringValue)) continue;
                layer.stringValue = name;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log($"[Tidy Up] Added the '{name}' layer at index {i}.");
                return i;
            }
            Debug.LogError($"[Tidy Up] No free user layer for '{name}'.");
            return 0;
        }
    }
}
