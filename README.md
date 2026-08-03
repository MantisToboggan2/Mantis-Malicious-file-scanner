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

## License
This project is licensed under the Creative Commons BY‑NC‑ND 4.0 License.
You may share the software with attribution, but commercial use and modification are not permitted.
