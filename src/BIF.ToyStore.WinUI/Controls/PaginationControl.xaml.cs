using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Windows.Input;

namespace BIF.ToyStore.WinUI.Controls
{
    public sealed partial class PaginationControl : UserControl
    {
        public static readonly DependencyProperty TotalItemsProperty =
            DependencyProperty.Register(nameof(TotalItems), typeof(int), typeof(PaginationControl), new PropertyMetadata(0));

        public static readonly DependencyProperty FirstPageCommandProperty =
            DependencyProperty.Register(nameof(FirstPageCommand), typeof(ICommand), typeof(PaginationControl), new PropertyMetadata(null));

        public static readonly DependencyProperty PreviousPageCommandProperty =
            DependencyProperty.Register(nameof(PreviousPageCommand), typeof(ICommand), typeof(PaginationControl), new PropertyMetadata(null));

        public static readonly DependencyProperty NextPageCommandProperty =
            DependencyProperty.Register(nameof(NextPageCommand), typeof(ICommand), typeof(PaginationControl), new PropertyMetadata(null));

        public static readonly DependencyProperty LastPageCommandProperty =
            DependencyProperty.Register(nameof(LastPageCommand), typeof(ICommand), typeof(PaginationControl), new PropertyMetadata(null));

        public int TotalItems
        {
            get => (int)GetValue(TotalItemsProperty);
            set => SetValue(TotalItemsProperty, value);
        }

        public ICommand FirstPageCommand
        {
            get => (ICommand)GetValue(FirstPageCommandProperty);
            set => SetValue(FirstPageCommandProperty, value);
        }

        public ICommand PreviousPageCommand
        {
            get => (ICommand)GetValue(PreviousPageCommandProperty);
            set => SetValue(PreviousPageCommandProperty, value);
        }

        public ICommand NextPageCommand
        {
            get => (ICommand)GetValue(NextPageCommandProperty);
            set => SetValue(NextPageCommandProperty, value);
        }

        public ICommand LastPageCommand
        {
            get => (ICommand)GetValue(LastPageCommandProperty);
            set => SetValue(LastPageCommandProperty, value);
        }

        public PaginationControl()
        {
            this.InitializeComponent();
        }
    }
}
