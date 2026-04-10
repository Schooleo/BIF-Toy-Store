using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;

namespace BIF.ToyStore.Infrastructure.Services
{
	public class ProductService(IGraphQLClient graphQLClient) : IProductService
	{
		private readonly IGraphQLClient _graphQLClient = graphQLClient;

		public async Task<IReadOnlyList<Category>> GetCategoriesAsync(int take = 250)
		{
			const string query = @"
				query GetCategories($first: Int) {
					categories(first: $first) {
						nodes {
							id
							name
						}
					}
				}";

			var result = await _graphQLClient.ExecuteAsync<CategoryConnection>(
				query,
				new { first = take },
				dataKey: "categories");

			return result?.Nodes ?? [];
		}

		public async Task<ProductListResult> GetProductsAsync(ProductListQuery query)
		{
			const string queryTemplate = @"
				query GetProducts(
					$first: Int, $last: Int, $after: String, $before: String,
					$where: ProductFilterInput, $order: [ProductSortInput!]
				) {
					products(
						first: $first,
						last: $last,
						after: $after,
						before: $before,
						where: $where,
						order: $order
					) {
						totalCount
						pageInfo {
							hasNextPage
							hasPreviousPage
							startCursor
							endCursor
						}
						nodes {
							id
							name
							categoryId
							category {
								id
								name
							}
							retailPrice
							importPrice
							stockQuantity
							imageUrl
						}
					}
				}";

			var whereConditions = new List<object>();

			if (!string.IsNullOrWhiteSpace(query.SearchText))
			{
				whereConditions.Add(new { name = new { contains = query.SearchText } });
			}

			if (query.CategoryId.HasValue)
			{
				whereConditions.Add(new { categoryId = new { eq = query.CategoryId.Value } });
			}

			if (query.MinRetailPrice.HasValue)
			{
				whereConditions.Add(new { retailPrice = new { gte = query.MinRetailPrice.Value } });
			}

			if (query.MaxRetailPrice.HasValue)
			{
				whereConditions.Add(new { retailPrice = new { lte = query.MaxRetailPrice.Value } });
			}

			object? whereClause = whereConditions.Count > 0
				? new { and = whereConditions }
				: null;

			object orderClause = query.SortValue switch
			{
				"price_asc" => new[] { new { retailPrice = "ASC" } },
				"price_desc" => new[] { new { retailPrice = "DESC" } },
				"stock_asc" => new[] { new { stockQuantity = "ASC" } },
				"name_asc" => new[] { new { name = "ASC" } },
				_ => new[] { new { id = "DESC" } }
			};

			int? firstVar = null;
			int? lastVar = null;
			string? afterVar = null;
			string? beforeVar = null;

			if (query.Direction == "next" && !string.IsNullOrWhiteSpace(query.AfterCursor))
			{
				firstVar = query.PageSize;
				afterVar = query.AfterCursor;
			}
			else if (query.Direction == "prev" && !string.IsNullOrWhiteSpace(query.BeforeCursor))
			{
				lastVar = query.PageSize;
				beforeVar = query.BeforeCursor;
			}
			else if (query.Direction == "last")
			{
				lastVar = query.PageSize;
			}
			else
			{
				firstVar = query.PageSize;
			}

			var variables = new
			{
				first = firstVar,
				last = lastVar,
				after = afterVar,
				before = beforeVar,
				where = whereClause,
				order = orderClause
			};

			var result = await _graphQLClient.ExecuteAsync<ProductConnection>(
				queryTemplate,
				variables,
				dataKey: "products");

			return new ProductListResult
			{
				TotalCount = result?.TotalCount ?? 0,
				HasNextPage = result?.PageInfo?.HasNextPage ?? false,
				HasPreviousPage = result?.PageInfo?.HasPreviousPage ?? false,
				StartCursor = result?.PageInfo?.StartCursor,
				EndCursor = result?.PageInfo?.EndCursor,
				Items = result?.Nodes ?? []
			};
		}

		public async Task<Product> CreateProductAsync(Product product)
		{
			const string mutation = @"
				mutation Create($input: CreateProductInput!) {
					createProduct(input: $input) {
						id
						name
						categoryId
						retailPrice
						importPrice
						stockQuantity
						imageUrl
					}
				}";

			var input = new
			{
				name = product.Name,
				categoryId = product.CategoryId,
				retailPrice = product.RetailPrice,
				importPrice = product.ImportPrice,
				stockQuantity = product.StockQuantity,
				imageUrl = product.ImageUrl
			};

			return await _graphQLClient.ExecuteAsync<Product>(
				mutation,
				new { input },
				dataKey: "createProduct")
				?? throw new InvalidOperationException("Failed to create product.");
		}

		public async Task<Product> UpdateProductAsync(Product product)
		{
			const string mutation = @"
				mutation Update($input: UpdateProductInput!) {
					updateProduct(input: $input) {
						id
						name
						categoryId
						retailPrice
						importPrice
						stockQuantity
						imageUrl
					}
				}";

			var input = new
			{
				id = product.Id,
				name = product.Name,
				categoryId = product.CategoryId,
				retailPrice = product.RetailPrice,
				importPrice = product.ImportPrice,
				stockQuantity = product.StockQuantity,
				imageUrl = product.ImageUrl
			};

			return await _graphQLClient.ExecuteAsync<Product>(
				mutation,
				new { input },
				dataKey: "updateProduct")
				?? throw new InvalidOperationException("Failed to update product.");
		}

		public async Task<bool> DeleteProductAsync(int id)
		{
			const string mutation = @"
				mutation Delete($id: Int!) {
					deleteProduct(id: $id)
				}";

			return await _graphQLClient.ExecuteAsync<bool>(
				mutation,
				new { id },
				dataKey: "deleteProduct");
		}

		public async Task<ProductImportResult> ImportProductsAsync(string filePath)
		{
			const string mutation = @"
				mutation Import($file: Upload!) {
					importProducts(file: $file) {
						importedCount
						errors
					}
				}";

			var result = await _graphQLClient.UploadFileAsync<ImportProductsPayload>(
				mutation,
				variableName: "file",
				filePath,
				dataKey: "importProducts");

			return new ProductImportResult
			{
				ImportedCount = result?.ImportedCount ?? 0,
				Errors = result?.Errors ?? []
			};
		}

		private sealed class CategoryConnection
		{
			public List<Category>? Nodes { get; set; }
		}

		private sealed class ProductConnection
		{
			public int TotalCount { get; set; }
			public PageInfo? PageInfo { get; set; }
			public List<Product>? Nodes { get; set; }
		}

		private sealed class PageInfo
		{
			public bool HasNextPage { get; set; }
			public bool HasPreviousPage { get; set; }
			public string? StartCursor { get; set; }
			public string? EndCursor { get; set; }
		}

		private sealed class ImportProductsPayload
		{
			public int ImportedCount { get; set; }
			public List<string> Errors { get; set; } = [];
		}
	}
}
