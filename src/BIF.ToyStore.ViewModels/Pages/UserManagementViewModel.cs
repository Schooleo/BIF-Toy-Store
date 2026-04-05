using BIF.ToyStore.Core.Enums;
using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.ViewModels.Base;
using BIF.ToyStore.ViewModels.Utils;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace BIF.ToyStore.ViewModels.Pages
{
    public partial class UserManagementViewModel : PaginatedViewModel
    {
        private readonly IGraphQLClient _graphQLClient;
        private readonly List<UserItemViewModel> _legacyUsers = new();
        private bool _isLegacyMode;
        private int _legacyPageIndex;

        [ObservableProperty]
        private ObservableCollection<UserItemViewModel> _visibleUsers = new();

        [ObservableProperty]
        private ObservableCollection<string> _nameFilters = new();

        [ObservableProperty]
        private string _selectedNameFilter = "All";

        [ObservableProperty]
        private ObservableCollection<string> _roleFilters = new();

        [ObservableProperty]
        private string _selectedRoleFilter = "All Roles";

        [ObservableProperty]
        private int _totalStaff;

        [ObservableProperty]
        private int _admins;

        [ObservableProperty]
        private int _activeSessions;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        public UserManagementViewModel(IGraphQLClient graphQLClient, ILocalSettingsService localSettingsService)
        {
            _graphQLClient = graphQLClient;
            Title = "Staff Directory";
            PageSize = localSettingsService.GetInt(AppPreferenceKeys.ProductsItemsPerPage, 20);

            NameFilters = new ObservableCollection<string>(BuildNameFilters());
            RoleFilters = new ObservableCollection<string>(BuildRoleFilters());
        }

        public async Task LoadAsync(bool forceRefresh = false)
        {
            if (forceRefresh)
            {
                _legacyUsers.Clear();
                _legacyPageIndex = 0;
            }

            if (!forceRefresh && _isLegacyMode && _legacyUsers.Count > 0)
            {
                ApplyLegacySortAndFilterAndPage(direction: null, resetToFirstPage: false);
                return;
            }

            BeforeCursor = null;
            AfterCursor = null;
            await LoadPageAsync(null);
        }

        [RelayCommand]
        public async Task ApplyFilterAsync()
        {
            if (_isLegacyMode && _legacyUsers.Count > 0)
            {
                ApplyLegacySortAndFilterAndPage(direction: null, resetToFirstPage: true);
                return;
            }

            if (IsBusy)
            {
                return;
            }

            BeforeCursor = null;
            AfterCursor = null;
            await LoadPageAsync(null);
        }

        [RelayCommand]
        public async Task ClearFilterAsync()
        {
            SelectedNameFilter = "All";
            SelectedRoleFilter = "All Roles";

            if (_isLegacyMode && _legacyUsers.Count > 0)
            {
                ApplyLegacySortAndFilterAndPage(direction: null, resetToFirstPage: true);
                return;
            }

            BeforeCursor = null;
            AfterCursor = null;
            await LoadPageAsync(null);
        }

        protected override async Task LoadPageAsync(string? direction)
        {
            IsBusy = true;
            try
            {
                ErrorMessage = string.Empty;

                if (_isLegacyMode && _legacyUsers.Count > 0)
                {
                    ApplyLegacySortAndFilterAndPage(direction, resetToFirstPage: false);
                    return;
                }

                bool loadedFromNewApi = await TryLoadFromNewApisAsync(direction);
                if (!loadedFromNewApi)
                {
                    await LoadFromLegacyUsersApiAsync();
                    ApplyLegacySortAndFilterAndPage(direction, resetToFirstPage: true);
                    ErrorMessage = "Running with legacy GraphQL schema. KPI ranking API is unavailable.";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Unable to load users: " + ex.Message;
                _legacyUsers.Clear();
                VisibleUsers.Clear();
                ApplyPageInfo(0, false, false, null, null);
                TotalStaff = 0;
                Admins = 0;
                ActiveSessions = 0;
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task<bool> CreateUserAsync(string username, string password, UserRole role = UserRole.Sale)
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
                    role = role == UserRole.Admin ? "ADMIN" : "SALE"
                };

                LoginUser? createdUser = await _graphQLClient.ExecuteAsync<LoginUser>(mutation, variables, dataKey: "createUser");
                if (createdUser is null)
                {
                    ErrorMessage = "Unable to create user.";
                    return false;
                }

                await LoadAsync(forceRefresh: true);
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

        public async Task<bool> DeleteUserAsync(UserItemViewModel? user)
        {
            if (user is null)
            {
                return false;
            }

            IsBusy = true;
            ErrorMessage = string.Empty;

            const string mutation = @"mutation DeleteExistingUser($id: Int!) {
                deleteUser(id: $id)
            }";

            try
            {
                bool deleted = await _graphQLClient.ExecuteAsync<bool>(
                    mutation,
                    new { id = user.Id },
                    dataKey: "deleteUser");

                if (!deleted)
                {
                    ErrorMessage = "Unable to delete user.";
                    return false;
                }

                await LoadAsync(forceRefresh: true);
                return true;
            }
            catch (Exception ex)
            {
                ErrorMessage = "Unable to delete user: " + ex.Message;
                return false;
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task<bool> UpdateUserAsync(UserItemViewModel? user, string username, string password)
        {
            if (user is null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ErrorMessage = "Please enter both username and password.";
                return false;
            }

            IsBusy = true;
            ErrorMessage = string.Empty;

            const string mutation = @"mutation UpdateExistingUser($id: Int!, $user: String!, $pass: String!) {
                updateUser(id: $id, username: $user, password: $pass) {
                    id
                    username
                    role
                }
            }";

            try
            {
                var variables = new
                {
                    id = user.Id,
                    user = username.Trim(),
                    pass = password
                };

                LoginUser? updatedUser = await _graphQLClient.ExecuteAsync<LoginUser>(
                    mutation,
                    variables,
                    dataKey: "updateUser");

                if (updatedUser is null)
                {
                    ErrorMessage = "Unable to update user.";
                    return false;
                }

                await LoadAsync(forceRefresh: true);
                return true;
            }
            catch (Exception ex)
            {
                ErrorMessage = "Unable to update user: " + ex.Message;
                return false;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task<bool> TryLoadFromNewApisAsync(string? direction)
        {
            const string query = @"query GetUsers(
                $first: Int, $last: Int, $after: String, $before: String,
                $where: UserFilterInput, $order: [UserSortInput!]
            ) {
                usersConnection(
                    first: $first,
                    last: $last,
                    after: $after,
                    before: $before,
                    where: $where,
                    order: $order
                ) {
                    totalCount
                    pageInfo {
                        hasNextPage
                        hasPreviousPage
                        startCursor
                        endCursor
                    }
                    nodes {
                        id
                        username
                        role
                    }
                }

                users {
                    id
                    passwordHash
                    role
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
                int? firstVar = null;
                int? lastVar = null;
                string? afterVar = null;
                string? beforeVar = null;

                if (direction == "next" && !string.IsNullOrEmpty(AfterCursor))
                {
                    firstVar = PageSize;
                    afterVar = AfterCursor;
                }
                else if (direction == "prev" && !string.IsNullOrEmpty(BeforeCursor))
                {
                    lastVar = PageSize;
                    beforeVar = BeforeCursor;
                }
                else if (direction == "last")
                {
                    lastVar = PageSize;
                }
                else
                {
                    firstVar = PageSize;
                }

                string selectedFilter = string.IsNullOrWhiteSpace(SelectedNameFilter)
                    ? "All"
                    : SelectedNameFilter;

                object? whereClause = string.Equals(selectedFilter, "All", StringComparison.OrdinalIgnoreCase)
                    ? null
                    : new { username = new { startsWith = selectedFilter } };

                object orderClause = new object[]
                {
                    new { username = "ASC" },
                    new { id = "ASC" }
                };

                var variables = new
                {
                    first = firstVar,
                    last = lastVar,
                    after = afterVar,
                    before = beforeVar,
                    where = whereClause,
                    order = orderClause
                };

                var response = await _graphQLClient.ExecuteAsync<UserManagementQueryData>(query, variables)
                    ?? new UserManagementQueryData();

                var usersConnection = response.UsersConnection ?? new UserConnection();

                var passwordByUserId = response.Users
                    .GroupBy(u => u.Id)
                    .ToDictionary(g => g.Key, g => g.First().PasswordHash);

                var rankingBySaleId = response.GetSaleKpiRanking
                    .GroupBy(r => r.SaleId)
                    .ToDictionary(g => g.Key, g => g.First());

                var mappedUsers = (usersConnection.Nodes ?? new List<UserListItemDto>())
                    .Select(item =>
                    {
                        passwordByUserId.TryGetValue(item.Id, out var passwordHash);
                        rankingBySaleId.TryGetValue(item.Id, out var ranking);

                        var role = ParseRole(item.Role);
                        int kpi = role == UserRole.Sale ? ranking?.TotalOrders ?? 0 : 0;

                        return new UserItemViewModel(item.Id, item.Username, passwordHash ?? string.Empty, role, kpi);
                    })
                    .OrderByDescending(x => x.Kpi)
                    .ThenBy(x => x.Username, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                string selectedRole = string.IsNullOrWhiteSpace(SelectedRoleFilter)
                    ? "All Roles"
                    : SelectedRoleFilter;

                if (!string.Equals(selectedRole, "All Roles", StringComparison.OrdinalIgnoreCase))
                {
                    mappedUsers = mappedUsers
                        .Where(x => MatchesRoleFilter(x, selectedRole))
                        .ToList();
                }

                VisibleUsers = new ObservableCollection<UserItemViewModel>(mappedUsers);

                _isLegacyMode = false;
                _legacyUsers.Clear();

                TotalStaff = usersConnection.TotalCount;
                Admins = response.Users.Count(x => ParseRole(x.Role) == UserRole.Admin);
                ActiveSessions = response.GetSaleKpiRanking.Count(x => x.TotalOrders >= 80);

                bool hasRoleFilter = !string.Equals(selectedRole, "All Roles", StringComparison.OrdinalIgnoreCase);
                ApplyPageInfo(
                    hasRoleFilter ? mappedUsers.Count : usersConnection.TotalCount,
                    hasRoleFilter ? false : usersConnection.PageInfo?.HasNextPage ?? false,
                    hasRoleFilter ? false : usersConnection.PageInfo?.HasPreviousPage ?? false,
                    hasRoleFilter ? null : usersConnection.PageInfo?.StartCursor,
                    hasRoleFilter ? null : usersConnection.PageInfo?.EndCursor);

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

            _isLegacyMode = true;
            _legacyPageIndex = 0;
            _legacyUsers.Clear();
            foreach (var user in users)
            {
                _legacyUsers.Add(new UserItemViewModel(user.Id, user.Username, user.PasswordHash, ParseRole(user.Role), 0));
            }

            TotalStaff = _legacyUsers.Count;
            Admins = _legacyUsers.Count(x => x.Role == UserRole.Admin);
            ActiveSessions = 0;
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

        private void ApplyLegacySortAndFilterAndPage(string? direction, bool resetToFirstPage)
        {
            IEnumerable<UserItemViewModel> query = _legacyUsers;
            string selectedFilter = string.IsNullOrWhiteSpace(SelectedNameFilter)
                ? "All"
                : SelectedNameFilter;

            if (!string.Equals(selectedFilter, "All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(u => u.Username.StartsWith(selectedFilter, StringComparison.OrdinalIgnoreCase));
            }

            string selectedRoleFilter = string.IsNullOrWhiteSpace(SelectedRoleFilter)
                ? "All Roles"
                : SelectedRoleFilter;

            if (!string.Equals(selectedRoleFilter, "All Roles", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(u => MatchesRoleFilter(u, selectedRoleFilter));
            }

            var result = query
                .OrderByDescending(u => u.Kpi)
                .ThenBy(u => u.Username, StringComparer.OrdinalIgnoreCase)
                .ToList();

            int totalCount = result.Count;
            int safePageSize = Math.Max(1, PageSize);
            int totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / safePageSize);

            if (resetToFirstPage)
            {
                _legacyPageIndex = 0;
            }
            else
            {
                if (direction == "next" && _legacyPageIndex < totalPages - 1)
                {
                    _legacyPageIndex++;
                }
                else if (direction == "prev" && _legacyPageIndex > 0)
                {
                    _legacyPageIndex--;
                }
                else if (direction == "last")
                {
                    _legacyPageIndex = Math.Max(totalPages - 1, 0);
                }
            }

            if (totalPages == 0)
            {
                _legacyPageIndex = 0;
            }
            else
            {
                _legacyPageIndex = Math.Clamp(_legacyPageIndex, 0, totalPages - 1);
            }

            var pagedResult = result
                .Skip(_legacyPageIndex * safePageSize)
                .Take(safePageSize)
                .ToList();

            VisibleUsers.Clear();
            foreach (var item in pagedResult)
            {
                VisibleUsers.Add(item);
            }

            bool hasPrevious = totalPages > 0 && _legacyPageIndex > 0;
            bool hasNext = totalPages > 0 && _legacyPageIndex < totalPages - 1;

            ApplyPageInfo(totalCount, hasNext, hasPrevious, null, null);
        }

        private static IEnumerable<string> BuildNameFilters()
        {
            yield return "All";
            for (char c = 'A'; c <= 'Z'; c++)
            {
                yield return c.ToString();
            }
        }

        private static IEnumerable<string> BuildRoleFilters()
        {
            yield return "All Roles";
            yield return "Admin";
            yield return "Sale";
        }

        private static bool MatchesRoleFilter(UserItemViewModel user, string selectedRole)
        {
            if (string.Equals(selectedRole, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                return user.Role == UserRole.Admin;
            }

            if (string.Equals(selectedRole, "Sale", StringComparison.OrdinalIgnoreCase))
            {
                return user.Role == UserRole.Sale;
            }

            return true;
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
        public string Role { get; set; } = string.Empty;
    }

    public sealed class UserConnection
    {
        public int TotalCount { get; set; }
        public UserPageInfo? PageInfo { get; set; }
        public List<UserListItemDto>? Nodes { get; set; }
    }

    public sealed class UserPageInfo
    {
        public bool HasNextPage { get; set; }
        public bool HasPreviousPage { get; set; }
        public string? StartCursor { get; set; }
        public string? EndCursor { get; set; }
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
        public UserConnection? UsersConnection { get; set; }
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
        public bool IsAdmin => Role == UserRole.Admin;
        public bool IsSale => Role == UserRole.Sale;

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
