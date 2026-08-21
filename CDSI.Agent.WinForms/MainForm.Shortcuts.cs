namespace CDSI.Agent.WinForms;

public sealed partial class MainForm
{
    internal enum MainShortcutCommand
    {
        None,
        CancelCurrentTask,
        FocusAssetFilter,
        CreateProject,
        SelectAllAssets,
        ShowAssetDetails,
        ShowContextMenu,
        PreviousTab,
        NextTab,
        LocateAsset,
        DeleteSelection
    }

    private async void MainForm_KeyDown(object? sender, KeyEventArgs e)
    {
        var command = ResolveMainShortcut(e.KeyData);
        if (!CanExecuteMainShortcut(command))
        {
            return;
        }

        e.Handled = true;
        e.SuppressKeyPress = true;
        try
        {
            await ExecuteMainShortcutAsync(command);
        }
        catch (Exception exception)
        {
            ShowError("无法执行快捷键", exception);
        }
    }

    internal static MainShortcutCommand ResolveMainShortcut(Keys keyData)
    {
        return keyData switch
        {
            Keys.Escape => MainShortcutCommand.CancelCurrentTask,
            Keys.Control | Keys.F => MainShortcutCommand.FocusAssetFilter,
            Keys.Control | Keys.N => MainShortcutCommand.CreateProject,
            Keys.Control | Keys.A => MainShortcutCommand.SelectAllAssets,
            Keys.Alt | Keys.Enter => MainShortcutCommand.ShowAssetDetails,
            Keys.Shift | Keys.F10 => MainShortcutCommand.ShowContextMenu,
            Keys.Control | Keys.Shift | Keys.Tab => MainShortcutCommand.PreviousTab,
            Keys.Control | Keys.Tab => MainShortcutCommand.NextTab,
            Keys.Enter => MainShortcutCommand.LocateAsset,
            Keys.Delete => MainShortcutCommand.DeleteSelection,
            _ => MainShortcutCommand.None
        };
    }

    internal static int GetAdjacentTabIndex(
        int currentIndex,
        int tabCount,
        bool previous)
    {
        if (tabCount <= 0)
        {
            return -1;
        }

        var normalizedIndex = currentIndex >= 0 && currentIndex < tabCount
            ? currentIndex
            : 0;
        return previous
            ? (normalizedIndex + tabCount - 1) % tabCount
            : (normalizedIndex + 1) % tabCount;
    }

    internal static bool SelectAllGridRows(DataGridView grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        if (grid.Rows.Count == 0)
        {
            return false;
        }

        grid.SelectAll();
        return true;
    }

    private bool CanExecuteMainShortcut(MainShortcutCommand command)
    {
        return command switch
        {
            MainShortcutCommand.CancelCurrentTask => _canCancelCurrentTask,
            MainShortcutCommand.FocusAssetFilter => !_isBusy,
            MainShortcutCommand.CreateProject => !_isBusy,
            MainShortcutCommand.SelectAllAssets =>
                !_isBusy &&
                ReferenceEquals(_mainTabControl.SelectedTab, _assetsTabPage) &&
                _assetGrid.ContainsFocus &&
                _assetGrid.Rows.Count > 0,
            MainShortcutCommand.ShowAssetDetails =>
                !_isBusy && HasFocusedAsset(),
            MainShortcutCommand.ShowContextMenu =>
                !_isBusy && TryGetFocusedContextMenu(out _, out _),
            MainShortcutCommand.PreviousTab or MainShortcutCommand.NextTab =>
                _mainTabControl.TabPages.Count > 1,
            MainShortcutCommand.LocateAsset =>
                !_isBusy && HasFocusedAsset(),
            MainShortcutCommand.DeleteSelection =>
                !_isBusy &&
                (HasFocusedAsset() || HasFocusedProject()),
            _ => false
        };
    }

