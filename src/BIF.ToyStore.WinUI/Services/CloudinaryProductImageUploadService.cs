using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace BIF.ToyStore.WinUI.Services
{
    public class CloudinaryProductImageUploadService : IProductImageUploadService
    {
        private const string CloudinaryUrlKey = "CLOUDINARY_URL";
        private const string RootFolder = "BIF-Toy-Store/products";
        private readonly Lazy<Cloudinary> _cloudinary;

        public CloudinaryProductImageUploadService()
        {
            _cloudinary = new Lazy<Cloudinary>(CreateClient);
        }

        public async Task<ProductImageUploadResult> UploadProductImageAsync(int productId, string filePath, CancellationToken cancellationToken = default)
        {
            if (productId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(productId), "A persisted product id is required before uploading.");
            }

            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("A valid file path is required.", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Selected image file was not found.", filePath);
            }

            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(filePath),
                PublicId = CreateProductPublicId(productId),
                Folder = RootFolder,
                Overwrite = true,
                Invalidate = true,
                UniqueFilename = false,
                UseFilename = false
            };

            var uploadResult = await _cloudinary.Value.UploadAsync(uploadParams, cancellationToken);

            if (uploadResult.Error is not null)
            {
                throw new InvalidOperationException(uploadResult.Error.Message);
            }

            var secureUrl = uploadResult.SecureUrl?.ToString() ?? uploadResult.Url?.ToString();
            if (string.IsNullOrWhiteSpace(secureUrl))
            {
                throw new InvalidOperationException("Cloudinary did not return an image URL.");
            }

            return new ProductImageUploadResult
            {
                ImageUrl = secureUrl,
                PublicId = uploadResult.PublicId ?? CreateProductPublicId(productId)
            };
        }

        public async Task DeleteProductImageAsync(string publicId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(publicId))
            {
                return;
            }

            var deletionParams = new DeletionParams(publicId)
            {
                ResourceType = ResourceType.Image,
                Invalidate = true
            };

            await _cloudinary.Value.DestroyAsync(deletionParams);
        }

        private static Cloudinary CreateClient()
        {
            var cloudinaryUrl = ResolveCloudinaryUrl();
            if (string.IsNullOrWhiteSpace(cloudinaryUrl))
            {
                throw new InvalidOperationException(
                    "Cloudinary is not configured. Set CLOUDINARY_URL in your environment or local .env file.");
            }

            if (!Uri.TryCreate(cloudinaryUrl, UriKind.Absolute, out var uri))
            {
                throw new InvalidOperationException("CLOUDINARY_URL is not a valid URI.");
            }

            if (!string.Equals(uri.Scheme, "cloudinary", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("CLOUDINARY_URL must use the cloudinary:// scheme.");
            }

            var userInfo = uri.UserInfo.Split(':', 2);
            if (userInfo.Length != 2 || string.IsNullOrWhiteSpace(uri.Host))
            {
                throw new InvalidOperationException("CLOUDINARY_URL is missing cloud name, API key, or API secret.");
            }

            var account = new Account(
                uri.Host,
                Uri.UnescapeDataString(userInfo[0]),
                Uri.UnescapeDataString(userInfo[1]));

            return new Cloudinary(account)
            {
                Api =
                {
                    Secure = true
                }
            };
        }

        private static string? ResolveCloudinaryUrl()
        {
            var environmentValue = Environment.GetEnvironmentVariable(CloudinaryUrlKey);
            if (!string.IsNullOrWhiteSpace(environmentValue))
            {
                return environmentValue.Trim();
            }

            foreach (var candidate in EnumerateDotEnvCandidates())
            {
                var resolved = ReadEnvValue(candidate, CloudinaryUrlKey);
                if (!string.IsNullOrWhiteSpace(resolved))
                {
                    return resolved;
                }
            }

            return null;
        }

        private static IEnumerable<string> EnumerateDotEnvCandidates()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var root in new[]
                     {
                         AppContext.BaseDirectory,
                         Directory.GetCurrentDirectory()
                     })
            {
                if (string.IsNullOrWhiteSpace(root))
                {
                    continue;
                }

                var directory = new DirectoryInfo(root);
                while (directory is not null)
                {
                    var candidate = Path.Combine(directory.FullName, ".env");
                    if (seen.Add(candidate))
                    {
                        yield return candidate;
                    }

                    directory = directory.Parent;
                }
            }
        }

        private static string? ReadEnvValue(string envPath, string key)
        {
            if (!File.Exists(envPath))
            {
                return null;
            }

            foreach (var rawLine in File.ReadLines(envPath))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                {
                    continue;
                }

                var separatorIndex = line.IndexOf('=');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                var currentKey = line[..separatorIndex].Trim();
                if (!string.Equals(currentKey, key, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return line[(separatorIndex + 1)..].Trim().Trim('"');
            }

            return null;
        }

        private static string CreateProductPublicId(int productId) => $"product-{productId}-{Guid.NewGuid():N}";
    }
}
