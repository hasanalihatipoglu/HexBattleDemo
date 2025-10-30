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
    public List<GameAction> GetPossibleActions(GameState state, SimulatedUnit unit)
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
    /// IMPORTANT: Returns attacks first, then moves - prioritizes attacking!
    /// </summary>
    public List<GameAction> GetAllPossibleActions(GameState state, Color faction)
    {
        List<GameAction> attacks = new List<GameAction>();
        List<GameAction> moves = new List<GameAction>();

        foreach (var unit in state.GetFactionUnits(faction))
        {
            if (unit.IsAlive && unit.State != HexBattleDemo.UnitState.Passive)
            {
                var unitActions = GetPossibleActions(state, unit);

                // Separate attacks from moves
                foreach (var action in unitActions)
                {
                    if (action.Type == ActionType.Attack || action.Type == ActionType.MoveAndAttack)
                    {
                        attacks.Add(action);
                    }
                    else
                    {
                        moves.Add(action);
                    }
                }
            }
        }

        // CRITICAL: Return attacks first! This ensures MCTS explores attacks before moves
        List<GameAction> allActions = new List<GameAction>();
        allActions.AddRange(attacks);
        allActions.AddRange(moves);

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
    private void PerformMove(GameState state, SimulatedUnit unit, Point destination)
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
    private void PerformAttack(GameState state, SimulatedUnit attacker, Point defenderPos)
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
    private int CalculateDamage(SimulatedUnit attacker, SimulatedUnit defender)
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
    private List<SimulatedUnit> GetAttackableEnemies(GameState state, SimulatedUnit unit)
    {
        return GetAttackableEnemiesFromPosition(state, unit.Position, unit.FactionColor, unit.AttackRange);
    }

    /// <summary>
    /// Get all enemies within attack range from a specific position
    /// </summary>
    private List<SimulatedUnit> GetAttackableEnemiesFromPosition(GameState state, Point position, Color faction, int attackRange)
    {
        List<SimulatedUnit> enemies = new List<SimulatedUnit>();

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

        // 1. Unit count advantage (game-winning)
        score += (friendlyUnits.Count - enemyUnits.Count) * 1000;

        // 2. Total health advantage (material advantage)
        int friendlyHealth = friendlyUnits.Sum(u => u.Health);
        int enemyHealth = enemyUnits.Sum(u => u.Health);
        score += (friendlyHealth - enemyHealth) * 5;

        // 3. Health percentage (prefer healthy units)
        double friendlyHealthPct = friendlyUnits.Average(u => (double)u.Health / u.MaxHealth);
        double enemyHealthPct = enemyUnits.Average(u => (double)u.Health / u.MaxHealth);
        score += (friendlyHealthPct - enemyHealthPct) * 200;

        // 4. Tactical positioning evaluation
        foreach (var friendly in friendlyUnits)
        {
            // Count enemies we can attack - HUGE bonus!
            var attackableEnemies = GetAttackableEnemiesFromPosition(state, friendly.Position, faction, friendly.AttackRange);
            score += attackableEnemies.Count * 200; // MASSIVE reward for attacking position

            // Bonus for being able to kill low-health enemies
            foreach (var enemy in attackableEnemies)
            {
                if (enemy.Health <= 40) // Killable enemy
                    score += 300; // HUGE bonus for kill opportunities
                else if (enemy.Health <= 60) // Weak enemy
                    score += 150; // Big bonus for damaging weak targets
            }

            // Only penalize being threatened if we CAN'T attack back
            if (attackableEnemies.Count == 0)
            {
                int enemiesThreateningUs = 0;
                foreach (var enemy in enemyUnits)
                {
                    int dist = pathFinder.GetDistance(friendly.Position, enemy.Position);
                    if (dist > 0 && dist <= enemy.AttackRange)
                    {
                        enemiesThreateningUs++;
                    }
                }

                // Only a small penalty for being threatened when not attacking
                score -= enemiesThreateningUs * 20;

                // Only retreat if severely wounded AND can't attack
                if (friendly.Health <= 20) // Very low health
                {
                    int minDistToEnemy = enemyUnits.Min(e => pathFinder.GetDistance(friendly.Position, e.Position));
                    score += minDistToEnemy * 10; // Small retreat bonus only if critical
                }
            }
        }

        // 5. Enemy vulnerability evaluation
        foreach (var enemy in enemyUnits)
        {
            // How many of our units can attack this enemy?
            int friendliesThreateningEnemy = 0;
            foreach (var friendly in friendlyUnits)
            {
                int dist = pathFinder.GetDistance(friendly.Position, enemy.Position);
                if (dist > 0 && dist <= friendly.AttackRange)
                {
                    friendliesThreateningEnemy++;
                }
            }

            // Massive bonus for outnumbering enemy (focus fire is key!)
            if (friendliesThreateningEnemy > 1)
                score += friendliesThreateningEnemy * 80; // Focus fire bonus

            // HUGE bonus if enemy is low health and we can reach them
            if (friendliesThreateningEnemy > 0)
            {
                if (enemy.Health <= 40)
                    score += 250; // Massive bonus - finish them!
                else if (enemy.Health <= 60)
                    score += 100; // Good bonus for weak targets
            }
        }

        // 6. Aggressive positioning - prefer being in combat range
        double avgDistToEnemy = 0;
        int distCount = 0;
        foreach (var friendly in friendlyUnits)
        {
            foreach (var enemy in enemyUnits)
            {
                int dist = pathFinder.GetDistance(friendly.Position, enemy.Position);
                avgDistToEnemy += dist;
                distCount++;

                // Extra bonus for being in attack range (ready to fight!)
                if (dist > 0 && dist <= friendly.AttackRange)
                {
                    score += 50; // Reward being in striking distance
                }
            }
        }
        if (distCount > 0)
        {
            avgDistToEnemy /= distCount;
            score -= avgDistToEnemy * 15; // Strong preference for being closer
        }

        return score;
    }
}
