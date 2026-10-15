# Changelog

## 2.2.3
- Reduced Debug logging volume dramatically.
- Removed per-query `Squad scaling` logs that could produce tens of thousands of lines during targeting/AI evaluation.
- Removed per-query `Indirect cover` logs from the combat hot path.
- Retained compact startup/API binding, settings, template summary, warning and fallback diagnostics.
- No gameplay or balance changes.

## 2.2.2
- Updated StrongerCover for the latest uploaded MENACE patch / IL2CPP API.
- Rebound skill templates through `BaseSkill.GetTemplate()` and the current `SkillTemplate.IsIgnoringCover` field.
- Rebound defender cover handling to `EntityProperties.CoverEffectivenessMult` and matched vanilla clamp/divide semantics.
- Made Tile distance/direction overload selection patch-safe.
- Kept current indirect-fire ATGM skills on vanilla cover behavior, matching the existing rocket/ATGM exclusion intent.
- No balance changes; debug logging remains off by default.

## 2.2.1
- Runtime logging is now completely silent by default.
- Renamed the diagnostics toggle label to **Debug logging**.
- All informational, warning and error output is emitted only while Debug logging is enabled.
- Removed the forced tactical-scene startup logging path.

## 2.2
- Added Garrisonable Buildings integration through the dedicated `cover_inside_building` inside-cover profile.
- Added configurable **Garrisoned building hit chance (%)**, default 30%.
- StrongerCover now overrides only building `AccuracyMult`; Garrisonable Buildings continues to own building damage, suppression and concealment.
- Restores the latest Garrisonable Buildings base accuracy when StrongerCover is disabled/unloaded.
- Preserved the 2.1 indirect-fire cover feature: 8-direction averaged cover, optional inside-cover combination, conservative mortar/howitzer/artillery/indirect detection, and no squad-size scaling for indirect fire.
- Added building and indirect-fire diagnostics to the DevConsole panel.
