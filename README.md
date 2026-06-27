
A versatile, lightweight, and highly customizable Windows taskbar widget designed to enhance your productivity and media experience. This application seamlessly integrates multiple useful tools directly into your Windows taskbar, offering a unified control center for your daily tasks.

### Preview


---


https://github.com/user-attachments/assets/951011ad-cb2f-4111-a9cb-2060fbdeda28


 
 video belong; version-1

 


## Features

- **Music Widget & Drawer:** Displays the currently playing song along with dynamic visualizers (Bars, Wave, or Cat). Interacting with the music widget opens a sleek, full-width popup drawer. The drawer features a blurred album art background, detailed track information, timeline seeking, and volume adjustment.
- **Global Hardware-Accelerated Bars:** The widget features ultra-thin, highly optimized global progress bars that span across all active widgets. The top bar tracks real-time playback progress (tinted by the current album's accent color), while the bottom bar reflects the master system volume. Both are GPU-accelerated for zero CPU overhead.
- **Pomodoro Timer:** Stay focused and manage your work sessions effectively using the Pomodoro technique.
- **Stopwatch:** A clean, quick stopwatch for timing your activities.
- **Countdown Timer:** Set a specific amount of time to count down to zero.
- **Control Panel:** A centralized settings menu to manage active widgets, reorder them, and customize application behavior.

## Smart Behavior & Settings

Through the integrated **Control Panel**, you can configure the widget to your liking:
- **Dynamic Positioning:** Adjust the widget's exact horizontal position on the taskbar (0% to 100%). The widget automatically calculates your screen resolution and taskbar width to ensure it perfectly scales and never bleeds off-screen.
- **Smart Auto-Hide:** Intelligently hides itself when you are playing a game or running any application in full-screen mode to ensure an uninterrupted experience.
- **Start with Windows:** Optionally launch the widget automatically when you sign in.

## Installation

To install the widget, simply run the provided batch script:

1. Double-click on `Install.bat`.
2. The script will automatically build the project, place it in your local application data folder, and add a shortcut to your startup folder so it runs automatically.

## Development

For developers, a `Run-Dev.bat` script is included to quickly compile, kill any existing instances, and run the application in Debug mode without installing it to the system directories.

## Uninstallation

To completely remove the widget from your system:

1. Double-click on `Uninstall.bat`.
2. This will safely stop the application, remove all background files, and delete the startup shortcut.

## Requirements & Notes

- **.NET 10.0 SDK** (Required to build and publish the project).
- **Windows 10 / Windows 11**.
- **Important:** To ensure this widget displays correctly without overlapping, you must **disable the native Windows Widgets** (Weather, News, etc.) from your Taskbar settings.
  - *To disable on Windows 11:* Right-click the Taskbar -> Taskbar settings -> Toggle off "Widgets".
