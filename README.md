# CompoundSentry

**Real-time Device Presence Detection & Analytics Platform**

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-6.0+-purple.svg)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-10.0+-green.svg)](https://docs.microsoft.com/en-us/dotnet/csharp/)

## 📡 Overview

CompoundSentry is a C#-based device presence detection and analytics system that tracks, monitors, and analyzes device entry/exit events in real-time. The system provides a live web dashboard, instant notifications, historical analytics, and comprehensive reporting capabilities.

### Perfect for:
- **Facility Security** - Track device presence across compound zones
- **Attendance Management** - Automatic check-in/out logging
- **Visitor Analytics** - Understand foot traffic patterns
- **Asset Tracking** - Monitor important equipment movement

## ✨ Core Features

### Detection & Tracking
- 🎯 **Real-time Presence Detection** - Instant entry/exit event logging
- 📊 **Live Device Dashboard** - Visualize currently present devices
- 🔔 **Instant Notifications** - Popup alerts for device movements
- 📈 **Historical Analytics** - Track patterns, trends, and statistics
- ⏱️ **Dwell Time Analysis** - Measure how long devices stay

### Data Management
- 💾 **Database Support** - SQL Server, SQLite, or PostgreSQL
- 📤 **Export Capabilities** - CSV/JSON/Excel reports
- 🔍 **Advanced Filtering** - Search by device ID, type, date range
- 📅 **Retention Policies** - Auto-cleanup of old records

### User Interface
- 🖥️ **ASP.NET Core Dashboard** - Modern web interface
- 📱 **Responsive Design** - Works on desktop, tablet, and mobile
- 🎨 **Dark/Light Mode** - Easy on the eyes
- 📊 **Interactive Charts** - Visual data exploration with Chart.js

## 🏗️ System Architecture



┌─────────────────┐ ┌──────────────────┐ ┌─────────────────┐
│ Wi-Fi Adapter │────▶│ Packet Sniffer │────▶│ Device Tracker │
│ (Monitor Mode) │ │ (Scapy/Pcap) │ │ (Entry/Exit) │
└─────────────────┘ └──────────────────┘ └────────┬────────┘
│
▼
┌─────────────────┐ ┌──────────────────┐ ┌─────────────────┐
│ Web Dashboard │◀────│ FastAPI/Flask │◀────│ Database │
│ (React/HTML) │ │ Backend │ │ (SQLite/PG) │
└─────────────────┘ └──────────────────┘ └─────────────────┘
│
▼
┌─────────────────┐
│ Notifications │
│ (WebSocket/ │
│ Desktop Popup)│
└─────────────────┘



## 🚀 Quick Start

### Prerequisites

- **.NET 8.0 SDK or later** (Download from [dotnet.microsoft.com](https://dotnet.microsoft.com/download))
- **SQL Server** (LocalDB, Express, or full) 
- **Git** (for cloning)

### Installation

```bash
# Clone the repository
git clone https://github.com/KIPLANGATLETTING/CompoundSentry.git
cd CompoundSentry

# Restore NuGet packages
dotnet restore

# Build the solution
dotnet build

# Set up the database
dotnet run --project src/CompoundSentry.Migrator

# Run the application
dotnet run --project src/CompoundSentry.Web

###Configuration

{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=CompoundSentry;Trusted_Connection=True;"
  },
  "AppSettings": {
    "DeviceTimeoutMinutes": 5,
    "RetentionDays": 90,
    "EnableNotifications": true
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  }
}

###Running the System

# Start the web server
dotnet run --project src/CompoundSentry.Web

# Access the dashboard
# Open browser to: https://localhost:5001
# Or: http://localhost:5000



📊 Dashboard Features
Live View
Currently Present Devices - Real-time device list with SignalR updates

Recent Activity Feed - Chronological entry/exit log

Status Summary - Counts and statistics at a glance

Auto-refresh - Live updates without page reload

Analytics Dashboard
Device Trends - Hourly/daily/weekly presence graphs

Peak Times - Identify busiest periods

Returning vs New - Device classification

Dwell Time Distribution - How long devices stay

Device Management
Device Registry - View and manage all tracked devices

Manual Check-in/out - Override when needed

Device Aliases - Assign friendly names to devices

Notes & Tags - Add metadata to devices

Reports
Export to CSV/Excel - Generate custom reports

Date Range Filtering - Focus on specific periods

Device Filtering - Report on specific devices

Scheduled Reports - Email reports automatically

🔌 API Endpoints
CompoundSentry provides a RESTful API for integration:
# Get all currently present devices
GET /api/devices/present

# Get device history
GET /api/devices/{deviceId}/history?from=2024-01-01&to=2024-01-31

# Register an entry event
POST /api/events/entry
{
  "deviceId": "AA:BB:CC:DD:EE:FF",
  "deviceType": "Smartphone",
  "location": "Main Gate",
  "timestamp": "2024-01-15T14:30:00Z"
}

# Register an exit event
POST /api/events/exit
{
  "deviceId": "AA:BB:CC:DD:EE:FF",
  "location": "Main Gate",
  "timestamp": "2024-01-15T17:45:00Z"
}

# Get analytics summary
GET /api/analytics/summary?period=week


🔔 Notifications
CompoundSentry supports multiple notification channels:

Desktop Notifications
Windows Toast Notifications

Sound alerts on entry/exit

Configurable threshold filtering

WebSocket (SignalR) Notifications
Real-time dashboard updates

Browser push notifications

Custom event handling

Webhook Integrations
Discord webhooks

Slack messages

###Custom HTTP endpoints
// Example: Register custom webhook
POST /api/settings/webhooks
{
  "name": "Security Team",
  "url": "https://your-server.com/webhook",
  "events": ["entry", "exit", "timeout"]
}


📁 Project Structure
CompoundSentry/
├── src/
│   ├── CompoundSentry.Core/           # Domain models & interfaces
│   │   ├── Entities/                  # Device, Event, Location
│   │   ├── Interfaces/                # Repository contracts
│   │   └── Services/                  # Business logic
│   ├── CompoundSentry.Infrastructure/ # Data access & external services
│   │   ├── Data/                      # DbContext & migrations
│   │   ├── Repositories/              # EF Core implementations
│   │   └── Notifications/             # SignalR & webhooks
│   ├── CompoundSentry.Web/            # ASP.NET Core frontend
│   │   ├── Controllers/               # API endpoints
│   │   ├── Views/                     # Razor pages
│   │   ├── wwwroot/                   # CSS, JS, images
│   │   └── Hubs/                      # SignalR hubs
│   └── CompoundSentry.Worker/         # Background services
│       ├── DeviceTimeoutService.cs    # Auto-exit after timeout
│       └── CleanupService.cs          # Data retention cleanup
├── tests/
│   ├── CompoundSentry.Tests.Unit/     # Unit tests
│   └── CompoundSentry.Tests.Integration/ # Integration tests
├── docs/
│   ├── setup.md
│   ├── api.md
│   └── deployment.md
├── scripts/
│   ├── setup-database.ps1
│   └── deploy.ps1
├── CompoundSentry.sln                 # Visual Studio solution
├── appsettings.json                   # Configuration
└── README.md                          # This file


🛠️ Development
Prerequisites for Development
Visual Studio 2022 or VS Code + C# extensions

SQL Server Developer Edition 

.NET 7.0 SD

Running Tests
# Run all tests
dotnet test

# Run with coverage report
dotnet test /p:CollectCoverage=true

Database Migrations
# Add a new migration
dotnet ef migrations add AddDeviceMetadata --project src/CompoundSentry.Infrastructure

# Update database
dotnet ef database update --project src/CompoundSentry.Infrastructure

🚢 Deployment
Windows Service
# Publish the application
dotnet publish src/CompoundSentry.Web -c Release -o ./publish

# Create and start Windows service
New-Service -Name "CompoundSentry" -BinaryPathName "C:\publish\CompoundSentry.Web.exe"
Start-Service -Name "CompoundSentry"

🔒 Security
JWT Authentication - Secure API access

Role-based Authorization - Admin, Viewer, Editor roles

HTTPS by default - TLS encryption

SQL Injection protection - Entity Framework parameterization

CORS configuration - Restrict API access to trusted origins

📊 Performance
SignalR for real-time updates with minimal overhead

EF Core with compiled queries for database efficiency

Response caching for frequently accessed data

Async/await throughout for scalability

Connection pooling for database connections

⭐ Star this repo if you find it useful!

Report bugs via Issues:ckiplangat49@gmail.com


