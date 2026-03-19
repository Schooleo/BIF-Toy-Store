using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.ViewModels.Base;
using BIF.ToyStore.ViewModels.Messages;
using BIF.ToyStore.ViewModels.Utils;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace BIF.ToyStore.ViewModels.Pages
{
    public partial class LoginViewModel : BaseViewModel
    {
        private const string CredentialResourceName = "BIF.ToyStore.POS";

        private readonly IGraphQLClient _graphQLClient;
        private readonly ICredentialVaultService _credentialVaultService;
        private readonly ILocalSettingsService _localSettingsService;
        private readonly IMessenger _messenger;

        [ObservableProperty]
        private string _username = string.Empty;

        [ObservableProperty]
        private string _password = string.Empty;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private string _appVersion = string.Empty;

        public LoginViewModel(
            IGraphQLClient graphQLClient,
            ICredentialVaultService credentialVaultService,
            ILocalSettingsService localSettingsService,
            IAppInfoService appInfoService,
            IMessenger messenger)
        {
            _graphQLClient = graphQLClient;
            _credentialVaultService = credentialVaultService;
            _localSettingsService = localSettingsService;
            _messenger = messenger;

            Title = "Login - BIF Toy Store POS";
            AppVersion = appInfoService.GetAppVersion();

            Username = _localSettingsService.GetString("LastUsername", string.Empty);
        }

        public async Task TryAutoLoginAsync()
        {
            var credential = _credentialVaultService.GetCredentials(CredentialResourceName);
            if (credential is null)
            {
                return;
            }

            Username = credential.Value.Username;
            Password = credential.Value.Password;
            await LoginInternalAsync(isAutoLoginAttempt: true);
        }

        [RelayCommand]
        private async Task LoginAsync()
        {
            await LoginInternalAsync(isAutoLoginAttempt: false);
        }

        private async Task LoginInternalAsync(bool isAutoLoginAttempt)
        {
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Please enter both username and password.";
                return;
            }

            IsBusy = true;
            ErrorMessage = string.Empty;

            try
            {
                string query = 
                    @"mutation PerformLogin($user: String!, $pass: String!) {
                        login(username: $user, password: $pass) {
                            id
                            username
                            role
                        }
                    }";

                var variables = new { user = Username, pass = Password };

                var user = await _graphQLClient.ExecuteAsync<BIF.ToyStore.Core.Models.LoginUser>(query, variables, dataKey: "login");

                if (user != null)
                {
                    _localSettingsService.SetString("LastUsername", user.Username);
                    _credentialVaultService.SaveCredentials(CredentialResourceName, Username, Password);

                    var lastRoute = _localSettingsService.GetString(AppPreferenceKeys.LastActiveRoute, "Dashboard");
                    _messenger.Send(new LoginSucceededMessage(user));
                    ErrorMessage = isAutoLoginAttempt
                        ? $"Auto-login successful. Routing to {lastRoute}."
                        : $"Login successful. Routing to {lastRoute}.";
                }
                else
                {
                    if (isAutoLoginAttempt)
                    {
                        _credentialVaultService.ClearCredentials(CredentialResourceName);
                    }

                    ErrorMessage = "Invalid username or password.";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = isAutoLoginAttempt
                    ? string.Empty
                    : "Connection Error: " + ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
