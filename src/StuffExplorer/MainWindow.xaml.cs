using Microsoft.Win32;
using StuffCore;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace StuffExplorer;

public partial class MainWindow : Window
{
    private const int SearchDebounceMilliseconds = 450;

    private enum GroupMode
    {
        None,
        FileType,
        AssetCategory
    }

    private readonly Dictionary<string, ArchiveTreeNode> _treeNodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _navigationHistory = [];
    private IReadOnlyList<AssetEntryViewModel> _allEntries = Array.Empty<AssetEntryViewModel>();
    private IReadOnlyList<AssetEntryViewModel> _visibleEntries = Array.Empty<AssetEntryViewModel>();
    private CancellationTokenSource? _exportCancellation;
    private CancellationTokenSource? _searchCancellation;
    private StuffArchive? _archive;
    private Bw2ArchiveAnalysis? _analysis;
    private string _selectedFolder = string.Empty;
    private int _historyIndex = -1;
    private string _sortProperty = nameof(AssetEntryViewModel.Name);
    private ListSortDirection _sortDirection = ListSortDirection.Ascending;
    private GroupMode _groupMode = GroupMode.FileType;
    private bool _closeAfterCancellation;
    private bool _suppressTreeSelection;

    public MainWindow()
    {
        InitializeComponent();
        FileGrid.ItemsSource = _visibleEntries;
        ApplyViewSettings();
        UpdateViewMenuChecks();
        UpdateBreadcrumbs();
        UpdateCommandState();
    }

    private async void OpenArchive_Click(object sender, RoutedEventArgs e)
    {
        if (_exportCancellation is not null)
            return;

        var dialog = new OpenFileDialog
        {
            Filter = "Black & White 2 STUFF archive (*.stuff)|*.stuff|All files (*.*)|*.*",
            Title = S("DialogOpenTitle")
        };

        if (dialog.ShowDialog(this) == true)
            await LoadArchiveAsync(dialog.FileName);
    }

    private async Task LoadArchiveAsync(string path)
    {
        try
        {
            IsEnabled = false;
            StatusText.Text = S("StatusReading");
            var result = await Task.Run(() =>
            {
                var loadedArchive = StuffArchive.Open(path);
                var analysis = Bw2ArchiveAnalyzer.Analyze(loadedArchive);
                return (Archive: loadedArchive, Analysis: analysis);
            });
            var archive = result.Archive;
            _archive = archive;
            _analysis = result.Analysis;
            _allEntries = archive.Entries
                .Select(entry => new AssetEntryViewModel(entry, result.Analysis.GetClassification(entry)))
                .ToArray();
            Title = $"{Path.GetFileName(archive.FilePath)} — BW2StuffExplorer";
            _selectedFolder = string.Empty;
            BuildTree(archive);
            ResetNavigationHistory();
            UpdateBreadcrumbs();
            await ApplyFilterAsync();
            StatusText.Text = string.Format(S("StatusArchive"), archive.Entries.Count, FileSizeConverter.Format(archive.ContentLength));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or StuffArchiveException)
        {
            MessageBox.Show(this, exception.Message, S("ErrorOpenTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText.Text = S("StatusOpenFailed");
        }
        finally
        {
            IsEnabled = true;
            UpdateCommandState();
        }
    }

    private void BuildTree(StuffArchive archive)
    {
        var root = new ArchiveTreeNode(Path.GetFileName(archive.FilePath), string.Empty);
        _treeNodes.Clear();
        _treeNodes[string.Empty] = root;

        foreach (var entry in archive.Entries.OrderBy(item => item.Path, StringComparer.OrdinalIgnoreCase))
        {
            var currentPath = string.Empty;
            foreach (var part in entry.DirectoryPath.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                var parentPath = currentPath;
                currentPath = string.IsNullOrEmpty(currentPath) ? part : $"{currentPath}/{part}";
                if (_treeNodes.ContainsKey(currentPath))
                    continue;

                var parent = _treeNodes[parentPath];
                var node = new ArchiveTreeNode(part, currentPath, parent);
                parent.Children.Add(node);
                _treeNodes[currentPath] = node;
            }
        }

        root.IsExpanded = true;
        root.IsSelected = true;
        FolderTree.ItemsSource = new[] { root };
    }

    private async Task ApplyFilterAsync(bool debounce = false)
    {
        _searchCancellation?.Cancel();

        if (_archive is null)
        {
            _searchCancellation = null;
            _allEntries = Array.Empty<AssetEntryViewModel>();
            _visibleEntries = Array.Empty<AssetEntryViewModel>();
            FileGrid.ItemsSource = _visibleEntries;
            ResultCountText.Text = string.Empty;
            return;
        }

        var search = SearchBox.Text.Trim();
        if (search.Length == 1 && !char.IsLetterOrDigit(search[0]))
        {
            ResultCountText.Text = S("StatusSearchMoreCharacters");
            return;
        }

        var cancellation = new CancellationTokenSource();
        var cancellationToken = cancellation.Token;
        _searchCancellation = cancellation;
        var archive = _archive;
        var allEntries = _allEntries;
        var selectedFolder = _selectedFolder;

        var selectedPaths = FileGrid.SelectedItems.Cast<AssetEntryViewModel>()
            .Select(entry => entry.Path)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        try
        {
            if (debounce)
                await Task.Delay(SearchDebounceMilliseconds, cancellationToken);

            var filtered = await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return string.IsNullOrEmpty(search)
                    ? allEntries
                        .Where(entry => entry.DirectoryPath.Equals(selectedFolder, StringComparison.OrdinalIgnoreCase))
                        .ToArray()
                    : allEntries
                        .Where(entry => IsInFolder(entry, selectedFolder)
                            && entry.Path.Contains(search, StringComparison.OrdinalIgnoreCase))
                        .ToArray();
            }, cancellationToken);

            if (cancellation.IsCancellationRequested || archive != _archive || selectedFolder != _selectedFolder)
                return;

            _visibleEntries = filtered;
            FileGrid.ItemsSource = _visibleEntries;
            SetPathColumnVisibility(!string.IsNullOrEmpty(search));

            ApplyViewSettings();
            foreach (var entry in _visibleEntries.Where(entry => selectedPaths.Contains(entry.Path)))
                FileGrid.SelectedItems.Add(entry);

            ResetFileGridScrollPosition();
            ResultCountText.Text = string.Format(S("StatusShown"), _visibleEntries.Count);
            UpdateCommandState();
        }
        catch (OperationCanceledException)
        {
            // A newer search or navigation request superseded this one.
        }
        finally
        {
            if (ReferenceEquals(_searchCancellation, cancellation))
                _searchCancellation = null;
            cancellation.Dispose();
        }
    }

