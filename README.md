# GregOrigin Cleaner Suite

![Platform](https://img.shields.io/badge/Platform-Windows%2011-0078D7)
![License](https://img.shields.io/badge/License-MIT-green)
![Status](https://img.shields.io/badge/Status-Active-brightgreen)
![Adware](https://img.shields.io/badge/Adware-None-red)

**A minimalist, native system utility for Windows 11.**  
GregOrigin Cleaner Suite is a "Glass Box" alternative to opaque system cleaners. It combines bulk uninstallation, safe system cleaning, and software updates into a single, lightweight dashboard—powered entirely by native Windows APIs.

[Visit Website](https://gregorigin.com) • [Download Latest Release](#download) • [Report Bug](../../issues)

---

## 🚀 Why GregOrigin Suite?

Most system utilities (CCleaner, IObit, etc.) are "Black Boxes." You don't know exactly what files they delete or what telemetry they send. Others (Bulk Crap Uninstaller) are powerful but overwhelming for general users.

**GregOrigin Cleaner Suite is different:**
*   **100% Native:** Uses standard PowerShell and `.NET` commands. No proprietary drivers.
*   **Safe by Design:** We do not touch the Registry for "cleaning." We do not vacuum SQLite databases. We only remove cache files that are safe to delete.
*   **Winget Integration:** Uses Microsoft's official package manager to fetch safe updates.
*   **Zero Bloat:** No background services, no ads, no "Pro" subscription upsells.

---

## 🛠 Features

### 1. Bulk Uninstaller
Stop clicking "Next" on every single wizard.
*   **Multi-Select:** Remove 10+ apps in a single batch.
*   **Silent Execution:** Automatically injects `/quiet` or `/qb` flags for MSI installers.
*   **Fast Output:** Reads directly from the Registry for instant loading (no slow file scans).

### 2. Safe System Cleaner
Evades the weaknesses of BleachBit and CCleaner.
*   **Cache Only:** Targets Windows Temp, User Temp, and Browser Caches (Edge/Chrome).
*   **No Corruption:** Deliberately avoids touching locked system files or browser databases (History/Passwords) to prevent profile corruption.
*   **SSD Friendly:** Uses standard deletion, not "Secure Wipe," to preserve your SSD's write cycles.

### 3. Software Updater
*   **Powered by Winget:** Scans your installed apps against the official Microsoft repository.
*   **One-Click Update:** Update all supported applications instantly without visiting 20 different websites.

---

## 📸 Screenshots


---

## 📥 Download & Installation

### Option 1: The Executable (Recommended)
Download the standalone `.exe` from the **[Releases](../../releases)** page.

> **Note on Windows SmartScreen:**  
> Because this is an open-source tool created by an indie developer (and not a corporation paying $500/year for a certificate), Windows may flag it as "Unrecognized."  
> **To Run:** Click `More Info` -> `Run Anyway`.

### Option 2: Run from Source (PowerShell)
If you prefer 100% transparency, you can run the script directly.
1. Download `GregOriginSuite.ps1`.
2. Right-click the file -> **Run with PowerShell**.

---

## 🏗️ Building from Source

Don't trust the EXE? Build it yourself. We provide the builder script in this repository.

1. Clone this repo.
2. Open `src/BuildConnector.ps1`.
3. Run the script.
4. It will compile `GregOriginSuite.ps1` into `GregOriginSuite.exe` using your local .NET framework.

---

## ⚠️ Philosophy & Safety

**We do not clean the Registry.**  
In the Windows 11 era, aggressive registry cleaning offers zero performance benefits and high risks of breaking OS features. GregOrigin Suite focuses on **Reclaimable Disk Space** and **Software Hygiene**, not "Magic Optimization."

**We do not bundle Adware.**  
The code is right here. You can read it.

---

## 🤝 Contributing

Native Windows 11 optimization is an ongoing battle. Pull requests are welcome!
1. Fork the Project.
2. Create your Feature Branch (`git checkout -b feature/AmazingFeature`).
3. Commit your Changes (`git commit -m 'Add some AmazingFeature'`).
4. Push to the Branch (`git push origin feature/AmazingFeature`).
5. Open a Pull Request.

---

## 📜 License

Distributed under the MIT License. See `LICENSE` for more information.

---

<p align="center">
  Built with ❤️ for Windows 11
</p>
