using ExcelToSqlClients.Dynamic.Desktop.WPF.ViewModels;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ExcelToSqlClients.Dynamic.Desktop.WPF;

public partial class MainWindow : Window
{
    private ScrollViewer? _scrollViewer;

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        // ✅ rebuild grid columns when columns OR readonly set changes
        vm.Columns.CollectionChanged += Columns_CollectionChanged;
        vm.ReadOnlyColumns.CollectionChanged += Columns_CollectionChanged;
    }

    private void Columns_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        Grid.Columns.Clear();

        var readOnly = new HashSet<string>(vm.ReadOnlyColumns, StringComparer.OrdinalIgnoreCase);

        foreach (var colName in vm.Columns)
        {
            var isReadOnly = readOnly.Contains(colName);

            Grid.Columns.Add(new DataGridTextColumn
            {
                Header = colName,
                Binding = new Binding($"[{colName}]")
                {
                    Mode = isReadOnly ? BindingMode.OneWay : BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                },
                IsReadOnly = isReadOnly,
                Width = new DataGridLength(1, DataGridLengthUnitType.SizeToHeader)
            });
        }
    }

    private void Grid_Loaded(object sender, RoutedEventArgs e)
    {
        _scrollViewer ??= FindVisualChild<ScrollViewer>(Grid);

        if (_scrollViewer != null)
        {
            _scrollViewer.ScrollChanged -= OnScrollChanged;
            _scrollViewer.ScrollChanged += OnScrollChanged;
        }
    }

    private async void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if (sender is not ScrollViewer sv) return;

        if (sv.VerticalOffset + sv.ViewportHeight >= sv.ExtentHeight - 200)
            await vm.LoadMoreIfNeededAsync();
    }

    private void Grid_AddingNewItem(object sender, AddingNewItemEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        e.NewItem = vm.CreateNewRow();
        vm.SaveCommand.RaiseCanExecuteChanged();
    }

    private void Grid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.SaveCommand.RaiseCanExecuteChanged();
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T t) return t;

            var result = FindVisualChild<T>(child);
            if (result != null) return result;
        }
        return null;
    }
}