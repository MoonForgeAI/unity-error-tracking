using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoonForge.ErrorTracking
{
    /// <summary>
    /// Queues errors for batched submission
    /// </summary>
    public class BatchQueue
    {
        private readonly ErrorTrackerConfig _config;
        private readonly HttpTransport _transport;
        private readonly Action<ErrorPayloadInner> _onSendFailed;

        private readonly List<BatchErrorItem> _queue;
        private readonly object _lock = new object();

        private float _lastBatchTime;
        private bool _isSending;

        public BatchQueue(ErrorTrackerConfig config, HttpTransport transport, Action<ErrorPayloadInner> onSendFailed = null)
        {
            _config = config;
            _transport = transport;
            _onSendFailed = onSendFailed;
            _queue = new List<BatchErrorItem>();
            _lastBatchTime = Time.unscaledTime;
        }

        /// <summary>
        /// Add an error to the batch queue
        /// </summary>
        public void Enqueue(ErrorPayloadInner payload)
        {
            var item = ConvertToQueueItem(payload);

            lock (_lock)
            {
                _queue.Add(item);

                if (_config.debugMode)
                {
                    Debug.Log($"[MoonForge] Error queued. Queue size: {_queue.Count}");
                }

                // Check if we should send immediately
                if (_queue.Count >= _config.maxBatchSize)
                {
                    FlushInternal();
                }
            }
        }

        /// <summary>
        /// Update the batch queue (call from Update loop)
        /// </summary>
        public void Update()
        {
            if (_isSending) return;

            lock (_lock)
            {
                if (_queue.Count == 0) return;

                // Check if max wait time exceeded
                var elapsed = Time.unscaledTime - _lastBatchTime;
                if (elapsed >= _config.maxBatchWaitTime)
                {
                    FlushInternal();
                }
            }
        }

        /// <summary>
        /// Flush all queued errors immediately
        /// </summary>
        public void Flush()
        {
            lock (_lock)
            {
                if (_queue.Count > 0)
                {
                    FlushInternal();
                }
            }
        }

        /// <summary>
        /// Get the current queue size
        /// </summary>
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _queue.Count;
                }
            }
        }

        /// <summary>
        /// Get all queued items (for offline storage)
        /// </summary>
        public List<BatchErrorItem> GetQueuedItems()
        {
            lock (_lock)
            {
                return new List<BatchErrorItem>(_queue);
            }
        }

        /// <summary>
        /// Clear the queue
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _queue.Clear();
            }
        }

        private void FlushInternal()
        {
            if (_isSending || _queue.Count == 0) return;
            if (!_transport.HasConnectivity()) return;

            _isSending = true;
            _lastBatchTime = Time.unscaledTime;

            // Take items from queue
            var itemsToSend = new List<BatchErrorItem>();
            var count = Math.Min(_queue.Count, _config.maxBatchSize);

            for (var i = 0; i < count; i++)
            {
                itemsToSend.Add(_queue[i]);
            }

            _queue.RemoveRange(0, count);

            // Create batch payload
            var batchPayload = new ErrorBatchPayload
            {
                game = _config.gameId,
                errors = itemsToSend
            };

            if (_config.debugMode)
            {
                Debug.Log($"[MoonForge] Flushing batch with {itemsToSend.Count} errors");
            }

            // Send batch
            _transport.SendBatch(batchPayload, response =>
            {
                _isSending = false;

                if (response?.status == "error")
                {
                    if (_config.debugMode)
                    {
                        Debug.LogWarning($"[MoonForge] Batch send failed: {response.error}");
                    }

                    // Re-queue failed items for retry (they might be persisted offline)
                    // Don't re-queue if it was a client error (4xx except 429)
                }
                else if (_config.debugMode)
                {
                    Debug.Log($"[MoonForge] Batch completed: {response?.accepted}/{response?.total} accepted");
                }
            });
        }

        private BatchErrorItem ConvertToQueueItem(ErrorPayloadInner payload)
        {
            return new BatchErrorItem
            {
                clientErrorId = Guid.NewGuid().ToString(),
                errorType = payload.errorType,
                errorCategory = payload.errorCategory,
                errorLevel = payload.errorLevel,
                message = payload.message,
                frames = payload.frames,
                rawStackTrace = payload.rawStackTrace,
                exceptionClass = payload.exceptionClass,
                fingerprint = payload.fingerprint,
                device = payload.device,
                network = payload.network,
                gameState = payload.gameState,
                appVersion = payload.appVersion,
                buildNumber = payload.buildNumber,
                unityVersion = payload.unityVersion,
                userId = payload.userId,
                sessionId = payload.sessionId,
                breadcrumbs = payload.breadcrumbs,
                timestamp = payload.timestamp,
                networkRequest = payload.networkRequest,
                tags = payload.tags
            };
        }
    }
}
