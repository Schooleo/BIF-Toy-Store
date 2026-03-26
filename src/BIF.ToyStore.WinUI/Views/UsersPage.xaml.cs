using BIF.ToyStore.ViewModels.Pages;
using BIF.ToyStore.WinUI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Threading.Tasks;
using Windows.Foundation;

namespace BIF.ToyStore.WinUI.Views
{
    public sealed partial class UsersPage : Page
    {
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

        private async void AddNewUserButton_Click(object sender, RoutedEventArgs e)
        {
            CreateUserDialog.XamlRoot = XamlRoot;
            ViewModel.ErrorMessage = string.Empty;
            CreateUsernameTextBox.Text = string.Empty;
            CreatePasswordBox.Password = string.Empty;

            await ShowCreateUserDialogAsync();
        }

        private async void CreateUserDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            string username = CreateUsernameTextBox.Text.Trim();
            string password = CreatePasswordBox.Password;

            var deferral = args.GetDeferral();
            try
            {
                bool created = await ViewModel.CreateUserAsync(username, password);
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

            var result = await CommonDialog.ShowAsync(
                XamlRoot,
                CommonDialogType.Warning,
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
