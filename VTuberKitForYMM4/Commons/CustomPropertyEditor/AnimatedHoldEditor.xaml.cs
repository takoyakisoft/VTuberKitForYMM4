using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;

namespace VTuberKitForYMM4.Commons.CustomPropertyEditor
{
    public partial class AnimatedHoldEditor : UserControl, IPropertyEditorControl, IPropertyEditorControl2
    {
        public static readonly DependencyProperty HoldValueProperty =
            DependencyProperty.Register(
                nameof(HoldValue),
                typeof(bool),
                typeof(AnimatedHoldEditor),
                new PropertyMetadata(false, OnHoldValueChanged));

        private ItemProperty[] animationItemProperties = [];
        private PropertyInfo? holdPropertyInfo;
        private object[] holdOwners = [];
        private bool isUpdatingHoldCheckBox;
        private IEditorInfo? editorInfo;
        private AnimatedHoldSliderAttribute? currentSettings;

        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;

        public bool HoldValue
        {
            get => (bool)GetValue(HoldValueProperty);
            set => SetValue(HoldValueProperty, value);
        }

        public AnimatedHoldEditor()
        {
            InitializeComponent();
        }

        public void Configure(ItemProperty[] itemProperties, AnimatedHoldSliderAttribute settings)
        {
            ArgumentNullException.ThrowIfNull(itemProperties);
            ArgumentOutOfRangeException.ThrowIfZero(itemProperties.Length);

            animationItemProperties = itemProperties;
            currentSettings = settings;

            var animation = itemProperties[0].GetValue<Animation>();
            var animations = AnimatedHoldSliderAttribute.GetAnimations(itemProperties);

            AnimationEditor.StringFormat = settings.StringFormat;
            AnimationEditor.Unit = settings.GetUnitText();
            AnimationEditor.Delta = settings.Delta;
            AnimationEditor.DefaultMin = settings.DefaultMin;
            AnimationEditor.DefaultMax = settings.DefaultMax;
            AnimationEditor.Delay = settings.Delay;
            AnimationEditor.DefaultValue = animation?.DefaultValue ?? settings.DefaultMin;
            AnimationEditor.Animation = animation;
            AnimationEditor.Animations = animations;
            ApplyPresentationSettings(settings);

            ResolveHoldProperty(itemProperties, settings.HoldPropertyName);
            UpdateHoldCheckBox();

            if (editorInfo != null)
            {
                AnimationEditor.SetEditorInfo(editorInfo);
                ApplyEditorInfoToAnimations(animations);
            }
        }

        public void ClearBindings()
        {
            animationItemProperties = [];
            holdPropertyInfo = null;
            holdOwners = [];
            AnimationEditor.Animations = null;
            AnimationEditor.Animation = null;
            currentSettings = null;
            HoldCheckBox.Visibility = Visibility.Hidden;
            HoldValue = false;
        }

        public void SetEditorInfo(IEditorInfo info)
        {
            editorInfo = info;
            AnimationEditor.SetEditorInfo(info);
            if (currentSettings != null)
            {
                ApplyPresentationSettings(currentSettings);
            }
            ApplyEditorInfoToAnimations(AnimationEditor.Animations ?? (AnimationEditor.Animation != null ? [AnimationEditor.Animation] : []));
        }

        public void SetFocus()
        {
            AnimationEditor.Focus();
        }

        private void ApplyEditorInfoToAnimations(Animation[] animations)
        {
            if (editorInfo == null || animations.Length == 0)
            {
                return;
            }

            var length = (int)Math.Clamp((long)editorInfo.ItemDuration.Frame, 1L, int.MaxValue);
            var fps = Math.Max(1, editorInfo.VideoInfo.FPS);
            var keyFrames = editorInfo.KeyFrames;

            foreach (var animation in animations)
            {
                animation.SetKeyFrames(keyFrames);
                animation.SetAnimationParameters(length, fps);
            }
        }

        private void ResolveHoldProperty(ItemProperty[] itemProperties, string? explicitName)
        {
            var ownerType = itemProperties[0].PropertyOwner.GetType();
            var propertyName = explicitName;
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                propertyName = itemProperties[0].PropertyInfo.Name + "Hold";
            }

            holdPropertyInfo = ownerType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (holdPropertyInfo == null || holdPropertyInfo.PropertyType != typeof(bool) || !holdPropertyInfo.CanRead || !holdPropertyInfo.CanWrite)
            {
                holdPropertyInfo = null;
                holdOwners = [];
                return;
            }

            holdOwners = itemProperties
                .Select(x => x.PropertyOwner)
                .Where(x => holdPropertyInfo.DeclaringType?.IsInstanceOfType(x) == true)
                .ToArray();
        }

        private void UpdateHoldCheckBox()
        {
            isUpdatingHoldCheckBox = true;
            try
            {
                if (holdPropertyInfo == null || holdOwners.Length == 0)
                {
                    HoldCheckBox.Visibility = Visibility.Hidden;
                    HoldValue = false;
                    return;
                }

                HoldCheckBox.Visibility = Visibility.Visible;
                HoldValue = (bool?)holdPropertyInfo.GetValue(holdOwners[0]) ?? false;
            }
            finally
            {
                isUpdatingHoldCheckBox = false;
            }
        }

