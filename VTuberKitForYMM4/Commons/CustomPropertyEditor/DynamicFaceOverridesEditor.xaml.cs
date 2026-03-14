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
        public static readonly DependencyProperty ParameterColumnsProperty =
            DependencyProperty.Register(
                nameof(ParameterColumns),
                typeof(int),
                typeof(DynamicFaceOverridesEditor),
                new PropertyMetadata(1));

        public static readonly DependencyProperty PartColumnsProperty =
            DependencyProperty.Register(
                nameof(PartColumns),
                typeof(int),
                typeof(DynamicFaceOverridesEditor),
                new PropertyMetadata(1));

        private readonly Dictionary<AnimationSlider, List<INotifyPropertyChanged>> sliderValueWatchers = [];
        private readonly Dictionary<AnimationSlider, SliderConfigurationState> sliderConfigurationStates = [];
        private NotifyCollectionChangedEventHandler? parameterRowsChangedHandler;
        private NotifyCollectionChangedEventHandler? partRowsChangedHandler;
        private PropertyChangedEventHandler? overridesPropertyChangedHandler;
        private bool isRefreshingMetadata;
        private Point dragStartPoint;
        private Live2DFaceDynamicParameterRow? draggingParameterRow;
        private Live2DFaceDynamicPartRow? draggingPartRow;
        private IEditorInfo? editorInfo;

        private readonly record struct SliderConfigurationState(
            object? DataContext,
            object? Animation,
            double DefaultValue,
            double DefaultMin,
            double DefaultMax,
            IEditorInfo? EditorInfo);

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

        public int ParameterColumns
        {
            get => (int)GetValue(ParameterColumnsProperty);
            set => SetValue(ParameterColumnsProperty, value);
        }

        public int PartColumns
        {
            get => (int)GetValue(PartColumnsProperty);
            set => SetValue(PartColumnsProperty, value);
        }

        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;

        public DynamicFaceOverridesEditor()
        {
            InitializeComponent();
            Unloaded += DynamicFaceOverridesEditor_Unloaded;
            Loaded += DynamicFaceOverridesEditor_Loaded;
        }

        public void SetEditorInfo(IEditorInfo info)
        {
            editorInfo = info;
            ApplyEditorInfoToVisibleAnimations(Overrides);
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

        private void DynamicFaceOverridesEditor_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateColumnCounts();
        }

        private void DynamicFaceOverridesEditor_Unloaded(object sender, RoutedEventArgs e)
        {
            foreach (var slider in sliderValueWatchers.Keys.ToList())
            {
                DetachSliderValueWatchers(slider);
            }
            sliderConfigurationStates.Clear();

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
            Loaded -= DynamicFaceOverridesEditor_Loaded;
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
                ParameterExpander.IsExpanded = false;
                PartExpander.IsExpanded = false;
                ParameterList.ItemsSource = null;
                PartList.ItemsSource = null;
                SelectedParameterRow = null;
                SelectedPartRow = null;
                return;
            }

            if (!ReferenceEquals(oldValue, newValue))
            {
                ParameterExpander.IsExpanded = false;
                PartExpander.IsExpanded = false;
            }

            BindingOperations.EnableCollectionSynchronization(newValue.ParameterRows, newValue.ParameterRowsSyncRoot);
            BindingOperations.EnableCollectionSynchronization(newValue.PartRows, newValue.PartRowsSyncRoot);

            if ((ParameterExpander.IsExpanded || PartExpander.IsExpanded) && EnsureModelFile(newValue))
            {
                RefreshMetadata(newValue);
            }

            parameterRowsChangedHandler = (_, e) => HandleParameterRowsChanged(newValue, e);
            partRowsChangedHandler = (_, e) => HandlePartRowsChanged(newValue, e);
            overridesPropertyChangedHandler = (_, e) =>
            {
                if (e.PropertyName == nameof(Live2DFaceDynamicOverrides.ModelFile))
                {
                    if (ParameterExpander.IsExpanded || PartExpander.IsExpanded)
                    {
                        RefreshMetadata(newValue);
                    }
                }
            };
            newValue.ParameterRows.CollectionChanged += parameterRowsChangedHandler;
            newValue.PartRows.CollectionChanged += partRowsChangedHandler;
            newValue.PropertyChanged += overridesPropertyChangedHandler;

            if (SelectedParameterRow == null || !newValue.ParameterRows.Contains(SelectedParameterRow))
            {
                SelectedParameterRow = newValue.ParameterRows.FirstOrDefault();
            }
            if (SelectedPartRow == null || !newValue.PartRows.Contains(SelectedPartRow))
            {
                SelectedPartRow = newValue.PartRows.FirstOrDefault();
            }
            AttachParameterCallbacks(newValue.ParameterRows);
            AttachPartCallbacks(newValue.PartRows);
            RefreshVisibleCollections(newValue);
            ApplyEditorInfoToVisibleAnimations(newValue);
            RefreshFilters();
        }

        private void HandleParameterRowsChanged(Live2DFaceDynamicOverrides overrides, NotifyCollectionChangedEventArgs e)
        {
            if (isRefreshingMetadata)
            {
                return;
            }

            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                AttachParameterCallbacks(overrides.ParameterRows);
                ApplyEditorInfoToParameterRows(overrides.ParameterRows);
                RefreshFilters();
                return;
            }

            AttachParameterCallbacks(e.NewItems?.OfType<Live2DFaceDynamicParameterRow>());
            ApplyEditorInfoToParameterRows(e.NewItems?.OfType<Live2DFaceDynamicParameterRow>());
            RefreshFilters();
        }

        private void HandlePartRowsChanged(Live2DFaceDynamicOverrides overrides, NotifyCollectionChangedEventArgs e)
        {
            if (isRefreshingMetadata)
            {
                return;
            }

            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                AttachPartCallbacks(overrides.PartRows);
                ApplyEditorInfoToPartRows(overrides.PartRows);
                RefreshFilters();
                return;
            }

            AttachPartCallbacks(e.NewItems?.OfType<Live2DFaceDynamicPartRow>());
            ApplyEditorInfoToPartRows(e.NewItems?.OfType<Live2DFaceDynamicPartRow>());
            RefreshFilters();
        }

        private void AttachParameterCallbacks(IEnumerable<Live2DFaceDynamicParameterRow>? rows)
        {
            if (rows == null || Overrides == null)
            {
                return;
            }

            foreach (var row in rows)
            {
                row.EditedCallback = () =>
                {
                    Overrides.NotifyRowsEdited();
                    NotifyEdited();
                };
            }
        }

        private void AttachPartCallbacks(IEnumerable<Live2DFaceDynamicPartRow>? rows)
        {
            if (rows == null || Overrides == null)
            {
                return;
            }

            foreach (var row in rows)
            {
                row.EditedCallback = () =>
                {
                    Overrides.NotifyRowsEdited();
                    NotifyEdited();
                };
            }
        }

        private void ApplyEditorInfoToVisibleAnimations(Live2DFaceDynamicOverrides? overrides)
        {
            if (editorInfo == null || overrides == null)
            {
                return;
            }

            var length = (int)Math.Clamp((long)editorInfo.ItemDuration.Frame, 1L, int.MaxValue);
            var fps = Math.Max(1, editorInfo.VideoInfo.FPS);
            var keyFrames = editorInfo.KeyFrames;

            if (ParameterExpander.IsExpanded)
            {
                ApplyEditorInfoToParameterRows(overrides.ParameterRows, keyFrames, length, fps);
            }

            if (PartExpander.IsExpanded)
            {
                ApplyEditorInfoToPartRows(overrides.PartRows, keyFrames, length, fps);
            }
        }

        private void ApplyEditorInfoToParameterRows(IEnumerable<Live2DFaceDynamicParameterRow>? rows)
        {
            if (editorInfo == null || rows == null || !ParameterExpander.IsExpanded)
            {
                return;
            }

            var length = (int)Math.Clamp((long)editorInfo.ItemDuration.Frame, 1L, int.MaxValue);
            var fps = Math.Max(1, editorInfo.VideoInfo.FPS);
            ApplyEditorInfoToParameterRows(rows, editorInfo.KeyFrames, length, fps);
        }

        private static void ApplyEditorInfoToParameterRows(
            IEnumerable<Live2DFaceDynamicParameterRow> rows,
            KeyFrames? keyFrames,
            int length,
            int fps)
        {
            foreach (var row in rows)
            {
                row.Value.SetKeyFrames(keyFrames);
                row.Value.SetAnimationParameters(length, fps);
            }
        }

        private void ApplyEditorInfoToPartRows(IEnumerable<Live2DFaceDynamicPartRow>? rows)
        {
            if (editorInfo == null || rows == null || !PartExpander.IsExpanded)
            {
                return;
            }

            var length = (int)Math.Clamp((long)editorInfo.ItemDuration.Frame, 1L, int.MaxValue);
            var fps = Math.Max(1, editorInfo.VideoInfo.FPS);
            ApplyEditorInfoToPartRows(rows, editorInfo.KeyFrames, length, fps);
        }

        private static void ApplyEditorInfoToPartRows(
            IEnumerable<Live2DFaceDynamicPartRow> rows,
            KeyFrames? keyFrames,
            int length,
            int fps)
        {
            foreach (var row in rows)
            {
                row.Opacity.SetKeyFrames(keyFrames);
                row.Opacity.SetAnimationParameters(length, fps);
            }
        }

        private void RefreshVisibleCollections(Live2DFaceDynamicOverrides? overrides)
        {
            ParameterList.ItemsSource = ParameterExpander.IsExpanded ? overrides?.ParameterRows : null;
            PartList.ItemsSource = PartExpander.IsExpanded ? overrides?.PartRows : null;
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
            FilterText = string.Empty;
        }

        private void RefreshMetadata(Live2DFaceDynamicOverrides overrides)
        {
            if (!EnsureModelFile(overrides))
            {
                return;
            }

            if (Dispatcher.CheckAccess())
            {
                RefreshMetadataCore(overrides);
                return;
            }

            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
            {
                return;
            }

            Dispatcher.Invoke(() => RefreshMetadataCore(overrides));
        }

        private void ParameterExpander_Expanded(object sender, RoutedEventArgs e)
        {
            if (Overrides != null)
            {
                RefreshMetadata(Overrides);
            }

            RefreshVisibleCollections(Overrides);
            UpdateColumnCounts();
            ApplyEditorInfoToVisibleAnimations(Overrides);
            RefreshFilters();
        }

        private void ParameterExpander_Collapsed(object sender, RoutedEventArgs e)
        {
            RefreshVisibleCollections(Overrides);
        }

        private void PartExpander_Expanded(object sender, RoutedEventArgs e)
        {
            if (Overrides != null)
            {
                RefreshMetadata(Overrides);
            }

            RefreshVisibleCollections(Overrides);
            UpdateColumnCounts();
            ApplyEditorInfoToVisibleAnimations(Overrides);
            RefreshFilters();
        }

        private void PartExpander_Collapsed(object sender, RoutedEventArgs e)
        {
            RefreshVisibleCollections(Overrides);
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
            var state = CreateSliderConfigurationState(slider);
            if (sliderConfigurationStates.TryGetValue(slider, out var currentState) &&
                currentState.Equals(state))
            {
                return;
            }

            DetachSliderValueWatchers(slider);
            ApplySliderConfiguration(slider);
            sliderConfigurationStates[slider] = state;
        }

        private SliderConfigurationState CreateSliderConfigurationState(AnimationSlider slider)
        {
            return slider.DataContext switch
            {
                Live2DFaceDynamicParameterRow parameterRow => new SliderConfigurationState(
                    slider.DataContext,
                    parameterRow.Value,
                    parameterRow.DefaultValue,
                    parameterRow.Min,
                    parameterRow.Max,
                    editorInfo),
                Live2DFaceDynamicPartRow partRow => new SliderConfigurationState(
                    slider.DataContext,
                    partRow.Opacity,
                    1,
                    0,
                    1,
                    editorInfo),
                _ => new SliderConfigurationState(
                    slider.DataContext,
                    slider.Animation,
                    slider.DefaultValue,
                    slider.DefaultMin,
                    slider.DefaultMax,
                    editorInfo),
            };
        }

        private void ApplySliderConfiguration(AnimationSlider slider)
        {
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

            slider.Delay = 0;

            if (editorInfo != null)
            {
                slider.SetEditorInfo(editorInfo);
            }

            AttachSliderValueWatchers(slider);
        }

        private static bool EnsureModelFile(Live2DFaceDynamicOverrides overrides)
        {
            return !string.IsNullOrWhiteSpace(overrides.ModelFile);
        }

        private void RefreshMetadataCore(Live2DFaceDynamicOverrides overrides)
        {
            if (!IsLoaded && PresentationSource.FromVisual(this) == null)
            {
                return;
            }

            isRefreshingMetadata = true;
            try
            {
                overrides.SyncWithMetadata();
            }
            finally
            {
                isRefreshingMetadata = false;
            }

            AttachParameterCallbacks(overrides.ParameterRows);
            AttachPartCallbacks(overrides.PartRows);
            RefreshVisibleCollections(overrides);
            ApplyEditorInfoToVisibleAnimations(overrides);
            RefreshFilters();
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
            sliderConfigurationStates.Remove(slider);
        }

        private void SliderValueViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (!IsLoaded || !string.Equals(e.PropertyName, "Value", StringComparison.Ordinal))
            {
                return;
            }

            Overrides?.NotifyRowsEdited();
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

        private void ParameterList_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateParameterColumns(e.NewSize.Width);
        }

        private void PartList_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdatePartColumns(e.NewSize.Width);
        }

        private void UpdateColumnCounts()
        {
            UpdateParameterColumns(ParameterList.ActualWidth);
            UpdatePartColumns(PartList.ActualWidth);
        }

        private void UpdateParameterColumns(double width)
        {
            var columns = CalculateColumns(width);
            if (ParameterColumns != columns)
            {
                ParameterColumns = columns;
            }
        }

        private void UpdatePartColumns(double width)
        {
            var columns = CalculateColumns(width);
            if (PartColumns != columns)
            {
                PartColumns = columns;
            }
        }

        private static int CalculateColumns(double width)
        {
            return Math.Max(1, (int)Math.Floor((width - 160.0) / 260.0) + 1);
        }
    }
}
