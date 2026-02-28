using System.Linq;
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
            ? "(未選択)"
            : $"{(string.IsNullOrEmpty(Group) ? "(Default)" : Group)}[{Index}] {FileName}";
    }

    public class MotionViewModel : CustomComboBoxViewModelBase
    {
        private readonly bool onlyIdle;
        private readonly bool excludeIdle;

        public MotionViewModel(string searchDisplayMember, bool onlyIdle = false, bool excludeIdle = false) : base(searchDisplayMember)
        {
            this.onlyIdle = onlyIdle;
            this.excludeIdle = excludeIdle;
            IsEnabled = true;
        }

        public string SelectedGroup
        {
            get
            {
                var selected = SelectedValue as MotionItem;
                return selected == null || selected.IsNone ? string.Empty : selected.Group;
            }
        }

        public int SelectedIndex
        {
            get
            {
                var selected = SelectedValue as MotionItem;
                return selected == null || selected.IsNone ? -1 : selected.Index;
            }
        }

        public override void UpdateItemsSource()
        {
            IsEnabled = true;
            ItemsSource.Clear();
            ItemsSource.Add(new MotionItem { IsNone = true, Group = string.Empty, Index = -1, FileName = string.Empty });

            foreach (var motion in ModelMetadataCatalog.Motions)
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
            if (ItemsSource.Count == 0)
            {
                return;
            }

            var selected = SelectedValue as MotionItem;
            MotionItem? found = null;
            if (selected != null && !selected.IsNone)
            {
                found = ItemsSource
                    .OfType<MotionItem>()
                    .FirstOrDefault(x => !x.IsNone && x.Group == selected.Group && x.Index == selected.Index);
            }

            SelectedValue = found ?? ItemsSource.First();
            SelectedDisplayMember = SelectedValue.DisplayMember;
        }
    }
}
