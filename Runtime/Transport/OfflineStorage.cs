using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MoonForge.ErrorTracking
{
    /// <summary>
    /// Persists errors to disk when offline for later submission
    /// </summary>
    public class OfflineStorage
    {
        private readonly ErrorTrackerConfig _config;
        private readonly string _storagePath;
        private readonly object _lock = new object();

        private const string StorageFolder = "MoonForgeErrors";
        private const string FileExtension = ".json";

        public OfflineStorage(ErrorTrackerConfig config)
        {
            _config = config;
            _storagePath = Path.Combine(Application.persistentDataPath, StorageFolder);

            // Ensure directory exists
            if (!Directory.Exists(_storagePath))
            {
                Directory.CreateDirectory(_storagePath);
            }
        }

        /// <summary>
        /// Store an error for later submission
        /// </summary>
        public bool Store(ErrorPayloadInner payload)
        {
            if (!_config.enableOfflineStorage) return false;

            lock (_lock)
            {
                try
                {
                    // Check if we've reached max stored errors
                    var existingFiles = GetStoredFiles();
                    if (existingFiles.Length >= _config.maxOfflineErrors)
                    {
                        // Remove oldest file
                        if (existingFiles.Length > 0)
                        {
                            Array.Sort(existingFiles, (a, b) => a.CreationTimeUtc.CompareTo(b.CreationTimeUtc));
                            File.Delete(existingFiles[0].FullName);

                            if (_config.debugMode)
                            {
                                Debug.Log($"[MoonForge] Removed oldest offline error to make room");
                            }
                        }
                    }

                    // Create wrapper for storage
                    var wrapper = new StoredError
                    {
                        payload = payload,
                        storedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    };

                    // Generate unique filename
                    var filename = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}{FileExtension}";
                    var filePath = Path.Combine(_storagePath, filename);

                    // Serialize and write
                    var json = JsonUtility.ToJson(wrapper);
                    File.WriteAllText(filePath, json);

                    if (_config.debugMode)
                    {
                        Debug.Log($"[MoonForge] Error stored offline: {filename}");
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    if (_config.debugMode)
                    {
                        Debug.LogWarning($"[MoonForge] Failed to store error offline: {ex.Message}");
                    }
                    return false;
                }
            }
        }

        /// <summary>
        /// Get all stored errors
        /// </summary>
        public List<ErrorPayloadInner> GetStoredErrors()
        {
            var errors = new List<ErrorPayloadInner>();

            lock (_lock)
            {
                try
                {
                    var files = GetStoredFiles();
                    foreach (var file in files)
                    {
                        try
                        {
                            var json = File.ReadAllText(file.FullName);
                            var wrapper = JsonUtility.FromJson<StoredError>(json);
                            if (wrapper?.payload != null)
                            {
                                errors.Add(wrapper.payload);
                            }
                        }
                        catch (Exception ex)
                        {
                            if (_config.debugMode)
                            {
                                Debug.LogWarning($"[MoonForge] Failed to read stored error: {ex.Message}");
                            }
                            // Delete corrupt file
                            try { File.Delete(file.FullName); } catch { }
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (_config.debugMode)
                    {
                        Debug.LogWarning($"[MoonForge] Failed to get stored errors: {ex.Message}");
                    }
                }
            }

            return errors;
        }

        /// <summary>
        /// Clear all stored errors
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                try
                {
                    var files = GetStoredFiles();
                    foreach (var file in files)
                    {
                        try
                        {
                            File.Delete(file.FullName);
                        }
                        catch { }
                    }

                    if (_config.debugMode)
                    {
                        Debug.Log($"[MoonForge] Cleared {files.Length} stored errors");
                    }
                }
                catch (Exception ex)
                {
                    if (_config.debugMode)
                    {
                        Debug.LogWarning($"[MoonForge] Failed to clear stored errors: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Get the count of stored errors
        /// </summary>
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    try
                    {
                        return GetStoredFiles().Length;
                    }
                    catch
                    {
                        return 0;
                    }
                }
            }
        }

        /// <summary>
        /// Remove a specific stored error file
        /// </summary>
        public void Remove(string filename)
        {
            lock (_lock)
            {
                try
                {
                    var filePath = Path.Combine(_storagePath, filename);
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// Clean up old stored errors (older than 7 days)
        /// </summary>
        public void Cleanup(int maxAgeDays = 7)
        {
            lock (_lock)
            {
                try
                {
                    var cutoff = DateTime.UtcNow.AddDays(-maxAgeDays);
                    var files = GetStoredFiles();
                    var removed = 0;

                    foreach (var file in files)
                    {
                        if (file.CreationTimeUtc < cutoff)
                        {
                            try
                            {
                                File.Delete(file.FullName);
                                removed++;
                            }
                            catch { }
                        }
                    }

                    if (_config.debugMode && removed > 0)
                    {
                        Debug.Log($"[MoonForge] Cleaned up {removed} old stored errors");
                    }
                }
                catch { }
            }
        }

        private FileInfo[] GetStoredFiles()
        {
            if (!Directory.Exists(_storagePath))
            {
                return Array.Empty<FileInfo>();
            }

            var directory = new DirectoryInfo(_storagePath);
            return directory.GetFiles($"*{FileExtension}");
        }

        /// <summary>
        /// Wrapper for stored error with metadata
        /// </summary>
        [Serializable]
        private class StoredError
        {
            public ErrorPayloadInner payload;
            public long storedAt;
        }
    }
}
