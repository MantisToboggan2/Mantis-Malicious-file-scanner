# Mantis Malicious File Scanner

Mantis Malicious File Scanner is a cross‑platform malicious code detection tool designed to analyze Lua scripts, archives, and folders for suspicious or unsafe behavior. It highlights dynamic code execution, obfuscated payloads, unsafe hooks, and other patterns commonly used in backdoors or malicious Lua addons.

Built with:
- Avalonia UI
- C# (.NET 10)
- Rust scanning engine
- Custom rule‑based detection system

---

## Features

- Scan files, folders, and archives (.zip)
- Detect dynamic code execution (loadstring, RunString, CompileString)
- Identify obfuscated or encoded payloads
- Flag suspicious hooks and callbacks
- Highlight unsafe file I/O operations
- Show detailed results with severity levels
- Remove flagged malicious code safely
- Cross‑platform support (Windows, Linux, macOS)

---

## Installation

Download the latest release from the **Releases** page.
(https://github.com/MantisToboggan2/Mantis-Malicious-file-scanner/releases/tag/v1.0.0)

Extract the ZIP and run.

---

## Mantis Malicious Code Scanner — Roadmap to v2.0.0
🎯 v2.0.0 Goal
Transform the scanner from a single‑platform ZIP‑based tool into a multi‑platform, auto‑updating, installer‑driven, multi‑language malicious code scanner hosted on Cloudflare R2 and distributed via my personal website: ## "https://mantisrealm.com".

This is a major milestone and will represent the jump to v2.0.0.

🧩 Phase 1 — Multi‑Language Scanning Engine
🎯 Objective
Expand the scanner to support most popular languages and file extensions, including (but not limited to*):

C#

C / C++

Java

Python

JavaScript / TypeScript

PHP

Go

Rust

Shell scripts

Batch files

HTML / CSS

JSON / XML

PowerShell

Ruby

Swift

Kotlin

🔧 Deliverables
Unified scanning engine

Language detection module

Extension → language mapping

Malicious pattern library per language

Configurable scanning rules

---

🧩 Phase 2 — Cross‑Platform Installer System
🎯 Objective
Create installers for:

Windows → MSI or EXE installer

macOS → .pkg or .dmg

Linux → .deb, .rpm, or AppImage

🔧 Deliverables
Unified build pipeline

Platform‑specific packaging scripts

Installer branding (icons, metadata)

Post‑install setup (config folders, logs, update agent)

---

🧩 Phase 3 — Cloudflare R2 Hosting Integration
🎯 Objective
Host all update assets on Cloudflare R2, including:

latest-version.txt

update.json

Installer files

Release ZIPs

Changelog files

🔧 Deliverables
Public R2 bucket

Version manifest system

Secure download URLs

Integration with mantisrealm.com

Optional CDN caching via Cloudflare

---

🧩 Phase 4 — Auto‑Update System
🎯 Objective
Implement a full auto‑update pipeline:

🔧 Deliverables
Version check on startup and the ability to change the auto update frequency

Download update manifest from R2

Compare versions

Download new installer or ZIP

Launch updater agent

Replace old files

Relaunch main app

Fallback recovery mode

🧩 Components
Main App

Checks version

Prompts user

Launches updater

Updater EXE / Agent

Downloads update

Extracts

Replaces files

Relaunches main app

---

🧩 Phase 5 — Unified Release Pipeline
🎯 Objective
Automate builds for:

Windows

macOS

Linux

And push artifacts to:

GitHub Releases

Cloudflare R2

mantisrealm.com downloads page

🔧 Deliverables
Build scripts

Publish scripts

Upload automation

Version tagging system

Release notes generator

---

## 🚀 v2.0.0 Release Definition
To officially ship v2.0.0, all of the following must be complete:

✔ Multi‑language/file scanning engine
✔ Cross‑platform installers
✔ Cloudflare R2 hosting
✔ Auto‑update system
✔ Unified release pipeline
✔ Website integration (mantisrealm.com)

---

## License
This project is licensed under the Creative Commons BY‑NC‑ND 4.0 License.
You may share the software with attribution, but commercial use and modification are not permitted.
