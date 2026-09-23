using System;
using System.Globalization;

namespace MoonForge.ErrorTracking
{
    /// <summary>
    /// Writes numbers as JSON number literals, independent of the player's OS locale.
    ///
    /// Before 1.0.6 event data numbers were written with value.ToString(), which
    /// follows the current culture: on a decimal-comma locale (Indonesian,
    /// Swedish, German, French, Brazilian Portuguese, ...) 1.5 became "1,5", some
    /// locales write negatives with U+2212, and NaN / infinity came out as bare
    /// words. Each made the request invalid JSON, so the collector rejected it and
    /// the event was lost.
    /// </summary>
    public static class JsonNumber
    {
        /// <summary>
        /// True if <paramref name="value"/> is a numeric type; <paramref name="json"/>
        /// is then its JSON literal ("null" for NaN and infinities, which JSON cannot express).
        /// </summary>
        public static bool TryFormat(object value, out string json)
        {
            switch (value)
            {
                case float f:
                    json = float.IsNaN(f) || float.IsInfinity(f) ? "null" : f.ToString("R", CultureInfo.InvariantCulture);
                    return true;
                case double d:
                    json = double.IsNaN(d) || double.IsInfinity(d) ? "null" : d.ToString("R", CultureInfo.InvariantCulture);
                    return true;
                case decimal m:
                    json = m.ToString(CultureInfo.InvariantCulture);
                    return true;
                case int _:
                case long _:
                case short _:
                case sbyte _:
                case byte _:
                case uint _:
                case ulong _:
                case ushort _:
                    json = Convert.ToString(value, CultureInfo.InvariantCulture);
                    return true;
                default:
                    json = null;
                    return false;
            }
        }

        /// <summary>An integer as a JSON literal. A culture can use U+2212 for minus.</summary>
        public static string Format(long value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>A float as a JSON literal ("null" when not finite).</summary>
        public static string Format(float value)
        {
            TryFormat(value, out var json);
            return json;
        }

        /// <summary>A double as a JSON literal ("null" when not finite).</summary>
        public static string Format(double value)
        {
            TryFormat(value, out var json);
            return json;
        }

        /// <summary>
        /// Text for a value that is not a JSON primitive, e.g. a Vector3. Uses the
        /// invariant culture so the text is the same on every player's machine.
        /// </summary>
        public static string InvariantText(object value)
        {
            return value is IFormattable formattable
                ? formattable.ToString(null, CultureInfo.InvariantCulture)
                : value.ToString();
        }
    }
}
