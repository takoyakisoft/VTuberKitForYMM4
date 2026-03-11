using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using System.Windows;
using VTuberKitForNative;
using YukkuriMovieMaker.Commons;

namespace VTuberKitForYMM4.Plugin
{
    public class Live2DFaceDynamicOverrides : Animatable
    {
        internal readonly record struct StandardParameterDefinition(
            string Id,
            string DisplayName,
            float Min,
            float Default,
            float Max,
            int SortOrder);

        private static readonly StandardParameterDefinition[] StandardParameters =
        [
            new(Live2DManager.ParamAngleX, "角度 X", -30.0f, 0.0f, 30.0f, 0),
            new(Live2DManager.ParamAngleY, "角度 Y", -30.0f, 0.0f, 30.0f, 1),
            new(Live2DManager.ParamAngleZ, "角度 Z", -30.0f, 0.0f, 30.0f, 2),
            new(Live2DManager.ParamEyeLOpen, "左目 開閉", 0.0f, 1.0f, 1.0f, 3),
            new(Live2DManager.ParamEyeLSmile, "左目 笑顔", 0.0f, 0.0f, 1.0f, 4),
            new(Live2DManager.ParamEyeROpen, "右目 開閉", 0.0f, 1.0f, 1.0f, 5),
            new(Live2DManager.ParamEyeRSmile, "右目 笑顔", 0.0f, 0.0f, 1.0f, 6),
            new(Live2DManager.ParamEyeBallX, "目玉 X", -1.0f, 0.0f, 1.0f, 7),
            new(Live2DManager.ParamEyeBallY, "目玉 Y", -1.0f, 0.0f, 1.0f, 8),
            new(Live2DManager.ParamEyeBallForm, "目玉の拡大縮小", -1.0f, 0.0f, 1.0f, 9),
            new(Live2DManager.ParamBrowLY, "左眉 上下", -1.0f, 0.0f, 1.0f, 10),
            new(Live2DManager.ParamBrowRY, "右眉 上下", -1.0f, 0.0f, 1.0f, 11),
            new(Live2DManager.ParamBrowLX, "左眉 左右", -1.0f, 0.0f, 1.0f, 12),
            new(Live2DManager.ParamBrowRX, "右眉 左右", -1.0f, 0.0f, 1.0f, 13),
            new(Live2DManager.ParamBrowLAngle, "左眉 角度", -1.0f, 0.0f, 1.0f, 14),
            new(Live2DManager.ParamBrowRAngle, "右眉 角度", -1.0f, 0.0f, 1.0f, 15),
            new(Live2DManager.ParamBrowLForm, "左眉 変形", -1.0f, 0.0f, 1.0f, 16),
            new(Live2DManager.ParamBrowRForm, "右眉 変形", -1.0f, 0.0f, 1.0f, 17),
            new(Live2DManager.ParamMouthForm, "口 変形", -1.0f, 0.0f, 1.0f, 18),
            new(Live2DManager.ParamMouthOpenY, "口 開閉", 0.0f, 0.0f, 1.0f, 19),
            new(Live2DManager.ParamCheek, "照れ", 0.0f, 0.0f, 1.0f, 20),
            new(Live2DManager.ParamBodyAngleX, "体の回転 X", -10.0f, 0.0f, 10.0f, 21),
            new(Live2DManager.ParamBodyAngleY, "体の回転 Y", -10.0f, 0.0f, 10.0f, 22),
            new(Live2DManager.ParamBodyAngleZ, "体の回転 Z", -10.0f, 0.0f, 10.0f, 23),
            new(Live2DManager.ParamBreath, "呼吸", 0.0f, 0.0f, 1.0f, 24),
            new(Live2DManager.ParamArmLA, "左腕 A", -30.0f, 0.0f, 30.0f, 25),
            new(Live2DManager.ParamArmRA, "右腕 A", -30.0f, 0.0f, 30.0f, 26),
            new(Live2DManager.ParamArmLB, "左腕 B", -30.0f, 0.0f, 30.0f, 27),
            new(Live2DManager.ParamArmRB, "右腕 B", -30.0f, 0.0f, 30.0f, 28),
            new(Live2DManager.ParamHandL, "左手", -10.0f, 0.0f, 10.0f, 29),
            new(Live2DManager.ParamHandR, "右手", -10.0f, 0.0f, 10.0f, 30),
            new(Live2DManager.ParamHairFront, "髪揺れ 前", -1.0f, 0.0f, 1.0f, 31),
            new(Live2DManager.ParamHairSide, "髪揺れ 横", -1.0f, 0.0f, 1.0f, 32),
            new(Live2DManager.ParamHairBack, "髪揺れ 後", -1.0f, 0.0f, 1.0f, 33),
            new(Live2DManager.ParamHairFluffy, "髪揺れ ふわ", -1.0f, 0.0f, 1.0f, 34),
            new(Live2DManager.ParamShoulderY, "肩すくめる", -10.0f, 0.0f, 10.0f, 35),
            new(Live2DManager.ParamBustX, "胸揺れ X", -1.0f, 0.0f, 1.0f, 36),
            new(Live2DManager.ParamBustY, "胸揺れ Y", -1.0f, 0.0f, 1.0f, 37),
            new(Live2DManager.ParamBaseX, "全体の左右", -10.0f, 0.0f, 10.0f, 38),
            new(Live2DManager.ParamBaseY, "全体の上下", -10.0f, 0.0f, 10.0f, 39),
        ];
        private static readonly Dictionary<string, StandardParameterDefinition> StandardParameterDefinitionsById =
            StandardParameters.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);

