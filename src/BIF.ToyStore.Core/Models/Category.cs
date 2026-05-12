using System.ComponentModel.DataAnnotations.Schema;

namespace BIF.ToyStore.Core.Models
{
    public class Category
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsDeleted { get; set; }

        [NotMapped]
        public int ProductCount { get; set; }

        public ICollection<Product> Products { get; set; } = [];
    }
}
