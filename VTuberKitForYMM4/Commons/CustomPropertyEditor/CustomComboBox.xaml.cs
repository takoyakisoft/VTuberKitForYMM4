using System.Windows;
using System.Windows.Controls;
using YukkuriMovieMaker.Commons;

namespace VTuberKitForYMM4.Commons.CustomPropertyEditor
{
    public partial class CustomComboBox : UserControl, IPropertyEditorControl
    {
        private readonly Button reloadButton;
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
                reloadButton.Content = "…";
                EndEdit?.Invoke(this, EventArgs.Empty);

                await CustomViewModel.PreUpdateItemsSource(update);
                CustomViewModel?.UpdateItemsSource();
                CustomViewModel?.UpdateSelectedValue();

                reloadButton.Content = "↻";
                isReloading = false;
            }
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CustomViewModel != null && CustomViewModel.SelectedValue != null)
            {
                CustomViewModel.SelectedDisplayMember = CustomViewModel.SelectedValue.DisplayMember;
            }
        }


        private void ReloadButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateView(true);
        }
    }
}
