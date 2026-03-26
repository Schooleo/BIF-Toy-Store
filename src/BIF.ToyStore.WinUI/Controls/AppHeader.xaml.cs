using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using Windows.UI;

namespace BIF.ToyStore.WinUI.Controls
{
    public sealed partial class AppHeader : UserControl
    {
        public static readonly DependencyProperty UserRoleProperty =
            DependencyProperty.Register(
                nameof(UserRole),
                typeof(string),
                typeof(AppHeader),
                new PropertyMetadata("ADMIN", OnUserRoleChanged));

        public AppHeader()
        {
            InitializeComponent();
            UpdateRoleVisuals(UserRole);
        }

        public string UserRole
        {
            get => (string)GetValue(UserRoleProperty);
            set => SetValue(UserRoleProperty, value);
        }

        private static void OnUserRoleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AppHeader header && e.NewValue is string role)
            {
                header.UpdateRoleVisuals(role);
            }
        }

        private void UpdateRoleVisuals(string role)
        {
            var isAdmin = string.Equals(role, "ADMIN", System.StringComparison.OrdinalIgnoreCase);

            RoleText.Text = isAdmin ? "ADMIN" : "SALE";
            RoleInitialText.Text = isAdmin ? "A" : "S";

            if (isAdmin)
            {
                RoleAvatar.Background = Application.Current.Resources["FluentPlayPrimaryBrush"] as Brush;
                return;
            }

            if (Application.Current.Resources.TryGetValue("FluentPlaySecondaryContainerColor", out var secondaryColorObj)
                && secondaryColorObj is Color secondaryColor)
            {
                RoleAvatar.Background = new SolidColorBrush(Color.FromArgb(secondaryColor.A, secondaryColor.R, secondaryColor.G, secondaryColor.B));
                return;
            }

            RoleAvatar.Background = Application.Current.Resources["FluentPlayOutlineVariantBrush"] as Brush;
        }
    }
}
