using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Media;
using Hnxs.FontManager.Models;

namespace Hnxs.FontManager.Services;

public sealed class FontScanner
{
    private static readonly string[] SupportedExtensions = [".ttf", ".otf", ".ttc"];

    public async Task<InstalledFontIndex> BuildInstalledIndexAsync(CancellationToken cancellationToken)
    {
        var index = new InstalledFontIndex();
        var installedPaths = EnumerateInstalledFontFiles();

        await Parallel.ForEachAsync(installedPaths, cancellationToken, async (path, token) =>
        {
            var asset = await ReadFontAsync(path, "Windows Fonts", token);
            if (asset is null)
            {
                return;
            }

            index.Add(asset);
        });

        return index;
    }

    public async IAsyncEnumerable<FontAsset> ScanFolderAsync(
        string folderPath,
        bool includeSubfolders,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var asset in ScanFolderIncrementalAsync(folderPath, includeSubfolders, new Dictionary<string, CachedFontAsset>(), cancellationToken))
        {
            yield return asset;
        }
    }

    public async IAsyncEnumerable<FontAsset> ScanFolderIncrementalAsync(
        string folderPath,
        bool includeSubfolders,
        IReadOnlyDictionary<string, CachedFontAsset> cache,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var searchOption = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        foreach (var path in Directory.EnumerateFiles(folderPath, "*.*", searchOption))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsSupportedFont(path))
            {
                continue;
            }

            var fileInfo = new FileInfo(path);
            if (cache.TryGetValue(path, out var cached)
                && cached.FileSize == fileInfo.Length
                && cached.LastWriteUtcTicks == fileInfo.LastWriteTimeUtc.Ticks)
            {
                yield return cached.ToAsset();
                continue;
            }

            var asset = await ReadFontAsync(path, folderPath, cancellationToken);
            if (asset is not null)
            {
                yield return asset;
            }
        }
    }

    public async Task<string> ExtractZipAsync(string zipPath, CancellationToken cancellationToken)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "HNXS-Font-Manager", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            ZipFile.ExtractToDirectory(zipPath, tempRoot, overwriteFiles: true);
        }, cancellationToken);

        return tempRoot;
    }

    public Task RepackZipAsync(string workingFolder, string outputZipPath, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(outputZipPath))
            {
                File.Delete(outputZipPath);
            }

            ZipFile.CreateFromDirectory(workingFolder, outputZipPath, CompressionLevel.Optimal, includeBaseDirectory: false);
        }, cancellationToken);
    }

    public void MarkDuplicates(IEnumerable<FontAsset> assets)
    {
        foreach (var asset in assets)
        {
            asset.DuplicateState = "-";
        }

        var groups = assets
            .GroupBy(asset => $"{asset.Family}|{asset.Style}", StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1);

        foreach (var group in groups)
        {
            var hashGroups = group.GroupBy(asset => asset.Sha256, StringComparer.OrdinalIgnoreCase).ToArray();
            var versions = group.Select(asset => asset.Version).Distinct(StringComparer.OrdinalIgnoreCase).Count();

            foreach (var asset in group)
            {
                var exactCount = hashGroups.FirstOrDefault(hashGroup => hashGroup.Key == asset.Sha256)?.Count() ?? 0;
                asset.DuplicateState = exactCount > 1
                    ? "완전 중복"
                    : versions > 1
                        ? "버전 다름"
                        : "이름 중복";
            }
        }
    }

    public string GetInstalledState(FontAsset asset, InstalledFontIndex index)
    {
        if (index.Hashes.ContainsKey(asset.Sha256))
        {
            return "동일 파일 설치됨";
        }

        var key = InstalledFontIndex.BuildKey(asset.Family, asset.Style);
        if (index.ByFamilyStyle.TryGetValue(key, out var installed))
        {
            return installed.Any(item => string.Equals(item.Version, asset.Version, StringComparison.OrdinalIgnoreCase))
                ? "이름 일치"
                : "버전 다름";
        }

        var familyKey = asset.Family.Trim().ToUpperInvariant();
        return index.Families.ContainsKey(familyKey) ? "충돌 가능" : "미설치";
    }

    private static IEnumerable<string> EnumerateInstalledFontFiles()
    {
        var folders = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Fonts),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Fonts")
        };

        return folders
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*.*", SearchOption.TopDirectoryOnly))
            .Where(IsSupportedFont);
    }

    private static bool IsSupportedFont(string path)
    {
        return SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<FontAsset?> ReadFontAsync(string path, string sourceLabel, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var glyph = new GlyphTypeface(new Uri(path, UriKind.Absolute));
            var asset = new FontAsset
            {
                FilePath = path,
                OriginalFileName = Path.GetFileName(path),
                SourceLabel = sourceLabel,
                Format = Path.GetExtension(path).TrimStart('.').ToUpperInvariant(),
                FileSize = new FileInfo(path).Length,
                LastWriteUtcTicks = File.GetLastWriteTimeUtc(path).Ticks
            };

            asset.Family = FirstValue(glyph.FamilyNames, Path.GetFileNameWithoutExtension(path));
            asset.Style = FirstValue(glyph.FaceNames, "Regular");
            ApplyPreviewStyle(asset, glyph);
            asset.HasKoreanGlyphs = HasGlyphs(glyph, "다람쥐헌쳇바퀴퀭뙤약볕쏭알");
            asset.Version = FirstValue(glyph.VersionStrings, "-");
            asset.Designer = FirstValue(glyph.DesignerNames, "-");
            asset.Foundry = FirstValue(glyph.ManufacturerNames, "-");
            asset.License = FirstValue(glyph.LicenseDescriptions, "-");
            asset.LanguageHint = string.Join(", ", glyph.FamilyNames.Keys.Select(key => key.IetfLanguageTag).Distinct().Take(3));
            if (string.IsNullOrWhiteSpace(asset.LanguageHint))
            {
                asset.LanguageHint = "-";
            }

            asset.Sha256 = await ComputeSha256Async(path, cancellationToken);
            asset.RefreshDisplayFields();
            return asset;
        }
        catch
        {
            return null;
        }
    }

    private static string FirstValue(IDictionary<CultureInfo, string> values, string fallback)
    {
        if (values.TryGetValue(CultureInfo.GetCultureInfo("ko-kr"), out var korean) && !string.IsNullOrWhiteSpace(korean))
        {
            return korean.Trim();
        }

        if (values.TryGetValue(CultureInfo.GetCultureInfo("en-us"), out var english) && !string.IsNullOrWhiteSpace(english))
        {
            return english.Trim();
        }

        return values.Values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? fallback;
    }

    private static void ApplyPreviewStyle(FontAsset asset, GlyphTypeface glyph)
    {
        var style = asset.Style.ToLowerInvariant();

        var weight = glyph.Weight.ToOpenTypeWeight();
        if (weight <= 0 || weight == 400 && style.Contains("bold"))
        {
            weight =
                style.Contains("thin") || style.Contains("hairline") ? 100 :
                style.Contains("extra light") || style.Contains("extralight") || style.Contains("ultra light") || style.Contains("ultralight") ? 200 :
                style.Contains("light") ? 300 :
                style.Contains("demi bold") || style.Contains("demibold") || style.Contains("semi bold") || style.Contains("semibold") ? 600 :
                style.Contains("extra bold") || style.Contains("extrabold") || style.Contains("ultra bold") || style.Contains("ultrabold") ? 800 :
                style.Contains("black") || style.Contains("heavy") ? 900 :
                style.Contains("bold") ? 700 :
                style.Contains("medium") ? 500 :
                400;
        }

        asset.PreviewWeight = weight;
        asset.PreviewStyle = glyph.Style == FontStyles.Italic || glyph.Style == FontStyles.Oblique || style.Contains("italic") || style.Contains("oblique")
            ? "Italic"
            : "Normal";
        asset.PreviewStretch = ToStretchName(glyph.Stretch, style);
    }

    private static bool HasGlyphs(GlyphTypeface glyph, string text)
    {
        return text
            .Where(ch => !char.IsWhiteSpace(ch))
            .All(ch => glyph.CharacterToGlyphMap.ContainsKey(ch));
    }

    private static string ToStretchName(FontStretch stretch, string style)
    {
        if (stretch == FontStretches.UltraCondensed || style.Contains("ultra condensed") || style.Contains("ultracondensed"))
        {
            return "UltraCondensed";
        }

        if (stretch == FontStretches.ExtraCondensed || style.Contains("extra condensed") || style.Contains("extracondensed"))
        {
            return "ExtraCondensed";
        }

        if (stretch == FontStretches.Condensed || style.Contains("condensed") || style.Contains("narrow"))
        {
            return "Condensed";
        }

        if (stretch == FontStretches.SemiCondensed || style.Contains("semi condensed") || style.Contains("semicondensed"))
        {
            return "SemiCondensed";
        }

        if (stretch == FontStretches.SemiExpanded || style.Contains("semi expanded") || style.Contains("semiexpanded"))
        {
            return "SemiExpanded";
        }

        if (stretch == FontStretches.Expanded || style.Contains("expanded") || style.Contains("wide"))
        {
            return "Expanded";
        }

        if (stretch == FontStretches.ExtraExpanded || style.Contains("extra expanded") || style.Contains("extraexpanded"))
        {
            return "ExtraExpanded";
        }

        if (stretch == FontStretches.UltraExpanded || style.Contains("ultra expanded") || style.Contains("ultraexpanded"))
        {
            return "UltraExpanded";
        }

        return "Normal";
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }
}

public sealed class InstalledFontIndex
{
    public ConcurrentDictionary<string, List<FontAsset>> ByFamilyStyle { get; } = new();
    public ConcurrentDictionary<string, byte> Hashes { get; } = new(StringComparer.OrdinalIgnoreCase);
    public ConcurrentDictionary<string, byte> Families { get; } = new();

    public static string BuildKey(string family, string style)
    {
        return $"{family.Trim().ToUpperInvariant()}|{style.Trim().ToUpperInvariant()}";
    }

    public void Add(FontAsset asset)
    {
        var key = BuildKey(asset.Family, asset.Style);
        ByFamilyStyle.AddOrUpdate(
            key,
            _ => [asset],
            (_, existing) =>
            {
                lock (existing)
                {
                    existing.Add(asset);
                }

                return existing;
            });

        Families.TryAdd(asset.Family.Trim().ToUpperInvariant(), 0);
        if (!string.IsNullOrWhiteSpace(asset.Sha256))
        {
            Hashes.TryAdd(asset.Sha256, 0);
        }
    }
}
