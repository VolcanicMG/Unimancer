# Unimancer 🔮

**Command the Unity engine with AI.** Unimancer is a Model Context Protocol (MCP)
server that bridges AI assistants (Claude, Cursor, etc.) to the Unity Editor —
with deep, first-class **Android build & device tooling** that other Unity MCP
servers don't have.

## Architecture

```
[ AI client ] --MCP/stdio--> [ Unimancer server (Node/TS) ] --WebSocket--> [ Unity Editor package (C#) ]
                                   |
                                   +--shell--> adb / emulator   (Android tools that need no Unity)
```

- **`src/`** — the Node/TypeScript MCP server. Owns tool definitions, the Unity
  WebSocket client, and the `adb`/`emulator` shell helpers.
- **`unity/`** — the Unity UPM package (C#). Hosts a WebSocket server inside the
  Editor and executes engine-side tools (build, player settings, manifest…).

## Tool tiers

| Tier | Area | Needs Unity? | Examples |
|------|------|--------------|----------|
| 1 | ADB device control | No (pure `adb`) | install, launch, logcat, screenshot |
| 2 | Android build & config | Yes (C# bridge) | build APK/AAB, player settings, keystore, manifest, gradle |
| 3 | Emulator & SDK env | Mostly no | list/start AVDs, verify SDK/NDK/JDK |

> Targets **Unity 6000.5 (6.5)** / **Android Gradle Plugin 9.0** defaults.

## Supply-chain safety

This repo is guarded by [depguard](https://github.com/) (`guard`). Use
`guard install <pkg>` instead of `npm install`; commits and pushes run
`guard check` automatically.

## Status

Early scaffold. See `src/tools/` for the tool surface.
