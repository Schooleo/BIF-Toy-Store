using BIF.ToyStore.Core.Enums;
using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.ViewModels.Pages;
using CommunityToolkit.Mvvm.Messaging;
using Moq;

namespace BIF.ToyStore.Tests.ViewModels.Pages
{
    public class LoginViewModelTests
    {
        private readonly Mock<IGraphQLClient> _graphQLClientMock;
        private readonly Mock<ICredentialVaultService> _credentialVaultServiceMock;
        private readonly Mock<ILocalSettingsService> _localSettingsServiceMock;
        private readonly Mock<IAppInfoService> _appInfoServiceMock;
        private readonly Mock<IMessenger> _messengerMock;
        private readonly LoginViewModel _viewModel;

        // GraphQL mutation query constant – must match what LoginViewModel sends
        private const string LoginMutation =
            @"mutation PerformLogin($user: String!, $pass: String!) {
                login(username: $user, password: $pass) {
                    id
                    username
                    role
                }
            }";

        public LoginViewModelTests()
        {
            _graphQLClientMock = new Mock<IGraphQLClient>();
            _credentialVaultServiceMock = new Mock<ICredentialVaultService>();
            _localSettingsServiceMock = new Mock<ILocalSettingsService>();
            _appInfoServiceMock = new Mock<IAppInfoService>();
            _messengerMock = new Mock<IMessenger>();

            _localSettingsServiceMock
                .Setup(x => x.GetString("LastUsername", It.IsAny<string>()))
                .Returns(string.Empty);
            _localSettingsServiceMock
                .Setup(x => x.GetString("LastActiveRoute", "Dashboard"))
                .Returns("Dashboard");
            _appInfoServiceMock
                .Setup(x => x.GetAppVersion())
                .Returns("Version 1.0.0.0");

            _viewModel = new LoginViewModel(
                _graphQLClientMock.Object,
                _credentialVaultServiceMock.Object,
                _localSettingsServiceMock.Object,
                _appInfoServiceMock.Object,
                _messengerMock.Object);
        }

        // ─── Constructor defaults ────────────────────────────────────────────────

        [Fact]
        public void Constructor_SetsCorrectTitle()
        {
            Assert.Equal("Login - BIF Toy Store POS", _viewModel.Title);
        }

        [Fact]
        public void Constructor_DefaultUsername_IsEmpty()
        {
            Assert.Equal(string.Empty, _viewModel.Username);
        }

        [Fact]
        public void Constructor_DefaultPassword_IsEmpty()
        {
            Assert.Equal(string.Empty, _viewModel.Password);
        }

        [Fact]
        public void Constructor_DefaultErrorMessage_IsEmpty()
        {
            Assert.Equal(string.Empty, _viewModel.ErrorMessage);
        }

        [Fact]
        public void Constructor_DefaultIsBusy_IsFalse()
        {
            Assert.False(_viewModel.IsBusy);
        }

        // ─── Empty / whitespace input validation ─────────────────────────────────

        [Fact]
        public async Task LoginAsync_EmptyUsername_SetsValidationError()
        {
            // Arrange
            _viewModel.Username = "";
            _viewModel.Password = "password123";

            // Act
            await _viewModel.LoginCommand.ExecuteAsync(null);

            // Assert
            Assert.Equal("Please enter both username and password.", _viewModel.ErrorMessage);
            Assert.False(_viewModel.IsBusy);
        }

        [Fact]
        public async Task LoginAsync_EmptyPassword_SetsValidationError()
        {
            // Arrange
            _viewModel.Username = "validUser";
            _viewModel.Password = "";

            // Act
            await _viewModel.LoginCommand.ExecuteAsync(null);

            // Assert
            Assert.Equal("Please enter both username and password.", _viewModel.ErrorMessage);
            Assert.False(_viewModel.IsBusy);
        }

        [Fact]
        public async Task LoginAsync_BothEmpty_SetsValidationError()
        {
            // Arrange
            _viewModel.Username = "";
            _viewModel.Password = "";

            // Act
            await _viewModel.LoginCommand.ExecuteAsync(null);

            // Assert
            Assert.Equal("Please enter both username and password.", _viewModel.ErrorMessage);
            Assert.False(_viewModel.IsBusy);
        }

