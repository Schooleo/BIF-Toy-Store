using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;
using BIF.ToyStore.ViewModels.Pages;
using Moq;

namespace BIF.ToyStore.Tests.ViewModels.Pages
{
    public class LoginViewModelTests
    {
        private readonly Mock<IAuthService> _authServiceMock;
        private readonly LoginViewModel _viewModel;

        public LoginViewModelTests()
        {
            _authServiceMock = new Mock<IAuthService>();
            _viewModel = new LoginViewModel(_authServiceMock.Object);
        }

        [Fact]
        public async Task LoginAsync_EmptyUsernameOrPassword_SetsErrorMessage()
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
        public async Task LoginAsync_ValidCredentials_ClearsErrorMessage()
        {
            // Arrange
            _viewModel.Username = "validUser";
            _viewModel.Password = "validPass";
            
            var user = new User { Username = "validUser" };
            _authServiceMock.Setup(x => x.LoginAsync("validUser", "validPass"))
                            .ReturnsAsync(user);

            // Act
            await _viewModel.LoginCommand.ExecuteAsync(null);

            // Assert
            Assert.Empty(_viewModel.ErrorMessage);
            Assert.False(_viewModel.IsBusy);
            // Verify that the service was called exactly once with the expected parameters
            _authServiceMock.Verify(x => x.LoginAsync("validUser", "validPass"), Times.Once);
        }

        [Fact]
        public async Task LoginAsync_InvalidCredentials_SetsErrorMessage()
        {
            // Arrange
            _viewModel.Username = "invalidUser";
            _viewModel.Password = "invalidPass";

            _authServiceMock.Setup(x => x.LoginAsync("invalidUser", "invalidPass"))
                            .ReturnsAsync((User?)null);

            // Act
            await _viewModel.LoginCommand.ExecuteAsync(null);

            // Assert
            Assert.Equal("Invalid username or password.", _viewModel.ErrorMessage);
            Assert.False(_viewModel.IsBusy);
        }
    }
}
