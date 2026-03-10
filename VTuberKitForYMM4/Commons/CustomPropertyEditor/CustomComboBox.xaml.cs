using System.Windows;
using System.Windows.Controls;
using YukkuriMovieMaker.Commons;

namespace VTuberKitForYMM4.Commons.CustomPropertyEditor
{
    public partial class CustomComboBox : UserControl, IPropertyEditorControl
    {
        private readonly Button reloadButton;
        private readonly TextBlock reloadIcon;
        private bool isReloading;

        public CustomComboBoxViewModelBase CustomViewModel
        {
            get { return (CustomComboBoxViewModelBase)GetValue(CustomViewModelProperty); }
            set { SetValue(CustomViewModelProperty, value); }
        }

        public static readonly DependencyProperty CustomViewModelProperty =
            DependencyProperty.Register(
                nameof(CustomViewModel),
                typeof(CustomComboBoxViewModelBase),
                typeof(CustomComboBox), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnViewModelChanged));

        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;

        public CustomComboBox()
        {
            InitializeComponent();
            reloadButton = (Button)FindName("PART_ReloadButton");
            reloadIcon = (TextBlock)FindName("PART_ReloadIcon");
            isReloading = false;
        }


        private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (CustomComboBox)d;
            control.UpdateView(false);
        }

        public async void UpdateView(bool update)
        {
            if (!isReloading && CustomViewModel != null)
            {
                isReloading = true;
                BeginEdit?.Invoke(this, EventArgs.Empty);
                reloadIcon.Text = "…";
                EndEdit?.Invoke(this, EventArgs.Empty);

                await CustomViewModel.PreUpdateItemsSource(update);
                CustomViewModel?.UpdateItemsSource();
                CustomViewModel?.UpdateSelectedValue();

                reloadIcon.Text = "\uE72C";
                isReloading = false;
            }
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CustomViewModel != null)
            {
                BeginEdit?.Invoke(this, EventArgs.Empty);
                EndEdit?.Invoke(this, EventArgs.Empty);
            }
        }


        private void ReloadButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateView(true);
        }
    }
}
