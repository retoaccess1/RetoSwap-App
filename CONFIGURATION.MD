#### This guide assumes that you have already set up Haveno for your network.
***********************************************

1.  Merge https://github.com/haveno-dex/haveno/commit/6c4f070b9309f38091c2f4d905f86b3a16e76e03.

    Run "make daemon" from the Haveno directory. The build script uses the current OS so you will need to run this for each architecture - windows x86_64, linux x86_64 and linux aarch64. Rename each .jar file with the corresponding arch name:

    daemon-windows.jar 

    daemon-linux-x86_64.jar

    daemon-linux-aarch64.jar

    You will need to host these files somewhere - GitHub releases works well. Save the url, you will need it for the next step.

2. Fork the haveno-app repository and open the Manta.csproj file in a text editor.

    | Property              | Value                                 
    |-----------------------|---------------------------------------
    | ApplicationTitle      | App name shown to the user
    | ApplicationId         | Change this to com.[SOMETHING], this is the app id that Android uses and not shown to the user
    | DaemonUrl             | Set this to the url from the previous step
    | Network               | Change this to XMR_MAINNET
    | HavenoAppName         | Change this to haveno-[SOMETHING], this works the same as the app data directory on desktop
    | MauiIcon              | This is the app icon, you will need to add your own icon to Resources/AppIcon and update the icon name
    | MauiSplashScreen      | This is the splash screen image shown when the app starts, add your own to Resources/Splash

***********************************************

![alt text](https://github.com/atsamd21/Haveno-app/blob/master/AppImages/CHANGETHESE1.png "Change these 1")

***********************************************

![alt text](https://github.com/atsamd21/Haveno-app/blob/master/AppImages/CHANGETHESE2.png "Change these 2")


3. Open the AppConstants.cs file in a text editor and change the ApplicationId string so that it matches the one in Manta.csproj

4. TODO - remote Haveno node

#### Updating the app

You will need to create a new release/url if updating the daemon since the app will compare the last installed url with the one set in Manta.csproj. However, you cannot update just because Haveno updates, there may be breaking changes and so the mobile app will need to be updated to support these changes first.

When the code for the app has been updated to support the latest Haveno version you can build the daemon again and create a new release. Update the url value for DaemonUrl in Manta.csproj. Then the app can be built. Once installed on the device it will automatically fetch the new daemon and update.