using BIF.ToyStore.Core.Models;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace BIF.ToyStore.ViewModels.Messages
{
    public sealed class LoginSucceededMessage(LoginUser value) : ValueChangedMessage<LoginUser>(value)
    {
    }
}