        public ObservableCollection<Live2DFaceDynamicParameterRow> ParameterRows { get; } = [];
        public ObservableCollection<Live2DFaceDynamicPartRow> PartRows { get; } = [];
        [Browsable(false)]
        [JsonIgnore]
        public object ParameterRowsSyncRoot { get; } = new();
        [Browsable(false)]
        [JsonIgnore]
        public object PartRowsSyncRoot { get; } = new();

        [Browsable(false)]
        public string ModelFile
        {
            get => modelFile;
            set => Set(ref modelFile, value ?? string.Empty);
        }
        string modelFile = string.Empty;

        public Live2DFaceDynamicOverrides()
        {
            CollectionChangedEventManager.AddHandler(ParameterRows, OnRowsChanged);
            CollectionChangedEventManager.AddHandler(PartRows, OnRowsChanged);
        }

        public void SyncWithMetadata()
        {
            SyncParameterRows();
            SyncPartRows();
        }

        protected override IEnumerable<IAnimatable> GetAnimatables()
        {
            Live2DFaceDynamicParameterRow[] parameterRows;
            lock (ParameterRowsSyncRoot)
            {
                parameterRows = ParameterRows.ToArray();
            }

            foreach (var row in parameterRows)
            {
                yield return row;
            }

            Live2DFaceDynamicPartRow[] partRows;
            lock (PartRowsSyncRoot)
            {
                partRows = PartRows.ToArray();
            }

            foreach (var row in partRows)
            {
                yield return row;
            }
        }

