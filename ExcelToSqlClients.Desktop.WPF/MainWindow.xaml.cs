using ExcelToSqlClients.Desktop.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ExcelToSqlClients.Desktop;

public partial class MainWindow : Window
{
    private ScrollViewer? _scrollViewer;

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    private void Grid_Loaded(object sender, RoutedEventArgs e)
    {
        // Найдём ScrollViewer внутри DataGrid
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

        // Подгрузка, когда пользователь близко к низу
        // Порог 200px, чтобы загрузка начиналась заранее
        if (sv.VerticalOffset + sv.ViewportHeight >= sv.ExtentHeight - 200)
        {
            await vm.LoadMoreIfNeededAsync();
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T t)
                return t;

            var result = FindVisualChild<T>(child);
            if (result != null)
                return result;
        }
        return null;
    }
}