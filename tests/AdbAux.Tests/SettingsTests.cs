using System;
using System.Reflection;
using Xunit;

namespace AdbAux.Tests
{
    /// <summary>Settings: Apply must compare and copy every setting shown on the Advanced tab.</summary>
    public class SettingsTests
    {
        // Settings that are not on the Advanced tab, so Apply neither compares nor copies them.
        private static readonly string[] NotOnAdvancedTab =
        {
            "Profiles", "Updates", "LoudModeWarningShown", "WindowLeft", "WindowTop", "WindowWidth", "WindowHeight", "WindowMaximized"
        };

        [Fact]
        public void CompareAndCopyCoverEveryAdvancedSetting()
        {
            foreach (FieldInfo field in typeof(AppSettings).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (Array.IndexOf(NotOnAdvancedTab, field.Name) >= 0)
                {
                    continue;
                }
                var original = new AppSettings();
                AppSettings changed = original.Clone();
                field.SetValue(changed, OtherValue(field.GetValue(changed)));

                Assert.False(original.SameAdvancedSettings(changed), field.Name + " is not compared by SameAdvancedSettings");

                var copy = new AppSettings();
                copy.CopyAdvancedSettings(changed);
                Assert.True(copy.SameAdvancedSettings(changed), field.Name + " is not copied by CopyAdvancedSettings");
            }
        }

        [Fact]
        public void SanitizeBringsValuesIntoRange()
        {
            var s = new AppSettings { MusicBufferMs = 0, VideoBufferMs = 5000, OutputBufferMs = 1000, Theme = (ThemeChoice)9, Profiles = null };

            s.Sanitize();

            Assert.Equal(Limits.MinBufferMs, s.MusicBufferMs);
            Assert.Equal(Limits.MaxBufferMs, s.VideoBufferMs);
            Assert.Equal(Limits.MaxOutputBufferMs, s.OutputBufferMs);
            Assert.Equal(ThemeChoice.System, s.Theme);
            Assert.NotNull(s.Profiles);
        }

        private static object OtherValue(object value)
        {
            if (value is int)
            {
                return (int)value + 1;
            }
            if (value is bool)
            {
                return !(bool)value;
            }
            if (value is string)
            {
                return (string)value + "x";
            }
            if (value is Enum)
            {
                Array values = Enum.GetValues(value.GetType());
                int index = Array.IndexOf(values, value);
                return values.GetValue((index + 1) % values.Length);
            }
            throw new InvalidOperationException("No test value for " + value.GetType().Name + "; extend OtherValue.");
        }
    }
}
