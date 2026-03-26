using BIF.ToyStore.Core.Models;
using BIF.ToyStore.ViewModels.Base;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace BIF.ToyStore.ViewModels.Pages
{
    public partial class AddProductFormViewModel : BaseViewModel
    {
        [ObservableProperty]
        private string? name = string.Empty;

        [ObservableProperty]
        private Category? selectedCategory;

        [ObservableProperty]
        private decimal importPrice;

        [ObservableProperty]
        private decimal retailPrice;

        [ObservableProperty]
        private int stockQuantity;

        // Error flag properties (use boolean instead of Visibility)
        [ObservableProperty]
        private bool hasNameError;

        [ObservableProperty]
        private bool hasCategoryError;

        [ObservableProperty]
        private bool hasImportPriceError;

        [ObservableProperty]
        private bool hasRetailPriceError;

        public ObservableCollection<Category> Categories { get; }

        private bool _isEditMode;
        private int _editingProductId;

        public AddProductFormViewModel()
        {
            Categories = [];
            Title = "Add New Product";
        }

        public void InitializeWithCategories(IEnumerable<Category> categories)
        {
            Categories.Clear();
            foreach (var category in categories)
            {
                Categories.Add(category);
            }
        }

        public void InitializeForEdit(Product existingProduct, IEnumerable<Category> categories)
        {
            InitializeWithCategories(categories);

            _isEditMode = true;
            _editingProductId = existingProduct.Id;
            Title = "Edit Product";

            Name = existingProduct.Name;
            SelectedCategory = categories.FirstOrDefault(c => c.Id == existingProduct.CategoryId);
            ImportPrice = existingProduct.ImportPrice;
            RetailPrice = existingProduct.RetailPrice;
            StockQuantity = existingProduct.StockQuantity;
        }

        public bool Validate()
        {
            bool isValid = true;

            // Validate Name
            if (string.IsNullOrWhiteSpace(Name))
            {
                HasNameError = true;
                isValid = false;
            }
            else
            {
                HasNameError = false;
            }

            // Validate Category
            if (SelectedCategory == null)
            {
                HasCategoryError = true;
                isValid = false;
            }
            else
            {
                HasCategoryError = false;
            }

            // Validate Import Price
            if (ImportPrice <= 0)
            {
                HasImportPriceError = true;
                isValid = false;
            }
            else
            {
                HasImportPriceError = false;
            }

            // Validate Retail Price
            if (RetailPrice <= 0)
            {
                HasRetailPriceError = true;
                isValid = false;
            }
            else
            {
                HasRetailPriceError = false;
            }

            return isValid;
        }

        public Product GetProduct()
        {
            return new Product
            {
                Id = _isEditMode ? _editingProductId : 0,
                Name = Name ?? string.Empty,
                CategoryId = SelectedCategory?.Id ?? 0,
                ImportPrice = ImportPrice,
                RetailPrice = RetailPrice,
                StockQuantity = StockQuantity
            };
        }

        public void ResetForm()
        {
            Name = string.Empty;
            SelectedCategory = null;
            ImportPrice = 0;
            RetailPrice = 0;
            StockQuantity = 0;

            HasNameError = false;
            HasCategoryError = false;
            HasImportPriceError = false;
            HasRetailPriceError = false;

            _isEditMode = false;
            Title = "Add New Product";
        }
    }
}
