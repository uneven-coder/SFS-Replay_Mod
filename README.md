# 🚀 SFS Replay Mod

[![Version](https://img.shields.io/badge/version-v1.0.0-blue.svg)](https://github.com/cratior/SFS-Replay_Mod)
[![Game Version](https://img.shields.io/badge/SFS-1.5.10.2+-green.svg)](https://store.steampowered.com/app/1718870/Spaceflight_Simulator/)
[![License](https://img.shields.io/badge/license-MIT-orange.svg)](https://choosealicense.com/licenses/mit/)
[![Status](https://img.shields.io/badge/status-Beta-yellow.svg)](https://github.com/cratior/SFS-Replay_Mod)

> Records SFS Gameplay and shows the recordings in-game with camera controls.

## 📋 Table of Contents

- [🎯 Features](#-features)
- [📦 Installation](#-installation)
- [🎮 Usage](#-usage)
- [⚠️ Important Notes](#️-important-notes)
- [📁 File Structure](#-file-structure)
- [🐛 Known Issues](#-known-issues)
- [📄 License](#-license)

## 🎯 Features

### Current Features
- **Game Recording**: Capture complete SFS gameplay sessions
- **Rocket Tracking**: Records all rockets and their states across different planets
- **Solar System Support**: Full compatibility with custom solar systems
- **In-Game Controls**: Start/stop recordings directly from the game menu

### Planned Features
- **Replay Playback**: Watch recordings with camera controls *(Coming Soon)*
- **Timeline Controls**: Scrub through recordings
- **Multiple Camera Modes**: Free cam, follow rocket, orbital view

## 📦 Installation

### Prerequisites
- Spaceflight Simulator (SFS) version 1.5.10.2 or higher
- ModLoader installed and configured

### Steps
1. Download the latest release from the [Releases](https://github.com/cratior/SFS-Replay_Mod/releases) page
2. Extract the mod files to your SFS mods directory
3. Launch SFS with ModLoader enabled
4. The mod will automatically create necessary folders and show a welcome message

## 🎮 Usage

### Starting a Recording
1. Open the in-game menu (ESC key)
2. Click **"Start Recording"** button
3. The mod will capture all rocket movements and states

### Stopping a Recording
1. Open the in-game menu again
2. Click **"Stop Recording"** button
3. Choose to save or discard the recording
4. Optionally rename your recording before saving

### Managing Recordings
- Recordings are saved to: `SFS/Saving/Recordings/`
- Each recording contains (subject to change as of beta):
  - `recording_info.json` - Session metadata
  - `Blueprints/` - Rocket blueprint data
  - `Changes/` - State change information

## ⚠️ Important Notes

> **⚠️ BETA WARNING**: This mod is in beta and may cause issues.

### Critical Guidelines
- **🚫 DO NOT delete solar systems** - They are required to replay recordings
- **💾 Large file sizes** - Recordings can become quite large depending on session length
- **🔒 Save compatibility** - Save loss is unlikely but always backup important saves

### Performance Considerations
- Long recording sessions will create larger files
- Complex missions with many rockets increase file size
- Consider stopping/starting recordings for very long play sessions
> the mod will save all rockets in the game not just the ones "in view", it wont activley record them if they dont move but this may affect peformance and can cause larger saves.

### System Requirements
- **Storage**: Variable (depends on recording length and complexity)
- **Memory**: Additional RAM usage during recording
- **Platform**: Windows, Mac, Linux (wherever SFS runs)

## 📁 File Structure

```
SFS-Replay_Mod/
├── Replay/
│   ├── Main.cs                 # Main mod class and settings
│   ├── RecordGame.cs          # Core recording functionality
│   ├── GameUiPatch.cs         # UI integration and menu patches
│   └── ReplayMod.csproj       # Project configuration
├── README.md                   # This file
└── Replay_mod.sln             # Visual Studio solution
```

### Generated Files
```
SFS/Saving/Recordings/
├── Recording_YYYYMMDD_HHMMSS/
│   ├── recording_info.json    # Session metadata
│   ├── Blueprints/           # Rocket blueprints
│   └── Changes/              # State changes
```

## 🐛 Known Issues

Current no known issues as its still in development but these are issues i have suspected may occur.

- **Playback System**: Not yet implemented (planned for future release)
- **Large Files**: Recordings can become very large
- **Memory Usage**: Increased RAM usage on record start with posibility to also occur while recording with lots of active rockets
- **Solar System Dependency**: Recordings tied to specific solar systems

## 📄 License

This project is licensed under the MIT License - see the [https://choosealicense.com/licenses/mit/](LICENSE) file for details.

---

## Support

- **Issues**: [GitHub Issues](https://github.com/cratior/SFS-Replay_Mod/issues)
- **Discussions**: [GitHub Discussions](https://github.com/cratior/SFS-Replay_Mod/discussions)

(@cratior to contact me)
- **Discord**: [SFS Server](https://discord.gg/hwfWm2d)
- **Discord**: [FSI SFS Server](https://discord.gg/P4Z2M652g6)
This is a mod moderators/admins cannot help you with mod specific issues.
---

**Encouraged by FSI**

[⬆ Back to Top](#-sfs-replay-mod)
