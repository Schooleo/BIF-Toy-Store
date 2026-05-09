namespace BIF.ToyStore.Core.Interfaces
{
    public interface IExcelFilePickerService
    {
        Task<string?> PickExcelFilePathAsync(nint windowHandle);
    }
}
