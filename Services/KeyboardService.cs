namespace Manta.Services;

public static class KeyboardService
{
    public static event Action<bool>? KeyboardStateChanged;

    public static void NotifyKeyboardStateChanged(bool isVisible)
    {
        KeyboardStateChanged?.Invoke(isVisible);
    }
}