        [Fact]
        public async Task LoginAsync_WhitespaceUsername_SetsValidationError()
        {
            // Arrange
            _viewModel.Username = "   ";
            _viewModel.Password = "password123";

            // Act
            await _viewModel.LoginCommand.ExecuteAsync(null);

            // Assert
            Assert.Equal("Please enter both username and password.", _viewModel.ErrorMessage);
        }

        [Fact]
        public async Task LoginAsync_WhitespacePassword_SetsValidationError()
        {
            // Arrange
            _viewModel.Username = "validUser";
            _viewModel.Password = "   ";

            // Act
            await _viewModel.LoginCommand.ExecuteAsync(null);

            // Assert
            Assert.Equal("Please enter both username and password.", _viewModel.ErrorMessage);
        }

        [Fact]
        public async Task LoginAsync_ValidationFailure_DoesNotCallGraphQL()
        {
            // Arrange
            _viewModel.Username = "";
            _viewModel.Password = "";

            // Act
            await _viewModel.LoginCommand.ExecuteAsync(null);

            // Assert – GraphQL must never be called on invalid input
            _graphQLClientMock.Verify(
                x => x.ExecuteAsync<LoginUser>(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>()),
                Times.Never);
        }

        // ─── Successful login ────────────────────────────────────────────────────

        [Fact]
        public async Task LoginAsync_ValidCredentials_AdminRole_SetsSuccessMessage()
        {
            // Arrange
            _viewModel.Username = "adminUser";
            _viewModel.Password = "adminPass";

            var user = new LoginUser { Id = 1, Username = "adminUser", Role = UserRole.Admin };
            _graphQLClientMock
                .Setup(x => x.ExecuteAsync<LoginUser>(It.IsAny<string>(), It.IsAny<object>(), "login"))
                .ReturnsAsync(user);

            // Act
            await _viewModel.LoginCommand.ExecuteAsync(null);

            // Assert
            Assert.Contains("Login successful", _viewModel.ErrorMessage);
            Assert.False(_viewModel.IsBusy);
        }

        [Fact]
        public async Task LoginAsync_ValidCredentials_SaleRole_SetsSuccessMessage()
        {
            // Arrange
            _viewModel.Username = "saleUser";
            _viewModel.Password = "salePass";

            var user = new LoginUser { Id = 2, Username = "saleUser", Role = UserRole.Sale };
            _graphQLClientMock
                .Setup(x => x.ExecuteAsync<LoginUser>(It.IsAny<string>(), It.IsAny<object>(), "login"))
                .ReturnsAsync(user);

            // Act
            await _viewModel.LoginCommand.ExecuteAsync(null);

            // Assert
            Assert.Contains("Login successful", _viewModel.ErrorMessage);
            Assert.False(_viewModel.IsBusy);
        }

        [Fact]
        public async Task LoginAsync_ValidCredentials_CallsGraphQL_ExactlyOnce()
        {
            // Arrange
            _viewModel.Username = "validUser";
            _viewModel.Password = "validPass";

            _graphQLClientMock
                .Setup(x => x.ExecuteAsync<LoginUser>(It.IsAny<string>(), It.IsAny<object>(), "login"))
                .ReturnsAsync(new LoginUser { Username = "validUser" });

            // Act
            await _viewModel.LoginCommand.ExecuteAsync(null);

            // Assert
            _graphQLClientMock.Verify(
                x => x.ExecuteAsync<LoginUser>(It.IsAny<string>(), It.IsAny<object>(), "login"),
                Times.Once);
        }

        // ─── Failed login ─────────────────────────────────────────────────────────

        [Fact]
        public async Task LoginAsync_InvalidCredentials_SetsErrorMessage()
        {
            // Arrange
            _viewModel.Username = "invalidUser";
            _viewModel.Password = "invalidPass";

            _graphQLClientMock
                .Setup(x => x.ExecuteAsync<LoginUser>(It.IsAny<string>(), It.IsAny<object>(), "login"))
                .ReturnsAsync((LoginUser?)null);

            // Act
            await _viewModel.LoginCommand.ExecuteAsync(null);

            // Assert
            Assert.Equal("Invalid username or password.", _viewModel.ErrorMessage);
            Assert.False(_viewModel.IsBusy);
        }

        // ─── Exception handling ───────────────────────────────────────────────────

