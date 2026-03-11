using System.Windows;
using System.Windows.Data;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Views.Converters;

namespace VTuberKitForYMM4.Commons.CustomPropertyEditor
{
    public class DynamicFaceOverridesEditorAttribute : PropertyEditorAttribute2
    {
        public override FrameworkElement Create()
        {
            return new DynamicFaceOverridesEditor();
        }

        public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
        {
            var binding = ItemPropertiesBinding.Create2(itemProperties);
            binding.Mode = BindingMode.OneWay;
            ((DynamicFaceOverridesEditor)control).SetBinding(
                DynamicFaceOverridesEditor.OverridesProperty,
                binding);
        }

        public override void ClearBindings(FrameworkElement control)
        {
            BindingOperations.ClearBinding(control, DynamicFaceOverridesEditor.OverridesProperty);
        }
    }
}
