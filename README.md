<div align="center">

# SlowWhy

[![Releases](https://img.shields.io/badge/Releases-Download-blue)](https://github.com/Gurates/SlowWhy/releases)

<br>

<img width="481" height="704" alt="Image" src="https://github.com/user-attachments/assets/b7d834c1-4eb9-491c-8497-ee13cf68a354" />
<img width="477" height="713" alt="Image" src="https://github.com/user-attachments/assets/c848cd41-6f80-43bf-b88c-40bca818efa0" />

</div>

<br>

SlowWhy is a lightweight system monitoring and optimization tool built with C# and WPF on .NET 8. It helps users diagnose performance issues by monitoring hardware metrics and provides tools to optimize system resources.

**Note:** This application is self-contained and does not require the .NET Runtime to be installed on your machine; therefore, the file size is larger than usual. Also, please note that this is the **initial release (v1.0)**, so it may contain bugs or imperfections reflecting the early stages of development.

## Features

- **Dashboard:** Real-time overview of CPU, RAM, GPU, and Disk status.
- **CPU Monitor:**
  - View real-time processor utilization.
  - List top processes consuming CPU resources.
  - Ability to terminate specific processes.
- **RAM Optimizer:**
  - Three cleaning modes: Safe (Cache only), Medium (File system), and Aggressive (Working sets).
  - Real-time memory usage tracking.
- **GPU Monitor:**
  - Detailed graphics card information (VRAM, Driver, Resolution, Usage).
  - Support for NVIDIA, AMD, and Intel GPUs via LibreHardwareMonitor.
- **Disk Analysis:**
  - Scan for large files (>100MB) to free up space.
  - List installed applications with size estimation.
- **Customization:**
  - Built-in Dark and Light themes.

## Requirements

- Windows 10 or Windows 11
- Administrator privileges (Required for hardware sensors and memory cleaning operations)

## Tech Stack

- C# / WPF
- .NET 8
- LibreHardwareMonitorLib (Hardware abstraction)
- CommunityToolkit.WinUI (UI components)
