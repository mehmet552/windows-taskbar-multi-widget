$ErrorActionPreference = "Stop"
$appName = "TaskbarMusicWidget"
$installDir = Join-Path $env:LOCALAPPDATA $appName
$exePath = Join-Path $installDir "$appName.exe"

$desktop = [Environment]::GetFolderPath("Desktop")
$shortcutPath = Join-Path $desktop "Taskbar Widget.lnk"

$WshShell = New-Object -comObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut($shortcutPath)
$Shortcut.TargetPath = $exePath
$Shortcut.WorkingDirectory = $installDir
$Shortcut.Description = "Taskbar Music Widget"
$Shortcut.IconLocation = "$exePath,0"
$Shortcut.Save()

# Also update the one in the project directory just in case
$projectShortcutPath = Join-Path (Get-Location) "Taskbar Widget.lnk"
$Shortcut2 = $WshShell.CreateShortcut($projectShortcutPath)
$Shortcut2.TargetPath = $exePath
$Shortcut2.WorkingDirectory = $installDir
$Shortcut2.Description = "Taskbar Music Widget"
$Shortcut2.IconLocation = "$exePath,0"
$Shortcut2.Save()

Write-Host "Shortcuts updated."

# Try to refresh Windows icon cache
ie4uinit.exe -show
