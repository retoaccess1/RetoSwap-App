# Tested with emulated Pixel 6 - Android 14.0 - API 34
# Tested with Samsung S21 - Android 14.0 - API 34

# Get Termux
Download/install Termux APK from f-droid

# Setup Termux
pkg install termux-am - Used to switch apps from Termux to Haveno, bring Haveno to foreground
termux-setup-storage - Needed for communication between Termux and Haveno app
allow-external-apps property must be set to true in ~/.termux/termux.properties - Used for RUN_COMMAND so we can send commands to Termux
termux-reload-settings

# Get ubuntu
pkg update -y
pkg upgrade -y  - this has some more options that need user input - said no to both!
pkg install proot-distro -y
proot-distro add ubuntu
proot-distro login ubuntu

# Set up ubuntu
apt update -y
apt upgrade -y
apt install make wget git -y
apt install openjdk-21-jdk -y  - have to run this twice?

# Build haveno
git clone https://github.com/haveno-dex/haveno.git
cd haveno
git checkout master
make skip-tests

# Start daemon
Note: just for testing, normally you should let the app start this
make user1-daemon-stagenet

# Update haveno - not tested, check the github repo for a stable release.
git checkout master
git pull
make clean && make skip-tests

# TOR
when running on actual device - tor needs to be installed separately
apt install tor -y
nano /etc/tor/torrc and uncomment torControlPort=9051

makefile update user1-daemon-stagenet with options
--torControlHost=127.0.0.1
--torControlPort=9051

# Other
The allow-external-apps property must be set to true in ~/.termux/termux.properties in Termux app, regardless of if the executable path is inside or outside the ~/.termux/tasker/ directory. Check here for more info.
termux-wake-lock ?

# Remote node
install orbot, play store or apk,
settings -> power user mode