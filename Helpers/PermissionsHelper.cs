namespace Manta.Helpers;

public static class PermissionsHelper
{
    public static async Task<bool> ShowRationaleAlert(string permissionName)
    {
        return await Application.Current.MainPage.DisplayAlert(
            "Permission Required",
            $"This feature needs the {permissionName} permission to work. Please grant it in settings.",
            "OK", "Cancel");
    }
}
