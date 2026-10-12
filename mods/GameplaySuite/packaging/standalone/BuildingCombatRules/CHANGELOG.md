# Changelog

## 0.1.4

- Fixed occupied multi-tile garrison collapse still being able to hard-crash the process.
- Native `EjectEntity()` can begin a leave/movement sequence that is not finished when the call returns. 0.1.3 destroyed the structure immediately afterward.
- For the exact multi-tile Garrisonable whitelist, an occupied lethal collapse is now deferred until the occupant is detached and no longer moving, with a short grace period before native `Structure.Die()` resumes.
- Repeated lethal hits during the pending exit do not restart ejection or death.
- Empty multi-tile collapse keeps the 0.1.3 vanilla-template normalization path unchanged.

## 0.1.3

- Hardened native collapse of Garrisonable Buildings multi-tile civilian structures after reproducing a hard process crash on `building_civilian_shelter_1x2_01` death.
- Any contained entity is still detached first through MENACE's native ejection API.
- During `Structure.Die()` only, the exact multi-tile Garrisonable whitelist temporarily uses vanilla `ContainerType=None` and no `ProvidesCoverInside`; the original garrison template values are restored immediately afterward.
- The guard also applies when no live occupant is present, covering stale containment/native multi-tile collapse state.
- No weapon-balance or garrison-entry behavior changes.

## 0.1.2

- Added a stack-scoped compatibility guard for invalid `Actor.GetCurrentProperties()` reads originating from Garrisonable Buildings.
- Failed stale-actor probes now resolve to `null` rather than propagating a reflected exception.
- Calls from MENACE and other mods are unaffected.
- No balance changes.

## 0.1.1

- Build/API compatibility fix only.
- Use the Modpack Loader `global::HarmonyLib.Harmony` signature and implement required `OnSceneLoaded`.
- No gameplay changes from 0.1.

## 0.1
- Added small-arms immunity for buildings without preventing attacks against garrisoned infantry.
- Added a tile-hit guard so small-arms `DestroyHalfCover` effects are suppressed on building tiles only.
- Added pre-collapse native occupant ejection before `Structure.Die()` to avoid destroying an occupied container while its occupant still references it.
- Explosive, incendiary, crew-served and anti-vehicle/anti-structure weapon families remain unaffected.
- Debug logging is OFF by default.
