using BIF.ToyStore.Core.Enums;
using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;
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

                bool loadedFromNewApi = await TryLoadFromNewApisAsync();
                if (!loadedFromNewApi)
                {
                    await LoadFromLegacyUsersApiAsync();
                    ErrorMessage = "Running with legacy GraphQL schema. KPI ranking API is unavailable.";
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

        public async Task<bool> CreateUserAsync(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ErrorMessage = "Please enter both username and password.";
                return false;
            }

            IsBusy = true;
            ErrorMessage = string.Empty;

            const string mutation = @"mutation CreateNewUser($user: String!, $pass: String!, $role: UserRole!) {
                createUser(username: $user, password: $pass, role: $role) {
                    id
                    username
                    role
                }
            }";

            try
            {
                var variables = new
                {
                    user = username.Trim(),
                    pass = password,
                    role = "SALE"
                };

                LoginUser? createdUser = await _graphQLClient.ExecuteAsync<LoginUser>(mutation, variables, dataKey: "createUser");
                if (createdUser is null)
                {
                    ErrorMessage = "Unable to create user.";
                    return false;
                }

                await LoadAsync();
                return true;
            }
            catch (Exception ex)
            {
                ErrorMessage = "Unable to create user: " + ex.Message;
                return false;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task<bool> TryLoadFromNewApisAsync()
        {
            const string query = @"query {
                getUserList {
                    id
                    username
                    role
                }

                users {
                    id
                    passwordHash
                }

                getSaleKpiRanking {
                    saleId
                    totalOrders
                    totalRevenue
                    rank
                }
            }";

            try
            {
                var response = await _graphQLClient.ExecuteAsync<UserManagementQueryData>(query)
                    ?? new UserManagementQueryData();

                var passwordByUserId = response.Users
                    .GroupBy(u => u.Id)
                    .ToDictionary(g => g.Key, g => g.First().PasswordHash);

                var rankingBySaleId = response.GetSaleKpiRanking
                    .GroupBy(r => r.SaleId)
                    .ToDictionary(g => g.Key, g => g.First());

                _allUsers.Clear();
                foreach (var item in response.GetUserList)
                {
                    passwordByUserId.TryGetValue(item.Id, out var passwordHash);
                    rankingBySaleId.TryGetValue(item.Id, out var ranking);

                    var role = ParseRole(item.Role);

                    int kpi = role == UserRole.Sale
                        ? ranking?.TotalOrders ?? 0
                        : 0;

                    _allUsers.Add(new UserItemViewModel(item.Id, item.Username, passwordHash ?? string.Empty, role, kpi));
                }

                return true;
            }
            catch (Exception ex) when (IsMissingSchemaFieldError(ex.Message))
            {
                return false;
            }
        }

        private async Task LoadFromLegacyUsersApiAsync()
        {
            const string legacyQuery = @"query {
                users {
                    id
                    username
                    passwordHash
                    role
                }
            }";

            var users = await _graphQLClient.ExecuteAsync<List<LegacyUserDto>>(legacyQuery, dataKey: "users")
                ?? new List<LegacyUserDto>();

            _allUsers.Clear();
            foreach (var user in users)
            {
                _allUsers.Add(new UserItemViewModel(user.Id, user.Username, user.PasswordHash, ParseRole(user.Role), 0));
            }
        }

        private static bool IsMissingSchemaFieldError(string message)
        {
            return message.Contains("does not exist on the type", StringComparison.OrdinalIgnoreCase);
        }

        private static UserRole ParseRole(string role)
        {
            if (Enum.TryParse<UserRole>(role, ignoreCase: true, out var parsedRole))
            {
                return parsedRole;
            }

            return UserRole.Sale;
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
            string selectedFilter = string.IsNullOrWhiteSpace(SelectedNameFilter)
                ? "All"
                : SelectedNameFilter;

            if (!string.Equals(selectedFilter, "All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(u => u.Username.StartsWith(selectedFilter, StringComparison.OrdinalIgnoreCase));
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

    public sealed class UserListItemDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }

    public sealed class UserPasswordDto
    {
        public int Id { get; set; }
        public string PasswordHash { get; set; } = string.Empty;
    }

    public sealed class SaleKpiRankingDto
    {
        public int SaleId { get; set; }
        public int TotalOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        public int Rank { get; set; }
    }

    public sealed class UserManagementQueryData
    {
        public List<UserListItemDto> GetUserList { get; set; } = new();
        public List<UserPasswordDto> Users { get; set; } = new();
        public List<SaleKpiRankingDto> GetSaleKpiRanking { get; set; } = new();
    }

    public sealed class LegacyUserDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
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

        public UserItemViewModel(int id, string username, string passwordHash, UserRole role, int kpi)
        {
            Id = id;
            Username = username;
            PasswordHash = passwordHash;
            Role = role;
            Kpi = kpi;
            Initials = BuildInitials(username);
        }

        public string PasswordDisplay => IsPasswordVisible ? PasswordHash : "........";

        partial void OnIsPasswordVisibleChanged(bool value)
        {
            OnPropertyChanged(nameof(PasswordDisplay));
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
