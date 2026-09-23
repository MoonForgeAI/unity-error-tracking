namespace MoonForge.ErrorTracking
{
    /// <summary>SDK identity sent to the collector. Keep Version in step with package.json.</summary>
    public static class SdkInfo
    {
        public const string Version = "1.0.6";

        /// <summary>User-Agent prefix; the collector's logs are split by it.</summary>
        public const string UserAgentProduct = "MoonForge-Unity-SDK/" + Version;
    }
}
