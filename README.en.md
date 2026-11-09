# <img src="ClawdPet/clawd.ico" width="36" style="vertical-align: middle;"> ClawdPet
cute pet Clawd (in the form of the Claude mascot) on your desktop


[![English](https://img.shields.io/badge/Language-English-blue?style=plastic)](README.en.md)
[![Русский](https://img.shields.io/badge/Language-Русский-yellow?style=plastic)](README.md)
[![LICENSE](https://img.shields.io/badge/LICENSE-grey?style=plastic)](LICENSE)

## Features

- Stands on the taskbar and on top of windows, rides them, falls/flies, bounces, hangs from the top edge, hides and peeks out from behind the screen edge
- Reacts to clicks, petting, dragging and throwing; dances to music; comments on the programs you open
- Steals icons of your running apps from the taskbar and eats them (and sometimes leaves a pile that needs cleaning up)
- Optionally: AI-written phrases via an API key, and a local memory of you
- 3 sizes, 7 colors, every behavior can be toggled, autostart with Windows

The menu and settings window are styled like the Claude Code terminal

## Controls

| Action | What happens |
| --- | --- |
| Click | Poke. Too many quick clicks in a row upset Clawd |
| Double-click | High jump with sparkles |
| Press and hold, then move the mouse left and right | Pet Clawd: hearts and a happy face appear |
| Drag | Carry Clawd around |
| Drag and release quickly | Throw. Clawd may get dizzy |
| Release near the top of the screen | He hangs by his hands, then falls |
| Click a pile | Clean it up |
| Right-click on Clawd | Open the menu |
| Tray icon, left click | Call Clawd to the cursor |
| Tray icon, right click | Open the menu |

## Settings

Open them from the menu: Settings… (*Настройки…*). Changes apply immediately

The interface is in Russian, so the original tab names are given in parentheses.

| Tab | Contents |
| --- | --- |
| Pet (*питомец*) | Visibility, size, color; gravity, riding windows, getting thrown off when the window is shaken, wandering, phrases, stealing icons, sleep delay, dancing, comments on programs, piles, hide on fullscreen, autostart |
| AI (*ии*) | Provider (preset list), API key, model, API address, how often he chats, AI-controlled mood and movement, window titles, built-in phrases as a fallback, character, backup provider, connection test |
| Brain (*мозг*) | Enable memory, your name, "about me", learning facts, sharing memory with the AI, show memory, import from the plugin, open data folder, "forget everything" |
| About (*о программе*) | Short help |

## AI replies (optional, off by default)

1. Open Settings → AI (*ии*) and enable AI.
2. Pick a provider from the list: Google Gemini, OpenAI, OpenRouter, Groq, DeepSeek, Mistral, Together AI, LM Studio, Ollama, or Custom (*Свой*), which is any server that supports `/chat/completions`. The API address and model are filled in for you, and you can change them.
3. Paste your API key. You create it in the account area of the chosen provider (for Gemini, at [aistudio.google.com/apikey](https://aistudio.google.com/apikey)). LM Studio and Ollama run locally and do not need a key.
4. Press "Test AI" (*Проверить ИИ*).

Good to know:

- If the Model field is empty, the default model is used (defined in `Constants.cs`). For some providers you have to enter it yourself, and you can always type your own.
- The model must answer with a small JSON object: `say` (the line), `mood` (one of 13 animations), `action` (one of 12 movements) and an optional `remember` (a new fact about you). If the AI is unavailable, Clawd falls back to built-in phrases (this can be turned off).
- Requests are rate-limited (a few seconds apart). On HTTP 429 or authorization errors the provider is paused for a while.
- The **backup provider** is chosen the same way as the primary one and is used when the primary one fails.
- "How often he chats on his own" (*Как часто болтает сам*) controls idle chatter: often, normal, rarely, never.
- The Character (*Характер*) field is appended to the system prompt, so you can change Clawd's personality.

## Memory ("brain")

Clawd records what happens (petting, throws, eaten icons, opened programs, tracks, how often you come by) in the `clawd_brain.json` file:

- **Attachment** from 0 to 100 with five levels: acquaintance, buddy, friend, close friend, best friend. It grows when you are kind to him and drops a little if you throw him hard, shake his window or stay away for several days.
- **Facts** the AI decides to remember about you (up to 40; similar ones are merged; links, long numbers and anything that looks like a password or prompt injection are dropped).
- **Habits** derived from statistics: when you usually show up, what you listen to.
- **Recent events** used as context for his lines.

The file format is compatible with `clawd_brain.json` from the original exteraGram plugin, so you can import an existing memory (Settings → Brain (*мозг*) → import from the plugin).

## Privacy and data

- Everything is stored locally in `%APPDATA%\ClawdPet\`: `config.json` (settings), `clawd_brain.json` (memory), `clawd.log` (errors, trimmed at about 512 KB).
- **API keys are stored in `config.json` as plain text.** The settings window hides them, but the file does not. Do not share this folder.
- The only network traffic goes to the AI provider you configured. With AI disabled, ClawdPet makes **no network requests**.
- When AI is on, a request can include: a description of the situation, local time, the name of the app in the foreground, how many times you opened it, the current track (artist and title), Clawd's position on screen and his recent lines.
  - Window titles are sent **only** if "Pass window titles to the AI" (*Передавать ИИ заголовки окон*) is enabled.
  - The memory block (your name, "about me", facts, habits, recent events) is sent **only** while "Share memory with the AI" (*Делиться памятью с ИИ*) is on.
  - Until you set a name yourself, Clawd uses your Windows user name (unless it is something generic like `user` or `admin`).
- To see the desktop, ClawdPet enumerates windows and their positions, detects the foreground app, process names and their icons, idle time and the current media session. It does not capture the screen, log keystrokes or read the clipboard.

<details>
<summary><h2>Project structure</h2></summary>

```
ClawdPet/
├── ClawdPet.csproj      .NET 8, WPF + Windows Forms, WinExe
├── app.manifest         Per-monitor DPI awareness
├── clawd.ico
└── src/
    ├── Program.cs
    ├── Config.cs
    ├── Constants.cs
    ├── Pet.cs
    ├── PetActions.cs
    ├── PetMind.cs
    ├── Ai.cs
    ├── Brain.cs
    ├── Desktop.cs
    ├── Icons.cs
    ├── Media.cs
    ├── Native.cs
    ├── Windows.cs
    ├── Sprites.cs
    ├── Theme.cs
    ├── MenuWindow.cs
    └── SettingsWindow.cs
```

</details>

## Requirements

- Windows 10 version 1809 (build 17763) or newer
- To build: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (the project uses WPF and Windows Forms, so it builds for Windows)
- To run a framework-dependent build: .NET 8 Desktop Runtime

## Usage

<img width="379" height="51" alt="Снимок экрана 2026-11-09 185657" src="https://github.com/user-attachments/assets/fa0d26a4-d702-461e-ab62-7727aafe6570"/>

On average, ClawdPet consumes 57.5 MB of RAM on Windows 11 Pro 26H2. At its peak, it uses 61.5 MB

## Known limitations

- Windows only.
- The interface, built-in phrases and AI replies are in Russian only; there is no localization layer yet.
- No automated tests.

## About

ClawdPet is a Windows port of the exteraGram plugin ClawdPetPlugin (original by @itskotovski, fork by @Skorvash).

This is an unofficial fan project. It is not affiliated with, endorsed by or sponsored by Anthropic. "Claude" is a trademark of Anthropic.
