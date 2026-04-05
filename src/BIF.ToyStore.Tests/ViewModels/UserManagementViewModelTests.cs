using BIF.ToyStore.Core.Enums;
using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.ViewModels.Pages;
using BIF.ToyStore.ViewModels.Utils;
using Moq;

namespace BIF.ToyStore.Tests.ViewModels.Pages
{
    public class UserManagementViewModelTests
    {
        private readonly Mock<IGraphQLClient> _graphQLClientMock;
        private readonly Mock<ILocalSettingsService> _localSettingsServiceMock;
        private readonly UserManagementViewModel _viewModel;

        public UserManagementViewModelTests()
        {
            _graphQLClientMock = new Mock<IGraphQLClient>();
            _localSettingsServiceMock = new Mock<ILocalSettingsService>();
            _localSettingsServiceMock
                .Setup(x => x.GetInt(AppPreferenceKeys.ProductsItemsPerPage, 20))
                .Returns(20);
            _viewModel = new UserManagementViewModel(_graphQLClientMock.Object, _localSettingsServiceMock.Object);
        }

        [Fact]
        public void Constructor_InitializesTitleAndFilters()
        {
            Assert.Equal("Staff Directory", _viewModel.Title);
            Assert.Equal("All", _viewModel.SelectedNameFilter);
            Assert.Equal(27, _viewModel.NameFilters.Count);
            Assert.Equal("A", _viewModel.NameFilters[1]);
            Assert.Equal("Z", _viewModel.NameFilters[^1]);
        }

        [Fact]
        public async Task LoadAsync_NewApi_PopulatesUsersAndKpisInSortedOrder()
        {
            _graphQLClientMock
                .Setup(x => x.ExecuteAsync<UserManagementQueryData>(
                    It.Is<string>(q => q.Contains("usersConnection")),
                    It.IsAny<object>(),
                    ""))
                .ReturnsAsync(new UserManagementQueryData
                {
                    UsersConnection = new UserConnection
                    {
                        TotalCount = 3,
                        PageInfo = new UserPageInfo
                        {
                            HasNextPage = false,
                            HasPreviousPage = false,
                            StartCursor = "cursor-start",
                            EndCursor = "cursor-end"
                        },
                        Nodes = new List<UserListItemDto>
                        {
                            new() { Id = 1, Username = "alex", Role = "Sale" },
                            new() { Id = 2, Username = "zoe", Role = "Admin" },
                            new() { Id = 3, Username = "amy", Role = "UnknownRole" }
                        }
                    },
                    Users = new List<UserPasswordDto>
                    {
                        new() { Id = 1, PasswordHash = "enc-1", Role = "Sale" },
                        new() { Id = 2, PasswordHash = "enc-2", Role = "Admin" },
                        new() { Id = 3, PasswordHash = "enc-3", Role = "UnknownRole" }
                    },
                    GetSaleKpiRanking = new List<SaleKpiRankingDto>
                    {
                        new() { SaleId = 1, TotalOrders = 95 },
                        new() { SaleId = 3, TotalOrders = 81 }
                    }
                });

            await _viewModel.LoadAsync();

            Assert.Equal(string.Empty, _viewModel.ErrorMessage);
            Assert.Equal(3, _viewModel.TotalStaff);
            Assert.Equal(1, _viewModel.Admins);
            Assert.Equal(2, _viewModel.ActiveSessions);
            Assert.False(_viewModel.HasNextPage);
            Assert.False(_viewModel.HasPreviousPage);
            Assert.Equal("cursor-end", _viewModel.AfterCursor);

            Assert.Equal(3, _viewModel.VisibleUsers.Count);
            Assert.Equal("alex", _viewModel.VisibleUsers[0].Username);
            Assert.Equal(95, _viewModel.VisibleUsers[0].Kpi);
            Assert.Equal("amy", _viewModel.VisibleUsers[1].Username);
            Assert.Equal(UserRole.Sale, _viewModel.VisibleUsers[1].Role);
            Assert.Equal("zoe", _viewModel.VisibleUsers[2].Username);
            Assert.Equal(0, _viewModel.VisibleUsers[2].Kpi);
        }

