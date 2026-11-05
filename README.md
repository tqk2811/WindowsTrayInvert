# Windows Tray Invert

English | [Tiếng Việt](README-vi.md)

Windows 11 hides most new notification area (tray) icons in the hidden icon menu, so you keep turning them on one by one in Settings.
Windows Tray Invert flips that rule: **every tray icon is shown on the taskbar**, and only the apps you pick go to the hidden icon menu.

## Features

- Shows every tray icon by default, including icons of apps installed later.
- A per-app list (icon, name, path) with a switch to show the icon on the taskbar or move it to the hidden icon menu.
- Show all / hide all for the listed apps (after the search filter), with confirmation.
- Search by name or path; open the app's folder from each row.
- Clean up orphaned entries: tray icon records whose exe no longer exists. You pick which ones to remove.
- Start with Windows (starts hidden in the tray).
- Windows 11 look (Mica), System / Light / Dark theme, English and Vietnamese.
- Single instance: launching it again brings the running window to the front.
- No administrator rights needed.

## Install

1. Download a zip from [Releases](../../releases):
   - `...-win-x64-standalone.zip`: includes the .NET runtime (pick this if unsure);
   - `...-win-x64-needs-dotnet8.zip`: smaller, needs the [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0).
2. Unzip anywhere and run `WindowsTrayInvert.exe`.
3. Turn on **Start with Windows** if you want it to keep working after a restart.

Requires Windows 11 (it uses the tray icon settings Windows 11 keeps per app).

## How it works

Windows stores one record per tray icon under `HKCU\Control Panel\NotifyIconSettings\<id>`. The value `IsPromoted` decides where the icon goes: `1` = on the taskbar, `0` or missing = hidden icon menu. Explorer watches this key and applies a change within about a second, with no restart.

The app watches the same key. Whenever a record is added or changed, it sets `IsPromoted` from your list: `1` unless the app's exe is in your hide list. It only writes values that differ.

Notes:
- The hide list is matched by exe path. Apps that put several icons in the tray from one exe (for example NVIDIA's `nvcontainer.exe`) share one setting.
- Changing an icon in Windows Settings while the app runs is overwritten by the app's list; change it in the app instead.
- Removing an orphaned entry deletes its registry record. Windows creates a new one if that app shows a tray icon again.

## Settings

Stored in `%AppData%\WindowsTrayInvert\config.json` (hide list, theme, language). Start with Windows is the `WindowsTrayInvert` value under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.

To uninstall: turn off Start with Windows, exit from the tray menu, then delete the app folder and `%AppData%\WindowsTrayInvert`.

## Build

Needs the .NET 8 SDK (or newer) on Windows.

```
dotnet build src/WindowsTrayInvert.slnx -c Release
```

The version comes from the nearest git tag `vM.N.P` (`M.N.P-dev.K` for commits after the tag).

## Release

Push a tag `vM.N.P` on a commit of `master`; GitHub Actions ([release.yml](.github/workflows/release.yml)) builds both zips and publishes a GitHub Release with notes from the commits since the previous tag:

```
git tag v1.0.0
git push origin v1.0.0
```
