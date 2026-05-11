using BIF.ToyStore.Core.Enums;
using BIF.ToyStore.ViewModels.Pages;
using BIF.ToyStore.WinUI.Controls;
using BIF.ToyStore.WinUI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Threading.Tasks;
using Windows.Foundation;

namespace BIF.ToyStore.WinUI.Views
{
    public sealed partial class UsersPage : Page
    {
        private UserItemViewModel? _editingUser;
        private string _createUserRoleTag = "SALE";

        public UserManagementViewModel ViewModel { get; }

        public UsersPage()
        {
            InitializeComponent();
            ViewModel = App.Current.Services.GetRequiredService<UserManagementViewModel>();
            DataContext = ViewModel;
            Loaded += OnLoaded;
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
        }

        private async void NameFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded || !ViewModel.ApplyFilterCommand.CanExecute(null))
            {
                return;
            }

            await ViewModel.ApplyFilterCommand.ExecuteAsync(null);

            if (sender is ListView { SelectedItem: not null })
            {
                CommonFlyout.HideAttachedFlyout(NameFilterButton);
            }
        }

        private async void RoleFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded || !ViewModel.ApplyFilterCommand.CanExecute(null))
            {
                return;
            }

            await ViewModel.ApplyFilterCommand.ExecuteAsync(null);

            if (sender is ListView { SelectedItem: not null })
            {
                CommonFlyout.HideAttachedFlyout(RoleFilterButton);
            }
        }

        private async void AddNewUserButton_Click(object sender, RoutedEventArgs e)
        {
            CreateUserDialog.XamlRoot = XamlRoot;
            ViewModel.ErrorMessage = string.Empty;
            CreateUsernameTextBox.Text = string.Empty;
            CreatePasswordBox.Password = string.Empty;
            _createUserRoleTag = "SALE";
            CreateRoleTextBlock.Text = "Sale";

            await ShowCreateUserDialogAsync();
        }

        private async void CreateUserDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            string username = CreateUsernameTextBox.Text.Trim();
            string password = CreatePasswordBox.Password;
            var selectedRole = string.Equals(_createUserRoleTag, "ADMIN", StringComparison.OrdinalIgnoreCase)
                ? UserRole.Admin
                : UserRole.Sale;

            var deferral = args.GetDeferral();
            try
            {
                bool created = await ViewModel.CreateUserAsync(username, password, selectedRole);
                args.Cancel = !created;
            }
            finally
            {
                deferral.Complete();
            }
        }

        private async void DeleteUserButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { CommandParameter: UserItemViewModel user })
            {
                return;
            }

            ClearUsersGridSelection();

            var result = await CommonDialog.ShowAsync(
                XamlRoot,
                CommonDialogType.Confirmation,
                "Confirm Delete User",
                $"Are you sure you want to delete user '{user.Username}'?",
                primaryButtonText: "Delete",
                closeButtonText: "Cancel",
                defaultButton: ContentDialogButton.Close);

            if (result != ContentDialogResult.Primary)
            {
                return;
            }

            await ViewModel.DeleteUserAsync(user);
        }

        private async void UpdateUserButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { CommandParameter: UserItemViewModel user })
            {
                return;
            }

            ClearUsersGridSelection();

            _editingUser = user;
            UpdateUserDialog.XamlRoot = XamlRoot;
            ViewModel.ErrorMessage = string.Empty;
            UpdateUsernameTextBox.Text = user.Username;
            UpdatePasswordBox.Password = user.PasswordHash;

            IAsyncOperation<ContentDialogResult> operation = UpdateUserDialog.ShowAsync();
            var completion = new TaskCompletionSource<ContentDialogResult>();

            operation.Completed = (info, status) =>
            {
                if (status == AsyncStatus.Error)
                {
                    completion.TrySetException(info.ErrorCode);
                    return;
                }

                if (status == AsyncStatus.Canceled)
                {
                    completion.TrySetCanceled();
                    return;
                }

                completion.TrySetResult(info.GetResults());
            };

            await completion.Task;
        }

        private async void UpdateUserDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (_editingUser is null)
            {
                args.Cancel = true;
                return;
            }

            string username = UpdateUsernameTextBox.Text.Trim();
            string password = UpdatePasswordBox.Password;

            var deferral = args.GetDeferral();
            try
            {
                bool updated = await ViewModel.UpdateUserAsync(_editingUser, username, password);
                args.Cancel = !updated;
            }
            finally
            {
                deferral.Complete();
                if (!args.Cancel)
                {
                    _editingUser = null;
                }
            }
        }

        private void CreateRoleOption_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuFlyoutItem item)
            {
                return;
            }

            _createUserRoleTag = item.Tag as string ?? "SALE";
            CreateRoleTextBlock.Text = item.Text;
        }

        private void TogglePasswordButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { CommandParameter: UserItemViewModel user })
            {
                return;
            }

            if (ViewModel.TogglePasswordCommand.CanExecute(user))
            {
                ViewModel.TogglePasswordCommand.Execute(user);
            }

            ClearUsersGridSelection();
        }

        private void UsersDataGrid_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sender is CommunityToolkit.WinUI.UI.Controls.DataGrid dataGrid)
            {
                dataGrid.SelectedItem = null;
            }

            if (IsFromButton(e.OriginalSource as DependencyObject))
            {
                return;
            }

            e.Handled = true;
        }

        private void UsersDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is CommunityToolkit.WinUI.UI.Controls.DataGrid dataGrid && dataGrid.SelectedItem != null)
            {
                dataGrid.SelectedItem = null;
            }
        }

        private static bool IsFromButton(DependencyObject? source)
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

        private void ClearUsersGridSelection()
        {
            UsersDataGrid.SelectedItem = null;
        }

        private Task<ContentDialogResult> ShowCreateUserDialogAsync()
        {
            IAsyncOperation<ContentDialogResult> operation = CreateUserDialog.ShowAsync();
            var completion = new TaskCompletionSource<ContentDialogResult>();

            operation.Completed = (info, status) =>
            {
                if (status == AsyncStatus.Error)
                {
                    completion.TrySetException(info.ErrorCode);
                    return;
                }

                if (status == AsyncStatus.Canceled)
                {
                    completion.TrySetCanceled();
                    return;
                }

                completion.TrySetResult(info.GetResults());
            };

            return completion.Task;
        }

    }
}
