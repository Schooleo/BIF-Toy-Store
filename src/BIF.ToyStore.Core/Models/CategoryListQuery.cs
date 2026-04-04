namespace BIF.ToyStore.Core.Models
{
    public class CategoryListQuery
    {
        public int PageSize { get; set; } = 20;
        public string? Direction { get; set; }
        public string? AfterCursor { get; set; }
        public string? BeforeCursor { get; set; }
        public string SearchText { get; set; } = string.Empty;
    }

    public class CategoryListResult
    {
        public int TotalCount { get; set; }
        public bool HasNextPage { get; set; }
        public bool HasPreviousPage { get; set; }
        public string? StartCursor { get; set; }
        public string? EndCursor { get; set; }
        public List<Category> Items { get; set; } = [];
    }
}
