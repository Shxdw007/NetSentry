# NetSentry 🛡️

**NetSentry** is a real-time Remote Monitoring & Management (RMM) . Built with **.NET 10**, it allows administrators to monitor CPU, RAM, GPU, and Storage usage of multiple remote agents instantly.

## 🚀 Features

- **Real-time Monitoring**: Uses **SignalR** (WebSockets) for instant data updates.
- **Hardware Recon**: Automatically detects **CPU & GPU models** and VRAM size using WMI.
- **Multi-Drive Support**: Dynamically monitors all connected storage devices (HDD, SSD, USB).
- **Cross-Platform Agent**: Collects system metrics (CPU, Memory, Disk) and hardware specs.
- **System Tray Integration**: Agent runs silently as a system tray icon (no visible window).
- **WPF Dashboard**: Modern UI with live charts, hardware specs display, and status indicators.
- **DedSec Aesthetic**: Custom dark theme with neon accents and terminal-style fonts.
- **Secure Configuration**: Server IP stored in local `appsettings.json` (not in repo).

## 🛠️ Tech Stack

- **Server**: ASP.NET Core Web API, SignalR Hub
- **Client (Agent)**: .NET Console App → WinExe (System Tray), System.Diagnostics, System.Management (WMI)
- **UI (Dashboard)**: WPF, XAML, MVVM pattern, JSON Serialization
- **Configuration**: Microsoft.Extensions.Configuration

## 📸 Screenshots
<img width="879" height="590" alt="image" src="https://github.com/user-attachments/assets/33401117-e4a2-44a3-8fac-fdd441012e93" />
<img width="572" height="130" alt="image" src="https://github.com/user-attachments/assets/6abd44e3-8331-48be-bf93-5098c3435740" />
<img width="1107" height="613" alt="image" src="https://github.com/user-attachments/assets/54341ebb-33e2-4552-8154-37760c9493e6" />

## 🔧 Setup

### Agent Setup
Create `appsettings.json` in agent folder:
```json
{
  "ServerUrl": "http://YOUR_SERVER_IP:5000/rmmHub"
}
```

### Dashboard Setup
Create appsettings.json in dashboard folder with same content
```json
{
  "ServerUrl": "http://YOUR_SERVER_IP:5000/rmmHub"
}
```
