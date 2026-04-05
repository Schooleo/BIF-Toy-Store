using System;
using BIF.ToyStore.ViewModels.Pages;
using BIF.ToyStore.WinUI.Controls;
using BIF.ToyStore.WinUI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BIF.ToyStore.WinUI.Views
{
    public sealed partial class OrderPage : Page
    {
        public OrderViewModel ViewModel { get; }

        public OrderPage()
        {
            ViewModel = App.Current.Services.GetRequiredService<OrderViewModel>();
            InitializeComponent();

            Loaded += async (s, e) =>
            {
                await ViewModel.LoadAsync();
            };
        }

        private void Filter_Changed(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilter();

            if (sender is DependencyObject source)
            {
                CommonFlyout.CloseParentFlyout(source);
            }
        }

        private void Filter_Changed(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            if (ViewModel.ApplyFilterCommand.CanExecute(null))
            {
                _ = ViewModel.ApplyFilterCommand.ExecuteAsync(null);
            }
        }

        private void ViewOrder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is OrderItemViewModel order)
            {
                if (ViewModel.OpenOrderDetailsCommand.CanExecute(order))
                {
                    _ = ViewModel.OpenOrderDetailsCommand.ExecuteAsync(order);
                }
            }
        }

        private async void DeleteOrder_Click(object sender, RoutedEventArgs e)
        {
            var orderId = 0;
            var orderDisplayId = string.Empty;

            if (sender is FrameworkElement element && element.Tag is OrderItemViewModel order)
            {
                orderId = order.Id;
                orderDisplayId = order.IdDisplay;
            }
            else if (ViewModel.SelectedOrder is not null)
            {
                orderId = ViewModel.SelectedOrder.Id;
                orderDisplayId = ViewModel.SelectedOrder.IdDisplay;
            }

            if (orderId <= 0) return;

            var result = await CommonDialog.ShowAsync(
                XamlRoot,
                CommonDialogType.Warning,
                title: "Confirm Delete",
                message: $"Are you sure you want to delete order {orderDisplayId}? This action cannot be undone.",
                primaryButtonText: "Delete",
                closeButtonText: "Cancel",
                defaultButton: ContentDialogButton.Primary,
                destructivePrimary: true);

            if (result == ContentDialogResult.Primary)
            {
                await ViewModel.DeleteOrderCommand.ExecuteAsync(orderId);
            }
        }

        private void OrdersDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Keep list interactions click-driven (link/buttons) without a persistent selected row highlight.
            if (sender is CommunityToolkit.WinUI.UI.Controls.DataGrid grid)
            {
                grid.SelectedItem = null;
            }
        }

        private void OrdersDataGrid_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            if (sender is CommunityToolkit.WinUI.UI.Controls.DataGrid grid)
            {
                grid.SelectedItem = null;
            }
        }

        private async void QuickUpdateStatus_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is string status)
            {
                await ViewModel.UpdateOrderStatusCommand.ExecuteAsync(status);
            }
        }

        private async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            // Build simple CSV in memory and show a save-file dialog
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Order ID,Date,Total,Employee,Status");
            foreach (var order in ViewModel.Orders)
            {
                sb.AppendLine(string.Join(",",
                    EscapeCsv(order.IdDisplay),
                    EscapeCsv(order.DateDisplay),
                    EscapeCsv(order.TotalDisplay),
                    EscapeCsv(order.EmployeeDisplay),
                    EscapeCsv(order.Status)));
            }

            var savePicker = new Windows.Storage.Pickers.FileSavePicker();
            savePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop;
            savePicker.FileTypeChoices.Add("CSV File", new[] { ".csv" });
            savePicker.SuggestedFileName = $"Orders_{System.DateTime.Now:yyyyMMdd_HHmm}";

            // Associate the picker with the current window
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.Current.MainWindowInstance!);
            WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);

            var file = await savePicker.PickSaveFileAsync();
            if (file != null)
            {
                await Windows.Storage.FileIO.WriteTextAsync(file, sb.ToString());

                await CommonDialog.ShowAsync(
                    XamlRoot,
                    CommonDialogType.Information,
                    title: "Export Complete",
                    message: $"Orders exported to {file.Name}.",
                    primaryButtonText: "OK",
                    closeButtonText: string.Empty);
            }
        }

        private static string EscapeCsv(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var escaped = value.Replace("\"", "\"\"");
            return escaped.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{escaped}\"" : escaped;
        }
    }
}
