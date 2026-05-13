using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Support.UI;
using Xunit;

namespace AppiumSmokeTest;

/// <summary>
/// Minimal Appium smoke test — verifies the app launches and does not crash.
///
/// Environment variables (set by scripts/run-tests.sh):
///   APP_PACKAGE   — Android package name  (e.g. "com.example.myapp")
///   APP_ACTIVITY  — Main activity class   (e.g. ".MainActivity")
///   APPIUM_HOST   — Appium server host    (default: 127.0.0.1)
///   APPIUM_PORT   — Appium server port    (default: 4723)
/// </summary>
public class AppLaunchSmokeTest : IDisposable
{
    private readonly AndroidDriver _driver;

    public AppLaunchSmokeTest()
    {
        var appPackage = Environment.GetEnvironmentVariable("APP_PACKAGE")
            ?? throw new InvalidOperationException("APP_PACKAGE env var not set");
        var appActivity = Environment.GetEnvironmentVariable("APP_ACTIVITY") ?? ".MainActivity";
        var appiumHost = Environment.GetEnvironmentVariable("APPIUM_HOST") ?? "127.0.0.1";
        var appiumPort = Environment.GetEnvironmentVariable("APPIUM_PORT") ?? "4723";

        var options = new AppiumOptions();
        options.PlatformName = "Android";
        options.AutomationName = "UIAutomator2";
        options.App = null; // APK already installed — launch by package name
        options.AddAdditionalAppiumOption("appPackage", appPackage);
        options.AddAdditionalAppiumOption("appActivity", appActivity);
        options.AddAdditionalAppiumOption("noReset", true);
        options.AddAdditionalAppiumOption("autoGrantPermissions", true);

        var serverUri = new Uri($"http://{appiumHost}:{appiumPort}");
        _driver = new AndroidDriver(serverUri, options, TimeSpan.FromSeconds(60));
    }

    [Fact]
    public void App_Launches_Without_Crash()
    {
        // Poll until the app is the foreground package (up to 15 seconds).
        // This guards against races where the session is established before
        // the Activity is fully in the foreground.
        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(15));
        var expected = Environment.GetEnvironmentVariable("APP_PACKAGE");
        var currentPackage = wait.Until(d => ((AndroidDriver)d).CurrentPackage == expected
            ? ((AndroidDriver)d).CurrentPackage
            : null);
        Assert.Equal(expected, currentPackage);
    }

    [Fact]
    public void App_Does_Not_Show_Crash_Dialog()
    {
        // If an ANR or crash dialog is visible, the app has crashed.
        // These are system-level dialogs identified by their package.
        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(5));

        try
        {
            var crashTitle = _driver.FindElement(
                By.XPath("//*[@resource-id='android:id/alertTitle']")
            );
            // If we found a dialog, check its text
            var text = crashTitle.Text;
            Assert.False(
                text.Contains("stopped") || text.Contains("isn't responding"),
                $"App crash dialog detected: '{text}'"
            );
        }
        catch (WebDriverException)
        {
            // No dialog found — good, the app is running normally
        }
    }

    public void Dispose()
    {
        _driver?.Quit();
    }
}
