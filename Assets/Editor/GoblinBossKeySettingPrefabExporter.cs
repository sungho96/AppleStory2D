using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GoblinBossKeySettingPrefabExporter
{
    private const string SourceScenePath = "Assets/Scenes/GoblinBoss.unity";
    private const string PrefabPath = "Assets/Resources/UI/GoblinBoss_KeySettingUI.prefab";
    private const string AutoExportSessionKey = "AppleStory.GoblinBossKeySettingPrefabExporter.AutoExported";

    [InitializeOnLoadMethod]
    private static void AutoExportIfMissing()
    {
        if (SessionState.GetBool(AutoExportSessionKey, false))
            return;

        SessionState.SetBool(AutoExportSessionKey, true);
        EditorApplication.delayCall += () =>
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
                return;

            Export();
        };
    }

    [MenuItem("AppleStory/UI/Export GoblinBoss KeySetting Prefab")]
    public static void Export()
    {
        Scene sourceScene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Additive);
        GameObject source = FindRootChildByName(sourceScene, "Canvas", "KeySettingUI");
        if (source == null)
        {
            Debug.LogError("[GoblinBossKeySettingPrefabExporter] GoblinBoss 씬에서 KeySettingUI를 찾지 못했습니다.");
            EditorSceneManager.CloseScene(sourceScene, true);
            return;
        }

        Directory.CreateDirectory("Assets/Resources/UI");

        // [Codex GoblinBoss KeySetting Export] GoblinBoss에서 이미 맞춰둔 키 설정 UI를 ReadyPanel 재사용용 프리팹으로 보존합니다.
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(source, PrefabPath);
        EditorUtility.SetDirty(prefab);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorSceneManager.CloseScene(sourceScene, true);

        Debug.Log($"[GoblinBossKeySettingPrefabExporter] Exported {PrefabPath}");
    }

    private static GameObject FindRootChildByName(Scene scene, string rootName, string childName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name != rootName)
                continue;

            Transform child = root.transform.Find(childName);
            if (child != null)
                return child.gameObject;
        }

        return null;
    }
}
