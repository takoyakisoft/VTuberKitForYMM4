using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using VTuberKitForYMM4.Plugin;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;

namespace VTuberKitForYMM4.Commons.CustomPropertyEditor
{
    public partial class DynamicFaceOverridesEditor : UserControl, IPropertyEditorControl, IPropertyEditorControl2
    {
        private readonly Dictionary<AnimationSlider, List<INotifyPropertyChanged>> sliderValueWatchers = [];
        private NotifyCollectionChangedEventHandler? parameterRowsChangedHandler;
        private NotifyCollectionChangedEventHandler? partRowsChangedHandler;
        private PropertyChangedEventHandler? overridesPropertyChangedHandler;
        private Point dragStartPoint;
        private Live2DFaceDynamicParameterRow? draggingParameterRow;
        private Live2DFaceDynamicPartRow? draggingPartRow;
        private IEditorInfo? editorInfo;

        public static readonly DependencyProperty OverridesProperty =
            DependencyProperty.Register(
                nameof(Overrides),
                typeof(Live2DFaceDynamicOverrides),
                typeof(DynamicFaceOverridesEditor),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnOverridesChanged));

        public static readonly DependencyProperty FilterTextProperty =
            DependencyProperty.Register(
                nameof(FilterText),
                typeof(string),
                typeof(DynamicFaceOverridesEditor),
                new PropertyMetadata(string.Empty, OnFilterTextChanged));

        public static readonly DependencyProperty SelectedParameterRowProperty =
            DependencyProperty.Register(
                nameof(SelectedParameterRow),
                typeof(Live2DFaceDynamicParameterRow),
                typeof(DynamicFaceOverridesEditor),
                new PropertyMetadata(null));

        public static readonly DependencyProperty SelectedPartRowProperty =
            DependencyProperty.Register(
                nameof(SelectedPartRow),
                typeof(Live2DFaceDynamicPartRow),
                typeof(DynamicFaceOverridesEditor),
                new PropertyMetadata(null));

        public Live2DFaceDynamicOverrides? Overrides
        {
            get => (Live2DFaceDynamicOverrides?)GetValue(OverridesProperty);
            set => SetValue(OverridesProperty, value);
        }

        public string FilterText
        {
            get => (string)GetValue(FilterTextProperty);
            set => SetValue(FilterTextProperty, value ?? string.Empty);
        }

        public Live2DFaceDynamicParameterRow? SelectedParameterRow
        {
            get => (Live2DFaceDynamicParameterRow?)GetValue(SelectedParameterRowProperty);
            set => SetValue(SelectedParameterRowProperty, value);
        }

        public Live2DFaceDynamicPartRow? SelectedPartRow
        {
            get => (Live2DFaceDynamicPartRow?)GetValue(SelectedPartRowProperty);
            set => SetValue(SelectedPartRowProperty, value);
        }

        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;

        public DynamicFaceOverridesEditor()
        {
            InitializeComponent();
            Unloaded += DynamicFaceOverridesEditor_Unloaded;
        }

        public void SetEditorInfo(IEditorInfo info)
        {
            editorInfo = info;
            ApplyEditorInfoToAnimations(Overrides);
        }

        public void SetFocus()
        {
            ParameterList.Focus();
        }