    private async Task ExecuteMainShortcutAsync(MainShortcutCommand command)
    {
        switch (command)
        {
            case MainShortcutCommand.CancelCurrentTask:
                _scanCancellation?.Cancel();
                break;
            case MainShortcutCommand.FocusAssetFilter:
                FocusAssetFilter();
                break;
            case MainShortcutCommand.CreateProject:
                _mainTabControl.SelectedTab = _collectionsTabPage;
                await CreateCollectionAsync();
                break;
            case MainShortcutCommand.SelectAllAssets:
                SelectAllGridRows(_assetGrid);
                break;
            case MainShortcutCommand.ShowAssetDetails:
                ShowCurrentAssetDetails();
                break;
            case MainShortcutCommand.ShowContextMenu:
                ShowFocusedContextMenu();
                break;
            case MainShortcutCommand.PreviousTab:
                SelectAdjacentMainTab(previous: true);
                break;
            case MainShortcutCommand.NextTab:
                SelectAdjacentMainTab(previous: false);
                break;
            case MainShortcutCommand.LocateAsset:
                OpenCurrentAssetFileLocation();
                break;
            case MainShortcutCommand.DeleteSelection:
                if (HasFocusedAsset())
                {
                    await HideSelectedAssetsFromListAsync();
                }
                else if (HasFocusedProject())
                {
                    await DeleteSelectedProjectAsync();
                }

                break;
        }
    }

    private void FocusAssetFilter()
    {
        _mainTabControl.SelectedTab = _assetsTabPage;
        _assetFileTypeFilterComboBox.Focus();
    }

    private bool HasFocusedAsset()
    {
        return ReferenceEquals(_mainTabControl.SelectedTab, _assetsTabPage) &&
            _assetGrid.ContainsFocus &&
            _assetGrid.CurrentRow?.Tag is Core.Assets.AssetListItem;
    }

    private bool HasFocusedProject()
    {
        return ReferenceEquals(_mainTabControl.SelectedTab, _collectionsTabPage) &&
            _collectionGrid.ContainsFocus &&
            GetSelectedCollection() is not null;
    }

    private void ShowCurrentAssetDetails()
    {
        if (_assetGrid.CurrentRow?.Tag is not Core.Assets.AssetListItem asset)
        {
            return;
        }

        using var details = new AssetDetailsForm(asset);
        details.ShowDialog(this);
    }

    private void SelectAdjacentMainTab(bool previous)
    {
        var index = GetAdjacentTabIndex(
            _mainTabControl.SelectedIndex,
            _mainTabControl.TabPages.Count,
            previous);
        if (index >= 0)
        {
            _mainTabControl.SelectedIndex = index;
        }
    }

    private bool TryGetFocusedContextMenu(
        out DataGridView grid,
        out ContextMenuStrip contextMenu)
    {
        if (_assetGrid.ContainsFocus)
        {
            grid = _assetGrid;
            contextMenu = _assetContextMenu;
        }
        else if (_assetDirectoryGrid.ContainsFocus)
        {
            grid = _assetDirectoryGrid;
            contextMenu = _assetDirectoryContextMenu;
        }
        else if (_duplicateGrid.ContainsFocus)
        {
            grid = _duplicateGrid;
            contextMenu = _duplicateContextMenu;
        }
        else if (_collectionGrid.ContainsFocus)
        {
            grid = _collectionGrid;
            contextMenu = _projectContextMenu;
        }
        else
        {
            grid = null!;
            contextMenu = null!;
            return false;
        }

        return grid.CurrentCell is not null && grid.CurrentRow is not null;
    }

    private void ShowFocusedContextMenu()
    {
        if (!TryGetFocusedContextMenu(out var grid, out var contextMenu) ||
            grid.CurrentCell is null)
        {
            return;
        }

        var cellBounds = grid.GetCellDisplayRectangle(
            grid.CurrentCell.ColumnIndex,
            grid.CurrentCell.RowIndex,
            cutOverflow: true);
        var location = cellBounds.IsEmpty
            ? new Point(8, Math.Min(grid.ClientSize.Height, 32))
            : new Point(cellBounds.Left + 8, cellBounds.Bottom);
        contextMenu.Show(grid, location);
    }
}
