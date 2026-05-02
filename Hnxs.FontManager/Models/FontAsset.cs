using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Hnxs.FontManager.Models;

public sealed class FontAsset : INotifyPropertyChanged
{
    private bool _isSelected;
    private string _installedState = "확인 중";
    private string _duplicateState = "-";
    private string _proposedFileName = "";

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    public required string FilePath { get; init; }
    public required string OriginalFileName { get; init; }
    public required string SourceLabel { get; init; }
    public required string Format { get; init; }
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

    public string DisplayName => $"{Family} {Style}".Trim();

    public string InstalledState
    {
        get => _installedState;
        set => SetField(ref _installedState, value);
    }

    public string DuplicateState
    {
        get => _duplicateState;
        set => SetField(ref _duplicateState, value);
    }

    public string ProposedFileName
    {
        get => _proposedFileName;
        set => SetField(ref _proposedFileName, value);
    }

    public void RefreshDisplayFields()
    {
        OnPropertyChanged(nameof(DisplayName));
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
