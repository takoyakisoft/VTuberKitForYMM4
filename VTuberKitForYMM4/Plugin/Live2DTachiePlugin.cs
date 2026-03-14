using System.IO;
using VTuberKitForNative;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Tachie;

namespace VTuberKitForYMM4.Plugin
{
    public class Live2DTachiePlugin : ITachiePlugin, IDisposable
    {
        public string Name => Translate.Plugin_Live2D_Name;

        public bool HasScriptFile => false;

        public Live2DTachiePlugin()
        {
        }

        public void Dispose()
        {
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
            File.WriteAllText(path, "// Live2D script");
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
