using BIF.ToyStore.Core.Interfaces;
using System;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace BIF.ToyStore.WinUI.Services
{
    public class ExcelFilePickerService : IExcelFilePickerService
    {
        public async Task<string?> PickExcelFilePathAsync(nint windowHandle)
        {
            if (windowHandle == 0)
            {
                throw new InvalidOperationException("Window handle is not initialized.");
            }

            var picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };

            picker.FileTypeFilter.Add(".xlsx");
            picker.FileTypeFilter.Add(".xls");

            InitializeWithWindow.Initialize(picker, windowHandle);

            var file = await picker.PickSingleFileAsync();
            return file?.Path;
        }
    }
}
