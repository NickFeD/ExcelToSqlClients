using ExcelToSqlClients.Core.Abstractions;
using ExcelToSqlClients.Core.Entities;
using System.Collections.ObjectModel;

namespace ExcelToSqlClients.Desktop.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly IClientCrudService _crud;

    public ObservableCollection<Client> Clients { get; } = new();

    private string _searchText = "";
    public string SearchText
    {
        get => _searchText;
        set => Set(ref _searchText, value);
    }

    private string _statusText = "";
    public string StatusText
    {
        get => _statusText;
        set => Set(ref _statusText, value);
    }

    public RelayCommand LoadCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand ReloadCommand { get; }

    private const int PageSize = 500;

    private int _skip = 0;
    private bool _isLoading = false;
    private bool _hasMore = true;

    public MainViewModel(IClientCrudService crud)
    {
        _crud = crud;

        LoadCommand = new RelayCommand(() => _ = LoadAsync());
        ReloadCommand = new RelayCommand(() => _ = LoadAsync());
        SaveCommand = new RelayCommand(() => _ = SaveAsync(), () => Clients.Count > 0);

        _ = LoadAsync();
    }

    public async Task LoadAsync()
    {
        if (_isLoading) return;

        _skip = 0;
        _hasMore = true;
        Clients.Clear();
        SaveCommand.RaiseCanExecuteChanged();

        await LoadMoreIfNeededAsync(force: true);
    }

    public async Task LoadMoreIfNeededAsync(bool force = false)
    {
        if (_isLoading) return;
        if (!_hasMore && !force) return;

        try
        {
            _isLoading = true;
            StatusText = "Loading...";

            var page = await _crud.SearchAsync(SearchText, skip: _skip, take: PageSize, CancellationToken.None);

            foreach (var c in page)
                Clients.Add(c);

            _skip += page.Count;
            if (page.Count < PageSize)
                _hasMore = false;

            StatusText = $"Loaded: {Clients.Count}" + (_hasMore ? "" : " (all)");
            SaveCommand.RaiseCanExecuteChanged();
        }
        catch (Exception ex)
        {
            StatusText = $"Load failed: {ex.Message}";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task SaveAsync()
    {
        try
        {
            StatusText = "Saving...";
            await _crud.SaveAsync(Clients, CancellationToken.None);
            StatusText = "Saved.";
        }
        catch (Exception ex)
        {
            StatusText = $"Save failed: {ex.Message}";
        }
    }
}