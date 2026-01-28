using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace MoonForge.ErrorTracking.Editor
{
    /// <summary>
    /// Adds MoonForge settings to Unity's Project Settings window.
    /// Accessible via: Edit > Project Settings > MoonForge
    /// </summary>
    public class MoonForgeSettingsProvider : SettingsProvider
    {
        private SerializedObject _serializedSettings;
        private MoonForgeSettings _settings;

        public MoonForgeSettingsProvider(string path, SettingsScope scope = SettingsScope.Project)
            : base(path, scope) { }

        public override void OnActivate(string searchContext, UnityEngine.UIElements.VisualElement rootElement)
        {
            _settings = MoonForgeSettings.GetOrCreateSettings();
            _serializedSettings = new SerializedObject(_settings);
        }

        public override void OnGUI(string searchContext)
        {
            if (_serializedSettings == null || _settings == null)
            {
                EditorGUILayout.HelpBox("Settings not found. Click below to create.", MessageType.Warning);
                if (GUILayout.Button("Create MoonForge Settings"))
                {
                    _settings = MoonForgeSettings.GetOrCreateSettings();
                    _serializedSettings = new SerializedObject(_settings);
                }
                return;
            }

            _serializedSettings.Update();

            EditorGUILayout.Space(10);

            // Quick setup button
            if (GUILayout.Button("🚀 Open Setup Wizard", GUILayout.Height(30)))
            {
                MoonForgeSetupWizard.ShowWindow();
            }

            EditorGUILayout.Space(10);

            // Status
            DrawStatus();

            EditorGUILayout.Space(10);

            // Draw all properties
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);

            var iterator = _serializedSettings.GetIterator();
            iterator.NextVisible(true); // Skip script reference

            while (iterator.NextVisible(false))
            {
                EditorGUILayout.PropertyField(iterator, true);
            }

            if (_serializedSettings.hasModifiedProperties)
            {
                _serializedSettings.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
            }
        }

        private void DrawStatus()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            bool isValid = _settings.IsValid;
            bool isActive = _settings.ShouldBeActive;

            if (!isValid)
            {
                EditorGUILayout.HelpBox("⚠️ Invalid or missing Game ID", MessageType.Warning);
            }
            else if (!isActive)
            {
                EditorGUILayout.HelpBox("ℹ️ Tracking is disabled or not active in editor", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("✅ MoonForge is configured and ready", MessageType.None);
            }

            EditorGUILayout.EndVertical();
        }

        [SettingsProvider]
        public static SettingsProvider CreateMoonForgeSettingsProvider()
        {
            var provider = new MoonForgeSettingsProvider("Project/MoonForge", SettingsScope.Project)
            {
                label = "MoonForge",
                keywords = new HashSet<string>(new[] { "MoonForge", "Error", "Tracking", "Analytics", "Crash" })
            };

            return provider;
        }
    }
}
