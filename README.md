# WPF Circle Monitor

A lightweight **circular CPU & RAM monitor** built with WPF. This application creates a round, borderless, draggable window that displays:

- **Real-time CPU usage** (%)
- **Real-time RAM usage** (in GB, with one decimal place)
- **Color-coded** progress rings (green / yellow / red based on usage)
- **System tray support** (minimize to tray, restore, exit)
- **Double-click** the circle to open Task Manager
- **Right-click** context menu for minimize and exit
- **Mouse hover** displays detailed RAM usage in MB
- **Mouse wheel** changes window opacity
- **Starts at the bottom-right corner** of the screen **where the mouse is** located

## Features

1. **Circular, borderless WPF window**  
   
   - Draggable by left-click dragging.  
   - Automatically placed at the bottom-right of the monitor under the mouse cursor.  
   - Opacity adjustable via mouse wheel (range 0.3 ~ 1.0).

2. **CPU & RAM usage**  
   
   - Uses `PerformanceCounter` and `ManagementObjectSearcher` to retrieve CPU and memory info.  
   - Updated every second via a `DispatcherTimer`.  
   - RAM usage displayed in GB, with a tooltip showing MB details.

3. **Color-coded arcs**  
   
   - Green (< 50%), Yellow (< 80%), Red (>= 80%) arcs for CPU and RAM usage.

4. **System tray (notify icon)**  
   
   - Minimizes to tray if the window is minimized.  
   - Right-click tray icon to **Show**, **Hide**, or **Exit** the app.  
   - Double-click tray icon to restore the window.

5. **Double-click**  
   
   - Double-clicking the circular window itself opens Windows Task Manager.

## Requirements

- **.NET 6+ (Windows)** or **.NET Framework 4.8** (Windows-only) with WPF support
- **Hardcodet.Wpf.TaskbarNotification** (NuGet package)
- **System.Management** (NuGet package) if using .NET 5+  
- Windows OS that supports PerformanceCounter and WMI

## How to Build and Run

1. **Clone** this repository or download the ZIP.  
2. Open the `.sln` file in **Visual Studio** (or another IDE that supports WPF).  
3. Make sure you have installed the required NuGet packages:
   - `Hardcodet.Wpf.TaskbarNotification`
   - `System.Management` (if .NET 5+)  
4. **Build** the project.  
5. **Run** the application.  
   - A round window appears, placed at the bottom-right of the monitor where your mouse was when you started the app.  
   - CPU & RAM usage arcs update every second.

## Usage

- **Left-click** + drag to move the window.  
- **Right-click** the window for a quick menu to `Minimize` or `Exit`.  
- **Double-click** the window to open **Task Manager**.  
- **Mouse wheel** up/down to increase/decrease window opacity.  
- **Minimize** (or right-click -> Hide) to send it to the system tray.  
- **Right-click** the tray icon to Show/Hide/Exit.  
- **Double-click** the tray icon to restore the window.

#### License

This project is released under the [MIT License](LICENSE).  
You are free to use, copy, modify, merge, publish, distribute, sublicense, 
and/or sell copies of the software, subject to the license terms.
