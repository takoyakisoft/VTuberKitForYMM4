using VTuberKitForYMM4.Commons.CustomPropertyEditor;
using System.Linq;

namespace VTuberKitForYMM4.Plugin.CustomPropertyEditor
{
    public class ExpressionItem : CustomComboBoxValueBase
    {
        public string Id { get; set; } = string.Empty;
        public override string DisplayMember => Id;
    }

    public class ExpressionViewModel : CustomComboBoxViewModelBase
    {
        private static string NoneExpression => Translate.Ui_NoneSelected;
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
                var id = (SelectedValue as ExpressionItem)?.Id ?? string.Empty;
                return string.IsNullOrEmpty(id) || id == NoneExpression ? selectedExpressionId : id;
            }
            set
            {
                selectedExpressionId = string.IsNullOrWhiteSpace(value) ? string.Empty : value;
                var normalized = string.IsNullOrWhiteSpace(selectedExpressionId) ? NoneExpression : selectedExpressionId;
                var found = ItemsSource.OfType<ExpressionItem>().FirstOrDefault(x => x.Id == normalized);
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
                var id = (value as ExpressionItem)?.Id ?? string.Empty;
                if (isRefreshing && (string.IsNullOrEmpty(id) || id == NoneExpression) && !string.IsNullOrEmpty(previousSelectedExpressionId))
                {
                    return;
                }

                selectedExpressionId = id == NoneExpression ? string.Empty : id;
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
            ItemsSource.Add(new ExpressionItem { Id = NoneExpression });

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

                var normalized = string.IsNullOrWhiteSpace(selectedExpressionId) ? NoneExpression : selectedExpressionId;
                var found = !string.IsNullOrEmpty(normalized)
                    ? ItemsSource.FirstOrDefault(x => (x as ExpressionItem)?.Id == normalized)
                    : null;

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
