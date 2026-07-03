# Mini Age — Game Build

A compiled Windows build of the Unity game "Mini Age." This is the distributable game executable with all required assets and runtime components.

## Contents

- mini age.exe — Game executable
- mini age_Data/ — Unity game data (assets, config, resources)
- MonoBleedingEdge/ — Mono runtime environment
- UnityCrashHandler.exe — Unity error reporting
- UnityPlayer.dll — Unity player runtime

## System Requirements

- **OS:** Windows 7+
- **Architecture:** x86_64
- **Runtime:** Included (MonoBleedingEdge)

## Running the Game

Double-click mini age.exe or run from terminal:

`ash
"./mini age.exe"
`

## Notes

This is a compiled binary distribution, not source code. For source code analysis and audit reports, see the [mini-age-audit](../mini-age-audit/) project.

## What's Missing / Future Improvements

- [ ] Source code repository
- [ ] Dedicated server build
- [ ] macOS/Linux builds
- [ ] WebGL browser version
- [ ] Auto-updater
- [ ] Crash reporting service integration
- [ ] Performance optimization pass
- [ ] Steam/Itch.io publishing pipeline
