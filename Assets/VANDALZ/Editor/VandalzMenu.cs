using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Пункт меню «VANDALZ → Создать сцену прототипа»: создаёт и сохраняет сцену
// с объектом SubwayLevelBuilder, который соберёт уровень при нажатии Play.
public static class VandalzMenu
{
    const string ScenePath = "Assets/VANDALZ/Scenes/Prototype.unity";

    [MenuItem("VANDALZ/Создать сцену прототипа")]
    static void CreatePrototypeScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        if (!AssetDatabase.IsValidFolder("Assets/VANDALZ/Scenes"))
            AssetDatabase.CreateFolder("Assets/VANDALZ", "Scenes");

        // DefaultGameObjects = Main Camera + Directional Light.
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        new GameObject("LevelBuilder").AddComponent<SubwayLevelBuilder>();
        EditorSceneManager.SaveScene(scene, ScenePath);

        // Добавляем сцену в Build Settings, чтобы она попала в сборку.
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!scenes.Exists(s => s.path == ScenePath))
        {
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        Debug.Log("VANDALZ: сцена прототипа создана — " + ScenePath + ". Жми Play!");
    }
}
