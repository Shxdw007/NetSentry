# NetSentry 🛡️

**NetSentry** is a real-time Remote Monitoring & Management (RMM). Built with **.NET 10**, it allows administrators to monitor CPU, RAM, GPU, and Storage usage of multiple remote agents instantly.

## 🚀 Features

- **Real-time Monitoring**: Uses **SignalR** (WebSockets) for instant data updates.
- **Hardware Recon**: Automatically detects **CPU & GPU models** and VRAM size using WMI.
- **Multi-Drive Support**: Dynamically monitors all connected storage devices (HDD, SSD, USB).
- **Cross-Platform Agent**: Collects system metrics (CPU, Memory, Disk) and hardware specs.
- **WPF Dashboard**: Modern UI with live charts, hardware specs display, and status indicators.

## 🛠️ Tech Stack

- **Server**: ASP.NET Core Web API, SignalR Hub
- **Client (Agent)**: .NET Console App, System.Diagnostics, System.Management (WMI)
- **UI (Dashboard)**: WPF, XAML, MVVM pattern, JSON Serialization

## 📸 Screenshots
![photo_1_2026-01-06_22-42-22](https://github.com/user-attachments/assets/7944c5c9-f1a7-442c-a086-ae5abb1bc9a7)
![photo_2_2026-01-06_22-42-22](https://github.com/user-attachments/assets/9469c37f-4d3f-4a85-873d-e926b1550c5d)
![photo_2026-01-06_19-51-08](https://github.com/user-attachments/assets/847735cc-b59e-4e27-82bc-2b31b8bdea65)



