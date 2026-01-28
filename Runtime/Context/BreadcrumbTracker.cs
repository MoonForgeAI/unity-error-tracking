using System.Collections.Generic;

namespace MoonForge.ErrorTracking
{
    /// <summary>
    /// Tracks breadcrumbs (user actions and events) leading up to errors
    /// </summary>
    public class BreadcrumbTracker
    {
        private readonly LinkedList<Breadcrumb> _breadcrumbs;
        private int _maxBreadcrumbs;
        private readonly object _lock = new object();

        private static BreadcrumbTracker _instance;
        public static BreadcrumbTracker Instance => _instance ??= new BreadcrumbTracker();

        private BreadcrumbTracker()
        {
            _breadcrumbs = new LinkedList<Breadcrumb>();
            _maxBreadcrumbs = 100;
        }

        /// <summary>
        /// Configure the maximum number of breadcrumbs to retain
        /// </summary>
        public void Configure(int maxBreadcrumbs)
        {
            lock (_lock)
            {
                _maxBreadcrumbs = maxBreadcrumbs;

                // Trim if necessary
                while (_breadcrumbs.Count > _maxBreadcrumbs)
                {
                    _breadcrumbs.RemoveFirst();
                }
            }
        }

        /// <summary>
        /// Add a breadcrumb
        /// </summary>
        public void Add(Breadcrumb breadcrumb)
        {
            if (breadcrumb == null) return;

            lock (_lock)
            {
                _breadcrumbs.AddLast(breadcrumb);

                while (_breadcrumbs.Count > _maxBreadcrumbs)
                {
                    _breadcrumbs.RemoveFirst();
                }
            }
        }

        /// <summary>
        /// Add a simple breadcrumb with message
        /// </summary>
        public void Add(BreadcrumbType type, string message, BreadcrumbLevel level = BreadcrumbLevel.Info, string category = null)
        {
            Add(new Breadcrumb(type, message, level, category));
        }

        /// <summary>
        /// Add a navigation breadcrumb (scene change, screen transition)
        /// </summary>
        public void AddNavigation(string message, string category = null)
        {
            Add(BreadcrumbType.Navigation, message, BreadcrumbLevel.Info, category);
        }

        /// <summary>
        /// Add a user interaction breadcrumb
        /// </summary>
        public void AddUserAction(string message, string category = null)
        {
            Add(BreadcrumbType.User, message, BreadcrumbLevel.Info, category);
        }

        /// <summary>
        /// Add a network request breadcrumb
        /// </summary>
        public void AddNetworkRequest(string method, string url, int? statusCode = null, float? durationMs = null)
        {
            var message = statusCode.HasValue
                ? $"{method} {url} - {statusCode}"
                : $"{method} {url}";

            var breadcrumb = new Breadcrumb(
                BreadcrumbType.Network,
                message,
                statusCode >= 400 ? BreadcrumbLevel.Error : BreadcrumbLevel.Info,
                "http"
            );

            if (durationMs.HasValue || statusCode.HasValue)
            {
                breadcrumb.WithData(new Dictionary<string, object>
                {
                    { "method", method },
                    { "url", url },
                    { "status_code", statusCode },
                    { "duration_ms", durationMs }
                });
            }

            Add(breadcrumb);
        }

        /// <summary>
        /// Add a debug breadcrumb
        /// </summary>
        public void AddDebug(string message, Dictionary<string, object> data = null)
        {
            var breadcrumb = new Breadcrumb(BreadcrumbType.Debug, message, BreadcrumbLevel.Debug);
            if (data != null)
            {
                breadcrumb.WithData(data);
            }
            Add(breadcrumb);
        }

        /// <summary>
        /// Add an error breadcrumb
        /// </summary>
        public void AddError(string message, string category = null)
        {
            Add(BreadcrumbType.Error, message, BreadcrumbLevel.Error, category);
        }

        /// <summary>
        /// Get a copy of all breadcrumbs
        /// </summary>
        public List<Breadcrumb> GetBreadcrumbs()
        {
            lock (_lock)
            {
                return new List<Breadcrumb>(_breadcrumbs);
            }
        }

        /// <summary>
        /// Clear all breadcrumbs
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _breadcrumbs.Clear();
            }
        }

        /// <summary>
        /// Get the count of breadcrumbs
        /// </summary>
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _breadcrumbs.Count;
                }
            }
        }
    }
}