        [Fact]
        public async Task LoginAsync_GraphQLThrowsException_SetsConnectionError()
        {
            // Arrange
            _viewModel.Username = "validUser";
            _viewModel.Password = "validPass";

            _graphQLClientMock
                .Setup(x => x.ExecuteAsync<LoginUser>(It.IsAny<string>(), It.IsAny<object>(), "login"))
                .ThrowsAsync(new Exception("Server unavailable"));

            // Act
            await _viewModel.LoginCommand.ExecuteAsync(null);

            // Assert
            Assert.StartsWith("Connection Error:", _viewModel.ErrorMessage);
            Assert.Contains("Server unavailable", _viewModel.ErrorMessage);
            Assert.False(_viewModel.IsBusy);
        }

        [Fact]
        public async Task LoginAsync_GraphQLThrowsException_IsBusy_ResetToFalse()
        {
            // Arrange
            _viewModel.Username = "validUser";
            _viewModel.Password = "validPass";

            _graphQLClientMock
                .Setup(x => x.ExecuteAsync<LoginUser>(It.IsAny<string>(), It.IsAny<object>(), "login"))
                .ThrowsAsync(new HttpRequestException("Network failure"));

            // Act
            await _viewModel.LoginCommand.ExecuteAsync(null);

            // Assert – finally block must always clear IsBusy
            Assert.False(_viewModel.IsBusy);
        }

        // ─── IsBusy state management ──────────────────────────────────────────────

        [Fact]
        public async Task LoginAsync_AfterSuccessfulLogin_IsBusyResetToFalse()
        {
            // Arrange
            _viewModel.Username = "validUser";
            _viewModel.Password = "validPass";

            _graphQLClientMock
                .Setup(x => x.ExecuteAsync<LoginUser>(It.IsAny<string>(), It.IsAny<object>(), "login"))
                .ReturnsAsync(new LoginUser { Username = "validUser" });

            // Act
            await _viewModel.LoginCommand.ExecuteAsync(null);

            // Assert
            Assert.False(_viewModel.IsBusy);
        }

        [Fact]
        public async Task LoginAsync_AfterFailedLogin_IsBusyResetToFalse()
        {
            // Arrange
            _viewModel.Username = "validUser";
            _viewModel.Password = "validPass";

            _graphQLClientMock
                .Setup(x => x.ExecuteAsync<LoginUser>(It.IsAny<string>(), It.IsAny<object>(), "login"))
                .ReturnsAsync((LoginUser?)null);

            // Act
            await _viewModel.LoginCommand.ExecuteAsync(null);

            // Assert
            Assert.False(_viewModel.IsBusy);
        }

        [Fact]
        public async Task LoginAsync_ErrorMessageClearedBeforeAttempt()
        {
            // Arrange – set a pre-existing error to verify it gets cleared
            _viewModel.Username = "validUser";
            _viewModel.Password = "validPass";
            _viewModel.ErrorMessage = "Old error";

            _graphQLClientMock
                .Setup(x => x.ExecuteAsync<LoginUser>(It.IsAny<string>(), It.IsAny<object>(), "login"))
                .ReturnsAsync(new LoginUser { Username = "validUser" });

            // Act
            await _viewModel.LoginCommand.ExecuteAsync(null);

            // Assert – the old error is gone (replaced with the success message)
            Assert.DoesNotContain("Old error", _viewModel.ErrorMessage);
        }

        [Fact]
        public async Task TryAutoLoginAsync_NoStoredCredentials_DoesNothing()
        {
            _credentialVaultServiceMock
                .Setup(x => x.GetCredentials("BIF.ToyStore.POS"))
                .Returns(((string Username, string Password)?)null);

            await _viewModel.TryAutoLoginAsync();

            _graphQLClientMock.Verify(
                x => x.ExecuteAsync<LoginUser>(It.IsAny<string>(), It.IsAny<object>(), "login"),
                Times.Never);
        }

        [Fact]
        public async Task TryAutoLoginAsync_InvalidStoredCredentials_ClearsVaultEntry()
        {
            _credentialVaultServiceMock
                .Setup(x => x.GetCredentials("BIF.ToyStore.POS"))
                .Returns(("saved-user", "wrong-pass"));

            _graphQLClientMock
                .Setup(x => x.ExecuteAsync<LoginUser>(It.IsAny<string>(), It.IsAny<object>(), "login"))
                .ReturnsAsync((LoginUser?)null);

            await _viewModel.TryAutoLoginAsync();

            _credentialVaultServiceMock.Verify(
                x => x.ClearCredentials("BIF.ToyStore.POS"),
                Times.Once);
        }
    }
}