        private void AnimationEditor_BeginEdit(object sender, EventArgs e)
        {
            if (!IsLoaded)
            {
                return;
            }

            EnsureHoldEnabled();
            BeginEdit?.Invoke(this, EventArgs.Empty);
        }

        private void AnimationEditor_EndEdit(object sender, EventArgs e)
        {
            if (!IsLoaded)
            {
                return;
            }

            EnsureHoldEnabled();
            EndEdit?.Invoke(this, EventArgs.Empty);
        }

        private void EnsureHoldEnabled()
        {
            if (holdPropertyInfo == null || holdOwners.Length == 0)
            {
                return;
            }

            var changed = false;
            foreach (var owner in holdOwners)
            {
                if ((bool?)holdPropertyInfo.GetValue(owner) == true)
                {
                    continue;
                }

                holdPropertyInfo.SetValue(owner, true);
                changed = true;
            }

            if (changed)
            {
                UpdateHoldCheckBox();
            }
        }

        private static void OnHoldValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AnimatedHoldEditor)d).ApplyHoldValueChanged((bool)e.NewValue);
        }

        private void ApplyPresentationSettings(AnimatedHoldSliderAttribute settings)
        {
            var scale = settings.DisplayScaleMode switch
            {
                AnimatedHoldDisplayScaleMode.VideoHalfMinDimension => ResolveVideoHalfMinDimension(),
                _ => settings.DisplayScale,
            };

            if (scale <= 0)
            {
                scale = 1.0;
            }

            SetAnimationEditorProperty("Scale", scale);
            ApplyDynamicRange(settings, scale);
        }

        private void ApplyDynamicRange(AnimatedHoldSliderAttribute settings, double scale)
        {
            var (defaultMin, defaultMax) = settings.DynamicRangeMode switch
            {
                AnimatedHoldDynamicRangeMode.SymmetricVideoHalfLongDimension => ResolveLongSideRange(scale, symmetric: true, settings.DefaultMin, settings.DefaultMax),
                AnimatedHoldDynamicRangeMode.PositiveVideoHalfLongDimension => ResolveLongSideRange(scale, symmetric: false, settings.DefaultMin, settings.DefaultMax),
                _ => (settings.DefaultMin, settings.DefaultMax),
            };

            AnimationEditor.DefaultMin = defaultMin;
            AnimationEditor.DefaultMax = defaultMax;
        }

        private (double Min, double Max) ResolveLongSideRange(double scale, bool symmetric, double fallbackMin, double fallbackMax)
        {
            if (editorInfo == null || scale <= 0)
            {
                return (fallbackMin, fallbackMax);
            }

            var videoInfo = editorInfo.VideoInfo;
            var width = ReadNumericProperty(videoInfo, "Width");
            var height = ReadNumericProperty(videoInfo, "Height");
            if (width <= 0 || height <= 0)
            {
                return (fallbackMin, fallbackMax);
            }

            var halfLongSide = Math.Max(width, height) / 2.0;
            var rawMax = halfLongSide / scale;
            return symmetric ? (-rawMax, rawMax) : (0.0, rawMax);
        }

        private double ResolveVideoHalfMinDimension()
        {
            if (editorInfo == null)
            {
                return 1.0;
            }

            var videoInfo = editorInfo.VideoInfo;
            var width = ReadNumericProperty(videoInfo, "Width");
            var height = ReadNumericProperty(videoInfo, "Height");
            if (width <= 0 || height <= 0)
            {
                return 1.0;
            }

            return Math.Min(width, height) / 2.0;
        }

        private static double ReadNumericProperty(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property?.CanRead != true)
            {
                return 0.0;
            }

            var value = property.GetValue(target);
            if (value == null)
            {
                return 0.0;
            }

            try
            {
                return Convert.ToDouble(value);
            }
            catch
            {
                return 0.0;
            }
        }

        private void SetAnimationEditorProperty(string propertyName, object value)
        {
            var property = AnimationEditor.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property?.CanWrite != true)
            {
                return;
            }

            var converted = Convert.ChangeType(value, property.PropertyType);
            property.SetValue(AnimationEditor, converted);
        }

        private void ApplyHoldValueChanged(bool value)
        {
            if (!IsLoaded || isUpdatingHoldCheckBox || holdPropertyInfo == null || holdOwners.Length == 0)
            {
                return;
            }

            BeginEdit?.Invoke(this, EventArgs.Empty);
            foreach (var owner in holdOwners)
            {
                holdPropertyInfo.SetValue(owner, value);
            }
            EndEdit?.Invoke(this, EventArgs.Empty);
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            var animations = AnimationEditor.Animations ?? (AnimationEditor.Animation != null ? [AnimationEditor.Animation] : []);
            if (animations.Length == 0)
            {
                return;
            }

            BeginEdit?.Invoke(this, EventArgs.Empty);

            foreach (var animation in animations)
            {
                animation.CopyFrom(new Animation(animation.DefaultValue, animation.MinValue, animation.MaxValue, animation.Loop));
            }

            if (holdPropertyInfo != null)
            {
                foreach (var owner in holdOwners)
                {
                    holdPropertyInfo.SetValue(owner, false);
                }
                UpdateHoldCheckBox();
            }

            EndEdit?.Invoke(this, EventArgs.Empty);
        }
    }
}
