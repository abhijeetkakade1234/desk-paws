# Desk Paws

A tiny Windows desktop cat that types along with you. Inspired by the idea behind [LittleOne](https://xiaozhilittleone.com/); this is an independent, original app and is not affiliated with its creator.

## Privacy

Desk Paws has no network code, analytics, account, activation, or update check. A Windows keyboard hook sees virtual key codes only to choose which paw moves. Codes stay in memory while keys are held and are removed on release. The app never reads typed characters or saves a key history. Mouse activity is used for animation only.

The source is short enough to audit: see `KeyboardCallback` in `CatWindow.cs`. To verify network behavior on your own machine, run the app while monitoring it with Windows Resource Monitor or a firewall.

## Use

Run it on Windows 10/11. Right click the cat for color, size, sleep timing, left handed mouse mode, and Quit. Drag its lower half to move it. Press and hold its head to pet it.

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

