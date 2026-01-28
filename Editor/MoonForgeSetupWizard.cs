using UnityEngine;
using UnityEditor;
using System;

namespace MoonForge.ErrorTracking.Editor
{
    /// <summary>
    /// One-click setup wizard for MoonForge Error Tracking.
    /// Accessible via: MoonForge > Setup Error Tracking
    /// </summary>
    public class MoonForgeSetupWizard : EditorWindow
    {
        private MoonForgeSettings _settings;
        private string _gameIdInput = "";
        private bool _testConnectionInProgress = false;
        private string _testConnectionResult = "";
        private bool _showAdvanced = false;
        private Vector2 _scrollPosition;

        private GUIStyle _headerStyle;
        private GUIStyle _successStyle;
        private GUIStyle _errorStyle;
        private GUIStyle _infoStyle;
        private bool _stylesInitialized = false;

        private const string DOCS_URL = "https://docs.moonforge.dev/unity-sdk";
        private const string DASHBOARD_URL = "https://app.moonforge.dev";

        [MenuItem("MoonForge/Setup Error Tracking", false, 0)]
        public static void ShowWindow()
        {
            var window = GetWindow<MoonForgeSetupWizard>("MoonForge Setup");
            window.minSize = new Vector2(450, 400);
            window.maxSize = new Vector2(500, 700);
            window.Show();
        }

        [MenuItem("MoonForge/Open Dashboard", false, 20)]
        public static void OpenDashboard()
        {
            Application.OpenURL(DASHBOARD_URL);
        }

        [MenuItem("MoonForge/Documentation", false, 21)]
        public static void OpenDocs()
        {
            Application.OpenURL(DOCS_URL);
        }

        [MenuItem("MoonForge/Settings", false, 40)]
        public static void SelectSettings()
        {
            MoonForgeSettings.SelectSettings();
        }

        private void OnEnable()
        {
            _settings = MoonForgeSettings.GetOrCreateSettings();
            if (_settings != null)
            {
                _gameIdInput = _settings.gameId ?? "";
            }
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;

            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter,
                margin = new RectOffset(0, 0, 10, 10)
            };

            _successStyle = new GUIStyle(EditorStyles.helpBox)
            {
                normal = { textColor = new Color(0.2f, 0.7f, 0.2f) },
                fontSize = 12,
                padding = new RectOffset(10, 10, 10, 10)
            };

            _errorStyle = new GUIStyle(EditorStyles.helpBox)
            {
                normal = { textColor = new Color(0.9f, 0.3f, 0.3f) },
                fontSize = 12,
                padding = new RectOffset(10, 10, 10, 10)
            };

            _infoStyle = new GUIStyle(EditorStyles.helpBox)
            {
                fontSize = 11,
                padding = new RectOffset(10, 10, 8, 8),
                wordWrap = true
            };

            _stylesInitialized = true;
        }

        private void OnGUI()
        {
            InitStyles();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            GUILayout.Space(10);

            // Header
            GUILayout.Label("🌙 MoonForge Setup", _headerStyle);
            GUILayout.Label("Game Analytics & Error Tracking", EditorStyles.centeredGreyMiniLabel);

            GUILayout.Space(20);

            // Status indicator
            DrawStatusIndicator();

            GUILayout.Space(20);

            // Main setup section
            DrawSetupSection();

            GUILayout.Space(20);

            // Quick actions
            DrawQuickActions();

            GUILayout.Space(20);

            // Advanced settings toggle
            DrawAdvancedSettings();

            GUILayout.Space(20);

            // Help section
            DrawHelpSection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawStatusIndicator()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            bool isConfigured = _settings != null && _settings.IsValid;
            bool isEnabled = _settings != null && _settings.enabled;
            bool isEditorEnabled = _settings != null && _settings.enableInEditor;

            string statusText;
            Color statusColor;

            if (!isConfigured)
            {
                statusText = "⚠️ Not Configured - Enter your Game ID below";
                statusColor = new Color(1f, 0.7f, 0.2f);
            }
            else if (!isEnabled)
            {
                statusText = "⏸️ Configured but Disabled";
                statusColor = new Color(0.6f, 0.6f, 0.6f);
            }
            else
            {
                statusText = "✅ Ready" + (isEditorEnabled ? " (Editor tracking ON)" : " (Editor tracking OFF)");
                statusColor = new Color(0.3f, 0.8f, 0.3f);
            }

            var originalColor = GUI.color;
            GUI.color = statusColor;
            GUILayout.Label(statusText, EditorStyles.boldLabel);
            GUI.color = originalColor;

            EditorGUILayout.EndVertical();
        }

