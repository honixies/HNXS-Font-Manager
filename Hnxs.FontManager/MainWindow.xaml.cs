using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Markup;
using Hnxs.FontManager.Models;
using Hnxs.FontManager.Services;
using Microsoft.Win32;

namespace Hnxs.FontManager;

public partial class MainWindow : Window
{
    private const int HwndBroadcast = 0xffff;
    private const int WmFontchange = 0x001D;
    private const int FrPrivate = 0x10;
    private const int FrNotEnum = 0x20;
    private static readonly string DefaultPreviewText = string.Join(
        Environment.NewLine,
        "The quick brown fox jumps over the lazy dog.",
        "다람쥐 헌 쳇바퀴에 타고파. 퀭한 뙤약볕 속 쏭알쏭알.",
        "いろはにほへと  チリヌルヲ",
        "靑春禮贊  憂鬱  幸福",
        "!@#$%^&*()_+-=  12345  abc  ABC",
        "！＠＃＄％＾＆＊（）＿＋＝  １２３４５  ａｂｃ  ＡＢＣ",
        "※ ○ ● ◎ ◇ ◆ □ ■ △ ▲ ▽ ▼ → ← ↑ ↓ ↔ ↕ ♨ ♭ ♩ ♪ ♬");
    private readonly ObservableCollection<FontAsset> _fonts = [];
    private readonly FontScanner _scanner = new();
    private readonly AppStateStore _stateStore = new();
    private readonly ICollectionView _fontView;
    private CancellationTokenSource? _scanCancellation;
    private InstalledFontIndex? _installedIndex;
    private FontListFilter _activeFilter = FontListFilter.All;
    private string? _zipWorkingFolder;
    private string? _zipOriginalPath;

    public MainWindow()
    {
        InitializeComponent();
        _fontView = CollectionViewSource.GetDefaultView(_fonts);
        _fontView.Filter = FilterFont;
        FontGrid.ItemsSource = _fontView;
        SearchBox.Text = "";
        PreviewTextBox.Text = DefaultPreviewText;
        LargePreviewText.Text = PreviewTextBox.Text;
        SaveZipButton.Visibility = Visibility.Collapsed;
        var isAdministrator = IsRunningAsAdministrator();
        InstallModeComboBox.SelectedIndex = isAdministrator ? 1 : 0;
        RestartAsAdminButton.Visibility = isAdministrator ? Visibility.Collapsed : Visibility.Visible;
        Log("준비됨");
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await _stateStore.LoadAsync();
        var lastSource = _stateStore.State.LastSource;
        if (lastSource is null || string.IsNullOrWhiteSpace(lastSource.Path))
        {
            return;
        }

        try
        {
            if (lastSource.Kind == "folder" && Directory.Exists(lastSource.Path))
            {
                IncludeSubfoldersCheckBox.IsChecked = lastSource.IncludeSubfolders;
                Log($"마지막 폴더 자동 복원: {lastSource.Path}");
                await StartFolderScanAsync(lastSource.Path, lastSource.Path);
            }
            else if (lastSource.Kind == "zip" && File.Exists(lastSource.Path))
            {
                Log($"마지막 ZIP 자동 복원: {Path.GetFileName(lastSource.Path)}");
                await OpenZipAsync(lastSource.Path, rememberSource: false);
            }
        }
        catch (Exception ex)
        {
            Log($"마지막 소스 복원 실패: {ex.Message}");
        }
    }

