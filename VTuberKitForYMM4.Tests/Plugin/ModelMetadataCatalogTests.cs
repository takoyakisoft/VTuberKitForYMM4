using System.IO;
using System.Text;
using VTuberKitForYMM4.Plugin;

namespace VTuberKitForYMM4.Tests.Plugin;

public sealed class ModelMetadataCatalogTests : IDisposable
{
    private readonly string tempRoot;

    public ModelMetadataCatalogTests()
    {
        tempRoot = Path.Combine(Path.GetTempPath(), "VTuberKitForYMM4.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
    }

    [Fact]
    public void ResolveModelSelection_ReturnsTopLevelModel3Json()
    {
        var modelPath = Path.Combine(tempRoot, "Test.model3.json");
        File.WriteAllText(modelPath, "{}");

        var resolution = ModelMetadataCatalog.ResolveModelSelection(tempRoot);

        Assert.True(resolution.IsValid);
        Assert.True(resolution.SelectionWasDirectory);
        Assert.Equal(Path.GetFullPath(modelPath), resolution.ResolvedModelPath);
    }

    [Fact]
    public void ResolveModelSelection_FlagsMultipleCandidates()
    {
        var firstPath = Path.Combine(tempRoot, "A.model3.json");
        var secondPath = Path.Combine(tempRoot, "B.model3.json");
        File.WriteAllText(firstPath, "{}");
        File.WriteAllText(secondPath, "{}");

        var resolution = ModelMetadataCatalog.ResolveModelSelection(tempRoot);

        Assert.True(resolution.IsValid);
        Assert.True(resolution.MultipleCandidatesFound);
        Assert.Equal(Path.GetFullPath(firstPath), resolution.ResolvedModelPath);
    }

    [Fact]
    public void GetModelWarnings_ReportsMissingReferencedFiles()
    {
        var modelPath = Path.Combine(tempRoot, "Warn.model3.json");
        File.WriteAllText(
            modelPath,
            """
            {
              "FileReferences": {
                "Moc": "Warn.moc3",
                "Textures": ["texture_00.png"],
                "Expressions": [
                  { "Name": "Smile", "File": "exp/smile.exp3.json" }
                ],
                "Motions": {
                  "Idle": [
                    { "File": "motions/idle.motion3.json" }
                  ]
                },
                "Physics": "Warn.physics3.json",
                "DisplayInfo": "Warn.cdi3.json"
              },
              "HitAreas": [
                { "Name": "Head" }
              ]
            }
            """,
            Encoding.UTF8);

        var warnings = ModelMetadataCatalog.GetModelWarnings(modelPath);

        Assert.Contains(warnings, x => x.Contains("Moc ファイル", StringComparison.Ordinal));
        Assert.Contains(warnings, x => x.Contains("Texture ファイル", StringComparison.Ordinal));
        Assert.Contains(warnings, x => x.Contains("Physics ファイル", StringComparison.Ordinal));
        Assert.Contains(warnings, x => x.Contains("DisplayInfo ファイル", StringComparison.Ordinal));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }
        catch
        {
        }
    }
}
