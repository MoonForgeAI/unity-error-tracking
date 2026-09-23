using System.Globalization;
using System.Threading;
using NUnit.Framework;

namespace MoonForge.ErrorTracking.Tests
{
    /// <summary>
    /// Numbers must serialise identically on every player's OS locale. Before
    /// 1.0.6 a decimal-comma locale wrote 1.5 as "1,5" and the collector
    /// rejected the whole event as invalid JSON.
    /// </summary>
    public class JsonNumberTests
    {
        private CultureInfo _saved;

        [SetUp] public void SaveCulture() => _saved = Thread.CurrentThread.CurrentCulture;
        [TearDown] public void RestoreCulture() => Thread.CurrentThread.CurrentCulture = _saved;

        [TestCase("id-ID")]
        [TestCase("sv-SE")]
        [TestCase("de-DE")]
        [TestCase("fr-FR")]
        [TestCase("en-US")]
        public void DecimalsUseADotInEveryLocale(string culture)
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo(culture);
            Assert.That(JsonNumber.Format(1.5f), Is.EqualTo("1.5"));
            Assert.That(JsonNumber.Format(-2.25), Is.EqualTo("-2.25"));
            Assert.That(JsonNumber.Format(-1758000000L), Is.EqualTo("-1758000000"));
        }

        [Test]
        public void NonFiniteValuesBecomeNull()
        {
            Assert.That(JsonNumber.Format(float.NaN), Is.EqualTo("null"));
            Assert.That(JsonNumber.Format(double.PositiveInfinity), Is.EqualTo("null"));
            Assert.That(JsonNumber.Format(float.NegativeInfinity), Is.EqualTo("null"));
        }

        [Test]
        public void EveryNumericTypeIsANumberAndNothingElseIs()
        {
            foreach (var v in new object[] { 1, 2L, (short)3, (byte)4, 5u, 6UL, (ushort)7, (sbyte)-8, 9.5f, 10.5, 11.5m })
                Assert.That(JsonNumber.TryFormat(v, out _), Is.True, v.GetType().Name);
            Assert.That(JsonNumber.TryFormat("1.5", out _), Is.False);
            Assert.That(JsonNumber.TryFormat(true, out _), Is.False);
        }

        [Test]
        public void FormattableValuesUseTheInvariantCulture()
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("id-ID");
            Assert.That(JsonNumber.InvariantText(new UnityEngine.Vector2(1.5f, 2f)), Does.Contain("1.5"));
        }
    }
}
