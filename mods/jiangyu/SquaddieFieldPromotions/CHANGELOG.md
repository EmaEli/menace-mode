# Changelog

## 0.1 (Jiangyu prototype)

- Ported prototype to Jiangyu native SDK (`JiangyuSystem`, loader-driven hotkeys, `jiangyu.json`, KDL template clone).
- Added F8 read-only Jiangyu control-clone and internal bridge inspection.
- Added explicitly gated F9 laboratory generation of a unique dynamic Squad Leader and SpeakerTemplate, added to vanilla Hire only after ID registration succeeds.
- Reused optional Squaddie Veterancy XP compatibility API, with conservative competent-rookie fallback attributes.
- Avoided running any mutations automatically or while the game is ticking. No recurring logging.
- Kept all squaddie-pool mutations, dossier redemption, cosmetic changes, and persistence out of scope for this first test.
- **Unverified authoring source**: build and live gameplay validation required.
