# Mortar-first smoke and close-threat gate (#1)

Source candidate: SmokeOverhaul **0.10.0**, AIPerformance **1.14.0**.

- Preventive smoke requires a known threat within an inclusive Euclidean **10 tiles** of
  the protected infantry, not of the mortar. Support requests retain weapon-range,
  exposure and two-round observation freshness checks. Hidden contacts use only their
  stored observed tile; no hidden actor coordinate/weapon refresh is permitted.
- Infantry prefer an equipped, usable, affordable mortar smoke shot with a legal local
  screen target. The mortar executes during its own Agent.Execute activation, not the
  requesting infantry's activation. No new scheduler, ammunition, AP or equipment grants.
- Mortar checks use at most 64 friendly actors, four mortar candidates and six local
  target tiles; mortar execution examines at most 24 infantry candidates. Native ammo,
  cooldown, setup and target checks remain authoritative. Failed/unavailable mortar use
  releases the infantry fallback; native turn order is not overridden.
- Hand-smoke fallback retains the coordinated group's movement-AP feasibility guard;
  mortar requests do not require the infantry to carry a grenade or pay grenade AP.
- A final native Skill.Use policy also prevents independent native preventive smoke
  from bypassing the proximity rule. Native mortar screens must land within three tiles
  of a legitimately threatened friendly. Player/allied smoke is unaffected.
- Suppressed/pinned hand-smoke emergencies retain existing odds and the detected-attacker
  bearing rule, including at longer ranges. This is deliberately a preventive-smoke
  restriction, not a ban on emergency concealment after actual incoming fire.
- Existing adjacent forward smoke prevents duplicate proactive hand throws. Detailed
  logging remains opt-in/OFF; no map scan, pathfinder or speculative positioning search.

## Local verification

- 54 linked smoke runtime checks pass using managed stand-ins.
- 19,786 linked AI recovery/advance checks pass, including recent hidden-contact behavior.
- Both production Release projects compile with zero warnings/errors against cached
  0.7.18 proxies. Windows build, deployment and gameplay are separate gates.

## Gameplay check

Try an advance with a usable smoke mortar and infantry facing a known threat within ten
tiles, then without usable mortar ammunition/AP. Mortar should screen first when its
activation is available; infantry can fall back when it is not. Units with contacts only
eleven or more tiles away should not reveal themselves with preventive smoke. Check
recently lost contacts and emergency smoke separately. Native activation order, actual
cloud placement, AP/ammo consumption and performance still need gameplay feedback.