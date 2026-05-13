using UnityEngine;
using UnityEditor;

public static class ScaleNeutralMobs
{
    private const string FolderPath = "Assets/MobaDev/Resources/Prefabs/Neutral mobs";

    [MenuItem("Tools/Scale Neutral Mobs x0.5")]
    private static void Execute()
    {
        var guids = AssetDatabase.FindAssets("t:Prefab", new[] { FolderPath });
        int count = 0;

        foreach (var guid in guids)
        {
            string path   = AssetDatabase.GUIDToAssetPath(guid);
            var    prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            using var scope = new PrefabUtility.EditPrefabContentsScope(path);
            var root = scope.prefabContentsRoot;
            root.transform.localScale *= 0.5f;
            count++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[ScaleNeutralMobs] Scaled {count} prefabs x0.5 in '{FolderPath}'.");
    }
}
