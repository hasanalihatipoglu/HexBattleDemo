using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace HexBattleDemo;

/// <summary>
/// Simulates game actions on a GameState for MCTS
/// </summary>
public class GameSimulator
{
    private PathFinder pathFinder;
    private Random random;

    public GameSimulator(int gridWidth, int gridHeight)
    {
        pathFinder = new PathFinder(gridWidth, gridHeight);
        random = new Random();
    }

    /// <summary>
    /// Get all possible actions for a unit in the current state
    /// </summary>
    public List<GameAction> GetPossibleActions(GameState state, UnitState unit)
    {
        List<GameAction> actions = new List<GameAction>();

        if (!unit.IsAlive || unit.State == HexBattleDemo.UnitState.Passive)
            return actions;

        // Get blocked positions (friendly units)
        HashSet<Point> blockedPositions = new HashSet<Point>();
        foreach (var u in state.Units)
        {
            if (u.IsAlive && u.FactionColor == unit.FactionColor && u.Position != unit.Position)
            {
                blockedPositions.Add(u.Position);
            }
        }

        // 1. Direct attack actions (if Active or Ready)
        if (unit.State == HexBattleDemo.UnitState.Active || unit.State == HexBattleDemo.UnitState.Ready)
        {
            var enemies = GetAttackableEnemies(state, unit);
            foreach (var enemy in enemies)
            {
                actions.Add(new GameAction(ActionType.Attack, unit.Position, null, enemy.Position));
            }
        }

        // 2. Movement actions (if Active)
        if (unit.State == HexBattleDemo.UnitState.Active)
        {
            int availableMovement = unit.MovementRange;
            var reachableHexes = pathFinder.FindMovementRange(unit.Position, availableMovement, blockedPositions);

            foreach (var hex in reachableHexes)
            {
                // Can't move onto enemy units
                var occupant = state.GetUnitAt(hex);
                if (occupant != null && occupant.FactionColor != unit.FactionColor)
                    continue;

                int distance = pathFinder.GetDistance(unit.Position, hex);

                // If moving 1 hex, check for move+attack options
                if (distance == 1)
                {
                    // Get enemies attackable from new position
                    var attackableFromNew = GetAttackableEnemiesFromPosition(state, hex, unit.FactionColor, unit.AttackRange);

                    if (attackableFromNew.Count > 0)
                    {
                        // Add move+attack actions
                        foreach (var enemy in attackableFromNew)
                        {
                            actions.Add(new GameAction(ActionType.MoveAndAttack, unit.Position, hex, enemy.Position));
                        }
                    }
                    else
                    {
                        // Just move
                        actions.Add(new GameAction(ActionType.Move, unit.Position, hex));
                    }
                }
                else
                {
                    // Move 2 hexes - no attack option
                    actions.Add(new GameAction(ActionType.Move, unit.Position, hex));
                }
            }
        }

        // 3. If no actions available, add a Pass action
        if (actions.Count == 0)
        {
            actions.Add(new GameAction(ActionType.Pass, unit.Position));
        }

        return actions;
    }

    /// <summary>
    /// Get all possible actions for all units of a faction
    /// </summary>
    public List<GameAction> GetAllPossibleActions(GameState state, Color faction)
    {
        List<GameAction> allActions = new List<GameAction>();

        foreach (var unit in state.GetFactionUnits(faction))
        {
            if (unit.IsAlive && unit.State != HexBattleDemo.UnitState.Passive)
            {
                allActions.AddRange(GetPossibleActions(state, unit));
            }
        }

        // If no actions available (all units passive), add pass action for first unit
        if (allActions.Count == 0)
        {
            var firstUnit = state.GetFactionUnits(faction).FirstOrDefault();
            if (firstUnit != null)
            {
                allActions.Add(new GameAction(ActionType.Pass, firstUnit.Position));
            }
        }

        return allActions;
    }

    /// <summary>
    /// Apply an action to the game state (modifies state in place)
    /// </summary>
    public void ApplyAction(GameState state, GameAction action)
    {
        var unit = state.GetUnitAt(action.UnitPosition);
        if (unit == null || !unit.IsAlive)
            return;

        switch (action.Type)
        {
            case ActionType.Move:
                PerformMove(state, unit, action.TargetPosition.Value);
                break;

            case ActionType.Attack:
                PerformAttack(state, unit, action.AttackPosition.Value);
                break;

            case ActionType.MoveAndAttack:
                PerformMove(state, unit, action.TargetPosition.Value);
                PerformAttack(state, unit, action.AttackPosition.Value);
                break;

            case ActionType.Pass:
                unit.State = HexBattleDemo.UnitState.Passive;
                break;
        }

        // Check if turn should advance
        if (state.AllUnitsPassive())
        {
            state.ResetUnitStates();
        }
    }

