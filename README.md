# AutoHeadsetSwitcher

AutoHeadsetSwitcher automatically switches your Windows audio output between speakers and wireless headsets that use USB dongles, solving a common issue with these devices.

## The Problem

USB dongle-based wireless headsets remain "connected" to Windows even when powered off, unlike Bluetooth devices. This means:

1. Windows doesn't automatically switch back to speakers when you turn off your headset.
2. You have to manually change audio devices each time you want to use speakers or the headset.

## The Solution

AutoHeadsetSwitcher detects when your wireless headset is actually on or off and automatically switches your Windows audio output accordingly.

## Current Status: MVP

This is a Minimum Viable Product (MVP) version:

- Currently supports Corsair wireless headsets
- Support for other brands and devices is planned for future releases

## Features

- Auto-detects wireless headset power state
- Switches to headset audio when headset is powered on
- Reverts to default speakers when headset is powered off
- Runs silently in system tray
- Starts with Windows

## Requirements

- Windows OS
- .NET Framework
- For Corsair devices: Corsair iCUE software

## Usage

1. Clone repo, obtain iCueSdk dll and place it in x64/x86 folder build and run `AutoHeadsetSwitcher.exe`(Run outside debug to register it on startup)
2. The app will run in your system tray
3. Use your headset and speakers as normal - switching is automatic

## Upcoming Features

- Support for additional headset brands and models
- Custom configuration options for audio devices

## Troubleshooting

Logs are stored in `%LocalAppData%\AutoHeadsetSwitcher\logs\`

## Contributing

Contributions are welcome, especially for adding support for new devices!

## License

[Your chosen license]