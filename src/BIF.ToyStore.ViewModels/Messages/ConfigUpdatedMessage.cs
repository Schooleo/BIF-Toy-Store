using CommunityToolkit.Mvvm.Messaging.Messages;

namespace BIF.ToyStore.ViewModels.Messages
{
    public sealed class ConfigUpdatedMessage(decimal value) : ValueChangedMessage<decimal>(value)
    {
    }
}
