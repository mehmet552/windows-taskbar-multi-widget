# Windows Taskbar Multi-Widget

A versatile, lightweight, and customizable Windows taskbar widget designed to enhance your productivity and media experience. This application seamlessly integrates multiple useful tools directly into your Windows taskbar, offering a unified control center for your daily tasks.

### Preview
https://github.com/user-attachments/assets/9663ef22-51eb-43ae-b1a8-5effa9c47565

---
video:version-2

## Features

- **Music Widget:** Displays the currently playing song along with a dynamic visualizer. Includes controls to pause, resume, skip tracks, seek through the song, and adjust the volume.
- **Pomodoro Timer:** Stay focused and manage your work sessions effectively using the Pomodoro technique.
- **Stopwatch:** A clean, quick stopwatch for timing your activities.
- **Countdown Timer:** Set a specific amount of time to count down to zero.
- **Control Panel:** A centralized settings menu to manage active widgets, reorder them, and customize app behavior.

## Smart Behavior & Settings

Through the integrated **Control Panel**, you can easily configure the widget to your liking:
- **Dynamic Positioning:** Adjust the widget's exact horizontal position on the taskbar (0% to 100%). The widget automatically calculates your screen resolution and taskbar width to ensure it perfectly scales and never bleeds off-screen.
- **Smart Auto-Hide:** Intelligently hides itself when you are playing a game or running any application in full-screen mode to ensure an uninterrupted experience.
- **Start with Windows:** Optionally launch the widget automatically when you sign in.

## Installation

To install the widget, simply run the provided batch script:

1. Double-click on `Install.bat`.
2. The script will automatically build the project, place it in your local application data folder, and add a shortcut to your startup folder so it runs automatically.

## Uninstallation

To completely remove the widget from your system:

1. Double-click on `Uninstall.bat`.
2. This will safely stop the application, remove all background files, and delete the startup shortcut.

## Requirements & Notes

- **.NET 10.0 SDK** (Required to build and publish the project using the install script).
- **Windows 10 / Windows 11**.
- **Important:** To ensure this widget displays correctly without overlapping, you must **disable the native Windows Widgets** (Weather, News, etc.) from your Taskbar settings.
  - *To disable on Windows 11:* Right-click the Taskbar -> Taskbar settings -> Toggle off "Widgets".
