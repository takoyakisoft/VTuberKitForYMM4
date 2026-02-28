using System.Windows;
using System.Windows.Data;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Views.Converters;

namespace VTuberKitForYMM4.Commons.CustomPropertyEditor
{
    public class CustomComboBoxAttribute : PropertyEditorAttribute2
    {
        public override FrameworkElement Create()
        {
            return new CustomComboBox();
        }

        public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
        {
            ((CustomComboBox)control).SetBinding(CustomComboBox.CustomViewModelProperty, ItemPropertiesBinding.Create2(itemProperties));
        }

        public override void ClearBindings(FrameworkElement control)
        {
            BindingOperations.ClearBinding(control, CustomComboBox.CustomViewModelProperty);
        }
    }
}
