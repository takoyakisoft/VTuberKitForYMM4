using VTuberKitForNative;
using VTuberKitForYMM4.Plugin;

namespace VTuberKitForYMM4.Tests.Plugin;

public class Live2DFaceDynamicOverridesTests
{
    [Fact]
    public void TryGetStandardParameterDefinition_ReturnsCubismEditorDefinition()
    {
        var found = Live2DFaceDynamicOverrides.TryGetStandardParameterDefinition(
            Live2DManager.ParamAngleX,
            out var definition);

        Assert.True(found);
        Assert.Equal("角度 X", definition.DisplayName);
        Assert.Equal(-30.0f, definition.Min);
        Assert.Equal(0.0f, definition.Default);
        Assert.Equal(30.0f, definition.Max);
        Assert.Equal(0, definition.SortOrder);
    }

    [Fact]
    public void GetStandardSortOrder_PrioritizesCubismStandardParameters()
    {
        var ids = new[]
        {
            "ParamCustomFoo",
            Live2DManager.ParamCheek,
            Live2DManager.ParamAngleX,
            Live2DManager.ParamMouthOpenY,
        };

        var sorted = ids
            .OrderBy(Live2DFaceDynamicOverrides.GetStandardSortOrder)
            .ThenBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Equal(Live2DManager.ParamAngleX, sorted[0]);
        Assert.Equal(Live2DManager.ParamMouthOpenY, sorted[1]);
        Assert.Equal(Live2DManager.ParamCheek, sorted[2]);
        Assert.Equal("ParamCustomFoo", sorted[3]);
    }
}
