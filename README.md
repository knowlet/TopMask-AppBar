# TopMask AppBar

The pixels at the top of my laptop screen are damaged, but replacing the screen is too expensive. So I built this small tool to cover the broken area and keep windows below it.

TopMask places a black, always-on-top window along the top of a selected display. It registers that window as a Windows AppBar, so the Windows shell can reserve the space and keep maximized windows in the remaining work area.

## Features

- Choose a display and set the mask height in physical pixels.
- Detect and preselect the laptop's built-in display when Windows reports its connection type.
- Use per-monitor DPI awareness and support multiple displays.
- Adjust the height or target display while the mask is active.
- Remove the AppBar and restore the work area when the mask is stopped or the application is closed.

## Requirements

- Windows 10 or Windows 11, x64.
- No separate .NET installation is needed for the self-contained release package.

## Download and run

Download `TopMask-AppBar-win-x64.zip` from the [Releases page](https://github.com/knowlet/TopMask-AppBar/releases), extract it, and run `TopMaskAppBar.exe`.

The current interface is in Traditional Chinese. Select the target display, enter the height in pixels, and click the left button to enable the mask. Click the right button to stop it and restore the work area, or close the application. The built-in display is selected automatically when it can be identified.

## Build from source

Install the [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0), then run the following in PowerShell from the project directory:

```powershell
dotnet publish .\TopMaskAppBar.csproj -c Release -r win-x64 --self-contained false
```

Run `TopMaskAppBar.exe` from `bin\Release\net10.0-windows\win-x64\publish\`. Keep the executable and the other files in that directory together. This framework-dependent build requires the .NET 10 Desktop Runtime on the computer running it.

## CI and releases

Pushes to `develop` and pull requests build a Windows x64 package and upload it as a workflow artifact. Pushing a version tag such as `v1.0.0` also publishes that self-contained package to GitHub Releases. The workflow can also be run manually to produce an artifact.

## Notes

- If the display configuration changes, TopMask stops the mask and refreshes the display list. Select the display again and re-enable it.
- Windows may move an AppBar to avoid the taskbar or other AppBars. Full-screen applications can also behave differently from maximized desktop windows.
- The mask and work-area restoration were checked on a Windows laptop with an internal display. An external display was connected, but was not used as the mask target during that check.
