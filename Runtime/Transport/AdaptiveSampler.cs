using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace MoonForge.ErrorTracking
{
    /// <summary>
    /// Client-side adaptive sampling to reduce error volume while preserving important errors
    /// </summary>
    public class AdaptiveSampler
    {
        private readonly ErrorTrackerConfig _config;

        // Track fingerprint counts within a time window
        private readonly Dictionary<string, FingerprintCounter> _counters;
        private readonly object _lock = new object();

        // Sampling thresholds
        private static readonly (int count, float rate)[] ExceptionThresholds = new[]
        {
            (10, 0.5f),
            (50, 0.2f),
            (100, 0.1f),
            (500, 0.05f)
        };

        private static readonly (int count, float rate)[] NetworkThresholds = new[]
        {
            (10, 0.2f),
            (50, 0.1f),
            (100, 0.05f),
            (500, 0.01f)
        };

        private static readonly (int count, float rate)[] CustomThresholds = new[]
        {
            (10, 0.1f),
            (50, 0.05f),
            (100, 0.02f),
            (500, 0.01f)
        };

        private const float MinSampleRate = 0.01f;
        private const float CounterWindowSeconds = 3600f; // 1 hour

        public AdaptiveSampler(ErrorTrackerConfig config)
        {
            _config = config;
            _counters = new Dictionary<string, FingerprintCounter>();
        }

        /// <summary>
        /// Determine if an error should be sampled (sent to server)
        /// </summary>
        public SamplingDecision ShouldSample(ErrorPayloadInner payload)
        {
            if (!_config.enableSampling)
            {
                return new SamplingDecision
                {
                    ShouldSend = true,
                    SampleRate = 1f,
                    Fingerprint = GenerateFingerprint(payload)
                };
            }

            var fingerprint = GenerateFingerprint(payload);
            var errorType = ParseErrorType(payload.errorType);
            var errorLevel = ParseErrorLevel(payload.errorLevel);

            // Fatal/crash errors are always sent
            if (errorLevel == ErrorLevel.Fatal || errorType == ErrorType.Crash)
            {
                IncrementCounter(fingerprint);
                return new SamplingDecision
                {
                    ShouldSend = true,
                    SampleRate = 1f,
                    Fingerprint = fingerprint
                };
            }

            // Get base sample rate from config
            var baseSampleRate = GetBaseSampleRate(errorType);

            // Get occurrence count
            var count = IncrementCounter(fingerprint);

            // Calculate adaptive sample rate based on frequency
            var sampleRate = CalculateSampleRate(errorType, baseSampleRate, count);

            // Make sampling decision
            var shouldSend = Random.value < sampleRate;

            if (_config.debugMode && !shouldSend)
            {
                Debug.Log($"[MoonForge] Error sampled out: {payload.exceptionClass ?? payload.message.Substring(0, Mathf.Min(50, payload.message.Length))} (rate: {sampleRate:F2}, count: {count})");
            }

            return new SamplingDecision
            {
                ShouldSend = shouldSend,
                SampleRate = sampleRate,
                Fingerprint = fingerprint,
                OccurrenceCount = count
            };
        }

        /// <summary>
        /// Generate a fingerprint for error grouping
        /// </summary>
        public string GenerateFingerprint(ErrorPayloadInner payload)
        {
            // Build fingerprint source
            var sb = new StringBuilder();

            // Include error class if available
            if (!string.IsNullOrEmpty(payload.exceptionClass))
            {
                sb.Append(payload.exceptionClass);
                sb.Append('|');
            }

            // Normalize message (remove numbers and memory addresses)
            var normalizedMessage = NormalizeMessage(payload.message);
            sb.Append(normalizedMessage.Substring(0, Mathf.Min(200, normalizedMessage.Length)));
            sb.Append('|');

            // Include top in-app frame
            if (payload.frames != null && payload.frames.Count > 0)
            {
                foreach (var frame in payload.frames)
                {
                    if (frame.inApp)
                    {
                        sb.Append(frame.module ?? "");
                        sb.Append(':');
                        sb.Append(frame.function ?? "");
                        sb.Append(':');
                        sb.Append(frame.lineno);
                        break;
                    }
                }
            }

            // Generate SHA256 hash
            using (var sha256 = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(sb.ToString());
                var hash = sha256.ComputeHash(bytes);

                // Convert to hex string (first 32 chars = 16 bytes)
                var hashSb = new StringBuilder(64);
                foreach (var b in hash)
                {
                    hashSb.Append(b.ToString("x2"));
                }
                return hashSb.ToString();
            }
        }

        /// <summary>
        /// Periodic cleanup of old counters
        /// </summary>
        public void Cleanup()
        {
            lock (_lock)
            {
                var now = Time.unscaledTime;
                var keysToRemove = new List<string>();

                foreach (var kvp in _counters)
                {
                    if (now - kvp.Value.FirstSeen > CounterWindowSeconds)
                    {
                        keysToRemove.Add(kvp.Key);
                    }
                }

                foreach (var key in keysToRemove)
                {
                    _counters.Remove(key);
                }
            }
        }

        /// <summary>
        /// Reset all counters
        /// </summary>
        public void Reset()
        {
            lock (_lock)
            {
                _counters.Clear();
            }
        }

        private float GetBaseSampleRate(ErrorType errorType)
        {
            return errorType switch
            {
                ErrorType.Crash => 1f, // Always sample crashes
                ErrorType.Exception => _config.exceptionSampleRate,
                ErrorType.Network => _config.networkErrorSampleRate,
                ErrorType.Custom => _config.customErrorSampleRate,
                _ => _config.exceptionSampleRate
            };
        }

        private float CalculateSampleRate(ErrorType errorType, float baseRate, int count)
        {
            var thresholds = errorType switch
            {
                ErrorType.Exception => ExceptionThresholds,
                ErrorType.Network => NetworkThresholds,
                ErrorType.Custom => CustomThresholds,
                _ => ExceptionThresholds
            };

            var rate = baseRate;
            foreach (var (thresholdCount, thresholdRate) in thresholds)
            {
                if (count >= thresholdCount)
                {
                    rate = thresholdRate;
                }
                else
                {
                    break;
                }
            }

            return Mathf.Max(rate, MinSampleRate);
        }

        private int IncrementCounter(string fingerprint)
        {
            lock (_lock)
            {
                var now = Time.unscaledTime;

                if (_counters.TryGetValue(fingerprint, out var counter))
                {
                    // Reset if window expired
                    if (now - counter.FirstSeen > CounterWindowSeconds)
                    {
                        counter.Count = 1;
                        counter.FirstSeen = now;
                    }
                    else
                    {
                        counter.Count++;
                    }
                    counter.LastSeen = now;
                }
                else
                {
                    counter = new FingerprintCounter
                    {
                        Count = 1,
                        FirstSeen = now,
                        LastSeen = now
                    };
                    _counters[fingerprint] = counter;
                }

                return counter.Count;
            }
        }

        private string NormalizeMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return "";

            // Replace memory addresses (0x...)
            var normalized = System.Text.RegularExpressions.Regex.Replace(
                message, @"0x[0-9a-fA-F]+", "<addr>");

            // Replace numbers
            normalized = System.Text.RegularExpressions.Regex.Replace(
                normalized, @"\d+", "<num>");

            return normalized;
        }

        private ErrorType ParseErrorType(string type)
        {
            return type?.ToLowerInvariant() switch
            {
                "crash" => ErrorType.Crash,
                "exception" => ErrorType.Exception,
                "network" => ErrorType.Network,
                "custom" => ErrorType.Custom,
                _ => ErrorType.Exception
            };
        }

        private ErrorLevel ParseErrorLevel(string level)
        {
            return level?.ToLowerInvariant() switch
            {
                "fatal" => ErrorLevel.Fatal,
                "error" => ErrorLevel.Error,
                "warning" => ErrorLevel.Warning,
                "info" => ErrorLevel.Info,
                _ => ErrorLevel.Error
            };
        }

        private class FingerprintCounter
        {
            public int Count;
            public float FirstSeen;
            public float LastSeen;
        }
    }

    /// <summary>
    /// Result of a sampling decision
    /// </summary>
    public struct SamplingDecision
    {
        public bool ShouldSend;
        public float SampleRate;
        public string Fingerprint;
        public int OccurrenceCount;
    }
}
