using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MoonForge.ErrorTracking.Editor
{
    /// <summary>
    /// Post-build processor that automatically uploads symbol files for symbolication.
    /// Runs after iOS and Android builds complete.
    /// </summary>
    public class BuildPostProcessor : IPostprocessBuildWithReport
    {
        public int callbackOrder => 999; // Run late to ensure all build artifacts exist

        public void OnPostprocessBuild(BuildReport report)
        {
            // Only process successful builds
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.Log("[MoonForge] Build did not succeed, skipping symbol upload");
                return;
            }

            // Get configuration
            var config = FindConfig();
            if (config == null)
            {
                Debug.LogWarning("[MoonForge] No ErrorTrackerConfig found, skipping symbol upload");
                return;
            }

            if (!config.autoUploadSymbols)
            {
                Debug.Log("[MoonForge] Auto symbol upload disabled in config");
                return;
            }

            var platform = report.summary.platform;
            var outputPath = report.summary.outputPath;

            Debug.Log($"[MoonForge] Post-build processing for {platform} at {outputPath}");

            // Upload symbols based on platform
            switch (platform)
            {
                case BuildTarget.iOS:
                    _ = UploadIOSSymbols(config, outputPath);
                    break;
                case BuildTarget.Android:
                    _ = UploadAndroidSymbols(config, outputPath);
                    break;
                default:
                    Debug.Log($"[MoonForge] Symbol upload not supported for {platform}");
                    break;
            }
        }

        private ErrorTrackerConfig FindConfig()
        {
            // Try to find config in project
            var guids = AssetDatabase.FindAssets("t:ErrorTrackerConfig");
            if (guids.Length == 0)
            {
                return null;
            }

            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<ErrorTrackerConfig>(path);
        }

        private async Task UploadIOSSymbols(ErrorTrackerConfig config, string outputPath)
        {
            try
            {
                // Find dSYM bundles in the build output
                var dsymPaths = FindDsymBundles(outputPath);
                if (dsymPaths.Count == 0)
                {
                    Debug.LogWarning("[MoonForge] No dSYM bundles found for iOS build");
                    return;
                }

                foreach (var dsymPath in dsymPaths)
                {
                    Debug.Log($"[MoonForge] Found dSYM: {dsymPath}");

                    // Zip the dSYM bundle
                    var zipPath = dsymPath + ".zip";
                    await ZipDirectory(dsymPath, zipPath);

                    // Upload
                    await UploadSymbolFile(config, zipPath, "ios", "dsym");

                    // Clean up zip
                    if (File.Exists(zipPath))
                    {
                        File.Delete(zipPath);
                    }
                }

                Debug.Log("[MoonForge] iOS symbol upload complete");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MoonForge] Failed to upload iOS symbols: {ex.Message}");
            }
        }

        private async Task UploadAndroidSymbols(ErrorTrackerConfig config, string outputPath)
        {
            try
            {
                var uploadedAny = false;

                // Find ProGuard mapping file
                var mappingPath = FindProguardMapping(outputPath);
                if (!string.IsNullOrEmpty(mappingPath))
                {
                    Debug.Log($"[MoonForge] Found ProGuard mapping: {mappingPath}");
                    await UploadSymbolFile(config, mappingPath, "android", "proguard");
                    uploadedAny = true;
                }

                // Find native symbols (libil2cpp.so.debug, etc.)
                var nativeSymbols = FindAndroidNativeSymbols(outputPath);
                if (nativeSymbols.Count > 0)
                {
                    Debug.Log($"[MoonForge] Found {nativeSymbols.Count} native symbol files");

                    // Zip all native symbols together
                    var symbolsZipPath = Path.Combine(Path.GetTempPath(), $"moonforge_android_symbols_{Guid.NewGuid()}.zip");
                    await ZipFiles(nativeSymbols, symbolsZipPath);

                    await UploadSymbolFile(config, symbolsZipPath, "android", "native");

                    if (File.Exists(symbolsZipPath))
                    {
                        File.Delete(symbolsZipPath);
                    }

                    uploadedAny = true;
                }

                if (!uploadedAny)
                {
                    Debug.LogWarning("[MoonForge] No symbol files found for Android build");
                }
                else
                {
                    Debug.Log("[MoonForge] Android symbol upload complete");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MoonForge] Failed to upload Android symbols: {ex.Message}");
            }
        }

        private List<string> FindDsymBundles(string buildPath)
        {
            var dsyms = new List<string>();

            // Check for dSYMs in common locations
            var searchPaths = new[]
            {
                buildPath,
                Path.Combine(buildPath, "dSYMs"),
                Path.GetDirectoryName(buildPath) ?? buildPath,
                Path.Combine(Path.GetDirectoryName(buildPath) ?? "", "dSYMs"),
            };

            foreach (var searchPath in searchPaths)
            {
                if (!Directory.Exists(searchPath)) continue;

                var directories = Directory.GetDirectories(searchPath, "*.dSYM", SearchOption.AllDirectories);
                dsyms.AddRange(directories);
            }

            return dsyms;
        }

        private string FindProguardMapping(string buildPath)
        {
            // Common locations for ProGuard mapping
            var searchPaths = new[]
            {
                Path.Combine(buildPath, "mapping.txt"),
                Path.Combine(Path.GetDirectoryName(buildPath) ?? "", "mapping.txt"),
                Path.Combine(Application.dataPath, "..", "Temp", "gradleOut", "build", "outputs", "mapping", "release", "mapping.txt"),
                Path.Combine(Application.dataPath, "..", "Library", "Bee", "Android", "Prj", "IL2CPP", "Gradle", "unityLibrary", "build", "outputs", "mapping", "release", "mapping.txt"),
            };

            foreach (var path in searchPaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            return null;
        }

        private List<string> FindAndroidNativeSymbols(string buildPath)
        {
            var symbols = new List<string>();

            // Search for .so.debug files and unstripped .so files
            var searchPaths = new[]
            {
                buildPath,
                Path.GetDirectoryName(buildPath) ?? buildPath,
                Path.Combine(Application.dataPath, "..", "Temp", "gradleOut"),
                Path.Combine(Application.dataPath, "..", "Library", "Bee", "Android"),
            };

            foreach (var searchPath in searchPaths)
            {
                if (!Directory.Exists(searchPath)) continue;

                // Look for debug symbols
                var debugSymbols = Directory.GetFiles(searchPath, "*.so.debug", SearchOption.AllDirectories);
                symbols.AddRange(debugSymbols);

                // Look for symbol directories
                var symbolDirs = Directory.GetDirectories(searchPath, "symbols", SearchOption.AllDirectories);
                foreach (var symbolDir in symbolDirs)
                {
                    var soFiles = Directory.GetFiles(symbolDir, "*.so", SearchOption.AllDirectories);
                    symbols.AddRange(soFiles);
                }
            }

            return symbols;
        }

        private async Task UploadSymbolFile(ErrorTrackerConfig config, string filePath, string platform, string symbolType)
        {
            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"[MoonForge] Symbol file not found: {filePath}");
                return;
            }

            var fileInfo = new FileInfo(filePath);
            Debug.Log($"[MoonForge] Uploading {symbolType} symbols ({fileInfo.Length / 1024}KB): {filePath}");

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(10); // Large files may take time

            using var content = new MultipartFormDataContent();

            // Add metadata
            var metadata = new
            {
                gameId = config.gameId,
                appVersion = Application.version,
                buildNumber = GetBuildNumber(),
                platform = platform,
                symbolType = symbolType,
                unityVersion = Application.unityVersion,
                il2cpp = IsIL2CPPBuild()
            };

            var metadataJson = JsonUtility.ToJson(new MetadataWrapper(metadata));
            content.Add(new StringContent(metadataJson), "metadata");

            // Add file
            var fileContent = new ByteArrayContent(await File.ReadAllBytesAsync(filePath));
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(GetContentType(symbolType));
            content.Add(fileContent, "file", Path.GetFileName(filePath));

            // Upload
            var url = $"{config.collectorUrl.TrimEnd('/')}/api/symbols/upload";
            var response = await client.PostAsync(url, content);

            if (response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                Debug.Log($"[MoonForge] Symbol upload successful: {responseBody}");
            }
            else
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                Debug.LogError($"[MoonForge] Symbol upload failed ({response.StatusCode}): {errorBody}");
            }
        }

        private string GetBuildNumber()
        {
#if UNITY_IOS
            return PlayerSettings.iOS.buildNumber;
#elif UNITY_ANDROID
            return PlayerSettings.Android.bundleVersionCode.ToString();
#else
            return Application.buildGUID?.Substring(0, 8) ?? "unknown";
#endif
        }

        private bool IsIL2CPPBuild()
        {
#if UNITY_IOS
            return PlayerSettings.GetScriptingBackend(BuildTargetGroup.iOS) == ScriptingBackend.IL2CPP;
#elif UNITY_ANDROID
            return PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android) == ScriptingBackend.IL2CPP;
