using BIF.ToyStore.ViewModels.Pages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using System;
using Windows.Graphics.Effects;

namespace BIF.ToyStore.WinUI.Views
{
    public sealed partial class LoginPage : Page
    {
        private SpriteVisual? _blurBackgroundVisual;

        public LoginViewModel ViewModel { get; }

        public LoginPage()
        {
            InitializeComponent();

            ViewModel = App.Current.Services.GetRequiredService<LoginViewModel>();

            this.DataContext = ViewModel;
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            EnsureBlurredBackground();
            await ViewModel.TryAutoLoginAsync();
        }

        private void EnsureBlurredBackground()
        {
            if (_blurBackgroundVisual is not null)
            {
                return;
            }

            var compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;
            var imageUri = new Uri("ms-appx:///Assets/Branding/ToyBanner.jpg");
            var imageSurface = Microsoft.UI.Xaml.Media.LoadedImageSurface.StartLoadFromUri(imageUri);
            var imageBrush = compositor.CreateSurfaceBrush(imageSurface);
            imageBrush.Stretch = CompositionStretch.UniformToFill;

            IGraphicsEffect blurEffect = new GaussianBlurEffect
            {
                Name = "BlurEffect",
                Source = new CompositionEffectSourceParameter("source"),
                BlurAmount = 20f,
                BorderMode = EffectBorderMode.Hard,
            };

            var effectFactory = compositor.CreateEffectFactory(blurEffect);
            var effectBrush = effectFactory.CreateBrush();
            effectBrush.SetSourceParameter("source", imageBrush);

            _blurBackgroundVisual = compositor.CreateSpriteVisual();
            _blurBackgroundVisual.Brush = effectBrush;

            UpdateBackgroundVisualSize();
            ElementCompositionPreview.SetElementChildVisual(BackgroundBlurHost, _blurBackgroundVisual);
        }

        private void BackgroundBlurHost_SizeChanged(object sender, Microsoft.UI.Xaml.SizeChangedEventArgs e)
        {
            UpdateBackgroundVisualSize();
        }

        private void UpdateBackgroundVisualSize()
        {
            if (_blurBackgroundVisual is null)
            {
                return;
            }

            _blurBackgroundVisual.Size = new System.Numerics.Vector2(
                (float)BackgroundBlurHost.ActualWidth,
                (float)BackgroundBlurHost.ActualHeight);
        }
    }
}
