using System;
using System.Drawing;

namespace HexBattleDemo;

/// <summary>
/// Represents a game action that can be performed by a unit
/// </summary>
public class GameAction
{
    public ActionType Type { get; set; }
    public Point UnitPosition { get; set; }
    public Point? TargetPosition { get; set; }
    public Point? AttackPosition { get; set; }

    public GameAction(ActionType type, Point unitPosition, Point? targetPosition = null, Point? attackPosition = null)
    {
        Type = type;
        UnitPosition = unitPosition;
        TargetPosition = targetPosition;
        AttackPosition = attackPosition;
    }

    public override string ToString()
    {
        switch (Type)
        {
            case ActionType.Move:
                return $"Move from ({UnitPosition.X},{UnitPosition.Y}) to ({TargetPosition?.X},{TargetPosition?.Y})";
            case ActionType.Attack:
                return $"Attack from ({UnitPosition.X},{UnitPosition.Y}) to ({AttackPosition?.X},{AttackPosition?.Y})";
            case ActionType.MoveAndAttack:
                return $"Move from ({UnitPosition.X},{UnitPosition.Y}) to ({TargetPosition?.X},{TargetPosition?.Y}) then attack ({AttackPosition?.X},{AttackPosition?.Y})";
            case ActionType.Pass:
                return $"Pass at ({UnitPosition.X},{UnitPosition.Y})";
            default:
                return "Unknown action";
        }
    }

    public override bool Equals(object obj)
    {
        if (obj is GameAction other)
        {
            return Type == other.Type &&
                   UnitPosition == other.UnitPosition &&
                   TargetPosition == other.TargetPosition &&
                   AttackPosition == other.AttackPosition;
        }
        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Type, UnitPosition, TargetPosition, AttackPosition);
    }
}

/// <summary>
/// Type of action a unit can perform
/// </summary>
public enum ActionType
{
    Move,           // Just move to a position
    Attack,         // Attack from current position
    MoveAndAttack,  // Move 1 hex then attack
    Pass            // Do nothing (unit becomes passive)
}
