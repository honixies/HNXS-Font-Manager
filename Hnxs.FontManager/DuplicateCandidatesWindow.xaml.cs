using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using Hnxs.FontManager.Models;

namespace Hnxs.FontManager;

public partial class DuplicateCandidatesWindow : Window
{
    private readonly ObservableCollection<DuplicateCandidateRow> _rows;

    public DuplicateCandidatesWindow(IEnumerable<FontAsset> duplicateAssets)
    {
        InitializeComponent();
        _rows = new ObservableCollection<DuplicateCandidateRow>(
            duplicateAssets
                .OrderBy(asset => asset.Family)
                .ThenBy(asset => asset.Style)
                .ThenBy(asset => asset.Version)
                .ThenBy(asset => asset.OriginalFileName)
                .Select(asset => new DuplicateCandidateRow(asset)));

        CandidateGrid.ItemsSource = _rows;
        SummaryText.Text = $"중복 후보 {_rows.Count:N0}개가 발견되었습니다. 자동 선택은 각 그룹에서 하나를 남기고 나머지를 삭제 후보로 표시합니다.";
        AutoSelectCandidates();
    }

    public IReadOnlyList<FontAsset> SelectedAssets => _rows
        .Where(row => row.IsSelected)
        .Select(row => row.Asset)
        .ToArray();

    private void AutoSelect_Click(object sender, RoutedEventArgs e)
    {
        AutoSelectCandidates();
    }

    private void ClearSelection_Click(object sender, RoutedEventArgs e)
    {
        foreach (var row in _rows)
        {
            row.IsSelected = false;
        }
    }

    private void DeleteSelected_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedAssets.Count == 0)
        {
            MessageBox.Show(this, "삭제할 후보를 선택하세요.", "HNXS Font Manager", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = MessageBox.Show(
            this,
            $"선택한 {SelectedAssets.Count:N0}개 폰트 파일을 삭제할까요?",
            "중복 후보 삭제",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.OK)
        {
            return;
        }

        DialogResult = true;
    }

    private void AutoSelectCandidates()
    {
        foreach (var row in _rows)
        {
            row.IsSelected = false;
        }

        var groups = _rows.GroupBy(row => row.GroupKey, StringComparer.OrdinalIgnoreCase);
        foreach (var group in groups)
        {
            var keep = group
                .OrderByDescending(row => row.DuplicateState == "완전 중복" ? 0 : 1)
                .ThenByDescending(row => IsInstalled(row.InstalledState))
                .ThenByDescending(row => row.Version)
                .ThenBy(row => row.FileName)
                .First();

            foreach (var row in group.Where(row => row != keep))
            {
                row.IsSelected = true;
            }
        }
    }

    private static bool IsInstalled(string installedState)
    {
        return installedState != "미설치" && installedState != "확인 중";
    }
}

public sealed class DuplicateCandidateRow : INotifyPropertyChanged
{
    private bool _isSelected;

    public DuplicateCandidateRow(FontAsset asset)
    {
        Asset = asset;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public FontAsset Asset { get; }
    public string GroupKey => $"{Asset.Family}|{Asset.Style}";
    public string GroupName => Asset.DisplayName;
    public string FileName => Asset.OriginalFileName;
    public string Version => Asset.Version;
    public string DuplicateState => Asset.DuplicateState;
    public string InstalledState => Asset.InstalledState;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }
}
