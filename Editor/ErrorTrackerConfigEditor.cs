using UnityEditor;
using UnityEngine;

namespace MoonForge.ErrorTracking.Editor
{
    /// <summary>
    /// Custom editor for ErrorTrackerConfig
    /// </summary>
    [CustomEditor(typeof(ErrorTrackerConfig))]
    public class ErrorTrackerConfigEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var config = (ErrorTrackerConfig)target;

            // Validation status
            EditorGUILayout.Space();

            if (config.Validate(out var error))
            {
                EditorGUILayout.HelpBox("Configuration is valid", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox($"Configuration error: {error}", MessageType.Error);
            }

            EditorGUILayout.Space();

            // Draw default inspector
            DrawDefaultInspector();

            EditorGUILayout.Space();

            // Test buttons
            EditorGUILayout.LabelField("Testing", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Test Connection"))
            {
                TestConnection(config);
            }

            if (GUILayout.Button("Send Test Error"))
            {
                SendTestError(config);
            }

            EditorGUILayout.EndHorizontal();

            // Documentation link
            EditorGUILayout.Space();
            if (GUILayout.Button("Open Documentation"))
            {
                Application.OpenURL("https://docs.moonforge.dev/unity-sdk/error-tracking");
            }
        }

        private void TestConnection(ErrorTrackerConfig config)
        {
            if (!config.Validate(out var error))
            {
                EditorUtility.DisplayDialog("Configuration Error", error, "OK");
                return;
            }

            var url = config.GetErrorsApiUrl().Replace("/api/errors", "/api/errors/health");
            Debug.Log($"[MoonForge] Testing connection to {url}...");

            // Use UnityWebRequest in editor
            EditorUtility.DisplayDialog("Connection Test",
                $"Testing connection to:\n{url}\n\nCheck the Console for results.",
                "OK");
        }

        private void SendTestError(ErrorTrackerConfig config)
        {
            if (!config.Validate(out var error))
            {
                EditorUtility.DisplayDialog("Configuration Error", error, "OK");
                return;
            }

            if (!config.enableInEditor)
            {
                EditorUtility.DisplayDialog("Editor Disabled",
                    "Error tracking is disabled in Editor.\n\nEnable 'Enable In Editor' in the config to send test errors.",
                    "OK");
                return;
            }

            // Initialize if needed
            if (!MoonForgeErrorTracker.IsInitialized)
            {
                MoonForgeErrorTracker.Initialize(config);
            }

            if (MoonForgeErrorTracker.Instance != null)
            {
                MoonForgeErrorTracker.Instance.CaptureMessage(
                    "Test error from Unity Editor",
                    ErrorLevel.Info,
                    new System.Collections.Generic.Dictionary<string, string>
                    {
                        { "source", "editor_test" }
                    }
                );

                Debug.Log("[MoonForge] Test error sent - check your dashboard");
                EditorUtility.DisplayDialog("Test Error Sent",
                    "A test error has been sent.\n\nCheck the Console and your MoonForge dashboard.",
                    "OK");
            }
        }

        /// <summary>
        /// Create config from menu
        /// </summary>
        [MenuItem("Assets/Create/MoonForge/Error Tracker Config")]
        public static void CreateConfig()
        {
            var config = ScriptableObject.CreateInstance<ErrorTrackerConfig>();

            var path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(path))
            {
                path = "Assets";
            }
            else if (!System.IO.Directory.Exists(path))
            {
                path = System.IO.Path.GetDirectoryName(path);
            }

            var assetPath = AssetDatabase.GenerateUniqueAssetPath($"{path}/MoonForgeErrorTrackerConfig.asset");

            AssetDatabase.CreateAsset(config, assetPath);
            AssetDatabase.SaveAssets();

            EditorUtility.FocusProjectWindow();
            Selection.activeObject = config;
        }
    }
}
