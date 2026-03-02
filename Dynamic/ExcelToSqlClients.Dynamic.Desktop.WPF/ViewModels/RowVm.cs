using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ExcelToSqlClients.Dynamic.Desktop.WPF.ViewModels;

public sealed class RowVm : INotifyPropertyChanged
{
    private readonly Dictionary<string, object?> _values;
    private readonly Dictionary<string, object?> _original;

    public bool IsNew { get; private set; }
    public bool IsDirty { get; private set; }

    public RowVm(Dictionary<string, object?> values, bool isNew)
    {
        _values = values;
        _original = values.ToDictionary(k => k.Key, v => v.Value);
        IsNew = isNew;
        IsDirty = isNew; // новая строка требует сохранения
    }

    public object? this[string column]
    {
        get => _values.TryGetValue(column, out var v) ? v : null;
        set
        {
            _values[column] = value;
            IsDirty = true;
            OnPropertyChanged("Item[]");
            OnPropertyChanged(nameof(IsDirty));
        }
    }

    public Dictionary<string, object?> CurrentValues => _values;
    public Dictionary<string, object?> OriginalValues => _original;

    public void AcceptChanges()
    {
        _original.Clear();
        foreach (var kv in _values)
            _original[kv.Key] = kv.Value;

        IsNew = false;
        IsDirty = false;

        OnPropertyChanged(nameof(IsNew));
        OnPropertyChanged(nameof(IsDirty));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}