        [Fact]
        public async Task LoadAsync_MissingSchema_FallsBackToLegacyAndSetsWarning()
        {
            _graphQLClientMock
                .Setup(x => x.ExecuteAsync<UserManagementQueryData>(
                    It.IsAny<string>(),
                    It.IsAny<object>(),
                    ""))
                .ThrowsAsync(new Exception("Field 'getSaleKpiRanking' does not exist on the type 'Query'."));

            _graphQLClientMock
                .Setup(x => x.ExecuteAsync<List<LegacyUserDto>>(
                    It.Is<string>(q => q.Contains("users")),
                    null,
                    "users"))
                .ReturnsAsync(new List<LegacyUserDto>
                {
                    new() { Id = 7, Username = "legacy.admin", PasswordHash = "hash-7", Role = "Admin" },
                    new() { Id = 8, Username = "legacy.sale", PasswordHash = "hash-8", Role = "Sale" }
                });

            await _viewModel.LoadAsync();

            Assert.Equal("Running with legacy GraphQL schema. KPI ranking API is unavailable.", _viewModel.ErrorMessage);
            Assert.Equal(2, _viewModel.VisibleUsers.Count);
            Assert.All(_viewModel.VisibleUsers, x => Assert.Equal(0, x.Kpi));
        }

        [Fact]
        public async Task LoadAsync_LegacyMode_PaginatesByPageSize()
        {
            _graphQLClientMock
                .Setup(x => x.ExecuteAsync<UserManagementQueryData>(
                    It.IsAny<string>(),
                    It.IsAny<object>(),
                    ""))
                .ThrowsAsync(new Exception("Field 'usersConnection' does not exist on the type 'Query'."));

            var legacyUsers = Enumerable.Range(1, 22)
                .Select(i => new LegacyUserDto
                {
                    Id = i,
                    Username = $"legacy.user{i:D2}",
                    PasswordHash = $"hash-{i}",
                    Role = "Sale"
                })
                .ToList();

            _graphQLClientMock
                .Setup(x => x.ExecuteAsync<List<LegacyUserDto>>(
                    It.Is<string>(q => q.Contains("users")),
                    null,
                    "users"))
                .ReturnsAsync(legacyUsers);

            await _viewModel.LoadAsync();

            Assert.Equal(22, _viewModel.TotalCount);
            Assert.Equal(20, _viewModel.VisibleUsers.Count);
            Assert.True(_viewModel.HasNextPage);
            Assert.False(_viewModel.HasPreviousPage);

            await _viewModel.NextPageCommand.ExecuteAsync(null);

            Assert.Equal(2, _viewModel.VisibleUsers.Count);
            Assert.False(_viewModel.HasNextPage);
            Assert.True(_viewModel.HasPreviousPage);
        }

        [Fact]
        public async Task LoadAsync_NewApiFailure_ClearsStateAndSetsError()
        {
            _graphQLClientMock
                .Setup(x => x.ExecuteAsync<UserManagementQueryData>(
                    It.IsAny<string>(),
                    It.IsAny<object>(),
                    ""))
                .ThrowsAsync(new Exception("network down"));

            await _viewModel.LoadAsync();

            Assert.StartsWith("Unable to load users: network down", _viewModel.ErrorMessage);
            Assert.Empty(_viewModel.VisibleUsers);
            Assert.Equal(0, _viewModel.TotalStaff);
            Assert.Equal(0, _viewModel.Admins);
            Assert.Equal(0, _viewModel.ActiveSessions);

            _graphQLClientMock.Verify(
                x => x.ExecuteAsync<List<LegacyUserDto>>(It.IsAny<string>(), It.IsAny<object>(), "users"),
                Times.Never);
        }

