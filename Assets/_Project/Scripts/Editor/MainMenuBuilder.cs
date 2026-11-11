using TidyUp.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace TidyUp.Editor
{
    /// <summary>
    /// Builds Scenes/MainMenu.unity: a camera and the GameMenu in main-menu mode (continue, new game,
    /// load, controls, quit), which starts or loads the City. Called by the vertical-slice builder.
    /// </summary>
    public static class MainMenuBuilder
    {
        public const string SceneName = "MainMenu";
        public const string ScenePath = "Assets/_Project/Scenes/MainMenu.unity";

        [MenuItem("Tools/Tidy Up/Build Main Menu Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildScene();
        }

        public static void BuildScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // A camera only so URP has something to render; the menu covers the screen.
            var cameraGo = new GameObject("Menu Camera");
            Camera camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.27f, 0.19f, 0.24f);
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            cameraGo.tag = "MainCamera";

            new GameObject("Main Menu").AddComponent<GameMenu>().EditorConfigure(MenuMode.Main, "City", null, null);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
        }
    }
}
