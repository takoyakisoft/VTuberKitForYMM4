using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using VTuberKitForNative;
using YukkuriMovieMaker.Commons;

namespace VTuberKitForYMM4.Plugin
{
    public class Live2DFaceDynamicOverrides : Animatable
    {
        private static readonly HashSet<string> StandardParameterIds =
        [
            Live2DManager.ParamEyeLOpen,
            Live2DManager.ParamEyeROpen,
            Live2DManager.ParamMouthOpenY,
            Live2DManager.ParamMouthForm,
            Live2DManager.ParamAngleX,
            Live2DManager.ParamAngleY,
            Live2DManager.ParamAngleZ,
            Live2DManager.ParamBodyAngleX,
            Live2DManager.ParamEyeBallX,
            Live2DManager.ParamEyeBallY,
            Live2DManager.ParamCheek,
            Live2DManager.ParamArmLA,
            Live2DManager.ParamArmRA,
        ];

        public ObservableCollection<Live2DFaceDynamicParameterRow> ParameterRows { get; } = [];
        public ObservableCollection<Live2DFaceDynamicPartRow> PartRows { get; } = [];
        public object ParameterRowsSyncRoot { get; } = new();
        public object PartRowsSyncRoot { get; } = new();

        public Live2DFaceDynamicOverrides()
        {
            ParameterRows.CollectionChanged += OnRowsChanged;
            PartRows.CollectionChanged += OnRowsChanged;
        }

        public void SyncWithMetadata()
        {
            SyncParameterRows();
            SyncPartRows();
        }

        protected override IEnumerable<IAnimatable> GetAnimatables()
        {
            foreach (var row in ParameterRows)
            {
                yield return row;
            }

            foreach (var row in PartRows)
            {
                yield return row;
            }
        }

        private void SyncParameterRows()
        {
            lock (ParameterRowsSyncRoot)
            {
                var metadata = ModelMetadataCatalog.Parameters
                    .Where(x => !StandardParameterIds.Contains(x.Id))
                    .OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                RemoveDuplicateParameterRows();
                var existing = ParameterRows.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
                var alive = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var item in metadata)
                {
                    var name = string.IsNullOrWhiteSpace(item.Name) ? item.Id : item.Name;
                    var hasNative = ModelMetadataCatalog.TryGetParameterMetadata(item.Id, out var native);
                    var defaultValue = hasNative ? native.Default : 0.0f;
                    var minValue = hasNative ? native.Min : -100.0f;
                    var maxValue = hasNative ? native.Max : 100.0f;

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
            }
        }

        private void SyncPartRows()
        {
            lock (PartRowsSyncRoot)
            {
                var parts = ModelMetadataCatalog.Parts
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
            OnPropertyChanged(nameof(ParameterRows));
            OnPropertyChanged(nameof(PartRows));
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
        public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Id : $"{Id} | {Name}";

        [Browsable(false)]
        [JsonIgnore]
        public Action? EditedCallback { get; set; }

        protected void NotifyEdited()
        {
            EditedCallback?.Invoke();
            OnPropertyChanged(nameof(DisplayName));
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
                SetCurrentValue(DefaultValue);
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
                    OnPropertyChanged(nameof(CurrentValue));
                    OnPropertyChanged(nameof(CurrentValueText));
                }
            }
        }
        Animation value = new(0, -100, 100);

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

        private static bool TryParseValue(string? text, out float value)
        {
            if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }

            return float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
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
            Opacity.CopyFrom(new Animation(1, 0, 1));
            ResetCommand = new ActionCommand(_ => true, _ =>
            {
                Hold = false;
                SetCurrentOpacity(1.0f);
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

        private static bool TryParseValue(string? text, out float value)
        {
            if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }

            return float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
        }
    }
}
