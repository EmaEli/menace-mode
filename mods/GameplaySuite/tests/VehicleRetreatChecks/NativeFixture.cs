using System;
using System.Collections.Generic;

namespace UnityEngine
{
    class Object
    {
        public bool Destroyed;
        public static implicit operator bool(Object? value) => value is not null && !value.Destroyed;
    }
}
namespace Il2CppMenace.Tactical
{
    class Native { public IntPtr Pointer = new(1); }
    class Tile : Native { public int GetX() => 0; public int GetZ() => 0; }
    enum MovementAction { Forward }
    enum MovementFlags { None }
    class Actor : Native
    {
        public bool Alive = true, Dying, Moving, Stunned, Leaving, TurnDone;
        public int AP = 100, Moves, ApWrites;
        public Skills.SkillContainer Skills = new();
        public bool IsAlive() => Alive;
        public bool IsDying() => Dying;
        public bool IsMoving() => Moving;
        public bool IsStunned() => Stunned;
        public bool IsLeavingMap() => Leaving;
        public bool IsTurnDone() => TurnDone;
        public Skills.SkillContainer GetSkills() => Skills;
        public Tile? Tile = new();
        public Tile? GetTile() => Tile;
        public int GetActionPoints() => AP;
        public void SetActionPoints(int value, bool notify) { AP = value; ApWrites++; }
        public bool MoveTo(Tile tile, ref MovementAction action, MovementFlags flags)
        { Moves++; Moving = true; return true; }
    }
    class Element : UnityEngine.Object { public bool Alive = true; public bool IsAlive() => Alive; }
    class PathMover : UnityEngine.Object { public Element? m_Element = new(); public Actor? m_Actor = new(); }
    class TacticalManager : Native
    {
        public Actor? Active;
        public static bool Busy;
        public Actor? GetActiveActor() => Active;
        public static bool IsSkillBusy() => Busy;
    }
}
namespace Il2CppMenace.Tactical.Skills
{
    enum UsageParameter { AutoAudioVolume }
    class BaseSkill : Native { public T? TryCast<T>() where T : class => this as T; }
    class SkillContainer : Native
    {
        public bool Busy;
        public Dictionary<string, BaseSkill> All = new();
        public bool IsBusy() => Busy;
        public BaseSkill? GetSkillByID(string id, object? item) => All.GetValueOrDefault(id);
    }
    class Skill : BaseSkill
    {
        public Actor Owner = null!;
        public bool Usable = true, Affordable = true, OutOfUses, TargetLegal = true, Accepted = true;
        public int Uses, Cost = 40;
        public bool IsOutOfUses() => OutOfUses;
        public bool IsUsable() => Usable;
        public bool IsAffordable() => Affordable && Owner.AP >= Cost;
        public bool IsUsableOn(Tile target, Tile origin) => TargetLegal && target == Owner.Tile && origin == Owner.Tile;
        public bool Use(Tile tile, UsageParameter usage)
        {
            Uses++;
            if (!Accepted) return false;
            Owner.AP -= Cost;
            Owner.Skills.Busy = true;
            Owner.Skills.All.Remove("damage_effect.small_fire");
            Owner.Skills.All.Remove("damage_effect.medium_fire");
            Owner.Skills.All.Remove("damage_effect.big_fire");
            return true;
        }
    }
}