#else
            return false;
#endif
        }

        private string GetContentType(string symbolType)
        {
            return symbolType switch
            {
                "dsym" or "native" => "application/zip",
                "proguard" => "text/plain",
                "sourcemap" => "application/json",
                _ => "application/octet-stream"
            };
        }

        private async Task ZipDirectory(string sourceDir, string zipPath)
        {
            // Use System.IO.Compression for zipping
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            await Task.Run(() =>
            {
                System.IO.Compression.ZipFile.CreateFromDirectory(sourceDir, zipPath);
            });
        }

        private async Task ZipFiles(List<string> files, string zipPath)
        {
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            await Task.Run(() =>
            {
                using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                {
                    foreach (var file in files)
                    {
                        if (File.Exists(file))
                        {
                            var entryName = Path.GetFileName(file);
                            var entry = archive.CreateEntry(entryName);
                            using (var entryStream = entry.Open())
                            using (var fileStream = File.OpenRead(file))
                            {
                                fileStream.CopyTo(entryStream);
                            }
                        }
                    }
                }
            });
        }

        // Helper class for JSON serialization
        [Serializable]
        private class MetadataWrapper
        {
            public string gameId;
            public string appVersion;
            public string buildNumber;
            public string platform;
            public string symbolType;
            public string unityVersion;
            public bool il2cpp;

            public MetadataWrapper(object data)
            {
                var type = data.GetType();
                gameId = (string)type.GetProperty("gameId")?.GetValue(data) ?? "";
                appVersion = (string)type.GetProperty("appVersion")?.GetValue(data) ?? "";
                buildNumber = (string)type.GetProperty("buildNumber")?.GetValue(data) ?? "";
                platform = (string)type.GetProperty("platform")?.GetValue(data) ?? "";
                symbolType = (string)type.GetProperty("symbolType")?.GetValue(data) ?? "";
                unityVersion = (string)type.GetProperty("unityVersion")?.GetValue(data) ?? "";
                il2cpp = (bool)(type.GetProperty("il2cpp")?.GetValue(data) ?? false);
            }
        }
    }
}
