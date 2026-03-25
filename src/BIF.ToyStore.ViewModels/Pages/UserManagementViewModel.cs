using BIF.ToyStore.Core.Enums;
using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.ViewModels.Base;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace BIF.ToyStore.ViewModels.Pages
{
    public partial class UserManagementViewModel : BaseViewModel
    {
        private readonly IGraphQLClient _graphQLClient;
        private readonly List<UserItemViewModel> _allUsers = new();

        [ObservableProperty]
        private ObservableCollection<UserItemViewModel> _visibleUsers = new();

        [ObservableProperty]
        private ObservableCollection<string> _nameFilters = new();

        [ObservableProperty]
        private string _selectedNameFilter = "All";

        [ObservableProperty]
        private int _totalStaff;

        [ObservableProperty]
        private int _admins;

        [ObservableProperty]
        private int _activeSessions;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        public UserManagementViewModel(IGraphQLClient graphQLClient)
        {
            _graphQLClient = graphQLClient;
            Title = "Staff Directory";

            NameFilters = new ObservableCollection<string>(BuildNameFilters());
        }

        partial void OnSelectedNameFilterChanged(string value)
        {
            ApplySortAndFilter();
        }

        public async Task LoadAsync()
        {
            try
            {
                ErrorMessage = string.Empty;

                const string query = @"query {
                    users {
                        id
                        username
                        passwordHash
                        role
                    }
                }";

                var users = await _graphQLClient.ExecuteAsync<List<UserDto>>(query, dataKey: "users")
                    ?? new List<UserDto>();

                _allUsers.Clear();
                foreach (var item in users)
                {
                    _allUsers.Add(new UserItemViewModel(item));
                }

                ApplySortAndFilter();
            }
            catch (Exception ex)
            {
                ErrorMessage = "Unable to load users: " + ex.Message;
                _allUsers.Clear();
                VisibleUsers.Clear();
                TotalStaff = 0;
                Admins = 0;
                ActiveSessions = 0;
            }
        }

        [RelayCommand]
        private void TogglePassword(UserItemViewModel? user)
        {
            if (user is null)
            {
                return;
            }

            user.IsPasswordVisible = !user.IsPasswordVisible;
        }

        private void ApplySortAndFilter()
        {
            IEnumerable<UserItemViewModel> query = _allUsers;

            if (!string.Equals(SelectedNameFilter, "All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(u => u.Username.StartsWith(SelectedNameFilter, StringComparison.OrdinalIgnoreCase));
            }

            var result = query
                .OrderByDescending(u => u.Kpi)
                .ThenBy(u => u.Username, StringComparer.OrdinalIgnoreCase)
                .ToList();

            VisibleUsers.Clear();
            foreach (var item in result)
            {
                VisibleUsers.Add(item);
            }

            TotalStaff = _allUsers.Count;
            Admins = _allUsers.Count(x => x.Role == UserRole.Admin);
            ActiveSessions = result.Count(x => x.Kpi >= 80);
        }

        private static IEnumerable<string> BuildNameFilters()
        {
            yield return "All";
            for (char c = 'A'; c <= 'Z'; c++)
            {
                yield return c.ToString();
            }
        }
    }

    public sealed class UserDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public UserRole Role { get; set; }
    }

    public partial class UserItemViewModel : ObservableObject
    {
        public int Id { get; }
        public string Username { get; }
        public string PasswordHash { get; }
        public UserRole Role { get; }
        public int Kpi { get; }
        public string Initials { get; }
        public string RoleLabel => Role == UserRole.Admin ? "ADMIN" : "SALE";

        [ObservableProperty]
        private bool _isPasswordVisible;

        public UserItemViewModel(UserDto user)
        {
            Id = user.Id;
            Username = user.Username;
            PasswordHash = user.PasswordHash;
            Role = user.Role;
            Kpi = CalculateKpi(user);
            Initials = BuildInitials(user.Username);
        }

        public string PasswordDisplay => IsPasswordVisible ? PasswordHash : "........";

        partial void OnIsPasswordVisibleChanged(bool value)
        {
            OnPropertyChanged(nameof(PasswordDisplay));
        }

        private static int CalculateKpi(UserDto user)
        {
            int baseScore = user.Role == UserRole.Admin ? 86 : 62;
            int spread = (user.Username.Length * 9 + user.Id * 5) % 15;
            return baseScore + spread;
        }

        private static string BuildInitials(string username)
        {
            var parts = username
                .Split(new[] { '.', '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Take(2)
                .Select(x => char.ToUpperInvariant(x[0]));

            string initials = string.Concat(parts);
            return string.IsNullOrWhiteSpace(initials) ? "U" : initials;
        }
    }
}
