// Assets/MobaDev/Editor/SetupLoginScene.cs
// Run once via: MOBA → Setup Login Scene  / MOBA → Setup Lobby Scene
// MOBA → Add Scenes to Build
// Configures Login and Lobby scenes: adds UIDocument, manager scripts,
// Camera and EventSystem, then saves.

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEditor.Build.Reporting;
using MobaDev.Network;

public static class SetupLoginScene
{
    private const string ScenePath = "Assets/MobaDev/Resources/Scenes/Login.unity";
    private const string UxmlPath  = "Assets/MobaDev/UI/Login.uxml";
    private const string PanelPath = "Assets/MobaDev/UI/LoginPanelSettings.asset";

    [MenuItem("MOBA/Setup Login Scene")]
    public static void Run()
    {
        // ── 1. Save current work ──────────────────────────────────────────────
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        // ── 2. Open Login scene ───────────────────────────────────────────────
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError("[Setup] Could not open " + ScenePath);
            return;
        }

        // ── 3. PanelSettings ──────────────────────────────────────────────────
        var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
        if (panelSettings == null)
        {
            panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.scaleMode      = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(1920, 1080);
            AssetDatabase.CreateAsset(panelSettings, PanelPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Setup] Created PanelSettings at " + PanelPath);
        }

        // ── 4. UXML source ────────────────────────────────────────────────────
        var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
        if (uxml == null)
        {
            Debug.LogError("[Setup] UXML not found at " + UxmlPath);
            return;
        }

        // ── 5. Find / fix LoginManager GameObject ─────────────────────────────
        var go = GameObject.Find("LoginManager");
        if (go == null)
        {
            go = new GameObject("LoginManager");
            Debug.Log("[Setup] Created LoginManager GameObject");
        }

        // UIDocument
        var doc = go.GetComponent<UIDocument>() ?? go.AddComponent<UIDocument>();
        doc.panelSettings    = panelSettings;
        doc.visualTreeAsset  = uxml;

        // LoginManager script
        if (go.GetComponent<LoginManager>() == null)
            go.AddComponent<LoginManager>();

