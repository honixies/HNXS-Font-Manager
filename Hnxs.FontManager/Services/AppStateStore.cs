using System.IO;
using System.Text.Json;
using Hnxs.FontManager.Models;

namespace Hnxs.FontManager.Services;

public sealed class AppStateStore
{
    private const int CurrentCacheVersion = 4;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public AppState State { get; private set; } = new();

    public string StatePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HNXS Font Manager",
        "state.json");

    public async Task LoadAsync()
    {
        try
        {
            if (!File.Exists(StatePath))
            {
                State = new AppState();
                return;
            }

            await using var stream = File.OpenRead(StatePath);
            State = await JsonSerializer.DeserializeAsync<AppState>(stream, _jsonOptions) ?? new AppState();
            if (State.CacheVersion != CurrentCacheVersion)
            {
                State.CachedFonts.Clear();
                State.CacheVersion = CurrentCacheVersion;
            }
        }
        catch
        {
            State = new AppState();
        }
    }

    public async Task SaveAsync()
    {
        var folder = Path.GetDirectoryName(StatePath);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
        }

        await using var stream = File.Create(StatePath);
        await JsonSerializer.SerializeAsync(stream, State, _jsonOptions);
    }

    public IReadOnlyDictionary<string, CachedFontAsset> BuildCacheIndex(string sourcePath)
    {
        return State.CachedFonts
            .Where(item => string.Equals(item.SourcePath, sourcePath, StringComparison.OrdinalIgnoreCase))
            .GroupBy(item => item.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
    }

    public void RememberFolder(string folderPath, bool includeSubfolders, IEnumerable<FontAsset> assets)
    {
        State.LastSource = new LastSourceState
        {
            Kind = "folder",
            Path = folderPath,
            IncludeSubfolders = includeSubfolders
        };
        State.CacheVersion = CurrentCacheVersion;

        ReplaceCache(folderPath, assets);
    }

    public void RememberZip(string zipPath)
    {
        State.LastSource = new LastSourceState
        {
            Kind = "zip",
            Path = zipPath,
            IncludeSubfolders = true
        };
    }

    private void ReplaceCache(string sourcePath, IEnumerable<FontAsset> assets)
    {
        State.CachedFonts.RemoveAll(item => string.Equals(item.SourcePath, sourcePath, StringComparison.OrdinalIgnoreCase));
        State.CachedFonts.AddRange(assets.Select(asset => CachedFontAsset.FromAsset(sourcePath, asset)));
    }
}

public sealed class AppState
{
    public int CacheVersion { get; set; } = 4;
    public LastSourceState? LastSource { get; set; }
    public List<CachedFontAsset> CachedFonts { get; set; } = [];
}

public sealed class LastSourceState
{
    public string Kind { get; set; } = "";
    public string Path { get; set; } = "";
    public bool IncludeSubfolders { get; set; } = true;
}

public sealed class CachedFontAsset
{
    public string SourcePath { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string OriginalFileName { get; set; } = "";
    public string SourceLabel { get; set; } = "";
    public string Format { get; set; } = "";
    public string Family { get; set; } = "Unknown";
    public string Style { get; set; } = "Regular";
    public string Version { get; set; } = "-";
    public string Designer { get; set; } = "-";
    public string Foundry { get; set; } = "-";
    public string License { get; set; } = "-";
    public string LanguageHint { get; set; } = "-";
    public string Sha256 { get; set; } = "";
    public long FileSize { get; set; }
    public long LastWriteUtcTicks { get; set; }
    public int PreviewWeight { get; set; } = 400;
    public string PreviewStyle { get; set; } = "Normal";
    public string PreviewStretch { get; set; } = "Normal";
    public bool HasKoreanGlyphs { get; set; }

    public static CachedFontAsset FromAsset(string sourcePath, FontAsset asset)
    {
        return new CachedFontAsset
        {
            SourcePath = sourcePath,
            FilePath = asset.FilePath,
            OriginalFileName = asset.OriginalFileName,
            SourceLabel = asset.SourceLabel,
            Format = asset.Format,
            Family = asset.Family,
            Style = asset.Style,
            Version = asset.Version,
            Designer = asset.Designer,
            Foundry = asset.Foundry,
            License = asset.License,
            LanguageHint = asset.LanguageHint,
            Sha256 = asset.Sha256,
            FileSize = asset.FileSize,
            LastWriteUtcTicks = asset.LastWriteUtcTicks,
            PreviewWeight = asset.PreviewWeight,
            PreviewStyle = asset.PreviewStyle,
            PreviewStretch = asset.PreviewStretch,
            HasKoreanGlyphs = asset.HasKoreanGlyphs
        };
    }

    public FontAsset ToAsset()
    {
        var asset = new FontAsset
        {
            FilePath = FilePath,
            OriginalFileName = OriginalFileName,
            SourceLabel = SourceLabel,
            Format = Format,
            FileSize = FileSize,
            LastWriteUtcTicks = LastWriteUtcTicks,
            PreviewWeight = PreviewWeight,
            PreviewStyle = PreviewStyle,
            PreviewStretch = PreviewStretch,
            HasKoreanGlyphs = HasKoreanGlyphs
        };

        asset.Family = Family;
        asset.Style = Style;
        asset.Version = Version;
        asset.Designer = Designer;
        asset.Foundry = Foundry;
        asset.License = License;
        asset.LanguageHint = LanguageHint;
        asset.Sha256 = Sha256;
        asset.RefreshDisplayFields();
        return asset;
    }
}
