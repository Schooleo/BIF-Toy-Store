using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.ViewModels.Base;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BIF.ToyStore.ViewModels.Pages
{
    public partial class LoginViewModel : BaseViewModel
    {
        private readonly IAuthService _authService;

        [ObservableProperty]
        private string _username = string.Empty;

        [ObservableProperty]
        private string _password = string.Empty;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        public LoginViewModel(IAuthService authService)
        {
            _authService = authService;
            Title = "Login - BIF Toy Store POS";
        }

        [RelayCommand]
        private async Task LoginAsync()
        {
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Please enter both username and password.";
                return;
            }

            IsBusy = true;
            ErrorMessage = string.Empty;

            var user = await _authService.LoginAsync(Username, Password);

            if (user != null)
            {
                // Login successful!
                // TODO: Save the logged-in user state ("Remember Me" requirement)
                // TODO: Navigate to the Dashboard
            }
            else
            {
                // Login failed
                ErrorMessage = "Invalid username or password.";
            }

            IsBusy = false;
        }
    }
}
