using TidyUp.Core;
using UnityEditor;
using UnityEngine;

namespace TidyUp.Editor
{
    /// <summary>Makes sure the project defines the layers the game relies on (TidyLayers).</summary>
    public static class TidyLayersEditor
    {
        /// <summary>Index of the Items layer, adding it to the first free user layer if needed.</summary>
        public static int EnsureItemsLayer()
        {
            int existing = LayerMask.NameToLayer(TidyLayers.ItemsName);
            if (existing >= 0) return existing;

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(layer.stringValue)) continue;
                layer.stringValue = TidyLayers.ItemsName;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log($"[Tidy Up] Added the '{TidyLayers.ItemsName}' layer at index {i}.");
                return i;
            }
            Debug.LogError("[Tidy Up] No free user layer for 'Items'.");
            return 0;
        }
    }
}