        private void SyncParameterRows()
        {
            lock (ParameterRowsSyncRoot)
            {
                var metadata = ModelMetadataCatalog.GetParameters(ModelFile)
                    .OrderBy(x => GetStandardSortOrder(x.Id))
                    .ThenBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                RemoveDuplicateParameterRows();
                var existing = ParameterRows.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
                var alive = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var item in metadata)
                {
                    var name = ResolveDisplayName(item.Id, item.Name);
                    var hasNative = ModelMetadataCatalog.TryGetParameterMetadata(ModelFile, item.Id, out var native);
                    var (defaultValue, minValue, maxValue) = ResolveParameterRange(item.Id, hasNative ? native : null);

                    if (existing.TryGetValue(item.Id, out var row))
                    {
                        row.UpdateMetadata(name, defaultValue, minValue, maxValue);
                    }
                    else
                    {
                        ParameterRows.Add(new Live2DFaceDynamicParameterRow(item.Id, name, defaultValue, minValue, maxValue));
                    }

                    alive.Add(item.Id);
                }

                for (var i = ParameterRows.Count - 1; i >= 0; i--)
                {
                    if (!alive.Contains(ParameterRows[i].Id))
                    {
                        ParameterRows.RemoveAt(i);
                    }
                }

                ReorderParameterRows(metadata.Select(x => x.Id).ToArray());
            }
        }

        internal static bool TryGetStandardParameterDefinition(string? id, out StandardParameterDefinition definition)
        {
            if (!string.IsNullOrWhiteSpace(id) &&
                StandardParameterDefinitionsById.TryGetValue(id, out definition))
            {
                return true;
            }

            definition = default;
            return false;
        }

        internal static int GetStandardSortOrder(string? id)
        {
            return TryGetStandardParameterDefinition(id, out var definition)
                ? definition.SortOrder
                : int.MaxValue;
        }

        private static string ResolveDisplayName(string id, string name)
        {
            if (TryGetStandardParameterDefinition(id, out var definition))
            {
                return definition.DisplayName;
            }

            return string.IsNullOrWhiteSpace(name) ? id : name;
        }

        private static (float DefaultValue, float MinValue, float MaxValue) ResolveParameterRange(
            string id,
            ModelMetadataCatalog.ParameterMetadata? nativeMetadata)
        {
            if (nativeMetadata is ModelMetadataCatalog.ParameterMetadata native)
            {
                return (native.Default, native.Min, native.Max);
            }

            if (TryGetStandardParameterDefinition(id, out var definition))
            {
                return (definition.Default, definition.Min, definition.Max);
            }

            return (0.0f, -100.0f, 100.0f);
        }

        private void SyncPartRows()
        {
            lock (PartRowsSyncRoot)
            {
                var parts = ModelMetadataCatalog.GetParts(ModelFile)
                    .OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                RemoveDuplicatePartRows();
                var existing = PartRows.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
                var alive = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var item in parts)
                {
                    var name = string.IsNullOrWhiteSpace(item.Name) ? item.Id : item.Name;
                    if (existing.TryGetValue(item.Id, out var row))
                    {
                        row.UpdateMetadata(name);
                    }
                    else
                    {
                        PartRows.Add(new Live2DFaceDynamicPartRow(item.Id, name));
                    }

                    alive.Add(item.Id);
                }

                for (var i = PartRows.Count - 1; i >= 0; i--)
                {
                    if (!alive.Contains(PartRows[i].Id))
                    {
                        PartRows.RemoveAt(i);
                    }
                }
            }
        }

        private void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateRowSubscriptions(e);

            if (ReferenceEquals(sender, ParameterRows))
            {
                OnPropertyChanged(nameof(ParameterRows));
                return;
            }

            if (ReferenceEquals(sender, PartRows))
            {
                OnPropertyChanged(nameof(PartRows));
                return;
            }

            OnPropertyChanged(nameof(ParameterRows));
            OnPropertyChanged(nameof(PartRows));
        }

        internal void NotifyRowsEdited()
        {
            OnPropertyChanged(nameof(ParameterRows));
            OnPropertyChanged(nameof(PartRows));
        }

        private void UpdateRowSubscriptions(NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (var row in e.OldItems.OfType<INotifyPropertyChanged>())
                {
                    row.PropertyChanged -= Row_PropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (var row in e.NewItems.OfType<INotifyPropertyChanged>())
                {
                    row.PropertyChanged -= Row_PropertyChanged;
                    row.PropertyChanged += Row_PropertyChanged;
                }
            }

            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                foreach (var row in ParameterRows.OfType<INotifyPropertyChanged>())
                {
                    row.PropertyChanged -= Row_PropertyChanged;
                    row.PropertyChanged += Row_PropertyChanged;
                }

                foreach (var row in PartRows.OfType<INotifyPropertyChanged>())
                {
                    row.PropertyChanged -= Row_PropertyChanged;
                    row.PropertyChanged += Row_PropertyChanged;
                }
            }
        }

        private void Row_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is Live2DFaceDynamicParameterRow)
            {
                OnPropertyChanged(nameof(ParameterRows));
                return;
            }

            if (sender is Live2DFaceDynamicPartRow)
            {
                OnPropertyChanged(nameof(PartRows));
                return;
            }

            NotifyRowsEdited();
        }

        private void RemoveDuplicateParameterRows()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = ParameterRows.Count - 1; i >= 0; i--)
            {
                var id = ParameterRows[i].Id ?? string.Empty;
                if (!seen.Add(id))
                {
                    ParameterRows.RemoveAt(i);
                }
            }
        }

        private void ReorderParameterRows(IReadOnlyList<string> orderedIds)
        {
            for (var targetIndex = 0; targetIndex < orderedIds.Count; targetIndex++)
            {
                var id = orderedIds[targetIndex];
                var currentIndex = FindParameterRowIndex(id);
                if (currentIndex < 0 || currentIndex == targetIndex)
                {
                    continue;
                }

                ParameterRows.Move(currentIndex, targetIndex);
            }
        }

        private int FindParameterRowIndex(string id)
        {
            for (var index = 0; index < ParameterRows.Count; index++)
            {
                if (string.Equals(ParameterRows[index].Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return -1;
        }

        private void RemoveDuplicatePartRows()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = PartRows.Count - 1; i >= 0; i--)
            {
                var id = PartRows[i].Id ?? string.Empty;
                if (!seen.Add(id))
                {
                    PartRows.RemoveAt(i);
                }
            }
        }
    }

    public abstract class Live2DFaceDynamicRowBase : Animatable
    {
        [Browsable(false)]
        public string Id { get => id; set => Set(ref id, value ?? string.Empty); }
        string id = string.Empty;

        [Browsable(false)]
        public string Name { get => name; set => Set(ref name, value ?? string.Empty); }
        string name = string.Empty;

        [Browsable(false)]
        public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Id : Name;

        [Browsable(false)]
        [JsonIgnore]
        public Action? EditedCallback { get; set; }

        protected void NotifyEdited()
        {
            EditedCallback?.Invoke();
            OnPropertyChanged(nameof(DisplayName));
        }

        protected static bool TryParseValue(string? text, out float value)
        {
            if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }

            return float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
        }
    }

    public sealed class Live2DFaceDynamicParameterRow : Live2DFaceDynamicRowBase
    {
        public Live2DFaceDynamicParameterRow()
            : this(string.Empty, string.Empty, 0.0f, -100.0f, 100.0f)
        {
        }

        public Live2DFaceDynamicParameterRow(string id, string name, float defaultValue, float min, float max)
        {
            Id = id;
            Name = name;
            DefaultValue = defaultValue;
            Min = min;
            Max = max;
            Value = new Animation(defaultValue, min, max);
            ResetCommand = new ActionCommand(_ => true, _ =>
            {
                Hold = false;
                Value.CopyFrom(new Animation(DefaultValue, Min, Max, Value.Loop));
                OnPropertyChanged(nameof(CurrentValue));
                OnPropertyChanged(nameof(CurrentValueText));
                NotifyEdited();
            });
        }

        [Browsable(false)]
        public float DefaultValue { get => defaultValue; set => Set(ref defaultValue, value); }
        float defaultValue;

        [Browsable(false)]
        public float Min { get => min; set => Set(ref min, value); }
        float min;

        [Browsable(false)]
        public float Max { get => max; set => Set(ref max, value); }
        float max;

        [Browsable(false)]
        public Animation Value
        {
            get => value;
            private set
            {
                if (Set(ref this.value, value))
                {
                    DetachValueWatcher();
                    AttachValueWatcher();
                    OnPropertyChanged(nameof(CurrentValue));
                    OnPropertyChanged(nameof(CurrentValueText));
                }
            }
        }
        Animation value = default!;
        INotifyPropertyChanged? valueWatcher;

        [Browsable(false)]
        [JsonIgnore]
        public double CurrentValue
        {
            get => Value.GetValue(0, 1, 60);
            set => SetCurrentValue((float)value);
        }

        [Browsable(false)]
        [JsonIgnore]
        public string CurrentValueText
        {
            get => CurrentValue.ToString("F3", CultureInfo.InvariantCulture);
            set
            {
                if (TryParseValue(value, out var parsed))
                {
                    SetCurrentValue(parsed);
                }
            }
        }

        [Browsable(false)]
        public bool Hold
        {
            get => hold;
            set
            {
                if (Set(ref hold, value))
                {
                    NotifyEdited();
                }
            }
        }
        bool hold;

        [Browsable(false)]
        [JsonIgnore]
        public ICommand ResetCommand { get; }

        public void UpdateMetadata(string name, float defaultValue, float min, float max)
        {
            var currentActual = (float)CurrentValue;
            Name = name;
            DefaultValue = defaultValue;
            Min = min;
            Max = max;
            Value = CreateAnimationWithCurrentState(Math.Clamp(currentActual, min, max), min, max);
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(CurrentValue));
            OnPropertyChanged(nameof(CurrentValueText));
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Value];

        private void SetCurrentValue(float value)
        {
            var clamped = Math.Clamp(value, Min, Max);
#pragma warning disable CS0618
            Value.From = clamped;
#pragma warning restore CS0618
            OnPropertyChanged(nameof(CurrentValue));
            OnPropertyChanged(nameof(CurrentValueText));
            NotifyEdited();
        }

        public float GetValue(long frame, long length, int fps)
        {
            return (float)Value.GetValue(frame, length, fps);
        }

        private Animation CreateAnimationWithCurrentState(float currentValue, float min, float max)
        {
            var animation = new Animation(currentValue, min, max, Value.Loop);
            animation.CopyFrom(Value);
#pragma warning disable CS0618
            animation.From = currentValue;
#pragma warning restore CS0618
            return animation;
        }

        private void AttachValueWatcher()
        {
            valueWatcher = Value as INotifyPropertyChanged;
            if (valueWatcher != null)
            {
                valueWatcher.PropertyChanged += Value_PropertyChanged;
            }
        }

        private void DetachValueWatcher()
        {
            if (valueWatcher != null)
            {
                valueWatcher.PropertyChanged -= Value_PropertyChanged;
                valueWatcher = null;
            }
        }

        private void Value_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(nameof(CurrentValue));
            OnPropertyChanged(nameof(CurrentValueText));
            NotifyEdited();
        }
    }

    public sealed class Live2DFaceDynamicPartRow : Live2DFaceDynamicRowBase
    {
        public Live2DFaceDynamicPartRow()
            : this(string.Empty, string.Empty)
        {
        }

        public Live2DFaceDynamicPartRow(string id, string name)
        {
            Id = id;
            Name = name;
            if (Opacity is INotifyPropertyChanged watcher)
            {
                watcher.PropertyChanged += Opacity_PropertyChanged;
            }
            ResetCommand = new ActionCommand(_ => true, _ =>
            {
                Hold = false;
                Opacity.CopyFrom(new Animation(1.0, 0.0, 1.0, Opacity.Loop));
                OnPropertyChanged(nameof(CurrentOpacity));
                OnPropertyChanged(nameof(CurrentOpacityText));
                NotifyEdited();
            });
        }

        [Browsable(false)]
        public Animation Opacity { get; } = new(1, 0, 1);

        [Browsable(false)]
        [JsonIgnore]
        public double CurrentOpacity
        {
            get => Opacity.GetValue(0, 1, 60);
            set => SetCurrentOpacity((float)value);
        }

        [Browsable(false)]
        [JsonIgnore]
        public string CurrentOpacityText
        {
            get => CurrentOpacity.ToString("F3", CultureInfo.InvariantCulture);
            set
            {
                if (TryParseValue(value, out var parsed))
                {
                    SetCurrentOpacity(parsed);
                }
            }
        }

        [Browsable(false)]
        public bool Hold
        {
            get => hold;
            set
            {
                if (Set(ref hold, value))
                {
                    NotifyEdited();
                }
            }
        }
        bool hold;

        [Browsable(false)]
        [JsonIgnore]
        public ICommand ResetCommand { get; }

        public void UpdateMetadata(string name)
        {
            Name = name;
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(CurrentOpacityText));
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Opacity];

        private void SetCurrentOpacity(float value)
        {
            var clamped = Math.Clamp(value, 0.0f, 1.0f);
#pragma warning disable CS0618
            Opacity.From = clamped;
#pragma warning restore CS0618
            OnPropertyChanged(nameof(CurrentOpacity));
            OnPropertyChanged(nameof(CurrentOpacityText));
            NotifyEdited();
        }

        private void Opacity_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(nameof(CurrentOpacity));
            OnPropertyChanged(nameof(CurrentOpacityText));
            NotifyEdited();
        }

    }
}
