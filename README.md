# Desk Paws

A tiny Windows desktop cat that reacts to typing and mouse movement. Inspired by the idea behind [LittleOne](https://xiaozhilittleone.com/); this is an independent app with original character artwork and is not affiliated with its creator.

## Privacy

Desk Paws has no network code, analytics, account, activation, or update check. A Windows keyboard hook sees virtual key codes only to choose which paw reacts. Codes stay in memory while keys are held and are removed on release. The app never reads typed characters or saves a key history. Mouse activity is used for animation only. Choose **Pause keyboard reactions** from the cat or tray menu to remove the keyboard hook entirely.

Only preferences and window position are saved in `%APPDATA%\DeskPaws\settings.json`.

The source is short enough to audit: see `KeyboardCallback` in `CatWindow.cs`. To verify network behavior on your own machine, run the app while monitoring it with Windows Resource Monitor or a firewall.

## Use

Download `DeskPaws.exe` from [Releases](../../releases), then run it on Windows 10/11. Right click the cat or its tray icon for four plush coats, size, sleep timing, left handed mouse mode, privacy pause, and Quit. Drag the cat to move it. Press and hold its head to pet it. Your choices persist across restarts. A second launch will not start another keyboard hook.

Windows may show a SmartScreen warning because the app is unsigned. The source and build workflow are public so you can inspect or build it yourself.

## Build

Install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) on Windows, then run:

```powershell
dotnet build -c Release
dotnet run -- --self-test
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

The standalone EXE appears in `bin/Release/net8.0-windows/win-x64/publish/`.

## License

MIT. See [LICENSE](LICENSE).

