# GregOrigin Cleaner Suite

![Platform](https://img.shields.io/badge/platform-Windows%2011-0078D7)
![Runtime](https://img.shields.io/badge/runtime-.NET%208-512BD4)
![License](https://img.shields.io/badge/license-MIT-2EA44F)
![Ads](https://img.shields.io/badge/ads-none-2EA44F)
![Telemetry](https://img.shields.io/badge/telemetry-none-2EA44F)

<img width="1920" height="1072" alt="GregoCleanerS" src="https://github.com/user-attachments/assets/f3e65009-372e-4162-aa84-d5e06ae5c6fb" /> <br>


[Watch it in action](https://www.youtube.com/watch?v=tRDfv7WFEIU)

GregOrigin Cleaner Suite is a transparent Windows 11 maintenance app for people who want practical cleanup, uninstall, update, startup, service, hardware, and disk optimization tools without adware, subscriptions, or hidden background services.

It is built around native Windows tools and readable source code. The app favors preview, audit logs, backups, and allowlists over risky "one-click magic" optimization.

[Download for Windows](https://github.com/gregorik/gregorigin-cleaner-suite/releases/latest/download/GregOriginSuiteApp.exe) | [View releases](https://github.com/gregorik/gregorigin-cleaner-suite/releases) | [Read the manual](https://gregorigin.gitbook.io/cleaner-suite/) | [Visit gregorigin.com](https://gregorigin.com)

---

## Why This Exists

Most PC cleaners ask for a lot of trust. They promise speed, touch unclear areas of the system, and often hide their real incentives behind trials, ads, telemetry, or upsells.

GregOrigin Cleaner Suite takes the opposite approach:

- No registry "cleaning"
- No adware or sponsored installs
- No background service
- No telemetry
- No locked premium tier
- No proprietary driver
- Source code available in this repository

The goal is simple: give Windows users a focused maintenance dashboard that does useful work while staying understandable.

[Updated manual is here.](https://gregorigin.com/Cleaner_Suite/Manual/)

---

## Download

The recommended download is the standalone Windows executable from the latest GitHub release:

**[Download GregOriginSuiteApp.exe](https://github.com/gregorik/gregorigin-cleaner-suite/releases/latest/download/GregOriginSuiteApp.exe)**

Current final build:

| File | Size | SHA256 |
| --- | ---: | --- |
| `GregOriginSuiteApp.exe` | 70.5 MB | `64F7B9FD672B4DA6AECD08B2294F03684EB903730D56435136AE0C0040F36B02` |

Windows SmartScreen may warn that the app is from an unknown publisher because this release is not code-signed. If you trust this repository and want to run it, choose **More info** and then **Run anyway**.

---

## What It Can Do

### Bulk Uninstaller

Remove multiple desktop apps from one searchable list.

- Reads installed app entries from standard Windows uninstall registry locations
- Supports multi-select uninstall batches
- Builds safer uninstall commands for MSI and common uninstall formats
- Includes an optional remnant scrub for selected app folders
- Requests administrator elevation only when the selected action needs it

### Safe System Cleaner

Preview cleanup targets before deleting files.

- Analyzes Windows Temp, user Temp, Edge cache, Chrome cache, Prefetch, Windows Update cache, Recycle Bin, and selected advanced targets
- Writes a dry-run audit before cleanup
- Appends execution results after cleanup
- Keeps audit logs under `%APPDATA%\GregOriginSuite\Audit`
- Leaves registry cleanup out by design

### Winget Software Manager

Use Microsoft's package manager from a friendlier dashboard.

- Checks available updates with `winget upgrade`
- Updates selected packages or all supported packages
- Searches the Winget repository
- Installs selected packages from search results

### Startup Manager

Review and manage startup entries without losing a way back.

- Reads user and machine startup registry keys
- Reads startup folder items
- Reads logon/startup scheduled tasks
- Enables, disables, deletes, and restores entries
- Saves backups under `%APPDATA%\GregOriginSuite\Backups`

### Service Tuning

Work only with a curated service allowlist instead of exposing every Windows service.

- Shows service status, startup type, default recommendation, and reason
- Starts or stops selected allowlisted services
- Disables selected allowlisted services
- Saves restore data before startup mode changes

### Hardware And Storage Checks

Quickly inspect the machine before doing maintenance.

- Shows CPU, RAM, GPU, and OS information
- Reads drive health status through WMI
- Scans `C:\` for the top 50 largest files

### Disk Optimization

Run native Windows drive optimization tools from the app.

- Analyzes fixed drives through `defrag.exe`
- Optimizes drives with native Windows options
- Supports SSD retrim
- Supports optional boot optimization
- Includes cancellation support for long operations

---

## Safety Model

GregOrigin Cleaner Suite is intentionally conservative where a cleaner app should be conservative.

- Cleanup has an **Analyze** step before deletion.
- Cleanup writes an audit log before and after execution.
- Startup and service actions create backups before destructive changes.
- Service management is limited to a curated allowlist.
- Privileged operations ask to restart elevated instead of silently failing.
- Registry cleaning is excluded because it is high risk and low value on modern Windows.

Some actions still modify the system. Read the on-screen prompts, review cleanup audits, and avoid advanced options unless you understand the consequence.

---

## Requirements

- Windows 11
- 64-bit Windows installation
- Winget installed for software update and install features
- Administrator rights for uninstall, protected cleanup, startup, service, network reset, and defrag operations

The app is built with .NET 8 WPF and published as a self-contained Windows executable.

---

## Screenshots

<img width="560" alt="Bulk uninstaller screenshot" src="https://github.com/user-attachments/assets/aadebaf7-6f80-488c-997b-6cdc93308624" />

<img width="489" alt="Cleaner screenshot" src="https://github.com/user-attachments/assets/3dd60f6b-cef6-4926-bf61-a574b5a0c175" />

<img width="686" alt="Winget software manager screenshot" src="https://github.com/user-attachments/assets/e8ea619d-0f8d-40ce-8a53-e259f2d4d7b7" />

---

## Build From Source

Install the .NET 8 SDK on Windows, then run:

```powershell
dotnet restore .\GregOriginSuite.slnx
dotnet test .\GregOriginSuite.slnx
dotnet publish .\GregOriginSuiteApp\GregOriginSuiteApp.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o .\final-publish
```

The WPF source is in `GregOriginSuiteApp`. The xUnit tests are in `GregOriginSuiteApp.Tests`. The original PowerShell edition is kept as `GregOriginSuite.ps1` for transparency and historical continuity.

---

## Verify The Download

After downloading the release EXE, you can verify the hash in PowerShell:

```powershell
Get-FileHash .\GregOriginSuiteApp.exe -Algorithm SHA256
```

Expected final build hash:

```text
64F7B9FD672B4DA6AECD08B2294F03684EB903730D56435136AE0C0040F36B02
```

---

## Support

Use GitHub Issues for bug reports, usability feedback, and feature requests:

[Open an issue](https://github.com/gregorik/gregorigin-cleaner-suite/issues)

When reporting a bug, include:

- Windows version
- What you clicked
- What you expected
- What happened instead
- Relevant audit or backup log details, with private paths removed if needed

---

## License

GregOrigin Cleaner Suite is released under the MIT License. See [LICENSE](LICENSE).

Copyright (c) 2025-2026 Andras Gregori @ GregOrigin.
