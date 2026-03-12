using BIF.ToyStore.WinUI.Views;
using Microsoft.UI.Xaml;
namespace BIF.ToyStore.WinUI
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            rootFrame.Navigate(typeof(LoginPage));
        }
    }
}
