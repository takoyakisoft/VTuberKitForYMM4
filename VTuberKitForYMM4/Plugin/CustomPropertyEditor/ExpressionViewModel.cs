using VTuberKitForYMM4.Commons.CustomPropertyEditor;
using System.Linq;

namespace VTuberKitForYMM4.Plugin.CustomPropertyEditor
{
    public class ExpressionItem : CustomComboBoxValueBase
    {
        public bool IsNone { get; set; }
        public string Id { get; set; } = string.Empty;
        public override string DisplayMember => IsNone ? Translate.Ui_NoneSelected : Id;
    }

    public class ExpressionViewModel : CustomComboBoxViewModelBase
    {
        private const string NoneExpressionId = "__none__";
        private readonly Func<string?>? modelPathProvider;
        private string selectedExpressionId = string.Empty;
        private bool isRefreshing;

        public ExpressionViewModel(string searchDisplayMember, Func<string?>? modelPathProvider = null) : base(searchDisplayMember)
        {
            this.modelPathProvider = modelPathProvider;
            IsEnabled = true;
        }

        public string SelectedExpressionId
        {
            get
            {
                var item = SelectedValue as ExpressionItem;
                var id = item?.Id ?? string.Empty;
                return string.IsNullOrEmpty(id) || item?.IsNone == true ? selectedExpressionId : id;
            }
            set
            {
                selectedExpressionId = string.IsNullOrWhiteSpace(value) ? string.Empty : value;
                var found = string.IsNullOrWhiteSpace(selectedExpressionId)
                    ? ItemsSource.OfType<ExpressionItem>().FirstOrDefault(x => x.IsNone)
                    : ItemsSource.OfType<ExpressionItem>().FirstOrDefault(x => !x.IsNone && x.Id == selectedExpressionId);
                if (found != null)
                {
                    SelectedValue = found;
                }
                OnPropertyChanged(nameof(SelectedExpressionId));
            }
        }

        public override CustomComboBoxValueBase SelectedValue
        {
            get => base.SelectedValue;
            set
            {
                var previousSelectedExpressionId = selectedExpressionId;
                base.SelectedValue = value;
                var item = value as ExpressionItem;
                var id = item?.Id ?? string.Empty;
                if (isRefreshing && (item?.IsNone == true || string.IsNullOrEmpty(id)) && !string.IsNullOrEmpty(previousSelectedExpressionId))
                {
                    return;
                }

                selectedExpressionId = item?.IsNone == true ? string.Empty : id;
                if (!isRefreshing)
                {
                    OnPropertyChanged(nameof(SelectedExpressionId));
                }
            }
        }

        public override void UpdateItemsSource()
        {
            isRefreshing = true;
            IsEnabled = true;
            ItemsSource.Clear();
            ItemsSource.Add(new ExpressionItem { Id = NoneExpressionId, IsNone = true });

            var expressions = modelPathProvider == null
                ? ModelMetadataCatalog.Expressions
                : ModelMetadataCatalog.GetExpressions(modelPathProvider());

            foreach (var expression in expressions)
            {
                ItemsSource.Add(new ExpressionItem { Id = expression });
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

                var found = string.IsNullOrWhiteSpace(selectedExpressionId)
                    ? ItemsSource.OfType<ExpressionItem>().FirstOrDefault(x => x.IsNone)
                    : ItemsSource.OfType<ExpressionItem>().FirstOrDefault(x => !x.IsNone && x.Id == selectedExpressionId);

                SelectedValue = found ?? ItemsSource.First();
            }
            finally
            {
                isRefreshing = false;
                OnPropertyChanged(nameof(SelectedExpressionId));
            }
        }
    }
}
