using BIF.ToyStore.ViewModels.Pages;
using BIF.ToyStore.WinUI.Controls;
using BIF.ToyStore.WinUI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace BIF.ToyStore.WinUI.Views
{
    public sealed partial class ReportsPage : Page
    {
        public ReportsViewModel ViewModel { get; }

        public ReportsPage()
        {
            InitializeComponent();
            ViewModel = App.Current.Services.GetRequiredService<ReportsViewModel>();
            DataContext = ViewModel;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;

            var mainWindow = App.Current.MainWindowInstance;
            if (mainWindow is null || !mainWindow.IsCurrentUserAdmin)
            {
                if (mainWindow is not null)
                {
                    mainWindow.NavigateToDashboard();
                }
                return;
            }

            await ViewModel.LoadAsync();
            QueueChartsScrollToRight();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            Unloaded -= OnUnloaded;
            ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ReportsViewModel.TrendScrollableWidth)
                || e.PropertyName == nameof(ReportsViewModel.HasTimeSeriesData))
            {
                QueueChartsScrollToRight();
            }
        }

        private void QueueChartsScrollToRight()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    ScrollChartToRight(SalesTrendScrollViewer);
                    ScrollChartToRight(RevenueProfitScrollViewer);
                });
            });
        }

        private static void ScrollChartToRight(ScrollViewer scrollViewer)
        {
            scrollViewer.UpdateLayout();

            if (scrollViewer.ScrollableWidth > 0)
            {
                scrollViewer.ChangeView(scrollViewer.ScrollableWidth, null, null, true);
            }
        }

        private void GroupBy_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is not ReportGroupByOption option)
            {
                return;
            }

            ViewModel.SelectedGroupBy = option;
            CommonFlyout.HideAttachedFlyout(GroupBySelectorButton);
        }

        private async void PrintDetailReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var savePicker = new FileSavePicker();
                savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                savePicker.FileTypeChoices.Add("PDF File", new[] { ".pdf" });
                savePicker.SuggestedFileName = $"Sales_Detail_Report_{DateTime.Now:yyyyMMdd_HHmm}";

                var hwnd = WindowNative.GetWindowHandle(App.Current.MainWindowInstance!);
                InitializeWithWindow.Initialize(savePicker, hwnd);

                StorageFile? file = await savePicker.PickSaveFileAsync();
                if (file is null)
                {
                    return;
                }

                var reportLines = BuildReportLines();
                var pdfBytes = BuildSimplePdf(reportLines);
                await FileIO.WriteBytesAsync(file, pdfBytes);

                await CommonDialog.ShowAsync(
                    XamlRoot,
                    CommonDialogType.Information,
                    title: "Export Complete",
                    message: $"Report exported to {file.Name}.",
                    primaryButtonText: "OK",
                    closeButtonText: null,
                    defaultButton: ContentDialogButton.Primary);
            }
            catch (Exception ex)
            {
                await CommonDialog.ShowAsync(
                    XamlRoot,
                    CommonDialogType.Error,
                    title: "Export Failed",
                    message: "Unable to export PDF report: " + ex.Message,
                    primaryButtonText: "OK",
                    closeButtonText: null,
                    defaultButton: ContentDialogButton.Primary);
            }
        }

        private List<string> BuildReportLines()
        {
            var lines = new List<string>
            {
                "BIF Toy Store - Sales Detail Report",
                "Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                string.Empty,
                "Range: "
                    + (ViewModel.FromDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "N/A")
                    + " to "
                    + (ViewModel.ToDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "N/A"),
                "Group By: " + ViewModel.SelectedGroupByLabel,
                string.Empty,
                "Total Items Sold: " + ViewModel.TotalItemsSoldDisplay,
                "Total Revenue: " + ViewModel.TotalRevenueDisplay,
                "Total Profit: " + ViewModel.TotalProfitDisplay,
                string.Empty,
                "Top Products:"
            };

            foreach (var product in ViewModel.TopProducts.Take(12))
            {
                lines.Add($"- #{product.RankDisplay} {product.ProductName}: {product.QuantityDisplay} | Rev {product.RevenueDisplay} | Profit {product.ProfitDisplay}");
            }

            lines.Add(string.Empty);
            lines.Add("Time Series:");
            foreach (var point in ViewModel.TimeSeriesPoints.Take(18))
            {
                lines.Add($"- {point.Label}: {point.QuantityDisplay} | Rev {point.RevenueDisplay} | Profit {point.ProfitDisplay}");
            }

            return lines;
        }

        private static byte[] BuildSimplePdf(IReadOnlyList<string> lines)
        {
            var clippedLines = lines
                .Select(SanitizePdfText)
                .Where(x => !string.IsNullOrWhiteSpace(x) || x == string.Empty)
                .Take(44)
                .ToList();

            var contentBuilder = new StringBuilder();
            contentBuilder.AppendLine("BT");
            contentBuilder.AppendLine("/F1 11 Tf");
            contentBuilder.AppendLine("50 770 Td");
            contentBuilder.AppendLine("14 TL");

            foreach (var line in clippedLines)
            {
                contentBuilder.Append('(')
                    .Append(line)
                    .AppendLine(") Tj");
                contentBuilder.AppendLine("T*");
            }

            contentBuilder.AppendLine("ET");
            string contentStream = contentBuilder.ToString();

            var pdfBuilder = new StringBuilder();
            var offsets = new List<int>();

            pdfBuilder.AppendLine("%PDF-1.4");

            AddPdfObject(pdfBuilder, offsets, "<< /Type /Catalog /Pages 2 0 R >>");
            AddPdfObject(pdfBuilder, offsets, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
            AddPdfObject(pdfBuilder, offsets, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>");
            AddPdfObject(pdfBuilder, offsets, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
            AddPdfObject(
                pdfBuilder,
                offsets,
                $"<< /Length {Encoding.ASCII.GetByteCount(contentStream)} >>\nstream\n{contentStream}endstream");

            int xrefOffset = Encoding.ASCII.GetByteCount(pdfBuilder.ToString());
            pdfBuilder.AppendLine("xref");
            pdfBuilder.AppendLine($"0 {offsets.Count + 1}");
            pdfBuilder.AppendLine("0000000000 65535 f ");
            foreach (int offset in offsets)
            {
                pdfBuilder.AppendLine($"{offset:D10} 00000 n ");
            }

            pdfBuilder.AppendLine("trailer");
            pdfBuilder.AppendLine($"<< /Size {offsets.Count + 1} /Root 1 0 R >>");
            pdfBuilder.AppendLine("startxref");
            pdfBuilder.AppendLine(xrefOffset.ToString(CultureInfo.InvariantCulture));
            pdfBuilder.Append("%%EOF");

            return Encoding.ASCII.GetBytes(pdfBuilder.ToString());
        }

        private static void AddPdfObject(StringBuilder pdfBuilder, List<int> offsets, string body)
        {
            int objectNumber = offsets.Count + 1;
            int offset = Encoding.ASCII.GetByteCount(pdfBuilder.ToString());
            offsets.Add(offset);

            pdfBuilder.AppendLine($"{objectNumber} 0 obj");
            pdfBuilder.AppendLine(body);
            pdfBuilder.AppendLine("endobj");
        }

        private static string SanitizePdfText(string value)
        {
            return value
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("(", "\\(", StringComparison.Ordinal)
                .Replace(")", "\\)", StringComparison.Ordinal);
        }
    }
}
