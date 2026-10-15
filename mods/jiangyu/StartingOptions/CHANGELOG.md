# Starting Options

## 0.1.1

- Fixed a crash to desktop at the start of the AI's first turn (access violation in the .NET JIT, `CEEInfo::resolveVirtualMethodHelper`). MENACE's AI tasks call hooked methods from IL2CPP worker threads, and the module's helper code was JIT-compiled there on first use. The Jiangyu host now compiles the whole module assembly on the main thread at load. No gameplay changes. GitHub issue #44.

## 0.1

- First release: configurable initial leader slider maximum, default 12 (vanilla 6).
- Configurable Black Market Headstart Spare Parts count, default 36 (maximum 1000, minimum 0).
- Leaves the game's original Black Market Headstart checkbox authoritative.
- Leaves individual Spare Parts trade value and unrelated items unchanged.
- Restricts leader maximum to the available pickable leader pool when that pool can be inspected.
- Does not modify existing campaigns or tactical game logic.
- Debug logging is OFF by default. No repeated gameplay logs.
