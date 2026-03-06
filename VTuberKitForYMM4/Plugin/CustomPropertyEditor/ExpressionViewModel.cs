using VTuberKitForYMM4.Commons.CustomPropertyEditor;

namespace VTuberKitForYMM4.Plugin.CustomPropertyEditor
{
    public class ExpressionItem : CustomComboBoxValueBase
    {
        public string Id { get; set; } = string.Empty;
        public override string DisplayMember => Id;
    }

    public class ExpressionViewModel : CustomComboBoxViewModelBase
    {
        private const string NoneExpression = "(未選択)";

        public ExpressionViewModel(string searchDisplayMember) : base(searchDisplayMember)
        {
            IsEnabled = true;
        }

        public string SelectedExpressionId
        {
            get
            {
                var id = (SelectedValue as ExpressionItem)?.Id ?? string.Empty;
                return id == NoneExpression ? string.Empty : id;
            }
        }

        public override void UpdateItemsSource()
        {
            IsEnabled = true;
            ItemsSource.Clear();
            ItemsSource.Add(new ExpressionItem { Id = NoneExpression });

            foreach (var expression in ModelMetadataCatalog.Expressions)
            {
                ItemsSource.Add(new ExpressionItem { Id = expression });
            }
        }

        public override void UpdateSelectedValue()
        {
            if (ItemsSource.Count == 0)
            {
                return;
            }

            var selectedId = (SelectedValue as ExpressionItem)?.Id;
            var found = !string.IsNullOrEmpty(selectedId)
                ? ItemsSource.FirstOrDefault(x => (x as ExpressionItem)?.Id == selectedId)
                : null;

            SelectedValue = found ?? ItemsSource.First();
            SelectedDisplayMember = SelectedValue.DisplayMember;
        }
    }
}
