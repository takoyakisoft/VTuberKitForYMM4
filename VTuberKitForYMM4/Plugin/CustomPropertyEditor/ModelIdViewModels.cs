using System.Linq;
using VTuberKitForYMM4.Commons.CustomPropertyEditor;

namespace VTuberKitForYMM4.Plugin.CustomPropertyEditor
{
    public class ModelIdItem : CustomComboBoxValueBase
    {
        public bool IsNone { get; set; }
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public override string DisplayMember => IsNone ? "(未選択)" : string.IsNullOrWhiteSpace(Name) ? Id : $"{Id} | {Name}";
    }

    public class ParameterIdViewModel : CustomComboBoxViewModelBase
    {
        public ParameterIdViewModel(string searchDisplayMember) : base(searchDisplayMember)
        {
            IsEnabled = true;
        }

        public string SelectedId
        {
            get
            {
                var selected = SelectedValue as ModelIdItem;
                return selected == null || selected.IsNone ? string.Empty : selected.Id;
            }
            set
            {
                SelectedDisplayMember = value ?? string.Empty;
                UpdateSelectedValue();
            }
        }

        public override void UpdateItemsSource()
        {
            IsEnabled = true;
            ItemsSource.Clear();
            ItemsSource.Add(new ModelIdItem { IsNone = true, Id = string.Empty });

            foreach (var parameter in ModelMetadataCatalog.Parameters)
            {
                ItemsSource.Add(new ModelIdItem { Id = parameter.Id, Name = parameter.Name });
            }
        }

        public override void UpdateSelectedValue()
        {
            if (ItemsSource.Count == 0)
            {
                return;
            }

            var currentId = SelectedId;
            var found = !string.IsNullOrEmpty(currentId)
                ? ItemsSource.OfType<ModelIdItem>().FirstOrDefault(x => !x.IsNone && x.Id == currentId)
                : null;

            SelectedValue = found ?? ItemsSource.First();
            SelectedDisplayMember = SelectedValue.DisplayMember;
        }
    }

    public class PartIdViewModel : CustomComboBoxViewModelBase
    {
        public PartIdViewModel(string searchDisplayMember) : base(searchDisplayMember)
        {
            IsEnabled = true;
        }

        public string SelectedId
        {
            get
            {
                var selected = SelectedValue as ModelIdItem;
                return selected == null || selected.IsNone ? string.Empty : selected.Id;
            }
            set
            {
                SelectedDisplayMember = value ?? string.Empty;
                UpdateSelectedValue();
            }
        }

        public override void UpdateItemsSource()
        {
            IsEnabled = true;
            ItemsSource.Clear();
            ItemsSource.Add(new ModelIdItem { IsNone = true, Id = string.Empty });

            foreach (var part in ModelMetadataCatalog.Parts)
            {
                ItemsSource.Add(new ModelIdItem { Id = part.Id, Name = part.Name });
            }
        }

        public override void UpdateSelectedValue()
        {
            if (ItemsSource.Count == 0)
            {
                return;
            }

            var currentId = SelectedId;
            var found = !string.IsNullOrEmpty(currentId)
                ? ItemsSource.OfType<ModelIdItem>().FirstOrDefault(x => !x.IsNone && x.Id == currentId)
                : null;

            SelectedValue = found ?? ItemsSource.First();
            SelectedDisplayMember = SelectedValue.DisplayMember;
        }
    }
}
