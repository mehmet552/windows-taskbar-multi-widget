

https://github.com/user-attachments/assets/11a0f4ee-1ef7-4f49-aea9-518daeb36ec4


# Windows Taskbar Multi-Widget

A versatile and lightweight Windows taskbar widget that enhances your productivity and media experience. This application integrates multiple useful tools directly into your Windows taskbar, including a Music Player controller, a Pomodoro timer, a Stopwatch, and a Countdown Timer. 

It is designed to be unobtrusive and automatically hides itself when a full-screen application or game is running, ensuring an uninterrupted experience.

## Features

- **Music Widget:** Displays the currently playing song with controls to pause, resume, skip tracks, change song duration, and adjust the volume.
- **Pomodoro Timer:** Stay focused and manage your work sessions effectively using the Pomodoro technique.
- **Stopwatch:** A simple and quick stopwatch for timing activities.
- **Countdown Timer:** Set a specific amount of time to count down to zero.
- **Auto-Hide:** Intelligently hides itself when you are playing a game or running any application in full-screen mode.

## Installation

To install the widget, simply run the provided batch script:

1. Double-click on `Install.bat`.
2. The script will automatically build the project, place it in your local application data folder, and add a shortcut to your startup folder so it runs automatically when you sign in.

## Uninstallation

To remove the widget from your system:

1. Double-click on `Uninstall.bat`.
2. This will stop the application, remove the files, and delete the startup shortcut.

## Requirements

- **.NET 10.0 SDK** (Required to build and publish the project using the install script)
- **Windows 10 / Windows 11**
- **Important:** To ensure this widget displays correctly without overlapping, you must **disable the native Windows Widgets** (Weather, News, etc.) from the Taskbar settings.
  - *To disable on Windows 11:* Right-click the Taskbar -> Taskbar settings -> Toggle off "Widgets".
