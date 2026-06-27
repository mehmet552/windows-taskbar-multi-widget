<# :
@echo off
setlocal
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -Command "Invoke-Command -ScriptBlock ([ScriptBlock]::Create((Get-Content -LiteralPath '%~f0' -Raw)))"
pause
exit /b
#>

$ErrorActionPreference = "Stop"

$appName = "TaskbarMusicWidget"
$installDir = Join-Path $env:LOCALAPPDATA $appName
$startMenuFolder = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"
$startMenuShortcut = Join-Path $startMenuFolder "$appName.lnk"
$desktopFolder = [Environment]::GetFolderPath("Desktop")
$desktopShortcut = Join-Path $desktopFolder "$appName.lnk"
$exePath = Join-Path $installDir "$appName.exe"
$projectDir = Join-Path (Get-Location) "windows-Taskbar-widget"

Write-Host ">>> Eski surum kapatiliyor..." -ForegroundColor Cyan
Stop-Process -Name $appName -Force -ErrorAction SilentlyContinue

Write-Host ">>> Proje derleniyor ve yayinlaniyor (Release Modu)..." -ForegroundColor Cyan
Set-Location $projectDir
dotnet publish -c Release -o $installDir

Write-Host ">>> Baslat (Start) Menusu ve Masaustu kisayollari olusturuluyor..." -ForegroundColor Cyan
$WshShell = New-Object -comObject WScript.Shell

$Shortcut1 = $WshShell.CreateShortcut($startMenuShortcut)
$Shortcut1.TargetPath = $exePath
$Shortcut1.WorkingDirectory = $installDir
$Shortcut1.Description = "Taskbar Widget"
$Shortcut1.IconLocation = "$exePath,0"
$Shortcut1.Save()

$Shortcut2 = $WshShell.CreateShortcut($desktopShortcut)
$Shortcut2.TargetPath = $exePath
$Shortcut2.WorkingDirectory = $installDir
$Shortcut2.Description = "Taskbar Widget"
$Shortcut2.IconLocation = "$exePath,0"
$Shortcut2.Save()

Write-Host ">>> Kurulum tamamlandi! Uygulama baslatiliyor..." -ForegroundColor Green
Start-Process -FilePath $exePath -WorkingDirectory $installDir
