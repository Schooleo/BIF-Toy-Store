using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.ViewModels.Base;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Text.Json;

namespace BIF.ToyStore.ViewModels.Pages
{
    public partial class LoginViewModel : BaseViewModel
    {
        private readonly IGraphQLClient _graphQLClient;

        [ObservableProperty]
        private string _username = string.Empty;

        [ObservableProperty]
        private string _password = string.Empty;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        public LoginViewModel(IGraphQLClient graphQLClient)
        {
            _graphQLClient = graphQLClient;
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

                var user = await _graphQLClient.ExecuteAsync<User>(query, variables, dataKey: "login");

                if (user != null)
                {
                    var role = user.Role;

                    ErrorMessage = $"User: {user.Username} | Role: {user.Role}";

                    // TODO: Navigate to Dashboard based on role
                }
                else
                {
                    ErrorMessage = "Invalid username or password.";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Connection Error: " + ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
