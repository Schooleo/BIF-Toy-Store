namespace BIF.ToyStore.Core.Interfaces
{
    public interface IImageFilePickerService
    {
        Task<string?> PickImageFilePathAsync(nint windowHandle);
    }
}
