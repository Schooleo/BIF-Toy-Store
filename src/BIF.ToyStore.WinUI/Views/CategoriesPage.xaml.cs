using BIF.ToyStore.Core.Models;
using BIF.ToyStore.Core.Settings;
using BIF.ToyStore.ViewModels.Pages;
using BIF.ToyStore.WinUI.Services;
using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;

using Microsoft.UI.Dispatching;

namespace BIF.ToyStore.WinUI.Views
{
    public sealed partial class CategoriesPage : Page
    {
        public CategoriesViewModel ViewModel { get; }

        private int _editingCategoryId;
        private DispatcherQueueTimer _searchDebounceTimer;

        public CategoriesPage()
        {
            ViewModel = App.Current.Services.GetRequiredService<CategoriesViewModel>();
            InitializeComponent();

            _searchDebounceTimer = DispatcherQueue.CreateTimer();
            _searchDebounceTimer.Interval = TimeSpan.FromMilliseconds(400);
            _searchDebounceTimer.Tick += SearchDebounceTimer_Tick;

            Loaded += async (s, e) =>
            {
                await ViewModel.LoadCategoriesCommand.ExecuteAsync(null);
            };
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchDebounceTimer.Stop();
            _searchDebounceTimer.Start();
        }

        private void SearchDebounceTimer_Tick(DispatcherQueueTimer sender, object args)
        {
            _searchDebounceTimer.Stop();
            if (ViewModel.ApplyFilterCommand.CanExecute(null))
            {
                ViewModel.ApplyFilterCommand.Execute(null);
            }
        }

        private void CategoriesDataGrid_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (IsFromButton(e.OriginalSource as DependencyObject))
            {
                return;
            }

            e.Handled = true;

            if (sender is DataGrid dataGrid)
            {
                dataGrid.SelectedItem = null;
            }
        }

        private void CategoriesDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is DataGrid dataGrid && dataGrid.SelectedItem != null)
            {
                dataGrid.SelectedItem = null;
            }
        }

        private static bool IsFromButton(DependencyObject source)
        {
            while (source != null)
            {
                if (source is Button)
                {
                    return true;
                }

                source = VisualTreeHelper.GetParent(source);
            }

            return false;
        }

        private async void AddCategory_Click(object sender, RoutedEventArgs e)
        {
            AddCategoryNameTextBox.Text = string.Empty;
            AddCategoryError.Visibility = Visibility.Collapsed;
            AddCategoryDialog.XamlRoot = this.XamlRoot;

            var result = await AddCategoryDialog.ShowAsync();

            // Dialog handles creation in PrimaryButtonClick handler
        }

        private void AddCategoryDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var name = AddCategoryNameTextBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                AddCategoryError.Visibility = Visibility.Visible;
                args.Cancel = true;
                return;
            }

            AddCategoryError.Visibility = Visibility.Collapsed;

            // Fire and forget – dialog will close, then data reloads
            _ = ViewModel.CreateCategoryAsync(new Category { Name = name });
        }

        private async void EditCategory_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: Category category })
            {
                _editingCategoryId = category.Id;
                EditCategoryNameTextBox.Text = category.Name;
                EditCategoryError.Visibility = Visibility.Collapsed;
                EditCategoryDialog.XamlRoot = this.XamlRoot;

                await EditCategoryDialog.ShowAsync();
            }
        }

        private void EditCategoryDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var name = EditCategoryNameTextBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                EditCategoryError.Visibility = Visibility.Visible;
                args.Cancel = true;
                return;
            }

            EditCategoryError.Visibility = Visibility.Collapsed;

            _ = ViewModel.UpdateCategoryAsync(new Category
            {
                Id = _editingCategoryId,
                Name = name
            });
        }

        private async void DeleteCategory_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag is Category category)
            {
                // Guard: do not allow deleting the "Other" category
                if (category.Id == AppConstants.OtherCategoryId)
                {
                    await CommonDialog.ShowAsync(
                        XamlRoot,
                        CommonDialogType.Warning,
                        title: "Cannot Delete",
                        message: $"The \"{category.Name}\" category is a system default and cannot be deleted.",
                        primaryButtonText: "OK",
                        closeButtonText: null,
                        defaultButton: ContentDialogButton.Primary
                    );
                    return;
                }

                var result = await CommonDialog.ShowAsync(
                    XamlRoot,
                    CommonDialogType.Confirmation,
                    title: "Confirm Delete",
                    message: $"Do you want to delete '{category.Name}'? Products in this category will be moved to 'Other'.",
                    primaryButtonText: "Delete",
                    closeButtonText: "Cancel",
                    defaultButton: ContentDialogButton.Primary
                );

                if (result == ContentDialogResult.Primary)
                {
                    await ViewModel.DeleteCategoryAsync(category.Id);
                }
            }
        }
    }
}
