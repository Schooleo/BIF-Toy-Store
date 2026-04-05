using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace BIF.ToyStore.WinUI.Controls
{
    public static class CommonFlyout
    {
        public static void CloseParentFlyout(DependencyObject? source)
        {
            DependencyObject? current = source;
            while (current is not null && current is not FlyoutPresenter && current is not MenuFlyoutPresenter)
            {
                current = VisualTreeHelper.GetParent(current);
            }

            if (current is FlyoutPresenter flyoutPresenter && flyoutPresenter.Parent is Popup flyoutPopup)
            {
                flyoutPopup.IsOpen = false;
                return;
            }

            if (current is MenuFlyoutPresenter menuFlyoutPresenter && menuFlyoutPresenter.Parent is Popup menuPopup)
            {
                menuPopup.IsOpen = false;
            }
        }

        public static void HideAttachedFlyout(Button? button)
        {
            button?.Flyout?.Hide();
        }
    }
}