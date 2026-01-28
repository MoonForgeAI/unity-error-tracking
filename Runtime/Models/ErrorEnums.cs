namespace MoonForge.ErrorTracking
{
    /// <summary>
    /// Type of error being reported
    /// </summary>
    public enum ErrorType
    {
        /// <summary>Native crash (SIGSEGV, etc.)</summary>
        Crash,
        /// <summary>Managed exception (C# Exception)</summary>
        Exception,
        /// <summary>Network/HTTP error</summary>
        Network,
        /// <summary>Custom error logged by game code</summary>
        Custom
    }

    /// <summary>
    /// Category of error
    /// </summary>
    public enum ErrorCategory
    {
        /// <summary>Native code error (iOS/Android native)</summary>
        Native,
        /// <summary>Managed code error (C#/IL2CPP)</summary>
        Managed,
        /// <summary>Error was caught and handled by game code</summary>
        Handled,
        /// <summary>Unhandled error that propagated to Unity</summary>
        Unhandled
    }

    /// <summary>
    /// Severity level of the error
    /// </summary>
    public enum ErrorLevel
    {
        /// <summary>Informational message</summary>
        Info,
        /// <summary>Warning that may indicate a problem</summary>
        Warning,
        /// <summary>Error that affected functionality</summary>
        Error,
        /// <summary>Fatal error that crashed or will crash the application</summary>
        Fatal
    }

    /// <summary>
    /// Type of breadcrumb event
    /// </summary>
    public enum BreadcrumbType
    {
        /// <summary>Navigation between scenes/screens</summary>
        Navigation,
        /// <summary>Network request or response</summary>
        Network,
        /// <summary>User interaction (button click, etc.)</summary>
        User,
        /// <summary>Debug information</summary>
        Debug,
        /// <summary>Error or warning that occurred</summary>
        Error
    }

    /// <summary>
    /// Level of breadcrumb event
    /// </summary>
    public enum BreadcrumbLevel
    {
        Debug,
        Info,
        Warning,
        Error,
        Fatal
    }

    /// <summary>
    /// Device thermal state (iOS/Android)
    /// </summary>
    public enum ThermalState
    {
        /// <summary>Normal operating temperature</summary>
        Nominal,
        /// <summary>Elevated temperature, may throttle soon</summary>
        Fair,
        /// <summary>High temperature, performance is being throttled</summary>
        Serious,
        /// <summary>Critical temperature, app may be terminated</summary>
        Critical
    }

    /// <summary>
    /// Network connection type
    /// </summary>
    public enum NetworkType
    {
        None,
        Wifi,
        Cellular,
        Ethernet
    }
}
