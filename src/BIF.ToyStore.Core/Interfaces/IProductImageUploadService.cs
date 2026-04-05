using BIF.ToyStore.Core.Models;

namespace BIF.ToyStore.Core.Interfaces
{
    public interface IProductImageUploadService
    {
        Task<ProductImageUploadResult> UploadProductImageAsync(int productId, string filePath, CancellationToken cancellationToken = default);
        Task DeleteProductImageAsync(string publicId, CancellationToken cancellationToken = default);
    }
}
