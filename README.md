# NetSentry 🛡️

NetSentry is a full-stack, real-time Remote Monitoring & Management (RMM) system. Built with **.NET 10**, it allows administrators to monitor CPU, RAM, GPU, and Storage usage of multiple remote agents instantly. 

The system features a centralized ASP.NET Core REST API, robust PostgreSQL data storage, and provides monitoring capabilities through both a modern WPF desktop client and a browser-based Blazor WebAssembly dashboard.

## 🚀 Key Features

* **Real-time Monitoring:** Uses **SignalR** (WebSockets) for instant, sub-second data updates across all connected clients.
* **Smart DB Throttling:** Metrics are sent to the UI instantly, but saved to the **PostgreSQL** database only once every 30 seconds to prevent database overload and optimize performance.
* **Role-Based Access Control (RBAC):** Secure JWT authentication with user roles (Admin, Viewer) to restrict access to the API and dashboards.
* **Dual Dashboards:** * 🌐 **Blazor Web Dashboard:** A dynamic, standalone browser application featuring a custom "DedSec" hacker aesthetic, real-time progress bars, and responsive design.
    * 🖥️ **WPF Desktop Dashboard:** Modern desktop UI with live charts and hardware spec displays.
* **Admin Console Utility:** A dedicated CLI tool for administrators to easily create, update, delete users, and assign roles directly in the database.
* **RESTful API & Swagger:** Fully documented REST API with Swagger UI integration for easy testing of CRUD operations.
* **Hardware Recon:** Automatically detects CPU & GPU models and VRAM size using WMI.
* **Multi-Drive Support:** Dynamically monitors all connected storage devices (HDD, SSD, USB).
* **Cross-Platform Agent:** Collects system metrics silently in the background (System Tray integration).

## 🛠️ Tech Stack

* **Server / Backend:** ASP.NET Core Web API, SignalR Hub, JWT Bearer Authentication, Swagger.
* **Database:** PostgreSQL, Entity Framework Core (Code-First approach).
* **Web Client:** Blazor WebAssembly (C#), HTML/CSS, SignalR Client.
* **Desktop Client:** WPF, XAML, MVVM pattern.
* **Agent / Admin Tools:** .NET 10 Console Applications, `System.Management` (WMI).

## 🏗️ System Architecture

1.  **Server (`NetSentry.Server`):** The core API. Handles agent connections, authenticates users, writes to the DB, and broadcasts metrics via SignalR.
2.  **Database:** PostgreSQL stores Users, Roles, Machines, and historical Metric records.
3.  **Agent (`NetSentry.Agent`):** Runs on target PCs, gathering and pushing hardware metrics.
4.  **Web/WPF Clients (`NetSentry.Web` / `NetSentry.WPF`):** Consume the REST API for historical data and SignalR for live monitoring.
5.  **Admin Manager (`NetSentry.AdminApp`):** Direct DB management tool for user accounts.

## 📸 Screenshots
<img width="401" height="453" alt="image" src="https://github.com/user-attachments/assets/f13d7a24-12a7-4d7f-8718-bcb6c530a42e" />
<img width="883" height="588" alt="image" src="https://github.com/user-attachments/assets/a96206ac-bd40-46d6-ae9a-d3639d0e46e1" />
<img width="1112" height="250" alt="1`32435467587ityjfg" src="https://github.com/user-attachments/assets/0ba64783-69ad-49c1-b64e-632b86b4c002" />
<img width="1903" height="919" alt="image" src="https://github.com/user-attachments/assets/a27390ba-a849-438f-9742-eb7c64a20a22" />
<img width="1919" height="483" alt="image" src="https://github.com/user-attachments/assets/45684321-85c0-4159-85f2-6a07b8f09aed" />
<img width="1105" height="626" alt="image" src="https://github.com/user-attachments/assets/91171931-433e-4629-a87e-f3738b85d098" />
<img width="568" height="576" alt="image" src="https://github.com/user-attachments/assets/a833cb4f-26c9-4ba6-9081-8abd9e1f4a8c" />



## 🔧 Setup & Installation

### 1. Server & Database Setup
1. Install PostgreSQL and create a database (e.g., `netsentry_db`).
2. In `NetSentry.Server/appsettings.json`, configure your connection string:
   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Host=localhost;Port=5432;Database=netsentry_db;Username=postgres;Password=YOUR_PASSWORD"
   },
   "Jwt": {
     "Key": "YOUR_SUPER_SECRET_LONG_KEY_HERE",
     "Issuer": "NetSentryServer",
     "Audience": "NetSentryClients"
   }
   ```
 3. Run Entity Framework migrations to create the tables:

```Bash
dotnet ef database update
```
### 2. Admin Setup (Creating the first user)
Run the Admin Console Utility and follow the on-screen prompts to create an Admin role and your first administrator account.

### 3. Agent & Dashboard Setup
Create appsettings.json in both the Agent and WPF Dashboard output folders:

```JSON
{
  "ServerUrl": "http://YOUR_SERVER_IP:5000/rmmHub",
  "ApiUrl": "http://YOUR_SERVER_IP:5000"
}
```
(Note: For the Blazor WebAssembly client, the API base address is configured in Program.cs)

### 📄 License
This project is open-source and available under the MIT License.
