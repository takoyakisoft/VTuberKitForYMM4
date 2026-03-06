using System.IO;
using VTuberKitForNative;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Tachie;

namespace VTuberKitForYMM4.Plugin
{
    public class Live2DTachiePlugin : ITachiePlugin, IDisposable
    {
        public string Name => "VTuberKit Live2D";

        public bool HasScriptFile => false;

        private bool _isLive2DInitialized = false;

        public Live2DTachiePlugin()
        {
            try
            {
                var manager = Live2DManager.GetInstance();
                if (manager != null)
                {
                    manager.Initialize();
                    _isLive2DInitialized = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Live2D Plugin Init Error: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_isLive2DInitialized)
            {
                Live2DManager.GetInstance()?.Release();
                _isLive2DInitialized = false;
            }
        }

        public ITachieCharacterParameter CreateCharacterParameter()
        {
            return new Live2DCharacterParameter();
        }

        public ITachieItemParameter CreateItemParameter()
        {
            return new Live2DItemParameter();
        }

        public ITachieFaceParameter CreateFaceParameter()
        {
            return new Live2DFaceParameter();
        }

        public ITachieSource CreateTachieSource(IGraphicsDevicesAndContext devices)
        {
            return new Live2DTachieSource(devices);
        }

        public void CreateScriptFile(string path)
        {
            // スクリプトファイル作成（必要に応じて実装）
            File.WriteAllText(path, "// Live2D script placeholder");
        }

        public IEnumerable<ExoItem> CreateExoItems(
            int FPS,
            IEnumerable<TachieItemExoDescription> tachieItemDescriptions,
            IEnumerable<TachieFaceItemExoDescription> faceItemDescriptions,
            IEnumerable<TachieVoiceItemExoDescription> voiceDescriptions)
        {
            return Enumerable.Empty<ExoItem>();
        }
    }
}
