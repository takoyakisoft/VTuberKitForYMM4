using VTuberKitForYMM4.Commons.CustomPropertyEditor;

namespace VTuberKitForYMM4.Plugin.CustomPropertyEditor
{
    public class MotionItem : CustomComboBoxValueBase
    {
        public bool IsNone { get; set; }
        public string Group { get; set; } = string.Empty;
        public int Index { get; set; }
        public string FileName { get; set; } = string.Empty;
        public override string DisplayMember => IsNone
            ? Translate.Ui_NoneSelected
            : $"{(string.IsNullOrEmpty(Group) ? Translate.Ui_DefaultGroup : Group)}[{Index}] {FileName}";
    }

    public class MotionViewModel : CustomComboBoxViewModelBase
    {
        private readonly Func<string?>? modelPathProvider;
        private readonly bool onlyIdle;
        private readonly bool excludeIdle;
        private string selectedGroup = string.Empty;
        private int selectedIndex = -1;
        private bool isRefreshing;

        public MotionViewModel(string searchDisplayMember, bool onlyIdle = false, bool excludeIdle = false, Func<string?>? modelPathProvider = null) : base(searchDisplayMember)
        {
            this.modelPathProvider = modelPathProvider;
            this.onlyIdle = onlyIdle;
            this.excludeIdle = excludeIdle;
            IsEnabled = true;
        }

        public string SelectedGroup
        {
            get
            {
                var selected = SelectedValue as MotionItem;
                return selected == null || selected.IsNone ? selectedGroup : selected.Group;
            }
            set
            {
                selectedGroup = value ?? string.Empty;
                OnPropertyChanged(nameof(SelectedGroup));
            }
        }

        public int SelectedIndex
        {
            get
            {
                var selected = SelectedValue as MotionItem;
                return selected == null || selected.IsNone ? selectedIndex : selected.Index;
            }
            set
            {
                selectedIndex = value;
                OnPropertyChanged(nameof(SelectedIndex));
            }
        }

        public override CustomComboBoxValueBase SelectedValue
        {
            get => base.SelectedValue;
            set
            {
                var previousSelectedGroup = selectedGroup;
                var previousSelectedIndex = selectedIndex;
                base.SelectedValue = value;
                var selected = value as MotionItem;
                var isNone = selected == null || selected.IsNone;
                if (isRefreshing && isNone && (!string.IsNullOrEmpty(previousSelectedGroup) || previousSelectedIndex >= 0))
                {
                    return;
                }

                if (selected == null || selected.IsNone)
                {
                    selectedGroup = string.Empty;
                    selectedIndex = -1;
                }
                else
                {
                    selectedGroup = selected.Group;
                    selectedIndex = selected.Index;
                }
                OnPropertyChanged(nameof(SelectedGroup));
                OnPropertyChanged(nameof(SelectedIndex));
            }
        }

        public override void UpdateItemsSource()
        {
            isRefreshing = true;
            IsEnabled = true;
            ItemsSource.Clear();
            ItemsSource.Add(new MotionItem { IsNone = true, Group = string.Empty, Index = -1, FileName = string.Empty });

            var motions = modelPathProvider == null
                ? ModelMetadataCatalog.Motions
                : ModelMetadataCatalog.GetMotions(modelPathProvider());

            foreach (var motion in motions)
            {
                var isIdle = string.Equals(motion.Group, "Idle", System.StringComparison.OrdinalIgnoreCase);
                if (onlyIdle && !isIdle)
                {
                    continue;
                }
                if (excludeIdle && isIdle)
                {
                    continue;
                }

                ItemsSource.Add(new MotionItem
                {
                    Group = motion.Group,
                    Index = motion.Index,
                    FileName = motion.FileName,
                });
            }
        }

        public override void UpdateSelectedValue()
        {
            try
            {
                if (ItemsSource.Count == 0)
                {
                    return;
                }

                MotionItem? found = null;
                if (!string.IsNullOrEmpty(selectedGroup) || selectedIndex >= 0)
                {
                    found = ItemsSource
                        .OfType<MotionItem>()
                        .FirstOrDefault(x => !x.IsNone && x.Group == selectedGroup && x.Index == selectedIndex);
                }

                SelectedValue = found ?? ItemsSource.First();
            }
            finally
            {
                isRefreshing = false;
                OnPropertyChanged(nameof(SelectedGroup));
                OnPropertyChanged(nameof(SelectedIndex));
            }
        }
    }
}
