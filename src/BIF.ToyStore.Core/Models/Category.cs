namespace BIF.ToyStore.Core.Models
{
    public class Category
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsDeleted { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public int ProductCount { get; set; }

        public ICollection<Product> Products { get; set; } = [];
    }
}
