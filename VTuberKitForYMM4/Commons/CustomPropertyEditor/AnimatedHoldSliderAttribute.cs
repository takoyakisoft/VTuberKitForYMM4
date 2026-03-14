using System.Reflection;
using System.Windows;
using YukkuriMovieMaker.Commons;

namespace VTuberKitForYMM4.Commons.CustomPropertyEditor
{
    public enum AnimatedHoldDisplayScaleMode
    {
        Raw,
        VideoHalfMinDimension,
    }

    public enum AnimatedHoldDynamicRangeMode
    {
        None,
        SymmetricVideoHalfLongDimension,
        PositiveVideoHalfLongDimension,
    }

    public class AnimatedHoldSliderAttribute : PropertyEditorAttribute2
    {
        public string StringFormat { get; }
        public string UnitText { get; }
        public double Delta { get; }
        public double DefaultMin { get; }
        public double DefaultMax { get; }
        public int Delay { get; }
        public string? HoldPropertyName { get; }
        public Type? ResourceType { get; set; }
        public double DisplayScale { get; set; } = 1.0;
        public AnimatedHoldDisplayScaleMode DisplayScaleMode { get; set; } = AnimatedHoldDisplayScaleMode.Raw;
        public AnimatedHoldDynamicRangeMode DynamicRangeMode { get; set; } = AnimatedHoldDynamicRangeMode.None;

        public AnimatedHoldSliderAttribute(string stringFormat, string unitText, double defaultMin, double defaultMax, string? holdPropertyName = null)
        {
            StringFormat = stringFormat;
            UnitText = unitText;
            HoldPropertyName = holdPropertyName;
            DefaultMin = defaultMin;
            DefaultMax = defaultMax;
            Delta = ParseDelta(stringFormat);
        }

        public override FrameworkElement Create()
        {
            return new AnimatedHoldEditor();
        }

        public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
        {
            ArgumentNullException.ThrowIfNull(control);
            ArgumentOutOfRangeException.ThrowIfZero(itemProperties.Length);
            ((AnimatedHoldEditor)control).Configure(itemProperties, this);
        }

        public override void ClearBindings(FrameworkElement control)
        {
            ((AnimatedHoldEditor)control).ClearBindings();
        }

        public string GetUnitText()
        {
            if (ResourceType != null)
            {
                return (string)ResourceType.GetProperty(UnitText)!.GetValue(null)!;
            }

            return UnitText;
        }

        public static Animation[] GetAnimations(ItemProperty[] itemProperties)
        {
            var source = itemProperties.Select(x => x.GetValue<Animation>()).OfType<Animation>();
            var first = source.FirstOrDefault();
            if (first != null)
            {
                return source.Where(x => x.MinValue == first.MinValue && x.MaxValue == first.MaxValue && x.Loop == first.Loop).ToArray();
            }

            return Array.Empty<Animation>();
        }

        private static double ParseDelta(string stringFormat)
        {
            if (stringFormat.StartsWith('F'))
            {
                return int.TryParse(stringFormat[1..], out var result) ? Math.Pow(0.1, result) : 1.0;
            }

            if (stringFormat.StartsWith('D'))
            {
                return 1.0;
            }

            throw new NotSupportedException("Not supported string format : " + stringFormat);
        }
    }
}