    /// <summary>
    /// Perform a move action
    /// </summary>
    private void PerformMove(GameState state, UnitState unit, Point destination)
    {
        int distance = pathFinder.GetDistance(unit.Position, destination);
        unit.MoveTo(destination);

        // Update unit state based on distance
        if (distance >= unit.MovementRange)
        {
            unit.State = HexBattleDemo.UnitState.Passive;
        }
        else if (distance == 1)
        {
            // Check if enemies in attack range
            var enemies = GetAttackableEnemiesFromPosition(state, destination, unit.FactionColor, unit.AttackRange);
            unit.State = enemies.Count > 0 ? HexBattleDemo.UnitState.Ready : HexBattleDemo.UnitState.Passive;
        }
        else
        {
            unit.State = HexBattleDemo.UnitState.Passive;
        }
    }

    /// <summary>
    /// Perform an attack action
    /// </summary>
    private void PerformAttack(GameState state, UnitState attacker, Point defenderPos)
    {
        var defender = state.GetUnitAt(defenderPos);
        if (defender == null || !defender.IsAlive)
            return;

        // Calculate damage
        int attackerDamage = CalculateDamage(attacker, defender);
        int defenderDamage = 0;

        // Counter-attack if adjacent
        int distance = pathFinder.GetDistance(attacker.Position, defender.Position);
        bool canCounterAttack = distance <= 1;

        // Apply damage
        defender.TakeDamage(attackerDamage);

        // Counter-attack if defender is still alive
        if (canCounterAttack && defender.IsAlive)
        {
            defenderDamage = CalculateDamage(defender, attacker);
            attacker.TakeDamage(defenderDamage);
        }

        // Remove dead units
        state.Units.RemoveAll(u => !u.IsAlive);

        // Attacker becomes passive after attacking
        if (attacker.IsAlive)
        {
            attacker.State = HexBattleDemo.UnitState.Passive;
        }
    }

    /// <summary>
    /// Calculate damage (same formula as CombatManager)
    /// </summary>
    private int CalculateDamage(UnitState attacker, UnitState defender)
    {
        // Base damage: 20-30% of defender's max health
        int baseDamage = (int)(defender.MaxHealth * (0.20 + random.NextDouble() * 0.10));

        // Add some randomness (±20%)
        double randomFactor = 0.8 + random.NextDouble() * 0.4;
        int finalDamage = (int)(baseDamage * randomFactor);

        // Ensure minimum damage of 10 and max of 50
        return Math.Max(10, Math.Min(50, finalDamage));
    }

    /// <summary>
    /// Get all enemies within attack range of a unit
    /// </summary>
    private List<UnitState> GetAttackableEnemies(GameState state, UnitState unit)
    {
        return GetAttackableEnemiesFromPosition(state, unit.Position, unit.FactionColor, unit.AttackRange);
    }

    /// <summary>
    /// Get all enemies within attack range from a specific position
    /// </summary>
    private List<UnitState> GetAttackableEnemiesFromPosition(GameState state, Point position, Color faction, int attackRange)
    {
        List<UnitState> enemies = new List<UnitState>();

        foreach (var unit in state.Units)
        {
            if (!unit.IsAlive || unit.FactionColor == faction)
                continue;

            int distance = pathFinder.GetDistance(position, unit.Position);
            if (distance > 0 && distance <= attackRange)
            {
                enemies.Add(unit);
            }
        }

        return enemies;
    }

    /// <summary>
    /// Evaluate a game state from the perspective of a faction
    /// Returns a score (higher is better for the faction)
    /// </summary>
    public double EvaluateState(GameState state, Color faction)
    {
        if (state.IsGameOver())
        {
            var winner = state.GetWinner();
            if (winner == faction)
                return 10000.0; // Big win
            else
                return -10000.0; // Big loss
        }

        var friendlyUnits = state.GetFactionUnits(faction);
        var enemyUnits = state.Units.Where(u => u.FactionColor != faction).ToList();

        if (friendlyUnits.Count == 0)
            return -10000.0;
        if (enemyUnits.Count == 0)
            return 10000.0;

        double score = 0;

        // Health advantage
        int friendlyHealth = friendlyUnits.Sum(u => u.Health);
        int enemyHealth = enemyUnits.Sum(u => u.Health);
        score += (friendlyHealth - enemyHealth) * 10;

        // Unit count advantage
        score += (friendlyUnits.Count - enemyUnits.Count) * 500;

        // Positional advantage - prefer being closer to enemies
        double minDistanceToEnemy = double.MaxValue;
        foreach (var friendly in friendlyUnits)
        {
            foreach (var enemy in enemyUnits)
            {
                int dist = pathFinder.GetDistance(friendly.Position, enemy.Position);
                if (dist < minDistanceToEnemy)
                    minDistanceToEnemy = dist;
            }
        }
        score -= minDistanceToEnemy * 5; // Closer is better

        return score;
    }
}