    private static bool IsInFolder(AssetEntryViewModel entry, string folder) => string.IsNullOrEmpty(folder)
        || entry.DirectoryPath.Equals(folder, StringComparison.OrdinalIgnoreCase)
        || entry.DirectoryPath.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase);

    private void SetPathColumnVisibility(bool visible)
    {
        var visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        if (PathColumn.Visibility == visibility)
            return;

        PathColumn.Visibility = visibility;
    }

    private void ResetFileGridScrollPosition() =>
        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(() =>
            {
                if (VisualTreeSearch.FindDescendant<ScrollViewer>(FileGrid) is not { } scrollViewer)
                    return;

                scrollViewer.ScrollToTop();
                scrollViewer.ScrollToLeftEnd();
            }));

    private void NavigateTo(string folderPath, bool addToHistory, bool selectTreeNode = true)
    {
        if (_archive is null || !_treeNodes.ContainsKey(folderPath))
            return;

        if (addToHistory && !string.Equals(_selectedFolder, folderPath, StringComparison.OrdinalIgnoreCase))
        {
            if (_historyIndex + 1 < _navigationHistory.Count)
                _navigationHistory.RemoveRange(_historyIndex + 1, _navigationHistory.Count - _historyIndex - 1);

            _navigationHistory.Add(folderPath);
            _historyIndex = _navigationHistory.Count - 1;
        }

        _selectedFolder = folderPath;
        if (selectTreeNode)
            SelectTreeNode(folderPath);

        UpdateBreadcrumbs();
        _ = ApplyFilterAsync();
    }

    private void SelectTreeNode(string folderPath)
    {
        if (!_treeNodes.TryGetValue(folderPath, out var node))
            return;

        _suppressTreeSelection = true;
        try
        {
            for (var parent = node.Parent; parent is not null; parent = parent.Parent)
                parent.IsExpanded = true;
            node.IsSelected = true;
        }
        finally
        {
            _suppressTreeSelection = false;
        }
    }

    private void ResetNavigationHistory()
    {
        _navigationHistory.Clear();
        _navigationHistory.Add(string.Empty);
        _historyIndex = 0;
    }

    private void UpdateBreadcrumbs()
    {
        BreadcrumbPanel.Children.Clear();
        SearchHintText.Text = _archive is null
            ? S("SearchHint")
            : $"Search {(string.IsNullOrEmpty(_selectedFolder) ? Path.GetFileName(_archive.FilePath) : _selectedFolder.Split('/')[^1])}";
        if (_archive is null)
        {
            BreadcrumbPanel.Children.Add(new TextBlock
            {
                Text = S("NoArchiveBreadcrumb"),
                Foreground = System.Windows.Media.Brushes.Gray,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(3, 0, 0, 0)
            });
            return;
        }

        AddBreadcrumb(Path.GetFileName(_archive.FilePath), string.Empty);
        var currentPath = string.Empty;
        foreach (var part in _selectedFolder.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            BreadcrumbPanel.Children.Add(new System.Windows.Shapes.Path
            {
                Data = System.Windows.Media.Geometry.Parse("M 1,1 L 6,6 L 1,11"),
                Stroke = new SolidColorBrush(Color.FromRgb(110, 120, 130)),
                StrokeThickness = 1.5,
                StrokeStartLineCap = System.Windows.Media.PenLineCap.Round,
                StrokeEndLineCap = System.Windows.Media.PenLineCap.Round,
                StrokeLineJoin = System.Windows.Media.PenLineJoin.Round,
                Width = 8,
                Height = 8,
                Stretch = System.Windows.Media.Stretch.Uniform,
                Margin = new Thickness(3, 0, 3, 0),
                VerticalAlignment = VerticalAlignment.Center,
                SnapsToDevicePixels = true
            });
            currentPath = string.IsNullOrEmpty(currentPath) ? part : $"{currentPath}/{part}";
            AddBreadcrumb(part, currentPath);
        }
    }

    private void AddBreadcrumb(string text, string path)
    {
        var button = new Button
        {
            Content = text,
            Tag = path,
            Style = (Style)FindResource("BreadcrumbButtonStyle")
        };

        button.Click += Breadcrumb_Click;
        BreadcrumbPanel.Children.Add(button);
    }

    private void Breadcrumb_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string path })
            NavigateTo(path, addToHistory: true);
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_historyIndex <= 0)
            return;

        _historyIndex--;
        NavigateTo(_navigationHistory[_historyIndex], addToHistory: false);
    }

    private void Forward_Click(object sender, RoutedEventArgs e)
    {
        if (_historyIndex + 1 >= _navigationHistory.Count)
            return;

        _historyIndex++;
        NavigateTo(_navigationHistory[_historyIndex], addToHistory: false);
    }

    private void Up_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedFolder))
            return;

        var separator = _selectedFolder.LastIndexOf('/');
        NavigateTo(separator < 0 ? string.Empty : _selectedFolder[..separator], addToHistory: true);
    }

    private void ApplyViewSettings()
    {
        var view = CollectionViewSource.GetDefaultView(FileGrid.ItemsSource);
        if (view is null)
            return;
        using (view.DeferRefresh())
        {
            view.SortDescriptions.Clear();
            view.GroupDescriptions.Clear();

            var groupProperty = _groupMode switch
            {
                GroupMode.FileType => nameof(AssetEntryViewModel.TypeDisplay),
                GroupMode.AssetCategory => nameof(AssetEntryViewModel.CategoryDisplay),
                _ => null
            };

            if (groupProperty is not null)
            {
                view.GroupDescriptions.Add(new PropertyGroupDescription(groupProperty));
                if (_sortProperty != groupProperty)
                    view.SortDescriptions.Add(new SortDescription(groupProperty, ListSortDirection.Ascending));
            }

            view.SortDescriptions.Add(new SortDescription(_sortProperty, _sortDirection));
            if (_sortProperty != nameof(AssetEntryViewModel.Name))
                view.SortDescriptions.Add(new SortDescription(nameof(AssetEntryViewModel.Name), ListSortDirection.Ascending));
        }

        UpdateViewMenuChecks();
        UpdateColumnSortIndicators();
    }

    private void SetSort(string property)
    {
        _sortProperty = property;
        ApplyViewSettings();
    }

    private void SetSortDirection(ListSortDirection direction)
    {
        _sortDirection = direction;
        ApplyViewSettings();
    }

    private void SetGrouping(GroupMode groupMode)
    {
        _groupMode = groupMode;
        ApplyViewSettings();
    }

    private void UpdateViewMenuChecks()
    {
        if (!IsInitialized)
            return;

        SortByNameMenuItem.IsChecked = _sortProperty == nameof(AssetEntryViewModel.Name);
        SortByModifiedMenuItem.IsChecked = _sortProperty == nameof(AssetEntryViewModel.ModifiedTimestamp);
        SortByTypeMenuItem.IsChecked = _sortProperty == nameof(AssetEntryViewModel.TypeDisplay);
        SortBySizeMenuItem.IsChecked = _sortProperty == nameof(AssetEntryViewModel.Length);
        SortByOffsetMenuItem.IsChecked = _sortProperty == nameof(AssetEntryViewModel.Offset);
        SortAscendingMenuItem.IsChecked = _sortDirection == ListSortDirection.Ascending;
        SortDescendingMenuItem.IsChecked = _sortDirection == ListSortDirection.Descending;
        GroupByNoneMenuItem.IsChecked = _groupMode == GroupMode.None;
        GroupByTypeMenuItem.IsChecked = _groupMode == GroupMode.FileType;
        GroupByAssetCategoryMenuItem.IsChecked = _groupMode == GroupMode.AssetCategory;
    }

    private void UpdateColumnSortIndicators()
    {
        if (!IsInitialized)
            return;

        foreach (var column in FileGrid.Columns)
            column.SortDirection = column.SortMemberPath == _sortProperty ? _sortDirection : null;
    }

    private void FileGrid_Sorting(object sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;
        var property = e.Column.SortMemberPath;
        if (string.IsNullOrEmpty(property))
            return;

        if (_sortProperty == property)
            _sortDirection = _sortDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;
        else
        {
            _sortProperty = property;
            _sortDirection = ListSortDirection.Ascending;
        }

        ApplyViewSettings();
    }

    private void SortByName_Click(object sender, RoutedEventArgs e) => SetSort(nameof(AssetEntryViewModel.Name));
    private void SortByModified_Click(object sender, RoutedEventArgs e) => SetSort(nameof(AssetEntryViewModel.ModifiedTimestamp));
    private void SortByType_Click(object sender, RoutedEventArgs e) => SetSort(nameof(AssetEntryViewModel.TypeDisplay));
    private void SortBySize_Click(object sender, RoutedEventArgs e) => SetSort(nameof(AssetEntryViewModel.Length));
    private void SortByOffset_Click(object sender, RoutedEventArgs e) => SetSort(nameof(AssetEntryViewModel.Offset));
    private void SortAscending_Click(object sender, RoutedEventArgs e) => SetSortDirection(ListSortDirection.Ascending);
    private void SortDescending_Click(object sender, RoutedEventArgs e) => SetSortDirection(ListSortDirection.Descending);
    private void GroupByNone_Click(object sender, RoutedEventArgs e) => SetGrouping(GroupMode.None);
    private void GroupByType_Click(object sender, RoutedEventArgs e) => SetGrouping(GroupMode.FileType);
    private void GroupByAssetCategory_Click(object sender, RoutedEventArgs e) => SetGrouping(GroupMode.AssetCategory);

    private void FolderTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (_suppressTreeSelection || e.NewValue is not ArchiveTreeNode node)
            return;

        NavigateTo(node.FullPath, addToHistory: true, selectTreeNode: false);
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        SearchHintText.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        SearchIcon.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        ClearSearchButton.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Collapsed : Visibility.Visible;
        _ = ApplyFilterAsync(debounce: !string.IsNullOrWhiteSpace(SearchBox.Text));
    }

    private void FileGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateCommandState();

    private void UpdateCommandState()
    {
        if (!IsInitialized)
            return;

        var isExporting = _exportCancellation is not null;
        var hasArchive = _archive is not null && !isExporting;
        var hasSelection = FileGrid.SelectedItems.Count > 0 && !isExporting;
        ExportSelectionMenuItem.IsEnabled = hasSelection;
        CopyFullPathMenuItem.IsEnabled = hasSelection;
        CopyFileNameMenuItem.IsEnabled = hasSelection;
        ExportFolderMenuItem.IsEnabled = hasArchive;
        ExtractAllMenuItem.IsEnabled = hasArchive;
        BackButton.IsEnabled = hasArchive && _historyIndex > 0;
        ForwardButton.IsEnabled = hasArchive && _historyIndex + 1 < _navigationHistory.Count;
        UpButton.IsEnabled = hasArchive && !string.IsNullOrEmpty(_selectedFolder);
        RefreshButton.IsEnabled = hasArchive;
    }

    private async void ExportSelected_Click(object sender, RoutedEventArgs e)
    {
        if (_archive is null || FileGrid.SelectedItems.Count == 0)
            return;

        var entries = FileGrid.SelectedItems
            .Cast<AssetEntryViewModel>()
            .Select(item => item.Entry)
            .ToArray();
        if (entries.Length == 1)
            await ExportSingleEntryAsync(entries[0]);
        else
            await ExportEntriesAsync(entries);
    }

    private async Task ExportSingleEntryAsync(StuffEntry entry)
    {
        if (_archive is null)
            return;

        var dialog = new SaveFileDialog { FileName = entry.Name, Title = S("DialogExportFileTitle"), OverwritePrompt = true };
        if (dialog.ShowDialog(this) != true)
            return;

        await RunExportAsync(
            [entry],
            (archive, progress, cancellationToken) =>
            {
                archive.ExtractEntry(entry, dialog.FileName, overwrite: true, progress, cancellationToken);
                return 1;
            },
            count => string.Format(S("StatusExportedFile"), entry.Path));
    }

    private async void ExportFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_archive is null)
            return;

        await ExportEntriesAsync(_allEntries
            .Where(IsInSelectedFolder)
            .Select(item => item.Entry)
            .ToArray());
    }

    private async void ExportAll_Click(object sender, RoutedEventArgs e)
    {
        if (_archive is not null)
            await ExportEntriesAsync(_archive.Entries);
    }

    private bool IsInSelectedFolder(AssetEntryViewModel entry) => IsInFolder(entry, _selectedFolder);

    private async Task ExportEntriesAsync(IReadOnlyCollection<StuffEntry> entries)
    {
        if (_archive is null || entries.Count == 0)
            return;

        var dialog = new OpenFolderDialog { Title = S("DialogDestinationTitle"), Multiselect = false };
        if (dialog.ShowDialog(this) != true)
            return;

        var archive = _archive;
        try
        {
            var existingCount = await Task.Run(() => archive.CountExistingDestinations(entries, dialog.FolderName));
            if (existingCount > 0 && MessageBox.Show(
                    this,
                    string.Format(S("ConfirmOverwrite"), existingCount),
                    S("ConfirmOverwriteTitle"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.No) != MessageBoxResult.Yes)
                return;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or StuffArchiveException)
        {
            ShowExportError(exception);
            return;
        }

        await RunExportAsync(
            entries,
            (currentArchive, progress, cancellationToken) =>
                currentArchive.ExtractEntries(entries, dialog.FolderName, overwrite: true, progress, cancellationToken),
            count => string.Format(S("StatusExportedFiles"), count, dialog.FolderName));
    }

    private async Task RunExportAsync(
        IReadOnlyCollection<StuffEntry> entries,
        Func<StuffArchive, IProgress<StuffExportProgress>, CancellationToken, int> operation,
        Func<int, string> successStatus)
    {
        if (_archive is null || entries.Count == 0 || _exportCancellation is not null)
            return;

        var archive = _archive;
        var cancellation = new CancellationTokenSource();
        _exportCancellation = cancellation;
        var latestProgress = new StuffExportProgress(0, entries.Count, 0, entries.Sum(entry => (long)entry.Length), string.Empty);
        var progress = new Progress<StuffExportProgress>(value =>
        {
            latestProgress = value;
            ExportProgressBar.Value = value.Percentage;
            ExportProgressText.ToolTip = value.CurrentPath;
            ExportProgressText.Text = string.Format(
                S("StatusExporting"),
                value.CompletedEntries,
                value.TotalEntries,
                (int)value.Percentage);
        });

        SetExportBusy(true);
        ExportProgressText.Text = string.Format(S("StatusExporting"), 0, entries.Count, 0);
        try
        {
            var count = await Task.Run(() => operation(archive, progress, cancellation.Token), cancellation.Token);
            StatusText.Text = successStatus(count);
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = string.Format(
                S("StatusExportCancelled"),
                latestProgress.CompletedEntries,
                latestProgress.TotalEntries);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or StuffArchiveException)
        {
            ShowExportError(exception);
        }
        finally
        {
            if (ReferenceEquals(_exportCancellation, cancellation))
                _exportCancellation = null;
            cancellation.Dispose();
            SetExportBusy(false);

            if (_closeAfterCancellation)
            {
                _closeAfterCancellation = false;
                Close();
            }
        }
    }

    private void SetExportBusy(bool busy)
    {
        MainMenu.IsEnabled = !busy;
        NavigationBar.IsEnabled = !busy;
        WorkspaceGrid.IsEnabled = !busy;
        ExportProgressItem.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        ExportProgressSeparator.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        ExportProgressBar.Value = 0;
        ExportProgressText.ToolTip = null;
        ExportProgressText.Text = busy ? string.Format(S("StatusExporting"), 0, 0, 0) : string.Empty;
        CancelExportButton.IsEnabled = busy;
        UpdateCommandState();
    }

    private void CancelExport_Click(object sender, RoutedEventArgs e)
    {
        if (_exportCancellation is null)
            return;

        CancelExportButton.IsEnabled = false;
        ExportProgressText.Text = S("StatusCancelling");
        _exportCancellation.Cancel();
    }

    private void ShowExportError(Exception exception)
    {
        StatusText.Text = S("StatusExportFailed");
        MessageBox.Show(this, exception.Message, S("ErrorExportTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private AssetEntryViewModel? SelectedEntry => FileGrid.SelectedItem as AssetEntryViewModel;

    private void CopyFullPath_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedEntry is { } entry) Clipboard.SetText(entry.Path);
    }

    private void CopyFileName_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedEntry is { } entry) Clipboard.SetText(entry.Name);
    }

    private void CopyFolderPath_Click(object sender, RoutedEventArgs e) => Clipboard.SetText(_selectedFolder);

    private void Properties_Click(object sender, RoutedEventArgs e)
    {
        if (_archive is not null && _analysis is not null && SelectedEntry is { } entry)
            new EntryPropertiesWindow(entry, _archive, _analysis) { Owner = this }.ShowDialog();
    }

    private void FileGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e) => Properties_Click(sender, e);

    private void About_Click(object sender, RoutedEventArgs e) =>
        MessageBox.Show(this, S("AboutText"), S("AboutTitle"), MessageBoxButton.OK, MessageBoxImage.Information);

    private void FocusSearch_Click(object sender, RoutedEventArgs e) { SearchBox.Focus(); SearchBox.SelectAll(); }
    private void ClearSearch_Click(object sender, RoutedEventArgs e) => SearchBox.Clear();
    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        UpdateBreadcrumbs();
        _ = ApplyFilterAsync();
    }
    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_exportCancellation is not null)
        {
            if (e.Key == Key.Escape)
                CancelExport_Click(sender, e);
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.O) { OpenArchive_Click(sender, e); e.Handled = true; }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.F) { FocusSearch_Click(sender, e); e.Handled = true; }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.E) { ExportSelected_Click(sender, e); e.Handled = true; }
        else if (e.Key == Key.F5) { Refresh_Click(sender, e); e.Handled = true; }
        else if (e.Key == Key.Escape && !string.IsNullOrEmpty(SearchBox.Text)) { SearchBox.Clear(); e.Handled = true; }
        else if (Keyboard.Modifiers == ModifierKeys.None
            && e.Key == Key.Enter
            && FileGrid.IsKeyboardFocusWithin
            && SelectedEntry is not null)
        {
            Properties_Click(sender, e);
            e.Handled = true;
        }
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = _exportCancellation is null && HasSingleStuffFile(e.Data) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (_exportCancellation is null
            && e.Data.GetData(DataFormats.FileDrop) is string[] { Length: 1 } files
            && string.Equals(Path.GetExtension(files[0]), ".stuff", StringComparison.OrdinalIgnoreCase))
            await LoadArchiveAsync(files[0]);
    }

    private static bool HasSingleStuffFile(IDataObject data) =>
        data.GetData(DataFormats.FileDrop) is string[] { Length: 1 } files
        && string.Equals(Path.GetExtension(files[0]), ".stuff", StringComparison.OrdinalIgnoreCase);

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_exportCancellation is null)
            return;

        e.Cancel = true;
        _closeAfterCancellation = true;
        CancelExport_Click(this, new RoutedEventArgs());
    }

    internal static string S(string key) => Application.Current.FindResource(key)?.ToString() ?? key;
}
