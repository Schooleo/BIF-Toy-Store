using BIF.ToyStore.Core.Models;
using BIF.ToyStore.Core.Interfaces;
using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using WinRT.Interop;

namespace BIF.ToyStore.WinUI.Controls
{
    public sealed partial class ProductDetailsForm : UserControl, INotifyPropertyChanged
    {
        private static readonly Product EmptyProduct = new();
        private readonly IImageFilePickerService? _imageFilePickerService;
        private readonly nint _windowHandle;
        private bool _isFormLoaded;
        private bool _isUploadingImage;
        private string _uploadErrorMessage = string.Empty;

        public ProductDetailsForm()
        {
            InitializeComponent();

            _imageFilePickerService = App.Current.Services.GetService<IImageFilePickerService>();
            _windowHandle = App.Current.MainWindowInstance is null
                ? 0
                : WindowNative.GetWindowHandle(App.Current.MainWindowInstance);

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public Product EditableProduct => Product ?? EmptyProduct;

        public string SelectedCategoryDisplay
        {
            get
            {
                if (Product?.Category is Category { Name: not null } selectedCategory)
                {
                    return selectedCategory.Name;
                }

                if (Product is not null && Categories is not null)
                {
                    foreach (var category in Categories)
                    {
                        if (category.Id == Product.CategoryId)
                        {
                            return category.Name;
                        }
                    }
                }

                return "Category";
            }
        }

        public bool IsErrorOpen => !string.IsNullOrWhiteSpace(EditErrorMessage);
        public bool IsUploadErrorOpen => !string.IsNullOrWhiteSpace(UploadErrorMessage);
        public bool IsUploadingImage
        {
            get => _isUploadingImage;
            private set
            {
                if (_isUploadingImage == value)
                {
                    return;
                }

                _isUploadingImage = value;
                OnPropertyChanged(nameof(IsUploadingImage));
            }
        }

        public string UploadErrorMessage
        {
            get => _uploadErrorMessage;
            private set
            {
                if (string.Equals(_uploadErrorMessage, value, StringComparison.Ordinal))
                {
                    return;
                }

                _uploadErrorMessage = value;
                OnPropertyChanged(nameof(UploadErrorMessage));
                OnPropertyChanged(nameof(IsUploadErrorOpen));
            }
        }

        public string? CurrentImageUrl => Product?.ImageUrl;

        public Product? Product
        {
            get => (Product?)GetValue(ProductProperty);
            set => SetValue(ProductProperty, value);
        }

        public static readonly DependencyProperty ProductProperty = DependencyProperty.Register(
            nameof(Product),
            typeof(Product),
            typeof(ProductDetailsForm),
            new PropertyMetadata(null, OnProductChanged));

        public ObservableCollection<Category>? Categories
        {
            get => (ObservableCollection<Category>?)GetValue(CategoriesProperty);
            set => SetValue(CategoriesProperty, value);
        }

        public static readonly DependencyProperty CategoriesProperty = DependencyProperty.Register(
            nameof(Categories),
            typeof(ObservableCollection<Category>),
            typeof(ProductDetailsForm),
            new PropertyMetadata(new ObservableCollection<Category>(), OnCategoriesChanged));

        public ICommand? SaveCommand
        {
            get => (ICommand?)GetValue(SaveCommandProperty);
            set => SetValue(SaveCommandProperty, value);
        }

        public static readonly DependencyProperty SaveCommandProperty = DependencyProperty.Register(
            nameof(SaveCommand),
            typeof(ICommand),
            typeof(ProductDetailsForm),
            new PropertyMetadata(null));

        public ICommand? CancelCommand
        {
            get => (ICommand?)GetValue(CancelCommandProperty);
            set => SetValue(CancelCommandProperty, value);
        }

        public static readonly DependencyProperty CancelCommandProperty = DependencyProperty.Register(
            nameof(CancelCommand),
            typeof(ICommand),
            typeof(ProductDetailsForm),
            new PropertyMetadata(null));

        public string EditErrorMessage
        {
            get => (string)GetValue(EditErrorMessageProperty);
            set => SetValue(EditErrorMessageProperty, value);
        }

        public static readonly DependencyProperty EditErrorMessageProperty = DependencyProperty.Register(
            nameof(EditErrorMessage),
            typeof(string),
            typeof(ProductDetailsForm),
            new PropertyMetadata(string.Empty, OnEditErrorMessageChanged));

        private static void OnProductChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ProductDetailsForm form && form._isFormLoaded)
            {
                form.OnPropertyChanged(nameof(EditableProduct));
                form.OnPropertyChanged(nameof(SelectedCategoryDisplay));
                form.OnPropertyChanged(nameof(CurrentImageUrl));
            }
        }

        private static void OnCategoriesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ProductDetailsForm form && form._isFormLoaded)
            {
                form.OnPropertyChanged(nameof(SelectedCategoryDisplay));
            }
        }

        private static void OnEditErrorMessageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ProductDetailsForm form && form._isFormLoaded)
            {
                form.OnPropertyChanged(nameof(IsErrorOpen));
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _isFormLoaded = true;
            OnPropertyChanged(nameof(EditableProduct));
            OnPropertyChanged(nameof(IsErrorOpen));
            OnPropertyChanged(nameof(IsUploadErrorOpen));
            OnPropertyChanged(nameof(SelectedCategoryDisplay));
            OnPropertyChanged(nameof(CurrentImageUrl));
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _isFormLoaded = false;
        }

        private void FieldChanged_ClearError(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(EditErrorMessage))
            {
                EditErrorMessage = string.Empty;
            }
        }

        private void CategoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Product is not null && sender is ListView { SelectedItem: Category category })
            {
                Product.CategoryId = category.Id;
                Product.Category = category;

                var flyout = CategorySelectorButton.Flyout;
                flyout?.Hide();
            }
            else if (Product is not null)
            {
                Product.CategoryId = 0;
                Product.Category = null;
            }

            OnPropertyChanged(nameof(SelectedCategoryDisplay));

            FieldChanged_ClearError(sender, e);
        }

        private void FieldChanged_ClearError(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (!string.IsNullOrWhiteSpace(EditErrorMessage))
            {
                EditErrorMessage = string.Empty;
            }
        }

        private async void UploadImageButton_Click(object sender, RoutedEventArgs e)
        {
            if (IsUploadingImage)
            {
                return;
            }

            if (Product is null)
            {
                UploadErrorMessage = "No product is selected for editing.";
                return;
            }

            if (_imageFilePickerService is null)
            {
                UploadErrorMessage = "Image picker service is not available.";
                return;
            }

            if (_windowHandle == 0)
            {
                UploadErrorMessage = "Cannot open image picker because the window is not ready.";
                return;
            }

            try
            {
                UploadErrorMessage = string.Empty;
                IsUploadingImage = true;

                var selectedFilePath = await _imageFilePickerService.PickImageFilePathAsync(_windowHandle);
                if (string.IsNullOrWhiteSpace(selectedFilePath))
                {
                    return;
                }

                Product.ImageUrl = selectedFilePath;
                OnPropertyChanged(nameof(CurrentImageUrl));
            }
            catch (Exception ex)
            {
                UploadErrorMessage = $"Image selection failed: {ex.Message}";
            }
            finally
            {
                IsUploadingImage = false;
            }
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