        // ── 6. Camera ─────────────────────────────────────────────────────────
        if (Camera.main == null)
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = new Vector3(0, 1, -10);
            Debug.Log("[Setup] Created Main Camera");
        }

        // ── 7. EventSystem ────────────────────────────────────────────────────
        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<InputSystemUIInputModule>();
            Debug.Log("[Setup] Created EventSystem");
        }

        // ── 8. Save ───────────────────────────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Setup] Login scene saved successfully!");
        EditorUtility.DisplayDialog("Setup complete",
            "Login scene configured:\n• UIDocument + Login.uxml\n• LoginManager script\n• Camera\n• EventSystem",
            "OK");
    }

    // =========================================================================
    // QUICK BUILD
    // =========================================================================

    [MenuItem("MOBA/Reset Match (Server)")]
    public static void ResetServerMatch()
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName               = "spacetime",
            Arguments              = "call moba-server ResetMatch",
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow         = true,
        };
        var proc = System.Diagnostics.Process.Start(psi);
        proc.WaitForExit();
        var err = proc.StandardError.ReadToEnd();
        if (proc.ExitCode == 0)
        {
            Debug.Log("[Reset] Match reset to Lobby!");
            EditorUtility.DisplayDialog("Done", "Server reset to Lobby.\nAll players can now join.", "OK");
        }
        else
        {
            Debug.LogError("[Reset] Failed: " + err);
            EditorUtility.DisplayDialog("Error", "Reset failed:\n" + err, "OK");
        }
    }

    [MenuItem("MOBA/Build Windows (Quick)")]
    public static void BuildWindows()
    {
        // Ensure scenes are registered first
        AddScenesToBuild();

        var buildPath = System.IO.Path.Combine(
            System.IO.Directory.GetParent(Application.dataPath).FullName,
            "Builds", "MOBA.exe");

        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(buildPath));

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes           = new[] {
                "Assets/MobaDev/Resources/Scenes/Login.unity",
                "Assets/MobaDev/Resources/Scenes/Lobby.unity",
                "Assets/MobaDev/Resources/Scenes/Game.unity",
            },
            locationPathName = buildPath,
            target           = BuildTarget.StandaloneWindows64,
            options          = BuildOptions.None,
        });

        if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.Log($"[Build] Success → {buildPath}");
            // Open folder in Explorer
            System.Diagnostics.Process.Start("explorer.exe",
                $"/select,\"{buildPath.Replace('/', '\\')}\"");
        }
        else
        {
            Debug.LogError($"[Build] Failed: {report.summary.totalErrors} errors");
        }
    }

    // =========================================================================
    // BUILD SETTINGS
    // =========================================================================

    [MenuItem("MOBA/Add Scenes to Build")]
    public static void AddScenesToBuild()
    {
        var scenes = new[]
        {
            "Assets/MobaDev/Resources/Scenes/Login.unity",
            "Assets/MobaDev/Resources/Scenes/Lobby.unity",
            "Assets/MobaDev/Resources/Scenes/Game.unity",
        };

        var current = new System.Collections.Generic.List<EditorBuildSettingsScene>(
            EditorBuildSettings.scenes);

        foreach (var path in scenes)
        {
            bool exists = false;
            foreach (var s in current)
                if (s.path == path) { exists = true; break; }

            if (!exists)
                current.Add(new EditorBuildSettingsScene(path, true));
            else
            {
                // ensure enabled
                for (int i = 0; i < current.Count; i++)
                    if (current[i].path == path)
                        current[i] = new EditorBuildSettingsScene(path, true);
            }
        }

        // Login first
        current.Sort((a, b) =>
        {
            int Rank(string p) => p.Contains("Login") ? 0 : p.Contains("Lobby") ? 1 : 2;
            return Rank(a.path).CompareTo(Rank(b.path));
        });

        EditorBuildSettings.scenes = current.ToArray();
        Debug.Log("[Setup] Build scenes set: Login(0) Lobby(1) Game(2)");
        EditorUtility.DisplayDialog("Done", "Scenes added to Build Settings:\n0 Login\n1 Lobby\n2 Game", "OK");
    }

    // =========================================================================
    // LOBBY SCENE
    // =========================================================================

    private const string LobbyScenePath = "Assets/MobaDev/Resources/Scenes/Lobby.unity";
    private const string LobbyUxmlPath  = "Assets/MobaDev/UI/Lobby.uxml";
    private const string LobbyPanelPath = "Assets/MobaDev/UI/LobbyPanelSettings.asset";

    [MenuItem("MOBA/Setup Lobby Scene")]
    public static void RunLobby()
    {
        // ── 1. Save current work ──────────────────────────────────────────────
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        // ── 2. Open Lobby scene ───────────────────────────────────────────────
        var scene = EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError("[Setup] Could not open " + LobbyScenePath);
            return;
        }

        // ── 3. PanelSettings ──────────────────────────────────────────────────
        var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(LobbyPanelPath);
        if (panelSettings == null)
        {
            panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.scaleMode           = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(1920, 1080);
            AssetDatabase.CreateAsset(panelSettings, LobbyPanelPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Setup] Created LobbyPanelSettings at " + LobbyPanelPath);
        }

        // ── 4. UXML source ────────────────────────────────────────────────────
        var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(LobbyUxmlPath);
        if (uxml == null)
        {
            Debug.LogError("[Setup] UXML not found at " + LobbyUxmlPath);
            return;
        }

        // ── 5. Find / fix LobbyManagerObject ──────────────────────────────────
        var go = GameObject.Find("LobbyManagerObject");
        if (go == null)
        {
            go = new GameObject("LobbyManagerObject");
            Debug.Log("[Setup] Created LobbyManagerObject");
        }

        // UIDocument
        var doc = go.GetComponent<UIDocument>() ?? go.AddComponent<UIDocument>();
        doc.panelSettings   = panelSettings;
        doc.visualTreeAsset = uxml;

        // LobbyManager script
        if (go.GetComponent<LobbyManager>() == null)
            go.AddComponent<LobbyManager>();

        // ── 6. Camera ─────────────────────────────────────────────────────────
        if (Camera.main == null)
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = new Vector3(0, 1, -10);
            Debug.Log("[Setup] Created Main Camera");
        }

        // ── 7. EventSystem ────────────────────────────────────────────────────
        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<InputSystemUIInputModule>();
            Debug.Log("[Setup] Created EventSystem");
        }

        // ── 8. Save ───────────────────────────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Setup] Lobby scene saved successfully!");
        EditorUtility.DisplayDialog("Setup complete",
            "Lobby scene configured:\n• UIDocument + Lobby.uxml\n• LobbyManager script\n• Camera\n• EventSystem",
            "OK");
    }
}