    private async void ImportFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "폰트 폴더 선택"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        _zipOriginalPath = null;
        _zipWorkingFolder = null;
        SaveZipButton.Visibility = Visibility.Collapsed;
        await StartFolderScanAsync(dialog.FolderName, dialog.FolderName);
    }

    private async void ImportZip_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "폰트 ZIP 선택",
            Filter = "ZIP archives (*.zip)|*.zip"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        await OpenZipAsync(dialog.FileName);
    }

    private async Task OpenZipAsync(string zipPath, bool rememberSource = true)
    {
        await RunBusyAsync("ZIP 압축 해제 중", async token =>
        {
            _zipOriginalPath = zipPath;
            _zipWorkingFolder = await _scanner.ExtractZipAsync(zipPath, token);
            await Dispatcher.InvokeAsync(() =>
            {
                SaveZipButton.Visibility = Visibility.Visible;
                Log($"ZIP 임시 해제: {Path.GetFileName(zipPath)}");
            });
        });

        if (_zipWorkingFolder is not null)
        {
            if (rememberSource)
            {
                _stateStore.RememberZip(zipPath);
                await _stateStore.SaveAsync();
            }

            await StartFolderScanAsync(_zipWorkingFolder, Path.GetFileName(zipPath), rememberSource: false);
            SaveZipButton.Visibility = Visibility.Visible;
        }
    }

    private async Task StartFolderScanAsync(string folderPath, string label, bool rememberSource = true)
    {
        CancelCurrentWork();
        _scanCancellation = new CancellationTokenSource();
        var token = _scanCancellation.Token;

        _fonts.Clear();
        SetFilter(FontListFilter.All);
        CurrentSourceText.Text = label;
        SummaryText.Text = "폰트를 찾는 중입니다...";
        ScanProgress.IsIndeterminate = true;
        Log($"스캔 시작: {label}");

        try
        {
            _installedIndex ??= await _scanner.BuildInstalledIndexAsync(token);
            var count = 0;

            var cache = _stateStore.BuildCacheIndex(folderPath);
            var reused = 0;
            var changed = 0;

            await foreach (var asset in _scanner.ScanFolderIncrementalAsync(folderPath, IncludeSubfoldersCheckBox.IsChecked == true, cache, token))
            {
                asset.InstalledState = _scanner.GetInstalledState(asset, _installedIndex);
                asset.PropertyChanged += FontAsset_PropertyChanged;
                _fonts.Add(asset);
                count++;
                if (cache.TryGetValue(asset.FilePath, out var cached)
                    && cached.FileSize == asset.FileSize
                    && cached.LastWriteUtcTicks == asset.LastWriteUtcTicks)
                {
                    reused++;
                }
                else
                {
                    changed++;
                }

                UpdateSummary();
            }

            _scanner.MarkDuplicates(_fonts);
            UpdateSummary();
            SummaryText.Text = $"{count:N0}개의 폰트를 찾았습니다. 캐시 {reused:N0}개, 새로 읽음 {changed:N0}개";
            Log($"스캔 완료: {count:N0}개 / 캐시 {reused:N0} / 변경 {changed:N0}");

            if (rememberSource)
            {
                _stateStore.RememberFolder(folderPath, IncludeSubfoldersCheckBox.IsChecked == true, _fonts);
                await _stateStore.SaveAsync();
            }
        }
        catch (OperationCanceledException)
        {
            SummaryText.Text = "작업이 취소되었습니다.";
            Log("스캔 취소");
        }
        catch (Exception ex)
        {
            SummaryText.Text = "스캔 중 오류가 발생했습니다.";
            Log($"오류: {ex.Message}");
        }
        finally
        {
            ScanProgress.IsIndeterminate = false;
        }
    }

    private void DuplicateCheck_Click(object sender, RoutedEventArgs e)
    {
        _scanner.MarkDuplicates(_fonts);
        UpdateSummary();
        Log("중복 검사 완료");
    }

    private void DuplicateCandidates_Click(object sender, RoutedEventArgs e)
    {
        _scanner.MarkDuplicates(_fonts);
        UpdateSummary();

        var duplicates = _fonts.Where(IsDuplicateCandidate).ToArray();
        if (duplicates.Length == 0)
        {
            MessageBox.Show(this, "중복으로 의심되는 폰트가 없습니다.", "HNXS Font Manager", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new DuplicateCandidatesWindow(duplicates)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var deleted = DeleteDuplicateCandidates(dialog.SelectedAssets);
        _scanner.MarkDuplicates(_fonts);
        UpdateSummary();
        _fontView.Refresh();
        Log($"중복 후보 삭제 완료: {deleted:N0}개");
    }

    private int DeleteDuplicateCandidates(IEnumerable<FontAsset> selectedAssets)
    {
        var deleted = 0;
        foreach (var asset in selectedAssets.ToArray())
        {
            try
            {
                if (File.Exists(asset.FilePath))
                {
                    File.Delete(asset.FilePath);
                }

                _fonts.Remove(asset);
                deleted++;
            }
            catch (Exception ex)
            {
                Log($"삭제 실패: {asset.OriginalFileName} - {ex.Message}");
            }
        }

        return deleted;
    }

    private void RenameSelected_Click(object sender, RoutedEventArgs e)
    {
        var selected = _fonts.Where(font => font.IsSelected).ToArray();
        if (selected.Length == 0)
        {
            MessageBox.Show(this, "파일명을 변경할 폰트를 선택하세요.", "HNXS Font Manager", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var templateDialog = new RenameTemplateWindow
        {
            Owner = this
        };

        if (templateDialog.ShowDialog() != true)
        {
            return;
        }

        var names = FileNameTemplateService.BuildUniqueNames(selected, templateDialog.SelectedTemplate);
        var preview = string.Join(Environment.NewLine, names.Take(12).Select(pair => $"{pair.Key.OriginalFileName}  ->  {pair.Value}"));
        if (names.Count > 12)
        {
            preview += $"{Environment.NewLine}... 외 {names.Count - 12:N0}개";
        }

        var confirm = MessageBox.Show(
            this,
            preview,
            "파일명 변경 미리보기",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.OK)
        {
            return;
        }

        var changed = 0;
        foreach (var (asset, newName) in names)
        {
            var targetPath = Path.Combine(Path.GetDirectoryName(asset.FilePath) ?? "", newName);
            if (string.Equals(asset.FilePath, targetPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                File.Move(asset.FilePath, targetPath);
                asset.ProposedFileName = newName;
                changed++;
            }
            catch (Exception ex)
            {
                Log($"이름 변경 실패: {asset.OriginalFileName} - {ex.Message}");
            }
        }

        Log($"파일명 변경 완료: {changed:N0}개");
        MessageBox.Show(this, $"{changed:N0}개 파일명을 변경했습니다.", "HNXS Font Manager", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void InstallSelected_Click(object sender, RoutedEventArgs e)
    {
        var selected = _fonts.Where(font => font.IsSelected).ToArray();
        if (selected.Length == 0)
        {
            MessageBox.Show(this, "설치할 폰트를 선택하세요.", "HNXS Font Manager", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var systemWide = InstallModeComboBox.SelectedIndex == 1;
        if (systemWide && !IsRunningAsAdministrator())
        {
            MessageBox.Show(
                this,
                "시스템 전체 설치는 관리자 권한이 필요합니다.\n\n프로그램을 관리자 권한으로 다시 실행한 뒤 다시 시도하세요.",
                "관리자 권한 필요",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            Log("시스템 전체 설치 중단: 관리자 권한 필요");
            return;
        }

        var installRoot = systemWide
            ? Environment.GetFolderPath(Environment.SpecialFolder.Fonts)
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Fonts");
        Directory.CreateDirectory(installRoot);
        var installed = 0;

        foreach (var asset in selected)
        {
            try
            {
                var targetPath = Path.Combine(installRoot, asset.OriginalFileName);
                if (!File.Exists(targetPath))
                {
                    File.Copy(asset.FilePath, targetPath);
                }

                RegisterFont(asset, targetPath, systemWide);
                asset.InstalledState = "동일 파일 설치됨";
                installed++;
            }
            catch (Exception ex)
            {
                Log($"설치 실패: {asset.OriginalFileName} - {ex.Message}");
            }
        }

        var scope = systemWide ? "시스템 전체" : "현재 사용자";
        UpdateSummary();
        _fontView.Refresh();
        Log($"{scope} 폰트 설치 완료: {installed:N0}개");
        MessageBox.Show(this, $"{installed:N0}개 폰트를 {scope} 범위로 설치했습니다.", "HNXS Font Manager", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void RestartAsAdmin_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exePath))
            {
                MessageBox.Show(this, "실행 파일 경로를 찾을 수 없습니다.", "HNXS Font Manager", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
                Verb = "runas"
            });
            Close();
        }
        catch (Win32Exception)
        {
            Log("관리자 권한 재실행 취소");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"관리자 권한으로 다시 실행하지 못했습니다.\n\n{ex.Message}", "HNXS Font Manager", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void SaveZip_Click(object sender, RoutedEventArgs e)
    {
        if (_zipWorkingFolder is null || _zipOriginalPath is null)
        {
            MessageBox.Show(this, "ZIP에서 가져온 작업이 없습니다.", "HNXS Font Manager", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var saveDialog = new SaveFileDialog
        {
            Title = "ZIP 저장",
            Filter = "ZIP archives (*.zip)|*.zip",
            FileName = $"{Path.GetFileNameWithoutExtension(_zipOriginalPath)}_renamed.zip",
            InitialDirectory = Path.GetDirectoryName(_zipOriginalPath)
        };

        if (saveDialog.ShowDialog(this) != true)
        {
            return;
        }

        await RunBusyAsync("ZIP 저장 중", async token =>
        {
            await _scanner.RepackZipAsync(_zipWorkingFolder, saveDialog.FileName, token);
            await Dispatcher.InvokeAsync(() => Log($"ZIP 저장 완료: {Path.GetFileName(saveDialog.FileName)}"));
        });
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        CancelCurrentWork();
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        var visibleFonts = _fontView.Cast<FontAsset>().ToArray();
        var shouldSelect = visibleFonts.Any(font => !font.IsSelected);

        foreach (var font in visibleFonts)
        {
            font.IsSelected = shouldSelect;
        }

        UpdateSelectedCount();
        Log(shouldSelect ? $"표시 항목 전체 선택: {visibleFonts.Length:N0}개" : "표시 항목 선택 해제");
    }

    private void AllFontsFilter_Click(object sender, RoutedEventArgs e)
    {
        SetFilter(FontListFilter.All);
    }

    private void UninstalledFilter_Click(object sender, RoutedEventArgs e)
    {
        SetFilter(FontListFilter.Uninstalled);
    }

    private void InstalledFilter_Click(object sender, RoutedEventArgs e)
    {
        SetFilter(FontListFilter.Installed);
    }

    private void SetFilter(FontListFilter filter)
    {
        _activeFilter = filter;
        _fontView.Refresh();
        UpdateSummary();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _fontView.Refresh();
    }

    private bool FilterFont(object item)
    {
        if (item is not FontAsset asset)
        {
            return false;
        }

        var filterMatches = _activeFilter switch
        {
            FontListFilter.Installed => IsInstalled(asset),
            FontListFilter.Uninstalled => string.Equals(asset.InstalledState, "미설치", StringComparison.OrdinalIgnoreCase),
            _ => true
        };

        if (!filterMatches)
        {
            return false;
        }

        var query = SearchBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return asset.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
            || asset.OriginalFileName.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private void FontGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FontGrid.SelectedItem is not FontAsset asset)
        {
            return;
        }

        LargePreviewText.Text = PreviewTextBox.Text;
        LargePreviewText.FontFamily = ResolvePreviewFontFamily(asset);
        LargePreviewText.FontWeight = FontWeight.FromOpenTypeWeight(asset.PreviewWeight);
        LargePreviewText.FontStyle = ParseFontStyle(asset.PreviewStyle);
        LargePreviewText.FontStretch = ParseFontStretch(asset.PreviewStretch);
        PreviewFontNameText.Text = asset.HasKoreanGlyphs
            ? asset.DisplayName
            : $"{asset.DisplayName} · 한글 글리프 없음";
        VersionText.Text = asset.Version;
        FoundryText.Text = asset.Foundry;
        DesignerText.Text = asset.Designer;
        LanguageText.Text = asset.LanguageHint;
        LicenseText.Text = asset.License;
    }

    private void PreviewTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (LargePreviewText is not null)
        {
            LargePreviewText.Text = PreviewTextBox.Text;
        }
    }

    private async Task RunBusyAsync(string message, Func<CancellationToken, Task> work)
    {
        CancelCurrentWork();
        _scanCancellation = new CancellationTokenSource();
        ScanProgress.IsIndeterminate = true;
        SummaryText.Text = message;

        try
        {
            await work(_scanCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            Log("작업 취소");
        }
        catch (Exception ex)
        {
            Log($"오류: {ex.Message}");
        }
        finally
        {
            ScanProgress.IsIndeterminate = false;
        }
    }

    private void CancelCurrentWork()
    {
        if (_scanCancellation is { IsCancellationRequested: false })
        {
            _scanCancellation.Cancel();
        }
    }

    private void UpdateSummary()
    {
        ManagedCountText.Text = _fonts.Count.ToString("N0");
        TodayAddedText.Text = $"오늘 +{_fonts.Count:N0}";
        DuplicateNavButton.Content = $"◇  중복 후보 ({_fonts.Count(IsDuplicateCandidate):N0})";
        UpdateSelectedCount();

        var totalBytes = _fonts.Sum(font =>
        {
            try
            {
                return new FileInfo(font.FilePath).Length;
            }
            catch
            {
                return 0;
            }
        });

        TotalSizeText.Text = $"{totalBytes / 1024d / 1024d:N0} MB";
    }

    private void UpdateSelectedCount()
    {
        var selected = _fonts.Count(font => font.IsSelected);
        var visible = _fontView.Cast<FontAsset>().Count();
        SelectedCountText.Text = selected == 0
            ? $"선택 0개 / 표시 {visible:N0}개"
            : $"선택 {selected:N0}개 / 표시 {visible:N0}개";
    }

    private void FontAsset_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FontAsset.IsSelected))
        {
            UpdateSelectedCount();
        }
    }

    private void Log(string message)
    {
        LogList.Items.Insert(0, $"{DateTime.Now:HH:mm:ss}  {message}");
    }

    private static bool IsInstalled(FontAsset asset)
    {
        return !string.Equals(asset.InstalledState, "미설치", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(asset.InstalledState, "확인 중", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDuplicateCandidate(FontAsset asset)
    {
        return !string.IsNullOrWhiteSpace(asset.DuplicateState) && asset.DuplicateState != "-";
    }

    private static FontFamily ResolvePreviewFontFamily(FontAsset asset)
    {
        try
        {
            var fileUri = new Uri(asset.FilePath, UriKind.Absolute);
            var family = Fonts.GetFontFamilies(fileUri).FirstOrDefault(fontFamily =>
                fontFamily.FamilyNames.Values.Any(name => string.Equals(name, asset.Family, StringComparison.OrdinalIgnoreCase)))
                ?? Fonts.GetFontFamilies(fileUri).FirstOrDefault();
            if (family is not null)
            {
                return family;
            }

            var folder = Path.GetDirectoryName(asset.FilePath);
            if (!string.IsNullOrWhiteSpace(folder))
            {
                var folderUri = new Uri(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar, UriKind.Absolute);
                return new FontFamily(folderUri, $"./#{asset.Family}");
            }

            return new FontFamily(fileUri, $"./#{asset.Family}");
        }
        catch
        {
            return new FontFamily(asset.Family);
        }
    }

    private static FontStyle ParseFontStyle(string value)
    {
        return string.Equals(value, "Italic", StringComparison.OrdinalIgnoreCase)
            ? FontStyles.Italic
            : FontStyles.Normal;
    }

    private static FontStretch ParseFontStretch(string value)
    {
        return value switch
        {
            "UltraCondensed" => FontStretches.UltraCondensed,
            "ExtraCondensed" => FontStretches.ExtraCondensed,
            "Condensed" => FontStretches.Condensed,
            "SemiCondensed" => FontStretches.SemiCondensed,
            "SemiExpanded" => FontStretches.SemiExpanded,
            "Expanded" => FontStretches.Expanded,
            "ExtraExpanded" => FontStretches.ExtraExpanded,
            "UltraExpanded" => FontStretches.UltraExpanded,
            _ => FontStretches.Normal
        };
    }

    private static bool IsRunningAsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private void RegisterFont(FontAsset asset, string targetPath, bool systemWide)
    {
        var registryPath = systemWide
            ? @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts"
            : @"Software\Microsoft\Windows NT\CurrentVersion\Fonts";
        var hive = systemWide ? Registry.LocalMachine : Registry.CurrentUser;
        using var key = hive.OpenSubKey(registryPath, writable: true) ?? hive.CreateSubKey(registryPath, writable: true);
        var registryName = $"{asset.DisplayName} ({asset.Format})";
        var registryValue = systemWide ? Path.GetFileName(targetPath) : targetPath;

        key?.SetValue(registryName, registryValue);

        // Permanent registration is handled by the Fonts registry entry above.
        // AddFontResourceEx only makes the font visible in the current session;
        // SendNotifyMessage avoids the app hanging or exiting during broadcast.
        var added = AddFontResourceEx(targetPath, systemWide ? 0 : FrPrivate | FrNotEnum, IntPtr.Zero);
        if (added == 0)
        {
            Log($"폰트 세션 등록 실패: {asset.OriginalFileName}");
        }

        SendNotifyMessage(new IntPtr(HwndBroadcast), WmFontchange, IntPtr.Zero, IntPtr.Zero);
    }

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int AddFontResourceEx(string name, int fl, IntPtr res);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SendNotifyMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
}

public enum FontListFilter
{
    All,
    Installed,
    Uninstalled
}
