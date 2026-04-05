using BIF.ToyStore.Core.Interfaces;
using System;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace BIF.ToyStore.WinUI.Services
{
    public class ImageFilePickerService : IImageFilePickerService
    {
        public async Task<string?> PickImageFilePathAsync(nint windowHandle)
        {
            if (windowHandle == 0)
            {
                throw new InvalidOperationException("Window handle is not initialized.");
            }

            var picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.PicturesLibrary
            };

            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".webp");

            InitializeWithWindow.Initialize(picker, windowHandle);

            var file = await picker.PickSingleFileAsync();
            return file?.Path;
        }
    }
}
