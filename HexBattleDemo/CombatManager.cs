using HexBattleDemo;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace HexBattleDemo;

/// <summary>
/// Manages combat between units on the hex grid
/// </summary>
public class CombatManager
{
    private PathFinder pathFinder;
    private Random random;

    public event EventHandler<CombatEventArgs> CombatResolved;

    public CombatManager(PathFinder pathFinder)
    {
        this.pathFinder = pathFinder;
        this.random = new Random();
    }

    /// <summary>
    /// Check if a unit can attack a target
    /// </summary>
    public bool CanAttack(Point attackerPos, Point targetPos, int attackRange = 1)
    {
        int distance = pathFinder.GetDistance(attackerPos, targetPos);
        return distance > 0 && distance <= attackRange;
    }

    /// <summary>
    /// Get all enemy units within attack range
    /// </summary>
    public List<Point> GetAttackablePositions(Point attackerPos, Color attackerFaction,
        Dictionary<Point, Unit> allUnits, int attackRange = 1)
    {
        List<Point> attackablePositions = new List<Point>();

        foreach (var kvp in allUnits)
        {
            Unit unit = kvp.Value;
            Point pos = kvp.Key;

            // Skip if same faction or dead
            if (!unit.IsAlive || unit.FactionColor == attackerFaction)
                continue;

            // Check if within attack range
            if (CanAttack(attackerPos, pos, attackRange))
            {
                attackablePositions.Add(pos);
            }
        }

        return attackablePositions;
    }

    /// <summary>
    /// Resolve combat between attacker and defender
    /// </summary>
    public CombatResult ResolveCombat(Unit attacker, Unit defender, Point attackerPos, Point defenderPos)
    {
        if (attacker == null || defender == null)
            return null;

        if (!attacker.IsAlive || !defender.IsAlive)
            return null;

        // Calculate base damage with some randomness
        int attackerDamage = CalculateDamage(attacker, defender);
        int defenderDamage = 0;

        // Defender can counter-attack if adjacent (within range 1)
        bool canCounterAttack = pathFinder.GetDistance(attackerPos, defenderPos) <= 1;
        if (canCounterAttack && defender.IsAlive)
        {
            defenderDamage = CalculateDamage(defender, attacker);
        }

        // Apply damage
        defender.TakeDamage(attackerDamage);

        bool defenderEliminated = !defender.IsAlive;
        bool attackerEliminated = false;

        // Counter-attack only if defender is still alive
        if (canCounterAttack && !defenderEliminated)
        {
            attacker.TakeDamage(defenderDamage);
            attackerEliminated = !attacker.IsAlive;
        }

        // Create combat result
        CombatResult result = new CombatResult
        {
            AttackerPosition = attackerPos,
            DefenderPosition = defenderPos,
            AttackerDamageDealt = attackerDamage,
            DefenderDamageDealt = defenderDamage,
            AttackerHealth = attacker.Health,
            DefenderHealth = defender.Health,
            DefenderEliminated = defenderEliminated,
            AttackerEliminated = attackerEliminated,
            CounterAttackOccurred = canCounterAttack
        };

        // Raise combat resolved event
        CombatResolved?.Invoke(this, new CombatEventArgs(result, attacker, defender));

        return result;
    }

    /// <summary>
    /// Calculate damage dealt by attacker to defender
    /// </summary>
    private int CalculateDamage(Unit attacker, Unit defender)
    {
        // Base damage: 20-30% of defender's max health
        int baseDamage = (int)(defender.MaxHealth * (0.20 + random.NextDouble() * 0.10));

        // Add some randomness (±20%)
        double randomFactor = 0.8 + random.NextDouble() * 0.4;
        int finalDamage = (int)(baseDamage * randomFactor);

        // Ensure minimum damage of 10 and max of 50
        finalDamage = Math.Max(10, Math.Min(50, finalDamage));

        return finalDamage;
    }

    /// <summary>
    /// Get positions for move+attack (move 1 hex then attack)
    /// </summary>
    public MoveAttackOptions GetMoveAttackOptions(Point unitPos, Color unitFaction,
        Dictionary<Point, Unit> allUnits, PathFinder pathFinder)
    {
        MoveAttackOptions options = new MoveAttackOptions();
        options.MovePositions = new List<Point>();
        options.AttackPositionsFromMove = new Dictionary<Point, List<Point>>();

        // Get all positions within 1 hex move
        HashSet<Point> blockedPositions = new HashSet<Point>();
        foreach (var kvp in allUnits)
        {
            if (kvp.Value.IsAlive && kvp.Key != unitPos)
            {
                blockedPositions.Add(kvp.Key);
            }
        }

        List<Point> moveRange = pathFinder.FindMovementRange(unitPos, 1, blockedPositions);

        foreach (Point movePos in moveRange)
        {
            // From this position, what can we attack?
            List<Point> attackable = GetAttackablePositions(movePos, unitFaction, allUnits, 1);

            if (attackable.Count > 0)
            {
                options.MovePositions.Add(movePos);
                options.AttackPositionsFromMove[movePos] = attackable;
            }
        }

        // Also check direct attacks from current position
        options.DirectAttackPositions = GetAttackablePositions(unitPos, unitFaction, allUnits, 1);

        return options;
    }
}

#region Supporting Classes

/// <summary>
/// Result of a combat encounter
/// </summary>
public class CombatResult
{
    public Point AttackerPosition { get; set; }
    public Point DefenderPosition { get; set; }
    public int AttackerDamageDealt { get; set; }
    public int DefenderDamageDealt { get; set; }
    public int AttackerHealth { get; set; }
    public int DefenderHealth { get; set; }
    public bool DefenderEliminated { get; set; }
    public bool AttackerEliminated { get; set; }
    public bool CounterAttackOccurred { get; set; }

    public override string ToString()
    {
        string result = $"Combat: Attacker dealt {AttackerDamageDealt} damage";
        if (CounterAttackOccurred)
        {
            result += $", Defender countered for {DefenderDamageDealt} damage";
        }
        if (DefenderEliminated)
        {
            result += " - Defender eliminated!";
        }
        if (AttackerEliminated)
        {
            result += " - Attacker eliminated!";
        }
        return result;
    }
}

/// <summary>
/// Event arguments for combat events
/// </summary>
public class CombatEventArgs : EventArgs
{
    public CombatResult Result { get; private set; }
    public Unit Attacker { get; private set; }
    public Unit Defender { get; private set; }

    public CombatEventArgs(CombatResult result, Unit attacker, Unit defender)
    {
        Result = result;
        Attacker = attacker;
        Defender = defender;
    }
}

/// <summary>
/// Options for move+attack combinations
/// </summary>
public class MoveAttackOptions
{
    /// <summary>
    /// Positions where unit can move (1 hex) and then attack
    /// </summary>
    public List<Point> MovePositions { get; set; }

    /// <summary>
    /// For each move position, which enemies can be attacked from there
    /// </summary>
    public Dictionary<Point, List<Point>> AttackPositionsFromMove { get; set; }

    /// <summary>
    /// Enemies that can be attacked directly from current position
    /// </summary>
    public List<Point> DirectAttackPositions { get; set; }
}

#endregion
