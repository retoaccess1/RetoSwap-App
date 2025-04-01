![alt text](https://github.com/atsamd21/Haveno-app/blob/master/App.png "Image of application")

# WIP
Currently only available for Android, however MAUI supports IOS and Windows so this could change

## Setup
The easiest way to run the project is by using Visual Studio 2022
The app can run either on a physical device with USB debugging and developer mode enabled or via the Android emulator

## Notes
There are two options for installing the Haveno daemon, both use Termux
1. There is a built in installer, it installs a modified Termux with allow-external-apps enabled by default. It does need quite a few permissions however. File access is one that I wish was not needed but currently I can't get the RUN_COMMAND stdout to return so I'm using a file to communicate between apps.
2. Manually set up Termux and daemon by following the instructions in haveno-termux-install.MD

This is very much work in progress, you can make simple trades on the public stagenet network but I have not tested all payment types

# Donate
```
8AjnVUsDsLTAynhXLZSymreRdvySXmt8vcDU6osXPTGudmazMPvM71h4Y14x4Je2iYHqv6tRUq52zixb5nV9oFwp7Y1DVRU
```