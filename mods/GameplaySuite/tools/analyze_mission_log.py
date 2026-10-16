"""Read a private MelonLoader log; emit only allowlisted identifiers and aggregates.

No raw lines, actor names, save paths, settings, tokens or arbitrary messages leave
the parser. A quiet module is not evidence that its gameplay action was exercised.
"""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import re

MODULES = (
    "MenaceModSettings", "BuildingCombatRules", "BuildingsBlockLineOfSight",
    "CinematicActionCamera", "CoordinatedFire", "CoverDestructionRebalance",
    "FieldLogistics", "ForestTraversal", "GarrisonableBuildings", "InfantryAssault",
    "LargerMaps", "Overwatch", "SmokeOverhaul", "SquaddieVeterancy", "StartingOptions",
    "StrongerCover", "VisibilityOverhaul", "WeaponMechanicsRebalanced", "WeaponRangesRebalanced",
)


def analyze(data):
    lines = data.decode("utf-8", errors="replace").splitlines()
    text = "\n".join(lines)
    times = re.findall(r"^\[(\d{2}:\d{2}:\d{2}\.\d+)\]", text, re.M)
    ai = [line for line in lines if "AI PERF turn=" in line]
    def values(records, key):
        return [int(m.group(1)) for line in records
                for m in [re.search(r"\b" + re.escape(key) + r"=[#]?([0-9]+)", line)] if m]
    activations = values(ai, "turn")
    versions = {}
    for name in MODULES:
        matches = [v for line in lines if re.search(r"\b" + name + r"\b", line)
                   for v in re.findall(r"\b[0-9]+\.[0-9]+\.[0-9]+(?:\.[0-9]+)?\b", line)]
        if matches:
            versions[name] = sorted(set(matches))
    game = re.search(r"Game Version: (v[0-9]+\.[0-9]+\.[0-9]+\+[0-9]+)", text)
    warnings = []
    for number, line in enumerate(lines, 1):
        if "[WARNING]" not in line:
            continue
        code = "unclassified"
        for needle, label in (
            ("SceneDiagnostics", "legacy_scene_diagnostic"),
            ("SaveSystemPatches", "legacy_save_patch_unavailable"),
            ("Squaddies.Kill", "legacy_squaddie_kill_hook_unavailable"),
            ("NewGameWindow", "starting_options_field_probe"),
            ("BlackMarket", "external_custom_leader_patch"),
            ("StrategyConfig", "interop_strategy_config_field"),
            ("Menace_Data_Extractor", "external_data_extractor"),
        ):
            if needle in line:
                code = label
                break
        warnings.append({"line": number, "code": code})
    defense = [line for line in lines if "Defensive fire:" in line]
    post = [line for line in lines if "[ASSAULT_DEBUG] defensive-post" in line]
    changes = {}
    for key in ("hp", "armor", "suppression", "morale"):
        pairs = [tuple(map(float, m.groups())) for line in post
                 for m in [re.search(r"\b" + key + r"=(-?[0-9.]+)->(-?[0-9.]+)", line)] if m]
        changes[key] = {"observed": len(pairs), "changed": sum(a != b for a, b in pairs)}
    ledger = [line for line in lines if "[MEN-004 LEDGER]" in line]
    consumes = [line for line in ledger if "op=ConsumeUse" in line]
    deltas = Counter()
    for line in consumes:
        m = re.search(r"\buses=([0-9]+)->([0-9]+)", line)
        if m:
            before, after = map(int, m.groups())
            deltas[before - after] += 1
    return {
        "sha256": hashlib.sha256(data).hexdigest(), "bytes": len(data), "lines": len(lines),
        "first_timestamp": times[0] if times else None, "last_timestamp": times[-1] if times else None,
        "game_version": game.group(1) if game else None, "module_versions_seen": versions,
        "errors": {"total_lines": sum("[ERROR]" in line for line in lines),
                   "external_custom_leader_lines": sum("[ERROR]" in line and "Menace_Custom_Leader" in line for line in lines),
                   "camera_failure_records": sum(any(x in line for x in (
                       "Automatic third-person event", "Third-person camera disabled after an error",
                       "Some cinematic event hooks were unavailable")) for line in lines)},
        "warnings": warnings,
        "ai": {"activation_count": len(ai), "first": activations[0] if activations else None,
               "last": activations[-1] if activations else None,
               "consecutive": bool(activations) and activations == list(range(activations[0], activations[-1] + 1)),
               "rounds": dict(sorted(Counter(values(ai, "round")).items())),
               "pending": dict(Counter(values(ai, "pending"))),
               "dropped_tasks": dict(Counter(values(ai, "droppedTasks"))),
               "closure": {x: sum("closedBy=" + x in line for line in ai) for x in ("finish", "next-activation")}},
        "assault": {"starts": sum("Assault started" in line for line in lines),
                    "attacker_fire": sum("Assault fire:" in line for line in lines),
                    "defensive_fire": len(defense),
                    "defensive_used_true": sum("used=True" in line for line in defense),
                    "old_pinned_true": sum("pinned=True" in line for line in defense),
                    "victory_advance": sum("Assault victory advance complete" in line for line in lines),
                    "repulsed": sum("Assault repulsed" in line for line in lines),
                    "defensive_post_changes": changes,
                    "managed_identity_false": sum("intendedMatch=False" in line for line in post)},
        "ammo": {"ledger_records": len(ledger), "consume_records": len(consumes),
                 "consume_decrements": dict(deltas)},
        "visibility": {"diagnostic_events": sum("[PirateVisibility] event=" in line for line in lines),
                       "event_limit_reached": any("[PirateVisibility] event limit reached" in line for line in lines),
                       "vision_application_counts": dict(Counter(map(int, re.findall(r"\bvoVisionApplicationsInUpdateCall=([0-9]+)", text)))),
                       "concealment_application_counts": dict(Counter(map(int, re.findall(r"\bvoConcealmentApplicationsInUpdateCall=([0-9]+)", text))))},
        "wrr": {"range_pass_records": sum("Applied range pass" in line for line in lines),
                "zero_write_failure_records": sum("write failures=0" in line for line in lines)},
        "larger_maps_96x96_records": sum("96x96" in line for line in lines),
        "module_tag_records": {name: sum("[" + name + "]" in line for line in lines) for name in MODULES},
    }


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("log", type=Path)
    args = parser.parse_args()
    print(json.dumps(analyze(args.log.read_bytes()), indent=2, sort_keys=True))