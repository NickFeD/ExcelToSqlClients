using ExcelToSqlClients.Core.Abstractions;
using ExcelToSqlClients.Core.Models.Db;
using System.Collections.ObjectModel;

namespace ExcelToSqlClients.Dynamic.Desktop.WPF.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly IDbSchemaService _schemaService;
    private readonly IDbTableDataService _dataService;

    public ObservableCollection<DbTableInfo> Tables { get; } = new();
    public ObservableCollection<RowVm> Rows { get; } = new();
    public ObservableCollection<string> Columns { get; } = new();

    // ✅ NEW: read-only columns calculated from schema
    public ObservableCollection<string> ReadOnlyColumns { get; } = new();

    private DbTableInfo? _selectedTable;
    public DbTableInfo? SelectedTable
    {
        get => _selectedTable;
        set
        {
            if (Set(ref _selectedTable, value))
                _ = LoadFirstPageAsync();
        }
    }

    private string _statusText = "";
    public string StatusText
    {
        get => _statusText;
        set => Set(ref _statusText, value);
    }

    private bool _canEdit = false;
    public bool CanEdit
    {
        get => _canEdit;
        set
        {
            if (Set(ref _canEdit, value))
                SaveCommand.RaiseCanExecuteChanged();
        }
    }

    public RelayCommand RefreshTablesCommand { get; }
    public RelayCommand SaveCommand { get; }

    private const int PageSize = 500;
    private int _skip = 0;
    private bool _isLoading = false;
    private bool _hasMore = true;

    private DbTableSchema? _currentSchema;

    public MainViewModel(IDbSchemaService schemaService, IDbTableDataService dataService)
    {
        _schemaService = schemaService;
        _dataService = dataService;

        RefreshTablesCommand = new RelayCommand(() => _ = LoadTablesAsync());
        SaveCommand = new RelayCommand(() => _ = SaveAsync(), () => CanEdit && Rows.Any(r => r.IsDirty));

        _ = LoadTablesAsync();
    }

    public async Task LoadTablesAsync()
    {
        try
        {
            StatusText = "Loading tables...";
            Tables.Clear();

            var tables = await _schemaService.GetTablesAsync(CancellationToken.None);
            foreach (var t in tables) Tables.Add(t);

            StatusText = $"Tables: {Tables.Count}";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to load tables: {ex.Message}";
        }
    }

    private async Task LoadFirstPageAsync()
    {
        if (SelectedTable == null) return;

        _skip = 0;
        _hasMore = true;
        Rows.Clear();
        Columns.Clear();
        ReadOnlyColumns.Clear();

        _currentSchema = null;
        CanEdit = false;
        SaveCommand.RaiseCanExecuteChanged();

        await LoadMoreIfNeededAsync(force: true);
    }

    public async Task LoadMoreIfNeededAsync(bool force = false)
    {
        if (_isLoading) return;
        if (!_hasMore && !force) return;
        if (SelectedTable == null) return;

        try
        {
            _isLoading = true;
            StatusText = "Loading data...";

            var page = await _dataService.ReadPageAsync(SelectedTable, _skip, PageSize, CancellationToken.None);
            _currentSchema ??= page.Schema;

            if (Columns.Count == 0)
            {
                foreach (var c in page.Schema.Columns.Select(x => x.Name))
                    Columns.Add(c);

                CanEdit = page.Schema.PrimaryKeyColumns.Count > 0;
                if (!CanEdit)
                    StatusText = "No PK detected → editing disabled. Viewing only.";

                // ✅ ReadOnlyColumns = PK ∪ Identity ∪ Computed
                var ro = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var pk in page.Schema.PrimaryKeyColumns)
                    ro.Add(pk);

                foreach (var c in page.Schema.Columns.Where(x => x.IsIdentity || x.IsComputed))
                    ro.Add(c.Name);

                ReadOnlyColumns.Clear();
                foreach (var name in ro.OrderBy(x => x))
                    ReadOnlyColumns.Add(name);
            }

            foreach (var row in page.Rows)
                Rows.Add(new RowVm(row, isNew: false));

            _skip += page.Rows.Count;
            _hasMore = page.HasMore;

            if (CanEdit)
                StatusText = $"Loaded rows: {Rows.Count}" + (_hasMore ? "" : " (all)");
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to load data: {ex.Message}";
        }
        finally
        {
            _isLoading = false;
            SaveCommand.RaiseCanExecuteChanged();
        }
    }

    public RowVm CreateNewRow()
    {
        if (_currentSchema == null)
            throw new InvalidOperationException("Schema is not loaded yet.");

        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in _currentSchema.Columns)
            dict[c.Name] = null;

        return new RowVm(dict, isNew: true);
    }

    private async Task SaveAsync()
    {
        if (!CanEdit)
        {
            StatusText = "Editing disabled (no PK).";
            return;
        }

        if (SelectedTable == null || _currentSchema == null)
        {
            StatusText = "No table selected.";
            return;
        }

        try
        {
            StatusText = "Saving...";

            var newRows = Rows.Where(r => r.IsNew && r.IsDirty)
                              .Select(r => r.CurrentValues)
                              .ToList();

            var changedRows = Rows.Where(r => !r.IsNew && r.IsDirty)
                                  .Select(r => (r.OriginalValues, r.CurrentValues))
                                  .ToList();

            var res = await _dataService.SaveChangesAsync(
                SelectedTable, _currentSchema, newRows, changedRows, CancellationToken.None);

            foreach (var r in Rows.Where(r => r.IsDirty))
                r.AcceptChanges();

            StatusText = $"Saved. Inserted={res.Inserted}, Updated={res.Updated}";
        }
        catch (Exception ex)
        {
            StatusText = $"Save failed: {ex.Message}";
        }
        finally
        {
            SaveCommand.RaiseCanExecuteChanged();
        }
    }
}