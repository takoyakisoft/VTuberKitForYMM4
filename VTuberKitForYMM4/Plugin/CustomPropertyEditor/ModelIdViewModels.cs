using VTuberKitForYMM4.Commons.CustomPropertyEditor;

namespace VTuberKitForYMM4.Plugin.CustomPropertyEditor
{
    public class ModelIdItem : CustomComboBoxValueBase
    {
        public bool IsNone { get; set; }
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public override string DisplayMember => IsNone ? Translate.Ui_NoneSelected : string.IsNullOrWhiteSpace(Name) ? Id : $"{Id} | {Name}";
    }

    public class InteractionTargetItem : ModelIdItem
    {
        public override string DisplayMember => IsNone ? Translate.Ui_NoneSelected : string.IsNullOrWhiteSpace(Name) ? Id : Name;
    }

    public class ParameterIdViewModel : CustomComboBoxViewModelBase
    {
        private string selectedId = string.Empty;

        public ParameterIdViewModel(string searchDisplayMember) : base(searchDisplayMember)
        {
            IsEnabled = true;
        }

        public string SelectedId
        {
            get
            {
                var selected = SelectedValue as ModelIdItem;
                return selected == null || selected.IsNone ? selectedId : selected.Id;
            }
            set
            {
                selectedId = value ?? string.Empty;
                UpdateSelectedValue();
                OnPropertyChanged(nameof(SelectedId));
            }
        }

        public override CustomComboBoxValueBase SelectedValue
        {
            get => base.SelectedValue;
            set
            {
                base.SelectedValue = value;
                var selected = value as ModelIdItem;
                selectedId = selected == null || selected.IsNone ? string.Empty : selected.Id;
                OnPropertyChanged(nameof(SelectedId));
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

            var found = !string.IsNullOrEmpty(selectedId)
                ? ItemsSource.OfType<ModelIdItem>().FirstOrDefault(x => !x.IsNone && x.Id == selectedId)
                : null;

            SelectedValue = found ?? ItemsSource.First();
        }
    }

    public class PartIdViewModel : CustomComboBoxViewModelBase
    {
        private string selectedId = string.Empty;

        public PartIdViewModel(string searchDisplayMember) : base(searchDisplayMember)
        {
            IsEnabled = true;
        }

        public string SelectedId
        {
            get
            {
                var selected = SelectedValue as ModelIdItem;
                return selected == null || selected.IsNone ? selectedId : selected.Id;
            }
            set
            {
                selectedId = value ?? string.Empty;
                UpdateSelectedValue();
                OnPropertyChanged(nameof(SelectedId));
            }
        }

        public override CustomComboBoxValueBase SelectedValue
        {
            get => base.SelectedValue;
            set
            {
                base.SelectedValue = value;
                var selected = value as ModelIdItem;
                selectedId = selected == null || selected.IsNone ? string.Empty : selected.Id;
                OnPropertyChanged(nameof(SelectedId));
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

            var found = !string.IsNullOrEmpty(selectedId)
                ? ItemsSource.OfType<ModelIdItem>().FirstOrDefault(x => !x.IsNone && x.Id == selectedId)
                : null;

            SelectedValue = found ?? ItemsSource.First();
        }
    }

    public class InteractionTargetViewModel : CustomComboBoxViewModelBase
    {
        private readonly Func<string?>? selectedLinkIdProvider;
        private string selectedLinkId = string.Empty;
        private bool isRefreshing;

        public InteractionTargetViewModel(string searchDisplayMember, Func<string?>? selectedLinkIdProvider = null) : base(searchDisplayMember)
        {
            this.selectedLinkIdProvider = selectedLinkIdProvider;
            IsEnabled = true;
        }

        public string SelectedLinkId
        {
            get
            {
                var selected = SelectedValue as ModelIdItem;
                return selected == null || selected.IsNone ? selectedLinkId : selected.Id;
            }
            set
            {
                selectedLinkId = value ?? string.Empty;
                UpdateSelectedValue();
                OnPropertyChanged(nameof(SelectedLinkId));
            }
        }

        public override CustomComboBoxValueBase SelectedValue
        {
            get => base.SelectedValue;
            set
            {
                var previousSelectedLinkId = selectedLinkId;
                base.SelectedValue = value;
                var selected = value as ModelIdItem;
                var newSelectedLinkId = selected == null || selected.IsNone ? string.Empty : selected.Id;
                if (isRefreshing && string.IsNullOrEmpty(newSelectedLinkId) && !string.IsNullOrEmpty(previousSelectedLinkId))
                {
                    return;
                }

                if (string.Equals(selectedLinkId, newSelectedLinkId, StringComparison.Ordinal))
                {
                    return;
                }

                selectedLinkId = newSelectedLinkId;
                if (!isRefreshing)
                {
                    OnPropertyChanged(nameof(SelectedLinkId));
                }
            }
        }

