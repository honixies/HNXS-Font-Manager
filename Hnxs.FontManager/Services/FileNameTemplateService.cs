using System.IO;
using System.Text;
using Hnxs.FontManager.Models;

namespace Hnxs.FontManager.Services;

public static class FileNameTemplateService
{
    public static readonly string[] Templates =
    [
        "{family} {style}",
        "{family} - {style}",
        "{family} v{version}",
        "{family} {style} v{version}",
        "{designer} - {family} {style}",
        "{foundry} - {family} {style}",
        "{family}_{style}",
        "{family}-{style}"
    ];

    public static string BuildFileName(FontAsset asset, string template)
    {
        var stem = template
            .Replace("{family}", asset.Family, StringComparison.OrdinalIgnoreCase)
            .Replace("{style}", asset.Style, StringComparison.OrdinalIgnoreCase)
            .Replace("{version}", asset.Version == "-" ? "" : asset.Version, StringComparison.OrdinalIgnoreCase)
            .Replace("{designer}", asset.Designer == "-" ? "" : asset.Designer, StringComparison.OrdinalIgnoreCase)
            .Replace("{foundry}", asset.Foundry == "-" ? "" : asset.Foundry, StringComparison.OrdinalIgnoreCase);

        stem = SanitizeFileName(stem);
        if (string.IsNullOrWhiteSpace(stem))
        {
            stem = Path.GetFileNameWithoutExtension(asset.OriginalFileName);
        }

        return $"{stem}{Path.GetExtension(asset.OriginalFileName).ToLowerInvariant()}";
    }

    public static IReadOnlyDictionary<FontAsset, string> BuildUniqueNames(IEnumerable<FontAsset> assets, string template)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var result = new Dictionary<FontAsset, string>();

        foreach (var asset in assets)
        {
            var fileName = BuildFileName(asset, template);
            var stem = Path.GetFileNameWithoutExtension(fileName);
            var ext = Path.GetExtension(fileName);
            var candidate = fileName;

            if (counts.TryGetValue(candidate, out var count))
            {
                count++;
                counts[fileName] = count;
                candidate = $"{stem} ({count}){ext}";
            }
            else
            {
                counts[fileName] = 1;
            }

            while (counts.ContainsKey(candidate) && !string.Equals(candidate, fileName, StringComparison.OrdinalIgnoreCase))
            {
                var next = counts[fileName] + 1;
                counts[fileName] = next;
                candidate = $"{stem} ({next}){ext}";
            }

            counts[candidate] = 1;
            result[asset] = candidate;
        }

        return result;
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        var previousWasSpace = false;

        foreach (var ch in value)
        {
            var next = invalid.Contains(ch) || char.IsControl(ch) ? ' ' : ch;
            if (char.IsWhiteSpace(next))
            {
                if (!previousWasSpace)
                {
                    builder.Append(' ');
                }

                previousWasSpace = true;
                continue;
            }

            builder.Append(next);
            previousWasSpace = false;
        }

        return builder.ToString().Trim().Trim('.', '-');
    }
}
