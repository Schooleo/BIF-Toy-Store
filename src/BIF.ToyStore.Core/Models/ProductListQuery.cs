namespace BIF.ToyStore.Core.Models
{
    public class ProductListQuery
    {
        public int PageSize { get; set; } = 20;
        public string? Direction { get; set; }
        public string? AfterCursor { get; set; }
        public string? BeforeCursor { get; set; }
        public string SearchText { get; set; } = string.Empty;
        public int? CategoryId { get; set; }
        public decimal? MinRetailPrice { get; set; }
        public decimal? MaxRetailPrice { get; set; }
        public string? SortValue { get; set; }
    }

    public class ProductListResult
    {
        public int TotalCount { get; set; }
        public bool HasNextPage { get; set; }
        public bool HasPreviousPage { get; set; }
        public string? StartCursor { get; set; }
        public string? EndCursor { get; set; }
        public List<Product> Items { get; set; } = [];
    }

    public class ProductImportResult
    {
        public int ImportedCount { get; set; }
        public List<string> Errors { get; set; } = [];
    }
}