        private static void OnOverridesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((DynamicFaceOverridesEditor)d).BindOverrides((Live2DFaceDynamicOverrides?)e.OldValue, (Live2DFaceDynamicOverrides?)e.NewValue);
        }

        private static void OnFilterTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((DynamicFaceOverridesEditor)d).RefreshFilters();
        }

        private void DynamicFaceOverridesEditor_Unloaded(object sender, RoutedEventArgs e)
        {
            foreach (var slider in sliderValueWatchers.Keys.ToList())
            {
                DetachSliderValueWatchers(slider);
            }

            if (Overrides != null)
            {
                if (parameterRowsChangedHandler != null)
                {
                    Overrides.ParameterRows.CollectionChanged -= parameterRowsChangedHandler;
                }

                if (partRowsChangedHandler != null)
                {
                    Overrides.PartRows.CollectionChanged -= partRowsChangedHandler;
                }

                if (overridesPropertyChangedHandler != null)
                {
                    Overrides.PropertyChanged -= overridesPropertyChangedHandler;
                }
            }

            Unloaded -= DynamicFaceOverridesEditor_Unloaded;
        }

        private void BindOverrides(Live2DFaceDynamicOverrides? oldValue, Live2DFaceDynamicOverrides? newValue)
        {
            if (oldValue != null)
            {
                if (parameterRowsChangedHandler != null)
                {
                    oldValue.ParameterRows.CollectionChanged -= parameterRowsChangedHandler;
                }

                if (partRowsChangedHandler != null)
                {
                    oldValue.PartRows.CollectionChanged -= partRowsChangedHandler;
                }

                if (overridesPropertyChangedHandler != null)
                {
                    oldValue.PropertyChanged -= overridesPropertyChangedHandler;
                }
            }

            if (newValue == null)
            {
                ParameterList.ItemsSource = null;
                PartList.ItemsSource = null;
                SelectedParameterRow = null;
                SelectedPartRow = null;
                return;
            }

            BindingOperations.EnableCollectionSynchronization(newValue.ParameterRows, newValue.ParameterRowsSyncRoot);
            BindingOperations.EnableCollectionSynchronization(newValue.PartRows, newValue.PartRowsSyncRoot);

            RefreshMetadata(newValue);
            parameterRowsChangedHandler = (_, _) =>
            {
                AttachCallbacks(newValue);
                ApplyEditorInfoToAnimations(newValue);
            };
            partRowsChangedHandler = (_, _) =>
            {
                AttachCallbacks(newValue);
                ApplyEditorInfoToAnimations(newValue);
            };
            overridesPropertyChangedHandler = (_, e) =>
            {
                if (e.PropertyName == nameof(Live2DFaceDynamicOverrides.ModelFile))
                {
                    RefreshMetadata(newValue);
                }
            };
            newValue.ParameterRows.CollectionChanged += parameterRowsChangedHandler;
            newValue.PartRows.CollectionChanged += partRowsChangedHandler;
            newValue.PropertyChanged += overridesPropertyChangedHandler;

            ParameterList.ItemsSource = newValue.ParameterRows;
            PartList.ItemsSource = newValue.PartRows;
            if (SelectedParameterRow == null || !newValue.ParameterRows.Contains(SelectedParameterRow))
            {
                SelectedParameterRow = newValue.ParameterRows.FirstOrDefault();
            }
            if (SelectedPartRow == null || !newValue.PartRows.Contains(SelectedPartRow))
            {
                SelectedPartRow = newValue.PartRows.FirstOrDefault();
            }
            AttachCallbacks(newValue);
            ApplyEditorInfoToAnimations(newValue);
            RefreshFilters();
        }

        private void AttachCallbacks(Live2DFaceDynamicOverrides overrides)
        {
            foreach (var row in overrides.ParameterRows)
            {
                row.EditedCallback = NotifyEdited;
            }

            foreach (var row in overrides.PartRows)
            {
                row.EditedCallback = NotifyEdited;
            }
        }

        private void ApplyEditorInfoToAnimations(Live2DFaceDynamicOverrides? overrides)
        {
            if (editorInfo == null || overrides == null)
            {
                return;
            }

            var length = (int)Math.Clamp((long)editorInfo.ItemDuration.Frame, 1L, int.MaxValue);
            var fps = Math.Max(1, editorInfo.VideoInfo.FPS);
            var keyFrames = editorInfo.KeyFrames;

            foreach (var row in overrides.ParameterRows)
            {
                row.Value.SetKeyFrames(keyFrames);
                row.Value.SetAnimationParameters(length, fps);
            }

            foreach (var row in overrides.PartRows)
            {
                row.Opacity.SetKeyFrames(keyFrames);
                row.Opacity.SetAnimationParameters(length, fps);
            }
        }

        private void NotifyEdited()
        {
            BeginEdit?.Invoke(this, EventArgs.Empty);
            EndEdit?.Invoke(this, EventArgs.Empty);
        }

        private void RefreshFilters()
        {
            RefreshFilter(ParameterList, item => item is Live2DFaceDynamicRowBase row && MatchesFilter(row.DisplayName));
            RefreshFilter(PartList, item => item is Live2DFaceDynamicRowBase row && MatchesFilter(row.DisplayName));
        }

        private void RefreshFilter(ItemsControl control, Predicate<object> predicate)
        {
            var view = CollectionViewSource.GetDefaultView(control.ItemsSource);
            if (view == null)
            {
                return;
            }

            view.Filter = item => predicate(item);
            view.Refresh();
        }

        private bool MatchesFilter(string text)
        {
            if (string.IsNullOrWhiteSpace(FilterText))
            {
                return true;
            }

            return text.Contains(FilterText, StringComparison.OrdinalIgnoreCase);
        }

        private void ReloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (Overrides != null)
            {
                RefreshMetadata(Overrides);
            }
        }

        private void RefreshMetadata(Live2DFaceDynamicOverrides overrides)
        {
            if (Dispatcher.CheckAccess())
            {
                overrides.SyncWithMetadata();
                RefreshFilters();
                return;
            }

            _ = Dispatcher.BeginInvoke(new Action(() =>
            {
                overrides.SyncWithMetadata();
                RefreshFilters();
            }));
        }

        private void AnimationSlider_BeginEdit(object sender, EventArgs e)
        {
            if (!IsLoaded)
            {
                return;
            }

            BeginEdit?.Invoke(this, EventArgs.Empty);
        }

        private void AnimationSlider_EndEdit(object sender, EventArgs e)
        {
            if (!IsLoaded)
            {
                return;
            }

            EndEdit?.Invoke(this, EventArgs.Empty);
        }

        private void DetailAnimationSlider_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is AnimationSlider slider)
            {
                ConfigureDetailAnimationSlider(slider);
            }
        }

        private void DetailAnimationSlider_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is AnimationSlider slider)
            {
                ConfigureDetailAnimationSlider(slider);
            }
        }

        private void ConfigureDetailAnimationSlider(AnimationSlider slider)
        {
            DetachSliderValueWatchers(slider);

            switch (slider.DataContext)
            {
                case Live2DFaceDynamicParameterRow parameterRow:
                    slider.Animation = parameterRow.Value;
                    slider.Animations = [parameterRow.Value];
                    slider.DefaultValue = parameterRow.DefaultValue;
                    slider.DefaultMin = parameterRow.Min;
                    slider.DefaultMax = parameterRow.Max;
                    break;
                case Live2DFaceDynamicPartRow partRow:
                    slider.Animation = partRow.Opacity;
                    slider.Animations = [partRow.Opacity];
                    slider.DefaultValue = 1;
                    slider.DefaultMin = 0;
                    slider.DefaultMax = 1;
                    break;
                default:
                    slider.Animations = slider.Animation != null ? [slider.Animation] : [];
                    break;
            }

            if (editorInfo != null)
            {
                slider.SetEditorInfo(editorInfo);
            }

            AttachSliderValueWatchers(slider);
        }

        private void AttachSliderValueWatchers(AnimationSlider slider)
        {
            if (slider.ViewModel?.Values == null)
            {
                return;
            }

            var watchers = new List<INotifyPropertyChanged>();
            foreach (var valueViewModel in slider.ViewModel.Values.OfType<INotifyPropertyChanged>())
            {
                valueViewModel.PropertyChanged += SliderValueViewModel_PropertyChanged;
                watchers.Add(valueViewModel);
            }

            if (watchers.Count > 0)
            {
                sliderValueWatchers[slider] = watchers;
            }
        }

        private void DetachSliderValueWatchers(AnimationSlider slider)
        {
            if (!sliderValueWatchers.TryGetValue(slider, out var watchers))
            {
                return;
            }

            foreach (var watcher in watchers)
            {
                watcher.PropertyChanged -= SliderValueViewModel_PropertyChanged;
            }

            sliderValueWatchers.Remove(slider);
        }

        private void SliderValueViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (!IsLoaded || !string.Equals(e.PropertyName, "Value", StringComparison.Ordinal))
            {
                return;
            }

            NotifyEdited();
        }

        private void ParameterLabel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is Live2DFaceDynamicParameterRow row)
            {
                SelectedParameterRow = row;
                draggingParameterRow = row;
                dragStartPoint = e.GetPosition(this);
                element.CaptureMouse();
                e.Handled = true;
            }
        }

        private void ParameterLabel_MouseMove(object sender, MouseEventArgs e)
        {
            if (draggingParameterRow == null || sender is not FrameworkElement || e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            var current = e.GetPosition(this);
            var delta = current.X - dragStartPoint.X;
            if (Math.Abs(delta) < 0.5)
            {
                return;
            }

            dragStartPoint = current;
            draggingParameterRow.CurrentValue += delta * GetParameterScrubStep(draggingParameterRow);
            e.Handled = true;
        }

        private void ParameterLabel_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is UIElement element)
            {
                element.ReleaseMouseCapture();
            }

            draggingParameterRow = null;
        }

        private void PartLabel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is Live2DFaceDynamicPartRow row)
            {
                SelectedPartRow = row;
                draggingPartRow = row;
                dragStartPoint = e.GetPosition(this);
                element.CaptureMouse();
                e.Handled = true;
            }
        }

        private void PartLabel_MouseMove(object sender, MouseEventArgs e)
        {
            if (draggingPartRow == null || sender is not FrameworkElement || e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            var current = e.GetPosition(this);
            var delta = current.X - dragStartPoint.X;
            if (Math.Abs(delta) < 0.5)
            {
                return;
            }

            dragStartPoint = current;
            draggingPartRow.CurrentOpacity += delta * GetPartScrubStep();
            e.Handled = true;
        }

        private void PartLabel_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is UIElement element)
            {
                element.ReleaseMouseCapture();
            }

            draggingPartRow = null;
        }

        private void CheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded)
            {
                return;
            }

            NotifyEdited();
        }

        private void ActionButton_Click(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded)
            {
                return;
            }

            NotifyEdited();
        }

        private static double GetParameterScrubStep(Live2DFaceDynamicParameterRow row)
        {
            var range = Math.Abs(row.Max - row.Min);
            var baseStep = range <= 0 ? 0.01 : Math.Max(range / 200.0, 0.001);
            return ApplyModifierScale(baseStep);
        }

        private static double GetPartScrubStep()
        {
            return ApplyModifierScale(0.01);
        }

        private static double ApplyModifierScale(double step)
        {
            var modifiers = Keyboard.Modifiers;
            if ((modifiers & ModifierKeys.Shift) != 0)
            {
                return step * 5.0;
            }

            if ((modifiers & ModifierKeys.Control) != 0)
            {
                return step * 0.2;
            }

            return step;
        }
    }
}