        public override void UpdateItemsSource()
        {
            isRefreshing = true;
            if (selectedLinkIdProvider != null)
            {
                selectedLinkId = selectedLinkIdProvider() ?? string.Empty;
            }

            IsEnabled = true;
            ItemsSource.Clear();
            ItemsSource.Add(new InteractionTargetItem { IsNone = true, Id = string.Empty });

            var targets = Live2DInteractionStore.GetInteractionTargets();
            var duplicateNames = targets
                .GroupBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Where(x => x.Count() > 1)
                .Select(x => x.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var target in targets)
            {
                var name = target.DisplayName;
                if (duplicateNames.Contains(target.DisplayName))
                {
                    var shortLinkId = target.LinkId.Length > 8
                        ? target.LinkId[..8]
                        : target.LinkId;
                    name = $"{target.DisplayName} ({shortLinkId})";
                }

                ItemsSource.Add(new InteractionTargetItem { Id = target.LinkId, Name = name });
            }

            if (!string.IsNullOrWhiteSpace(selectedLinkId) &&
                !ItemsSource.OfType<InteractionTargetItem>().Any(x => !x.IsNone && x.Id == selectedLinkId))
            {
                ItemsSource.Add(new InteractionTargetItem { Id = selectedLinkId, Name = selectedLinkId });
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

                var found = !string.IsNullOrEmpty(selectedLinkId)
                    ? ItemsSource.OfType<InteractionTargetItem>().FirstOrDefault(x => !x.IsNone && x.Id == selectedLinkId)
                    : null;

                SelectedValue = found ?? ItemsSource.First();
            }
            finally
            {
                isRefreshing = false;
                OnPropertyChanged(nameof(SelectedLinkId));
            }
        }
    }

    public class HitAreaIdViewModel : CustomComboBoxViewModelBase
    {
        private readonly Func<string?>? modelPathProvider;
        private string selectedId = string.Empty;
        private bool isRefreshing;

        public HitAreaIdViewModel(string searchDisplayMember, Func<string?>? modelPathProvider = null) : base(searchDisplayMember)
        {
            this.modelPathProvider = modelPathProvider;
            IsEnabled = true;
        }

        public string SelectedId
        {
            get
            {
                var selected = SelectedValue as ModelIdItem;
                return selected == null || selected.IsNone ? selectedId : selected.Id;
            }
            set
            {
                selectedId = value ?? string.Empty;
                UpdateSelectedValue();
                OnPropertyChanged(nameof(SelectedId));
            }
        }

        public override CustomComboBoxValueBase SelectedValue
        {
            get => base.SelectedValue;
            set
            {
                var previousSelectedId = selectedId;
                base.SelectedValue = value;
                var selected = value as ModelIdItem;
                var newSelectedId = selected == null || selected.IsNone ? string.Empty : selected.Id;
                if (isRefreshing && string.IsNullOrEmpty(newSelectedId) && !string.IsNullOrEmpty(previousSelectedId))
                {
                    return;
                }

                selectedId = newSelectedId;
                if (!isRefreshing)
                {
                    OnPropertyChanged(nameof(SelectedId));
                }
            }
        }

        public override void UpdateItemsSource()
        {
            isRefreshing = true;
            IsEnabled = true;
            ItemsSource.Clear();
            ItemsSource.Add(new ModelIdItem { IsNone = true, Id = string.Empty });

            var hitAreas = modelPathProvider == null
                ? ModelMetadataCatalog.HitAreas
                : ModelMetadataCatalog.GetHitAreas(modelPathProvider());

            foreach (var hitArea in hitAreas)
            {
                ItemsSource.Add(new ModelIdItem { Id = hitArea.Id, Name = hitArea.Name });
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

                var found = !string.IsNullOrEmpty(selectedId)
                    ? ItemsSource.OfType<ModelIdItem>().FirstOrDefault(x => !x.IsNone && x.Id == selectedId)
                    : null;

                SelectedValue = found ?? ItemsSource.First();
            }
            finally
            {
                isRefreshing = false;
                OnPropertyChanged(nameof(SelectedId));
            }
        }
    }
}