        [Fact]
        public async Task SelectedNameFilter_AppliesPrefixFilter()
        {
            _graphQLClientMock
                .Setup(x => x.ExecuteAsync<UserManagementQueryData>(
                    It.Is<string>(q => q.Contains("usersConnection")),
                    It.IsAny<object>(),
                    ""))
                .ReturnsAsync(new UserManagementQueryData
                {
                    UsersConnection = new UserConnection
                    {
                        TotalCount = 2,
                        PageInfo = new UserPageInfo(),
                        Nodes = new List<UserListItemDto>
                        {
                            new() { Id = 1, Username = "anna", Role = "Sale" },
                            new() { Id = 3, Username = "adam", Role = "Admin" }
                        }
                    }
                });

            await _viewModel.LoadAsync();
            _viewModel.SelectedNameFilter = "A";
            await _viewModel.ApplyFilterCommand.ExecuteAsync(null);

            Assert.Equal(2, _viewModel.VisibleUsers.Count);
            Assert.All(_viewModel.VisibleUsers, u => Assert.StartsWith("a", u.Username, StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void TogglePasswordCommand_TogglesPasswordVisibilityAndDisplay()
        {
            var user = new UserItemViewModel(1, "sale1", "encrypted", UserRole.Sale, 0);

            Assert.False(user.IsPasswordVisible);
            Assert.Equal("........", user.PasswordDisplay);

            _viewModel.TogglePasswordCommand.Execute(user);

            Assert.True(user.IsPasswordVisible);
            Assert.Equal("encrypted", user.PasswordDisplay);

            _viewModel.TogglePasswordCommand.Execute(user);

            Assert.False(user.IsPasswordVisible);
            Assert.Equal("........", user.PasswordDisplay);
        }

        [Fact]
        public void TogglePasswordCommand_NullInput_DoesNotThrow()
        {
            _viewModel.TogglePasswordCommand.Execute(null);
        }

        [Fact]
        public async Task CreateUserAsync_ValidInput_CallsMutationAndRefreshesList()
        {
            _graphQLClientMock
                .Setup(x => x.ExecuteAsync<LoginUser>(
                    It.Is<string>(q => q.Contains("mutation CreateNewUser")),
                    It.IsAny<object>(),
                    "createUser"))
                .ReturnsAsync(new LoginUser
                {
                    Id = 50,
                    Username = "new.sale",
                    Role = UserRole.Sale
                });

            _graphQLClientMock
                .Setup(x => x.ExecuteAsync<UserManagementQueryData>(
                    It.Is<string>(q => q.Contains("usersConnection")),
                    It.IsAny<object>(),
                    ""))
                .ReturnsAsync(new UserManagementQueryData
                {
                    UsersConnection = new UserConnection
                    {
                        TotalCount = 1,
                        Nodes = new List<UserListItemDto>
                        {
                            new() { Id = 50, Username = "new.sale", Role = "Sale" }
                        }
                    },
                    Users = new List<UserPasswordDto>
                    {
                        new() { Id = 50, PasswordHash = "enc-50", Role = "Sale" }
                    }
                });

            bool created = await _viewModel.CreateUserAsync("new.sale", "123456");

            Assert.True(created);
            Assert.Single(_viewModel.VisibleUsers);
            Assert.Equal("new.sale", _viewModel.VisibleUsers[0].Username);

            _graphQLClientMock.Verify(
                x => x.ExecuteAsync<LoginUser>(It.IsAny<string>(), It.IsAny<object>(), "createUser"),
                Times.Once);
            _graphQLClientMock.Verify(
                x => x.ExecuteAsync<UserManagementQueryData>(It.IsAny<string>(), It.IsAny<object>(), ""),
                Times.Once);
        }

        [Fact]
        public async Task CreateUserAsync_MissingCredentials_ReturnsFalseAndSkipsApi()
        {
            bool created = await _viewModel.CreateUserAsync("", "");

            Assert.False(created);
            Assert.Equal("Please enter both username and password.", _viewModel.ErrorMessage);

            _graphQLClientMock.Verify(
                x => x.ExecuteAsync<LoginUser>(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task DeleteUserAsync_ValidUser_CallsMutationAndRefreshesList()
        {
            _graphQLClientMock
                .Setup(x => x.ExecuteAsync<bool>(
                    It.Is<string>(q => q.Contains("mutation DeleteExistingUser")),
                    It.IsAny<object>(),
                    "deleteUser"))
                .ReturnsAsync(true);

            _graphQLClientMock
                .Setup(x => x.ExecuteAsync<UserManagementQueryData>(
                    It.Is<string>(q => q.Contains("usersConnection")),
                    It.IsAny<object>(),
                    ""))
                .ReturnsAsync(new UserManagementQueryData());

            bool deleted = await _viewModel.DeleteUserAsync(new UserItemViewModel(9, "remove.me", "hash", UserRole.Sale, 0));

            Assert.True(deleted);
            _graphQLClientMock.Verify(
                x => x.ExecuteAsync<bool>(It.IsAny<string>(), It.IsAny<object>(), "deleteUser"),
                Times.Once);
            _graphQLClientMock.Verify(
                x => x.ExecuteAsync<UserManagementQueryData>(It.IsAny<string>(), It.IsAny<object>(), ""),
                Times.Once);
        }

        [Fact]
        public async Task DeleteUserAsync_NullUser_ReturnsFalseAndSkipsApi()
        {
            bool deleted = await _viewModel.DeleteUserAsync(null);

            Assert.False(deleted);
            _graphQLClientMock.Verify(
                x => x.ExecuteAsync<bool>(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task UpdateUserAsync_ValidInput_CallsMutationAndRefreshesList()
        {
            _graphQLClientMock
                .Setup(x => x.ExecuteAsync<LoginUser>(
                    It.Is<string>(q => q.Contains("mutation UpdateExistingUser")),
                    It.IsAny<object>(),
                    "updateUser"))
                .ReturnsAsync(new LoginUser
                {
                    Id = 9,
                    Username = "updated.user",
                    Role = UserRole.Sale
                });

            _graphQLClientMock
                .Setup(x => x.ExecuteAsync<UserManagementQueryData>(
                    It.Is<string>(q => q.Contains("usersConnection")),
                    It.IsAny<object>(),
                    ""))
                .ReturnsAsync(new UserManagementQueryData
                {
                    UsersConnection = new UserConnection
                    {
                        TotalCount = 1,
                        Nodes = new List<UserListItemDto>
                        {
                            new() { Id = 9, Username = "updated.user", Role = "Sale" }
                        }
                    },
                    Users = new List<UserPasswordDto>
                    {
                        new() { Id = 9, PasswordHash = "enc-9", Role = "Sale" }
                    }
                });

            bool updated = await _viewModel.UpdateUserAsync(
                new UserItemViewModel(9, "old.user", "oldHash", UserRole.Sale, 0),
                "updated.user",
                "newpass");

            Assert.True(updated);
            Assert.Single(_viewModel.VisibleUsers);
            Assert.Equal("updated.user", _viewModel.VisibleUsers[0].Username);

            _graphQLClientMock.Verify(
                x => x.ExecuteAsync<LoginUser>(It.IsAny<string>(), It.IsAny<object>(), "updateUser"),
                Times.Once);
            _graphQLClientMock.Verify(
                x => x.ExecuteAsync<UserManagementQueryData>(It.IsAny<string>(), It.IsAny<object>(), ""),
                Times.Once);
        }

        [Fact]
        public async Task UpdateUserAsync_InvalidInput_ReturnsFalseAndSkipsApi()
        {
            bool updated = await _viewModel.UpdateUserAsync(
                new UserItemViewModel(9, "old.user", "oldHash", UserRole.Sale, 0),
                "",
                "");

            Assert.False(updated);
            Assert.Equal("Please enter both username and password.", _viewModel.ErrorMessage);

            _graphQLClientMock.Verify(
                x => x.ExecuteAsync<LoginUser>(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>()),
                Times.Never);
        }
    }
}
