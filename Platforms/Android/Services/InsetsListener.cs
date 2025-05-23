using AndroidX.Core.View;

namespace Manta.Services;

public class InsetsListener : Java.Lang.Object, IOnApplyWindowInsetsListener
{
    public WindowInsetsCompat OnApplyWindowInsets(Android.Views.View v, WindowInsetsCompat insets)
    {
        bool keyboardVisible = insets.IsVisible(WindowInsetsCompat.Type.Ime());
        KeyboardService.NotifyKeyboardStateChanged(keyboardVisible);

        // This works but ehhh
        v.SetBackgroundColor(Android.Graphics.Color.ParseColor("#000000"));

        return insets;
    }
}
