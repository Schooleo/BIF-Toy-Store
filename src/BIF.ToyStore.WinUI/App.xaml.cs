using BIF.ToyStore.Core.Interfaces;
using BIF.ToyStore.Infrastructure.Data;
using BIF.ToyStore.Infrastructure.Repositories;
using BIF.ToyStore.Infrastructure.Services;
using BIF.ToyStore.ViewModels.Pages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using System;

namespace BIF.ToyStore.WinUI
{
    public partial class App : Application
    {
        public IServiceProvider Services { get; }

        public new static App Current => (App)Application.Current;

        public App()
        {
            InitializeComponent();

            // Dependency Injection
            var services = new ServiceCollection();

            // Register DB
            services.AddDbContext<AppDbContext>();

            // Register Repositories
            services.AddScoped(typeof(IRepository<>), typeof(BaseRepository<>));

            // Register Services
            services.AddScoped<IAuthService, AuthService>();

            // Register ViewModels
            services.AddTransient<LoginViewModel>();

            // Build
            Services = services.BuildServiceProvider();
        }

        private Window? _window;
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            _window = new MainWindow();
            _window.Activate();
        }
    }
}
