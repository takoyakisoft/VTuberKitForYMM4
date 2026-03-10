using System.Numerics;
using System.ComponentModel;
using System.Windows;
using VTuberKitForYMM4.Commons.CustomPropertyEditor;
using VTuberKitForYMM4.Plugin;
using VTuberKitForYMM4.Plugin.Shape;
using YukkuriMovieMaker.Player.Video;

namespace VTuberKitForYMM4.Tests.Plugin.Shape;

public class InteractionShapeTransformTests
{
    [Fact]
    public void TargetPointPixelTransform_UsesHitTestScaleForItemTranslation()
    {
        var state = new Live2DInteractionStore.InteractionTransformState(
            1.0f,
            0.0f,
            1.0f,
            0.0f,
            0.0f,
            0.0f,
            1.5324074f,
            1.0f,
            0.0f,
            0.0f);

        var pixel = InteractionShapeTransform.TransformTargetPointToPixel(Vector2.Zero, state, 1920, 1080);

        Assert.InRange(pixel.X, 827.4f, 827.6f);
        Assert.InRange(pixel.Y, -0.01f, 0.01f);
    }

    [Fact]
    public void PreferredTargetPoint_UsesHighestLayer()
    {
        var linkId = Guid.NewGuid().ToString("N");
        Live2DInteractionStore.UpdateTargetPoint($"tp-low-{linkId}", linkId, -0.5f, 0.0f, 1);
        Live2DInteractionStore.UpdateTargetPoint($"tp-high-{linkId}", linkId, 0.25f, 0.0f, 3);

        var found = Live2DInteractionStore.TryGetTargetPoint(linkId, out var x, out var y);

        Assert.True(found);
        Assert.Equal(0.25f, x);
        Assert.Equal(0.0f, y);
    }

    [Fact]
    public void PreferredHitAreaReaction_UsesHighestLayerBeforeActivationTime()
    {
        var linkId = Guid.NewGuid().ToString("N");
        Live2DInteractionStore.UpdateHitAreaRect($"ha-low-{linkId}", linkId, "Body", "exp-low", string.Empty, -1, 0, 0, 0.2f, 0.2f, 1);
        Live2DInteractionStore.UpdateHitAreaRect($"ha-high-{linkId}", linkId, "Body", "exp-high", string.Empty, -1, 0, 0, 0.2f, 0.2f, 5);
        Live2DInteractionStore.SetHitAreaResult($"ha-low-{linkId}", true);
        Live2DInteractionStore.SetHitAreaResult($"ha-high-{linkId}", true);

        var preferred = Live2DInteractionStore.GetPreferredHitAreaReaction(
            linkId,
            sourceId => sourceId.Contains("low", StringComparison.Ordinal) ? 100.0 : 10.0);

        Assert.NotNull(preferred);
        Assert.Equal($"ha-high-{linkId}", preferred!.SourceId);
        Assert.Equal("exp-high", preferred.ExpressionId);
    }

    [Fact]
    public void ResolveActiveFace_UsesHighestLayerAmongOverlappingFaces()
    {
        var lowFace = new Live2DFaceParameter();
        var highFace = new Live2DFaceParameter();
        var fps = 60;
        var current = new FrameTime(90, fps);
        var faces = new[]
        {
            new TachieFaceDescription(new FrameTime(60, fps), new FrameTime(120, fps), 1, lowFace),
            new TachieFaceDescription(new FrameTime(60, fps), new FrameTime(120, fps), 4, highFace),
        };

        var resolved = Live2DTachieSource.ResolveActiveFace(faces, current);

        Assert.Same(highFace, resolved.Face);
    }

    [Fact]
    public void ResolveActiveFace_UsesLatestStartWithinSameLayer()
    {
        var earlierFace = new Live2DFaceParameter();
        var laterFace = new Live2DFaceParameter();
        var fps = 60;
        var current = new FrameTime(90, fps);
        var faces = new[]
        {
            new TachieFaceDescription(new FrameTime(30, fps), new FrameTime(120, fps), 2, earlierFace),
            new TachieFaceDescription(new FrameTime(60, fps), new FrameTime(120, fps), 2, laterFace),
        };

        var resolved = Live2DTachieSource.ResolveActiveFace(faces, current);

        Assert.Same(laterFace, resolved.Face);
    }

    [Fact]
    public void ResolveActiveFace_ComputesFaceLocalFrameFromFaceStart()
    {
        var face = new Live2DFaceParameter();
        var fps = 60;
        var current = new FrameTime(150, fps);
        var faces = new[]
        {
            new TachieFaceDescription(new FrameTime(120, fps), new FrameTime(180, fps), 1, face),
        };

        var resolved = Live2DTachieSource.ResolveActiveFace(faces, current);

        Assert.Same(face, resolved.Face);
        Assert.Equal(30.0, resolved.LocalFrame);
        Assert.Equal(180.0, resolved.DurationFrame);
        Assert.Equal(0.5f, resolved.RelativeTimeSeconds);
    }

    [Fact]
    public void DynamicFaceOverridesEditor_RaisesEditEvents_WhenSliderValueNotificationArrives()
    {
        RunSta(() =>
        {
            if (Application.Current == null)
            {
                _ = new Application();
            }

            var overrides = new Live2DFaceDynamicOverrides();
            overrides.ParameterRows.Add(new Live2DFaceDynamicParameterRow("ParamTest", "Test", 0.0f, -30.0f, 30.0f));

            var editor = new DynamicFaceOverridesEditor
            {
                Overrides = overrides,
            };

            var beginEditCount = 0;
            var endEditCount = 0;
            editor.BeginEdit += (_, _) => beginEditCount++;
            editor.EndEdit += (_, _) => endEditCount++;

            var window = new Window
            {
                Width = 640,
                Height = 480,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                Content = editor,
            };

            try
            {
                window.Show();
                PumpDispatcher();

                typeof(DynamicFaceOverridesEditor)
                    .GetMethod("SliderValueViewModel_PropertyChanged", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(editor, [null, new PropertyChangedEventArgs("Value")]);

                Assert.True(beginEditCount > 0);
                Assert.True(endEditCount > 0);
            }
            finally
            {
                window.Close();
                PumpDispatcher();
            }
        });
    }

    private static void RunSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception != null)
        {
            throw exception;
        }
    }

    private static void PumpDispatcher()
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        _ = System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Background,
            new Action(() => frame.Continue = false));
        System.Windows.Threading.Dispatcher.PushFrame(frame);
    }
}
