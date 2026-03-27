using BIF.ToyStore.Core.Models;
using BIF.ToyStore.ViewModels.Pages;

namespace BIF.ToyStore.Tests.ViewModels.Pages
{
    public class AddProductFormViewModelTests
    {
        [Fact]
        public void Validate_EmptyRequiredFields_ReturnsFalseAndSetsErrors()
        {
            var vm = new AddProductFormViewModel
            {
                Name = "",
                SelectedCategory = null,
                ImportPrice = 0,
                RetailPrice = 0
            };

            var result = vm.Validate();

            Assert.False(result);
            Assert.True(vm.HasNameError);
            Assert.True(vm.HasCategoryError);
            Assert.True(vm.HasImportPriceError);
            Assert.True(vm.HasRetailPriceError);
        }

        [Fact]
        public void InitializeForEdit_LoadsExistingProductAndSwitchesTitle()
        {
            var vm = new AddProductFormViewModel();
            var categories = new[]
            {
                new Category { Id = 1, Name = "Action" },
                new Category { Id = 2, Name = "Puzzle" }
            };
            var product = new Product
            {
                Id = 12,
                Name = "Lego City",
                CategoryId = 2,
                ImportPrice = 10m,
                RetailPrice = 20m,
                StockQuantity = 5
            };

            vm.InitializeForEdit(product, categories);

            Assert.Equal("Edit Product", vm.Title);
            Assert.Equal("Lego City", vm.Name);
            Assert.NotNull(vm.SelectedCategory);
            Assert.Equal(2, vm.SelectedCategory!.Id);
            Assert.Equal(10m, vm.ImportPrice);
            Assert.Equal(20m, vm.RetailPrice);
            Assert.Equal(5, vm.StockQuantity);
            Assert.Equal(2, vm.Categories.Count);
        }

        [Fact]
        public void GetProduct_EditMode_PreservesOriginalId()
        {
            var vm = new AddProductFormViewModel();
            var categories = new[] { new Category { Id = 9, Name = "Blocks" } };
            var existing = new Product
            {
                Id = 99,
                Name = "Old Name",
                CategoryId = 9,
                ImportPrice = 4m,
                RetailPrice = 8m,
                StockQuantity = 2
            };
            vm.InitializeForEdit(existing, categories);

            vm.Name = "New Name";
            vm.ImportPrice = 5m;
            vm.RetailPrice = 9m;
            vm.StockQuantity = 3;

            var result = vm.GetProduct();

            Assert.Equal(99, result.Id);
            Assert.Equal("New Name", result.Name);
            Assert.Equal(9, result.CategoryId);
            Assert.Equal(5m, result.ImportPrice);
            Assert.Equal(9m, result.RetailPrice);
            Assert.Equal(3, result.StockQuantity);
        }

        [Fact]
        public void ResetForm_ClearsStateAndReturnsToAddMode()
        {
            var vm = new AddProductFormViewModel();
            vm.Name = "Toy";
            vm.SelectedCategory = new Category { Id = 1, Name = "A" };
            vm.ImportPrice = 1;
            vm.RetailPrice = 2;
            vm.StockQuantity = 3;
            vm.HasNameError = true;
            vm.HasCategoryError = true;
            vm.HasImportPriceError = true;
            vm.HasRetailPriceError = true;

            vm.ResetForm();

            Assert.Equal("Add New Product", vm.Title);
            Assert.Equal(string.Empty, vm.Name);
            Assert.Null(vm.SelectedCategory);
            Assert.Equal(0m, vm.ImportPrice);
            Assert.Equal(0m, vm.RetailPrice);
            Assert.Equal(0, vm.StockQuantity);
            Assert.False(vm.HasNameError);
            Assert.False(vm.HasCategoryError);
            Assert.False(vm.HasImportPriceError);
            Assert.False(vm.HasRetailPriceError);
        }
    }
}
