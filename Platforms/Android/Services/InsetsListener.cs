using Android.OS;
using Android.Views;
using AndroidX.Core.View;

namespace Manta.Services;

public class InsetsListener : Java.Lang.Object, IOnApplyWindowInsetsListener
{
    public WindowInsetsCompat? OnApplyWindowInsets(Android.Views.View? v, WindowInsetsCompat? insets)
    {
        if (v is null || insets is null)
            return insets;

        bool keyboardVisible = insets.IsVisible(WindowInsetsCompat.Type.Ime());
        KeyboardService.NotifyKeyboardStateChanged(keyboardVisible);

        // Keeping this but might have been fixed in .NET or package update
//#if ANDROID35_0_OR_GREATER
//        if (Build.VERSION.SdkInt >= BuildVersionCodes.VanillaIceCream)
//        {
//            var statusBarInsets = insets.GetInsets(WindowInsets.Type.StatusBars());
//            var navigationBarInsets = insets.GetInsets(WindowInsets.Type.NavigationBars());
//            v.SetPadding(0, statusBarInsets.Top, 0, navigationBarInsets.Bottom);
//        }
//#endif

        // This works but ehhh
        v.SetBackgroundColor(Android.Graphics.Color.ParseColor("#000000"));

        return insets;
    }
}
