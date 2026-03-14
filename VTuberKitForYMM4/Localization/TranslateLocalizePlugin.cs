using System.Globalization;
using YukkuriMovieMaker.Plugin;

namespace VTuberKitForYMM4.Plugin;

public sealed class TranslateLocalizePlugin : ILocalizePlugin
{
    public string Name => Translate.Plugin_Live2D_Name;

    public void SetCulture(CultureInfo cultureInfo)
    {
        Translate.Culture = cultureInfo;
    }
}
