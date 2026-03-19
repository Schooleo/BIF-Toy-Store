using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.ViewModels.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using System.Linq;

namespace BIF.ToyStore.WinUI.Views
{
    public sealed partial class DashboardPage : Page
    {
        private readonly ILocalSettingsService _localSettingsService;

        public DashboardPage()
        {
            InitializeComponent();

            _localSettingsService = App.Current.Services.GetRequiredService<ILocalSettingsService>();
            int itemsPerPage = _localSettingsService.GetInt(AppPreferenceKeys.ProductsItemsPerPage, 20);
            SelectItemsPerPage(itemsPerPage);
        }

        private void ItemsPerPageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ItemsPerPageComboBox.SelectedItem is ComboBoxItem item
                && int.TryParse(item.Content?.ToString(), out int take))
            {
                _localSettingsService.SetInt(AppPreferenceKeys.ProductsItemsPerPage, take);

                // This value is the query variable source for future getProducts(take: $take) calls.
                var variables = new { take };
                _ = variables;
            }
        }

        private void SelectItemsPerPage(int value)
        {
            foreach (var item in ItemsPerPageComboBox.Items.OfType<ComboBoxItem>())
            {
                if (item.Content?.ToString() == value.ToString())
                {
                    ItemsPerPageComboBox.SelectedItem = item;
                    return;
                }
            }

            ItemsPerPageComboBox.SelectedIndex = 0;
        }
    }
}
