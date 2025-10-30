using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Linq;

namespace HexBattleDemo;

/// <summary>
/// AI player that uses Monte Carlo Tree Search to make decisions
/// </summary>
public class AIPlayer
{
    private Color aiColor;
    private MonteCarloTreeSearch mcts;
    private HexGrid grid;
    private int thinkingTimeMs;

    public event EventHandler<AIActionEventArgs> ActionSelected;
    public event EventHandler ThinkingStarted;
    public event EventHandler ThinkingCompleted;

    public AIPlayer(HexGrid grid, Color aiColor, int thinkingTimeMs = 2000)
    {
        this.grid = grid;
        this.aiColor = aiColor;
        this.thinkingTimeMs = thinkingTimeMs;
        this.mcts = new MonteCarloTreeSearch(aiColor, grid.GridWidth, grid.GridHeight);
    }

    /// <summary>
    /// Check if it's the AI's turn
    /// </summary>
    public bool IsAITurn()
    {
        // Check if there are any AI units that are not passive
        for (int q = 0; q < grid.GridWidth; q++)
        {
            for (int r = 0; r < grid.GridHeight; r++)
            {
                Unit unit = grid.GetUnit(q, r);
                if (unit != null && unit.IsAlive &&
                    unit.FactionColor == aiColor &&
                    unit.State != UnitState.Passive)
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Make the AI take its turn asynchronously
    /// </summary>
    public async Task TakeTurnAsync()
    {
        if (!IsAITurn())
            return;

        ThinkingStarted?.Invoke(this, EventArgs.Empty);

        // Run MCTS in background thread to avoid blocking UI
        GameAction bestAction = await Task.Run(() =>
        {
            // Get current game state
            GameState currentState = GameState.FromHexGrid(grid);

            // Find best action using MCTS
            return mcts.FindBestAction(currentState, thinkingTimeMs);
        });

        ThinkingCompleted?.Invoke(this, EventArgs.Empty);

        if (bestAction != null)
        {
            ActionSelected?.Invoke(this, new AIActionEventArgs(bestAction));
            ExecuteAction(bestAction);
        }
    }

    /// <summary>
    /// Execute an action on the actual game grid
    /// </summary>
    public void ExecuteAction(GameAction action)
    {
        if (action == null)
            return;

        // Use the grid's ExecuteAction method
        grid.ExecuteAction(action);
    }
}

/// <summary>
/// Event arguments for AI action selection
/// </summary>
public class AIActionEventArgs : EventArgs
{
    public GameAction Action { get; private set; }

    public AIActionEventArgs(GameAction action)
    {
        Action = action;
    }
}
