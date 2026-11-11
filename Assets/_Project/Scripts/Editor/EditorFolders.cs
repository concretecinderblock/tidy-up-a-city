using UnityEditor;

namespace TidyUp.Editor
{
    internal static class EditorFolders
    {
        /// <summary>Creates an Assets/... folder path (and parents) through the AssetDatabase.</summary>
        public static void Ensure(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent)) Ensure(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
