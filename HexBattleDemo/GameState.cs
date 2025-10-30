using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace HexBattleDemo;

/// <summary>
/// Represents a snapshot of the game state for MCTS simulations
/// </summary>
public class GameState
{
    public List<SimulatedUnit> Units { get; set; }
    public int TurnNumber { get; set; }
    public int GridWidth { get; set; }
    public int GridHeight { get; set; }

    public GameState()
    {
        Units = new List<SimulatedUnit>();
    }

    /// <summary>
    /// Create a deep copy of the game state
    /// </summary>
    public GameState Clone()
    {
        GameState clone = new GameState
        {
            TurnNumber = this.TurnNumber,
            GridWidth = this.GridWidth,
            GridHeight = this.GridHeight,
            Units = new List<SimulatedUnit>()
        };

        foreach (var unit in Units)
        {
            clone.Units.Add(unit.Clone());
        }

        return clone;
    }

    /// <summary>
    /// Create a GameState from the current HexGrid state
    /// </summary>
    public static GameState FromHexGrid(HexGrid grid)
    {
        GameState state = new GameState
        {
            TurnNumber = grid.CurrentTurn,
            GridWidth = grid.GridWidth,
            GridHeight = grid.GridHeight,
            Units = new List<SimulatedUnit>()
        };

        // Extract all units from the grid
        for (int q = 0; q < grid.GridWidth; q++)
        {
            for (int r = 0; r < grid.GridHeight; r++)
            {
                Unit unit = grid.GetUnit(q, r);
                if (unit != null && unit.IsAlive)
                {
                    state.Units.Add(new SimulatedUnit
                    {
                        Position = new Point(q, r),
                        Health = unit.Health,
                        MaxHealth = unit.MaxHealth,
                        FactionColor = unit.FactionColor,
                        MovementRange = unit.MovementRange,
                        AttackRange = unit.AttackRange,
                        State = unit.State
                    });
                }
            }
        }

        return state;
    }

    /// <summary>
    /// Get unit at specified position
    /// </summary>
    public SimulatedUnit GetUnitAt(Point position)
    {
        return Units.FirstOrDefault(u => u.Position.X == position.X && u.Position.Y == position.Y);
    }

    /// <summary>
    /// Get all units of a specific faction
    /// </summary>
    public List<SimulatedUnit> GetFactionUnits(Color faction)
    {
        return Units.Where(u => u.FactionColor == faction).ToList();
    }

    /// <summary>
    /// Check if the game is over (one faction eliminated)
    /// </summary>
    public bool IsGameOver()
    {
        var factions = Units.Select(u => u.FactionColor).Distinct().Count();
        return factions <= 1;
    }

    /// <summary>
    /// Get the winning faction (if game is over)
    /// </summary>
    public Color? GetWinner()
    {
        if (!IsGameOver() || Units.Count == 0)
            return null;

        return Units[0].FactionColor;
    }

    /// <summary>
    /// Check if all units are passive (turn should end)
    /// </summary>
    public bool AllUnitsPassive()
    {
        return Units.All(u => u.State == HexBattleDemo.UnitState.Passive);
    }

    /// <summary>
    /// Reset all units to Active state (new turn)
    /// </summary>
    public void ResetUnitStates()
    {
        foreach (var unit in Units)
        {
            unit.State = HexBattleDemo.UnitState.Active;
        }
        TurnNumber++;
    }
}

/// <summary>
/// Lightweight representation of a unit for simulations
/// </summary>
public class SimulatedUnit
{
    public Point Position { get; set; }
    public int Health { get; set; }
    public int MaxHealth { get; set; }
    public Color FactionColor { get; set; }
    public int MovementRange { get; set; }
    public int AttackRange { get; set; }
    public HexBattleDemo.UnitState State { get; set; }

    public bool IsAlive => Health > 0;

    public SimulatedUnit Clone()
    {
        return new SimulatedUnit
        {
            Position = new Point(Position.X, Position.Y),
            Health = this.Health,
            MaxHealth = this.MaxHealth,
            FactionColor = this.FactionColor,
            MovementRange = this.MovementRange,
            AttackRange = this.AttackRange,
            State = this.State
        };
    }

    public void TakeDamage(int damage)
    {
        Health = Math.Max(0, Health - damage);
    }

    public void MoveTo(Point newPosition)
    {
        Position = newPosition;
    }
}
