using CodeWalker.GameFiles;
using SharpDX;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CodeWalker.World
{
    [TypeConverter(typeof(ExpandableObjectConverter))] public class Ped
    {
        public string Name { get; set; } = string.Empty;
        public MetaHash NameHash { get; set; } = 0;//ped name hash
        public CPedModelInfo__InitData? InitData { get; set; } //ped init data
        public YddFile? Ydd { get; set; } //ped drawables
        public YtdFile? Ytd { get; set; } //ped textures
        public YldFile? Yld { get; set; } //ped clothes
        public YcdFile? Ycd { get; set; } //ped animations
        public YedFile? Yed { get; set; } //ped expressions
        public YftFile? Yft { get; set; } //ped skeleton YFT
        public PedFile? Ymt { get; set; } //ped variation info
        public Dictionary<MetaHash, RpfFileEntry>? DrawableFilesDict { get; set; }
        public Dictionary<MetaHash, RpfFileEntry>? TextureFilesDict { get; set; }
        public Dictionary<MetaHash, RpfFileEntry>? ClothFilesDict { get; set; }
        public RpfFileEntry[] DrawableFiles { get; set; } = [];
        public RpfFileEntry[] TextureFiles { get; set; } = [];
        public RpfFileEntry[] ClothFiles { get; set; } = [];
        public ClipMapEntry? AnimClip { get; set; }
        public ClipMapEntry? FaceAnimClip { get; set; }
        public Expression? Expression { get; set; }
        public string?[] DrawableNames { get; set; } = new string?[12];
        public gtaDrawable?[] Drawables { get; set; } = new gtaDrawable?[12];
        public Texture?[] Textures { get; set; } = new Texture?[12];
        public Expression?[] Expressions { get; set; } = new Expression?[12];
        public ClothInstance?[] Clothes { get; set; } = new ClothInstance?[12];
        public bool EnableRootMotion { get; set; } = false; //used to toggle whether or not to include root motion when playing animations
        public crSkeletonData? Skeleton { get; set; }

        public Vector3 Position { get; set; } = Vector3.Zero;
        public Quaternion Rotation { get; set; } = Quaternion.Identity;

        public YmapEntityDef RenderEntity = new(); //placeholder entity object for rendering

        // Track if base ped files are loaded
        public bool IsLoaded => Yft?.Loaded ?? false;
        public bool IsLoading { get; private set; } = false;

        public void Init(string name, GameFileCache gfc)
        {
            var hash = JenkHash.GenHashLowerInvariant(name);
            Init(hash, gfc);
            Name = name;
        }
        public void Init(MetaHash pedhash, GameFileCache gfc)
        {
            // Use async version internally but wait for completion
            InitAsync(pedhash, gfc).GetAwaiter().GetResult();
        }

        public async Task InitAsync(string name, GameFileCache gfc)
        {
            var hash = JenkHash.GenHashLowerInvariant(name);
            await InitAsync(hash, gfc);
            Name = name;
        }

        public async Task InitAsync(MetaHash pedhash, GameFileCache gfc)
        {
            IsLoading = true;

            Name = string.Empty;
            NameHash = 0;
            InitData = null;
            Ydd = null;
            Ytd = null;
            Yld = null;
            Ycd = null;
            Yed = null;
            Yft = null;
            Ymt = null;
            AnimClip = null;
            FaceAnimClip = null;
            for (int i = 0; i < 12; i++)
            {
                Drawables[i] = null;
                Textures[i] = null;
                Expressions[i] = null;
            }


            CPedModelInfo__InitData? initdata = null;
            if (!gfc.PedsInitDict.TryGetValue(pedhash, out initdata))
            {
                IsLoading = false;
                return;
            }

            var ycdhash = JenkHash.GenHashLowerInvariant(initdata.ClipDictionaryName);
            var yedhash = JenkHash.GenHashLowerInvariant(initdata.ExpressionDictionaryName);

            NameHash = pedhash;
            InitData = initdata;

            // Request all files at once - they'll load in parallel via the content thread
            Ydd = gfc.GetYdd(pedhash);
            Ytd = gfc.GetYtd(pedhash);
            Ycd = gfc.GetYcd(ycdhash);
            Yed = gfc.GetYed(yedhash);
            Yft = gfc.GetYft(pedhash);

            PedFile? pedFile = null;
            gfc.PedVariationsDict?.TryGetValue(pedhash, out pedFile);
            Ymt = pedFile;

            Dictionary<MetaHash, RpfFileEntry>? peddict = null;
            gfc.PedDrawableDicts.TryGetValue(NameHash, out peddict);
            DrawableFilesDict = peddict;
            DrawableFiles = DrawableFilesDict?.Values.ToArray() ?? [];
            gfc.PedTextureDicts.TryGetValue(NameHash, out peddict);
            TextureFilesDict = peddict;
            TextureFiles = TextureFilesDict?.Values.ToArray() ?? [];
            gfc.PedClothDicts.TryGetValue(NameHash, out peddict);
            ClothFilesDict = peddict;
            ClothFiles = ClothFilesDict?.Values.ToArray() ?? [];

            RpfFileEntry? clothFile = null;
            if (ClothFilesDict?.TryGetValue(pedhash, out clothFile) ?? false)
            {
                Yld = gfc.GetFileUncached<YldFile>(clothFile);
            }

            // Wait for all files to load in parallel using async delay instead of Thread.Sleep
            await WaitForFilesAsync(gfc, pedhash, ycdhash, yedhash);

            Skeleton = Yft?.Fragment?.Drawable?.SkeletonData?.Clone();

            MetaHash cliphash = JenkHash.GenHash("idle");
            ClipMapEntry? cme = null;
            Ycd?.ClipMap?.TryGetValue(cliphash, out cme);
            AnimClip = cme;

            var exprhash = JenkHash.GenHashLowerInvariant(initdata.ExpressionName);
            Expression? expr = null;
            Yed?.ExprMap?.TryGetValue(exprhash, out expr);
            Expression = expr;

            IsLoading = false;
            UpdateEntity();
        }

        private async Task WaitForFilesAsync(GameFileCache gfc, MetaHash pedhash, MetaHash ycdhash, MetaHash yedhash)
        {
            const int maxWaitMs = 10000; // 10 second timeout
            const int checkIntervalMs = 5; // Check every 5ms instead of 1ms
            var startTime = DateTime.UtcNow;

            while ((DateTime.UtcNow - startTime).TotalMilliseconds < maxWaitMs)
            {
                bool allLoaded = true;

                // Check and update references for files that might have been reloaded
                if (Ydd != null && !Ydd.Loaded) { Ydd = gfc.GetYdd(pedhash); allLoaded = false; }
                if (Ytd != null && !Ytd.Loaded) { Ytd = gfc.GetYtd(pedhash); allLoaded = false; }
                if (Ycd != null && !Ycd.Loaded) { Ycd = gfc.GetYcd(ycdhash); allLoaded = false; }
                if (Yed != null && !Yed.Loaded) { Yed = gfc.GetYed(yedhash); allLoaded = false; }
                if (Yft != null && !Yft.Loaded) { Yft = gfc.GetYft(pedhash); allLoaded = false; }
                if (Yld != null && !Yld.Loaded) { gfc.TryLoadEnqueue(Yld); allLoaded = false; }

                if (allLoaded) break;

                await Task.Delay(checkIntervalMs);
            }

            if ((Ydd != null && !Ydd.Loaded) || (Ytd != null && !Ytd.Loaded) || (Ycd != null && !Ycd.Loaded) ||
                (Yed != null && !Yed.Loaded) || (Yft != null && !Yft.Loaded) || (Yld != null && !Yld.Loaded))
            {
                gfc.ErrorLog?.Invoke($"Ped {JenkIndex.GetString(pedhash)}: timed out waiting for" +
                    (Ydd != null && !Ydd.Loaded ? " Ydd" : "") +
                    (Ytd != null && !Ytd.Loaded ? " Ytd" : "") +
                    (Ycd != null && !Ycd.Loaded ? " Ycd" : "") +
                    (Yed != null && !Yed.Loaded ? " Yed" : "") +
                    (Yft != null && !Yft.Loaded ? " Yft" : "") +
                    (Yld != null && !Yld.Loaded ? " Yld" : ""));
            }
        }





        public void SetComponentDrawable(int index, string? name, string? tex, GameFileCache gfc)
        {
            // Use async version internally
            SetComponentDrawableAsync(index, name, tex, gfc).GetAwaiter().GetResult();
        }

        public async Task SetComponentDrawableAsync(int index, string? name, string? tex, GameFileCache gfc)
        {
            if (string.IsNullOrEmpty(name))
            {
                DrawableNames[index] = null;
                Drawables[index] = null;
                Textures[index] = null;
                Expressions[index] = null;
                Clothes[index] = null;
                return;
            }

            MetaHash namehash = JenkHash.GenHashLowerInvariant(name);
            MetaHash texhash = JenkHash.GenHashLowerInvariant(tex);

            // Start loading all required files in parallel
            YddFile? yddFile = null;
            YtdFile? ytdFile = null;
            YldFile? yldFile = null;

            // Check if drawable is in the main ped YDD first
            gtaDrawable? d = null;
            if (Ydd?.Dict != null)
            {
                Ydd.Dict.TryGetValue(namehash, out d);
            }

            // If not found, need to load from component-specific file
            if (d == null && DrawableFilesDict != null && DrawableFilesDict.TryGetValue(namehash, out RpfFileEntry? drawableFile))
            {
                yddFile = gfc.GetFileUncached<YddFile>(drawableFile);
            }

            // Check if texture is in the main ped YTD first
            Texture? t = null;
            if (Ytd?.TextureDict?.Dict != null)
            {
                Ytd.TextureDict.Dict.TryGetValue(texhash, out t);
            }

            // If not found, need to load from component-specific file
            if (t == null && TextureFilesDict != null && TextureFilesDict.TryGetValue(texhash, out RpfFileEntry? textureFile))
            {
                ytdFile = gfc.GetFileUncached<YtdFile>(textureFile);
            }

            // Check if cloth is in the main ped YLD first
            CharacterCloth? cc = null;
            if (Yld?.Dict != null)
            {
                Yld.Dict.TryGetValue(namehash, out cc);
            }

            // If not found, need to load from component-specific file
            if (cc == null && ClothFilesDict != null && ClothFilesDict.TryGetValue(namehash, out RpfFileEntry? clothFile))
            {
                yldFile = gfc.GetFileUncached<YldFile>(clothFile);
            }

            // Wait for any files that need loading
            const int maxWaitMs = 5000;
            const int checkIntervalMs = 5;
            var startTime = DateTime.UtcNow;

            while ((DateTime.UtcNow - startTime).TotalMilliseconds < maxWaitMs)
            {
                bool allLoaded = true;

                if (yddFile != null && !yddFile.Loaded) { gfc.TryLoadEnqueue(yddFile); allLoaded = false; }
                if (ytdFile != null && !ytdFile.Loaded) { gfc.TryLoadEnqueue(ytdFile); allLoaded = false; }
                if (yldFile != null && !yldFile.Loaded) { gfc.TryLoadEnqueue(yldFile); allLoaded = false; }

                if (allLoaded) break;

                await Task.Delay(checkIntervalMs);
            }

            // Extract results from loaded files
            if (d == null && yddFile?.Drawables?.Length > 0)
            {
                d = yddFile.Drawables[0];
            }

            if (t == null && ytdFile?.TextureDict?.Textures?.data_items?.Length > 0)
            {
                t = ytdFile.TextureDict.Textures.data_items[0];
            }

            if (cc == null && yldFile?.ClothDictionary?.Clothes?.data_items?.Length > 0)
            {
                cc = yldFile.ClothDictionary.Clothes.data_items[0];
            }

            ClothInstance? c = null;
            if (cc != null)
            {
                c = new ClothInstance();
                c.Init(cc, Skeleton);
            }

            Expression? e = null;
            if (Yed?.ExprMap != null)
            {
                Yed.ExprMap.TryGetValue(namehash, out e);
            }

            if (d != null)
            {
                var component = d.ShallowCopy() as gtaDrawable;
                // Binding a pose must not mutate a cached drawable or another actor's palette.
                if (component != null) component.SkeletonData = (d.SkeletonData ?? Skeleton)?.Clone();
                Drawables[index] = component;
            }
            if (t != null) Textures[index] = t;
            if (c != null) Clothes[index] = c;
            if (e != null) Expressions[index] = e;

            DrawableNames[index] = name;
        }

        public void SetComponentDrawable(int index, int drawbl, int alt, int tex, GameFileCache gfc)
        {
            SetComponentDrawableAsync(index, drawbl, alt, tex, gfc).GetAwaiter().GetResult();
        }

        public async Task SetComponentDrawableAsync(int index, int drawbl, int alt, int tex, GameFileCache gfc)
        {
            var vi = Ymt?.VariationInfo;
            if (vi != null)
            {
                var compData = vi.GetComponentData(index);
                if (compData?.DrawblData3 != null)
                {
                    var item = (drawbl >= 0 && drawbl < compData.DrawblData3.Length) ? compData.DrawblData3[drawbl] : null;
                    if (item != null)
                    {
                        var name = item?.GetDrawableName(alt);
                        var texn = item?.GetTextureName(tex);
                        await SetComponentDrawableAsync(index, name, texn, gfc);
                    }
                }
            }
        }

        public void LoadDefaultComponents(GameFileCache gfc)
        {
            LoadDefaultComponentsAsync(gfc).GetAwaiter().GetResult();
        }

        public async Task LoadDefaultComponentsAsync(GameFileCache gfc)
        {
            // Load all 12 components in parallel
            var tasks = new Task[12];
            for (int i = 0; i < 12; i++)
            {
                tasks[i] = SetComponentDrawableAsync(i, 0, 0, 0, gfc);
            }
            await Task.WhenAll(tasks);
        }




        public void UpdateEntity()
        {
            RenderEntity.SetPosition(Position);
            RenderEntity.SetOrientation(Rotation);
        }

    }
}
