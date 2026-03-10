using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;

namespace VTuberKitForYMM4.Commons.CustomPropertyEditor
{
    public partial class AnimatedHoldEditor : UserControl, IPropertyEditorControl, IPropertyEditorControl2
    {
        private ItemProperty[] animationItemProperties = [];
        private PropertyInfo? holdPropertyInfo;
        private object[] holdOwners = [];
        private bool isUpdatingHoldCheckBox;
        private IEditorInfo? editorInfo;

        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;

        public AnimatedHoldEditor()
        {
            InitializeComponent();
        }

        public void Configure(ItemProperty[] itemProperties, AnimatedHoldSliderAttribute settings)
        {
            ArgumentNullException.ThrowIfNull(itemProperties);
            ArgumentOutOfRangeException.ThrowIfZero(itemProperties.Length);

            animationItemProperties = itemProperties;

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
            HoldCheckBox.Visibility = Visibility.Hidden;
            HoldCheckBox.IsChecked = false;
        }

        public void SetEditorInfo(IEditorInfo info)
        {
            editorInfo = info;
            AnimationEditor.SetEditorInfo(info);
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
                    HoldCheckBox.IsChecked = false;
                    return;
                }

                HoldCheckBox.Visibility = Visibility.Visible;
                HoldCheckBox.IsChecked = (bool?)holdPropertyInfo.GetValue(holdOwners[0]) ?? false;
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

        private void CheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded || isUpdatingHoldCheckBox || holdPropertyInfo == null || holdOwners.Length == 0)
            {
                return;
            }

            var value = HoldCheckBox.IsChecked == true;
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
