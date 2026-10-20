"""Source integration guards for #47/#48/#49; not an in-game camera/ballistics test."""
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[3]
ASSAULT = (ROOT / "mods/GameplaySuite/src/InfantryAssault/Plugin.cs").read_text()
BUILDINGS = (ROOT / "mods/GameplaySuite/src/BuildingCombatRules/Plugin.cs").read_text()
CAMERA = (ROOT / "mods/ThirdPersonCamera/src/ThirdPersonCameraMod.cs").read_text()


def section(source, start, end):
    return source.split(start, 1)[1].split(end, 1)[0]


class MissionFeedbackTests(unittest.TestCase):
    def test_assault_uses_executable_policy_without_pinned_exception(self):
        gate = section(ASSAULT, "private static bool CanDefensiveFire", "private static bool UseWeaponReaction")
        self.assertIn("AssaultDefenseRules.CanReact", gate)
        self.assertIn("GetSuppressionStateValue(defender), GetStanceValue(defender), IsStunned(defender)", gate)
        self.assertNotIn("!pinned", gate)
        self.assertIn("AssaultDefenseRules.CqbBonusScale(GetSuppressionStateValue(defender))", ASSAULT)
        self.assertIn("private const int AssaultApCost = 40;", ASSAULT)

    def test_both_tree_damage_paths_use_native_classification(self):
        direct = section(BUILDINGS, "private static void OnStructureDamage", "private readonly struct CoverDestructionState")
        tile = section(BUILDINGS, "private static CoverDestructionState BeforeAttackTileHit", "private sealed class PendingCollapse")
        self.assertIn("IsProtectedStructure(structure) || !IsSmallArmsSkill(skill)", direct)
        self.assertIn("IsProtectedStructure(target)", tile)
        self.assertIn("!IsSmallArmsSkill(skill)", tile)
        self.assertIn("entity.IsVegetation()", BUILDINGS)
        self.assertIn("entity!.IsStructure()", BUILDINGS)
        self.assertIn('Setting("ProtectBuildingsFromSmallArms", true)', BUILDINGS)
        self.assertIn('Setting("ProtectVegetationFromSmallArms", true)', BUILDINGS)
        self.assertIn("state.Template!.DestroyHalfCover = state.Original", tile)

    def test_heavy_exclusions_and_collapse_scope_are_preserved(self):
        for tag in ("ANTI_STRUCTURE", "VEHICLE_WEAPON", "INDIRECT_FIRE", "GRENADE", "ROCKET", "MINE"):
            self.assertIn("template.HasTag(TagType." + tag + ")", BUILDINGS)
        protected = section(BUILDINGS, "private static bool IsProtectedStructure", "private static bool Valid")
        self.assertIn("if (!Valid(entity)) return false;", protected)
        self.assertIn("entity!.IsStructure()", protected)
        self.assertIn("entity.IsBuilding()", protected)
        self.assertIn("catch { return false; }", protected)
        safe_collapse = (ROOT / "mods/GameplaySuite/src/GarrisonableBuildings/SafeCollapse.cs").read_text()
        self.assertIn("PendingCollapses.Clear();", safe_collapse)

    def test_attack_target_reaches_all_accepted_camera_paths(self):
        attack = section(CAMERA, "private void HandleAttack", "private bool RollChance")
        self.assertIn("map.TileCenterToWorldPos(tile.GetTilePos(), true)", attack)
        self.assertIn("ConsiderAutoCamera(attacker, EventKind.Firing, clip, target)", attack)
        self.assertEqual(attack.count("SetAutoAttackTarget(kind, attackTarget);"), 3)
        self.assertEqual(attack.count("if (!RollChance(actor, kind)) return;"), 1)
        self.assertIn("if (_active && _manual) return;", attack)

    def test_attack_view_ignores_leader_heading_and_targets_firing_axis(self):
        update = section(CAMERA, "private void UpdateThirdPersonCamera", "private static float Blend")
        self.assertIn("!_manual && _autoKind == EventKind.Firing && _autoAttackTarget.HasValue", update)
        self.assertIn("attackView ? _trackedActor.GetPos() : elementPosition", update)
        self.assertIn("FlatDirection(_autoAttackTarget.Value - position, _heading)", update)
        self.assertIn("else if (_trackedActor.IsMoving()", update)
        self.assertIn("_autoAttackTarget.Value + Vector3.up * lookHeight", update)
        cleanup = section(CAMERA, "private void ExitThirdPerson", "public override void OnSceneWasUnloaded")
        self.assertIn("_autoAttackTarget = null;", cleanup)
        self.assertIn("RestoreTacticalUi();", cleanup)

    def test_versions_match_source_manifests(self):
        for name, source in (("InfantryAssault", ASSAULT), ("BuildingCombatRules", BUILDINGS)):
            package = ROOT / "mods/jiangyu" / name
            version = json.loads((package / "jiangyu.json").read_text())["version"]
            self.assertIn(f'AssemblyVersion("{version}.0")', source)
            self.assertIn(f"<Version>{version}</Version>", (package / "code" / (name + ".csproj")).read_text())


if __name__ == "__main__":
    unittest.main()