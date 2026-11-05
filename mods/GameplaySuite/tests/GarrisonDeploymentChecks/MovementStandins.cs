using System.Reflection;
using Il2CppMenace.Tactical;

namespace Il2CppTactical
{
    public sealed class MovementResult
    {
        public bool Success;
        public int Cost;
    }
    public static class MovementSystem
    {
        public static bool BlockDeployed = true;
        public static Action? DuringQuery;
        public static bool IsMovementPossible(Actor actor, Tile tile, bool force, out MovementFailedReason reason)
        {
            var nativeReason = MovementFailedReason.None;
            bool result = HarmonyLib.Harmony.Movement(MethodBase.GetCurrentMethod()!, actor, () =>
            {
                DuringQuery?.Invoke();
                if (actor.Suppression == SuppressionState.PinnedDown || actor.Stance == ActorStance.PinnedDown) return false;
                if (actor.Stance == ActorStance.Deployed && BlockDeployed)
                { nativeReason = MovementFailedReason.Deployed; return false; }
                var properties = actor.GetCurrentProperties();
                if ((properties.Flags & EntityFlags.Stunned) != 0 || properties.IsRooted())
                { nativeReason = MovementFailedReason.Rooted; return false; }
                return tile.Walkable;
            });
            reason = nativeReason;
            return result;
        }
        public static MovementResult GetPath(Actor actor, Tile tile, MoveBackwardsMode backwards, MovementFlags flags = MovementFlags.None)
            => HarmonyLib.Harmony.Movement(MethodBase.GetCurrentMethod()!, actor, () => new MovementResult
            {
                Success = IsMovementPossible(actor, tile, false, out _),
                Cost = actor.GetCurrentProperties().APLeaveCost + (actor.Stance == ActorStance.Deployed ? 20 : 0)
            });
    }
}
namespace Il2CppMenace.States
{
    public sealed class TacticalState
    {
        public Tile Target = new();
        public int PreviewCost;
        public bool Started, UndeploySkillUsable;
        public void ComputeActorPath(Actor actor)
            => HarmonyLib.Harmony.Movement(MethodBase.GetCurrentMethod()!, actor, () =>
            {
                var path = Il2CppTactical.MovementSystem.GetPath(actor, Target, MoveBackwardsMode.None);
                PreviewCost = path.Cost + (actor.Stance == ActorStance.Deployed ? 20 : 0);
                return path.Success;
            });
        public void ExecuteActorTravel(Actor actor)
            => HarmonyLib.Harmony.Movement(MethodBase.GetCurrentMethod()!, actor, () =>
            {
                if (actor.Stance == ActorStance.Deployed && !UndeploySkillUsable) return false;
                var action = MovementAction.Leave;
                Started = actor.MoveTo(Target, ref action);
                return Started;
            });
    }
}