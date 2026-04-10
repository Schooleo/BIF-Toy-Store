using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Core.Models;

namespace BIF.ToyStore.Infrastructure.Services
{
	public class CategoryService(IGraphQLClient graphQLClient) : ICategoryService
	{
		private readonly IGraphQLClient _graphQLClient = graphQLClient;

		public async Task<CategoryListResult> GetCategoriesAsync(CategoryListQuery query)
		{
			const string queryTemplate = @"
				query GetCategories(
					$first: Int, $last: Int, $after: String, $before: String,
					$where: CategoryFilterInput, $order: [CategorySortInput!]
				) {
					categories(
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
							products {
								id
								name
							}
						}
					}
				}";

			object? whereClause = !string.IsNullOrWhiteSpace(query.SearchText)
				? new { name = new { contains = query.SearchText } }
				: null;

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
				firstVar = null;
				lastVar = query.PageSize;
				afterVar = null;
				beforeVar = query.BeforeCursor;
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
				order = new[] { new { id = "ASC" } }
			};

			var result = await _graphQLClient.ExecuteAsync<CategoryConnection>(
				queryTemplate,
				variables,
				dataKey: "categories");

			return new CategoryListResult
			{
				TotalCount = result?.TotalCount ?? 0,
				HasNextPage = result?.PageInfo?.HasNextPage ?? false,
				HasPreviousPage = result?.PageInfo?.HasPreviousPage ?? false,
				StartCursor = result?.PageInfo?.StartCursor,
				EndCursor = result?.PageInfo?.EndCursor,
				Items = result?.Nodes ?? []
			};
		}

		public async Task<Category> CreateCategoryAsync(Category category)
		{
			const string mutation = @"
				mutation Create($input: CreateCategoryInput!) {
					createCategory(input: $input) {
						id
						name
					}
				}";

			var input = new { name = category.Name };

			return await _graphQLClient.ExecuteAsync<Category>(
				mutation,
				new { input },
				dataKey: "createCategory")
				?? throw new InvalidOperationException("Failed to create category.");
		}

		public async Task<Category> UpdateCategoryAsync(Category category)
		{
			const string mutation = @"
				mutation Update($input: UpdateCategoryInput!) {
					updateCategory(input: $input) {
						id
						name
					}
				}";

			var input = new
			{
				id = category.Id,
				name = category.Name
			};

			return await _graphQLClient.ExecuteAsync<Category>(
				mutation,
				new { input },
				dataKey: "updateCategory")
				?? throw new InvalidOperationException("Failed to update category.");
		}

		public async Task<bool> DeleteCategoryAsync(int id)
		{
			const string mutation = @"
				mutation Delete($id: Int!) {
					deleteCategory(id: $id)
				}";

			return await _graphQLClient.ExecuteAsync<bool>(
				mutation,
				new { id },
				dataKey: "deleteCategory");
		}

		public async Task<Category> RestoreCategoryAsync(int id)
		{
			const string mutation = @"
				mutation Restore($id: Int!) {
					restoreCategory(id: $id) {
						id
						name
					}
				}";

			return await _graphQLClient.ExecuteAsync<Category>(
				mutation,
				new { id },
				dataKey: "restoreCategory")
				?? throw new InvalidOperationException("Failed to restore category.");
		}

		private sealed class CategoryConnection
		{
			public int TotalCount { get; set; }
			public PageInfo? PageInfo { get; set; }
			public List<Category>? Nodes { get; set; }
		}

		private sealed class PageInfo
		{
			public bool HasNextPage { get; set; }
			public bool HasPreviousPage { get; set; }
			public string? StartCursor { get; set; }
			public string? EndCursor { get; set; }
		}
	}
}