        private void DrawSetupSection()
        {
            EditorGUILayout.LabelField("Step 1: Enter Your Game ID", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField("Game ID (from MoonForge Dashboard):", EditorStyles.miniLabel);

            EditorGUI.BeginChangeCheck();
            _gameIdInput = EditorGUILayout.TextField(_gameIdInput);

            if (EditorGUI.EndChangeCheck() && _settings != null)
            {
                Undo.RecordObject(_settings, "Change MoonForge Game ID");
                _settings.gameId = _gameIdInput.Trim();
                EditorUtility.SetDirty(_settings);
                AssetDatabase.SaveAssets();
            }

            // Validation feedback
            if (string.IsNullOrWhiteSpace(_gameIdInput))
            {
                EditorGUILayout.HelpBox("Paste your Game ID from the MoonForge dashboard.", MessageType.Info);
            }
            else if (!MoonForgeSettings.IsValidGameId(_gameIdInput))
            {
                EditorGUILayout.HelpBox("Invalid Game ID format. Should be a UUID like: a1b2c3d4-e5f6-7890-abcd-ef1234567890", MessageType.Error);
            }
            else
            {
                EditorGUILayout.HelpBox("✓ Valid Game ID", MessageType.None);
            }

            EditorGUILayout.Space(5);

            if (GUILayout.Button("📋 Don't have a Game ID? Open Dashboard"))
            {
                Application.OpenURL(DASHBOARD_URL);
            }

            EditorGUILayout.EndVertical();

            GUILayout.Space(15);

            // Enable toggles
            EditorGUILayout.LabelField("Step 2: Enable Tracking", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (_settings != null)
            {
                EditorGUI.BeginChangeCheck();

                _settings.enabled = EditorGUILayout.Toggle("Enable Error Tracking", _settings.enabled);

                EditorGUILayout.Space(5);

                _settings.enableInEditor = EditorGUILayout.Toggle(
                    new GUIContent("Enable in Editor", "Track errors while testing in Unity Editor"),
                    _settings.enableInEditor
                );

                _settings.debugMode = EditorGUILayout.Toggle(
                    new GUIContent("Debug Mode", "Show detailed logs in Console"),
                    _settings.debugMode
                );

                if (EditorGUI.EndChangeCheck())
                {
                    EditorUtility.SetDirty(_settings);
                    AssetDatabase.SaveAssets();
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawQuickActions()
        {
            EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();

            // Test connection button
            EditorGUI.BeginDisabledGroup(_testConnectionInProgress || !(_settings?.IsValid ?? false));
            if (GUILayout.Button("🔗 Test Connection", GUILayout.Height(30)))
            {
                TestConnection();
            }
            EditorGUI.EndDisabledGroup();

            // Send test error button
            EditorGUI.BeginDisabledGroup(!(_settings?.IsValid ?? false) || !(_settings?.enableInEditor ?? false));
            if (GUILayout.Button("🧪 Send Test Error", GUILayout.Height(30)))
            {
                SendTestError();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();

            // Connection test result
            if (!string.IsNullOrEmpty(_testConnectionResult))
            {
                EditorGUILayout.Space(5);
                bool isSuccess = _testConnectionResult.StartsWith("✓");
                EditorGUILayout.LabelField(_testConnectionResult,
                    isSuccess ? _successStyle : _errorStyle);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawAdvancedSettings()
        {
            _showAdvanced = EditorGUILayout.Foldout(_showAdvanced, "Advanced Settings", true);

            if (_showAdvanced && _settings != null)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUI.BeginChangeCheck();

                // API Endpoint
                EditorGUILayout.LabelField("API Endpoint", EditorStyles.miniLabel);
                _settings.apiEndpoint = EditorGUILayout.TextField(_settings.apiEndpoint);

                EditorGUILayout.Space(10);

                // Capture settings
                EditorGUILayout.LabelField("Capture Settings", EditorStyles.boldLabel);
                _settings.captureUnhandledExceptions = EditorGUILayout.Toggle("Capture Exceptions", _settings.captureUnhandledExceptions);
                _settings.captureLogErrors = EditorGUILayout.Toggle("Capture Log Errors", _settings.captureLogErrors);
                _settings.captureNativeCrashes = EditorGUILayout.Toggle("Capture Native Crashes", _settings.captureNativeCrashes);
                _settings.trackSceneChanges = EditorGUILayout.Toggle("Track Scene Changes", _settings.trackSceneChanges);

                EditorGUILayout.Space(10);

                // Performance settings
                EditorGUILayout.LabelField("Performance", EditorStyles.boldLabel);
                _settings.enableBatching = EditorGUILayout.Toggle("Enable Batching", _settings.enableBatching);
                _settings.batchSize = EditorGUILayout.IntSlider("Batch Size", _settings.batchSize, 1, 50);
                _settings.maxBreadcrumbs = EditorGUILayout.IntSlider("Max Breadcrumbs", _settings.maxBreadcrumbs, 10, 200);

                EditorGUILayout.Space(10);

                // Storage & Privacy
                EditorGUILayout.LabelField("Storage & Privacy", EditorStyles.boldLabel);
                _settings.enableOfflineStorage = EditorGUILayout.Toggle("Offline Storage", _settings.enableOfflineStorage);
                _settings.scrubSensitiveData = EditorGUILayout.Toggle("Scrub Sensitive Data", _settings.scrubSensitiveData);

                EditorGUILayout.Space(10);

                // Build settings
                EditorGUILayout.LabelField("Build Settings", EditorStyles.boldLabel);
                _settings.autoUploadSymbols = EditorGUILayout.Toggle("Auto-Upload Symbols", _settings.autoUploadSymbols);

                if (EditorGUI.EndChangeCheck())
                {
                    EditorUtility.SetDirty(_settings);
                    AssetDatabase.SaveAssets();
                }

                EditorGUILayout.EndVertical();
            }
        }

        private void DrawHelpSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            GUILayout.Label(_infoStyle != null ?
                "💡 That's it! MoonForge will automatically start tracking when your game runs. No code required for basic setup." :
                "MoonForge will automatically start tracking when your game runs.");

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("📖 Documentation"))
            {
                Application.OpenURL(DOCS_URL);
            }

            if (GUILayout.Button("🎮 Dashboard"))
            {
                Application.OpenURL(DASHBOARD_URL);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private async void TestConnection()
        {
            if (_settings == null || !_settings.IsValid) return;

            _testConnectionInProgress = true;
            _testConnectionResult = "Testing...";
            Repaint();

            try
            {
                using (var client = new System.Net.Http.HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(10);
                    var response = await client.GetAsync(_settings.apiEndpoint.Replace("/errors", "/health"));

                    if (response.IsSuccessStatusCode)
                    {
                        _testConnectionResult = "✓ Connection successful! MoonForge is ready.";
                    }
                    else
                    {
                        _testConnectionResult = $"✗ Server returned: {response.StatusCode}";
                    }
                }
            }
            catch (Exception ex)
            {
                _testConnectionResult = $"✗ Connection failed: {ex.Message}";
            }

            _testConnectionInProgress = false;
            Repaint();
        }

        private void SendTestError()
        {
            if (_settings == null || !_settings.IsValid) return;

            // Ensure tracker is initialized
            if (MoonForgeErrorTracker.Instance == null)
            {
                MoonForgeAutoInitializer.Initialize();
            }

            // Send test error
            try
            {
                MoonForgeErrorTracker.Instance.CaptureMessage(
                    "Test error from MoonForge Setup Wizard",
                    ErrorLevel.Info
                );
                _testConnectionResult = "✓ Test error sent! Check your dashboard in a few seconds.";
            }
            catch (Exception ex)
            {
                _testConnectionResult = $"✗ Failed to send: {ex.Message}";
            }

            Repaint();
        }
    }
}
