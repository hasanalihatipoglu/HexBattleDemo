using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace HexBattleDemo;

/// <summary>
/// Monte Carlo Tree Search node
/// </summary>
public class MCTSNode
{
    public GameState State { get; set; }
    public GameAction Action { get; set; }
    public MCTSNode Parent { get; set; }
    public List<MCTSNode> Children { get; set; }
    public int Visits { get; set; }
    public double TotalScore { get; set; }
    public Color PlayerColor { get; set; }
    public List<GameAction> UntriedActions { get; set; }

    public MCTSNode(GameState state, GameAction action, MCTSNode parent, Color playerColor)
    {
        State = state;
        Action = action;
        Parent = parent;
        PlayerColor = playerColor;
        Children = new List<MCTSNode>();
        Visits = 0;
        TotalScore = 0;
        UntriedActions = new List<GameAction>();
    }

    /// <summary>
    /// Check if node is fully expanded
    /// </summary>
    public bool IsFullyExpanded()
    {
        return UntriedActions.Count == 0;
    }

    /// <summary>
    /// Check if node is terminal (game over)
    /// </summary>
    public bool IsTerminal()
    {
        return State.IsGameOver();
    }

    /// <summary>
    /// Get average score
    /// </summary>
    public double AverageScore()
    {
        return Visits > 0 ? TotalScore / Visits : 0;
    }

    /// <summary>
    /// Calculate UCB1 value for this node
    /// </summary>
    public double UCB1(double explorationConstant = 1.41)
    {
        if (Visits == 0)
            return double.MaxValue;

        double exploitation = AverageScore();
        double exploration = explorationConstant * Math.Sqrt(Math.Log(Parent.Visits) / Visits);

        return exploitation + exploration;
    }
}

/// <summary>
/// Monte Carlo Tree Search implementation for game AI
/// </summary>
public class MonteCarloTreeSearch
{
    private GameSimulator simulator;
    private Random random;
    private Color aiColor;
    private int maxIterations;
    private double explorationConstant;

    public MonteCarloTreeSearch(Color aiColor, int gridWidth, int gridHeight, int maxIterations = 5000)
    {
        this.aiColor = aiColor;
        this.simulator = new GameSimulator(gridWidth, gridHeight);
        this.random = new Random();
        this.maxIterations = maxIterations;
        this.explorationConstant = 1.41; // Standard UCB1 constant
    }

    /// <summary>
    /// Find the best action for the AI using MCTS
    /// </summary>
    public GameAction FindBestAction(GameState initialState, int timeoutMs = 2000)
    {
        // Create root node
        MCTSNode root = new MCTSNode(initialState, null, null, aiColor);
        root.UntriedActions = simulator.GetAllPossibleActions(initialState, aiColor);

        // Time limit
        DateTime startTime = DateTime.Now;
        int iterations = 0;

        while ((DateTime.Now - startTime).TotalMilliseconds < timeoutMs && iterations < maxIterations)
        {
            // 1. Selection - select a node to expand
            MCTSNode selectedNode = Select(root);

            // 2. Expansion - expand the selected node
            MCTSNode expandedNode = Expand(selectedNode);

            // 3. Simulation - simulate a random game from the expanded node
            double score = Simulate(expandedNode.State);

            // 4. Backpropagation - update scores up the tree
            Backpropagate(expandedNode, score);

            iterations++;
        }

        // Return the best child based on visit count (most robust)
        if (root.Children.Count == 0)
        {
            // No children expanded, return random action
            if (root.UntriedActions.Count > 0)
                return root.UntriedActions[random.Next(root.UntriedActions.Count)];

            // Fallback - pass action
            var firstUnit = initialState.GetFactionUnits(aiColor).FirstOrDefault();
            if (firstUnit != null)
                return new GameAction(ActionType.Pass, firstUnit.Position);

            return null;
        }

        MCTSNode bestChild = root.Children.OrderByDescending(c => c.Visits).First();
        return bestChild.Action;
    }

    /// <summary>
    /// Selection phase - traverse tree using UCB1
    /// </summary>
    private MCTSNode Select(MCTSNode node)
    {
        while (!node.IsTerminal())
        {
            if (!node.IsFullyExpanded())
            {
                return node;
            }
            else
            {
                node = SelectBestChild(node);
            }
        }
        return node;
    }

    /// <summary>
    /// Select best child using UCB1
    /// </summary>
    private MCTSNode SelectBestChild(MCTSNode node)
    {
        return node.Children.OrderByDescending(c => c.UCB1(explorationConstant)).First();
    }

    /// <summary>
    /// Expansion phase - add a new child node
    /// </summary>
    private MCTSNode Expand(MCTSNode node)
    {
        // If terminal, return the node itself
        if (node.IsTerminal())
            return node;

        // If not fully expanded, expand a random untried action
        if (node.UntriedActions.Count > 0)
        {
            // Pick a random untried action
            int index = random.Next(node.UntriedActions.Count);
            GameAction action = node.UntriedActions[index];
            node.UntriedActions.RemoveAt(index);

            // Clone state and apply action
            GameState newState = node.State.Clone();
            simulator.ApplyAction(newState, action);

            // Determine which player's turn it is in the new state
            Color currentPlayer = DetermineCurrentPlayer(newState);

            // Create new node
            MCTSNode newNode = new MCTSNode(newState, action, node, currentPlayer);

            // Get untried actions for the new state
            newNode.UntriedActions = simulator.GetAllPossibleActions(newState, currentPlayer);

            // Add to children
            node.Children.Add(newNode);

            return newNode;
        }

        // If fully expanded, return a random child
        return node.Children[random.Next(node.Children.Count)];
    }

    /// <summary>
    /// Simulation phase - play out the game randomly
    /// </summary>
    private double Simulate(GameState state)
    {
        GameState simState = state.Clone();
        int maxMoves = 50; // Prevent infinite loops
        int moveCount = 0;

        while (!simState.IsGameOver() && moveCount < maxMoves)
        {
            Color currentPlayer = DetermineCurrentPlayer(simState);

            // Get all possible actions for current player
            var actions = simulator.GetAllPossibleActions(simState, currentPlayer);

            if (actions.Count == 0)
                break;

            // Choose a random action with some heuristic weighting
            GameAction action = ChooseSimulationAction(simState, actions, currentPlayer);

            // Apply the action
            simulator.ApplyAction(simState, action);

            moveCount++;
        }

        // Evaluate final state
        return simulator.EvaluateState(simState, aiColor);
    }

    /// <summary>
    /// Choose an action during simulation (with intelligent heuristics)
    /// </summary>
    private GameAction ChooseSimulationAction(GameState state, List<GameAction> actions, Color player)
    {
        // Separate action types
        var attacks = actions.Where(a => a.Type == ActionType.Attack || a.Type == ActionType.MoveAndAttack).ToList();
        var moves = actions.Where(a => a.Type == ActionType.Move).ToList();

        // Strongly prefer attacks (90% of the time if available)
        if (attacks.Count > 0 && random.NextDouble() > 0.1)
        {
            // Smart attack selection: prioritize killing blows and low-health targets
            var scoredAttacks = new List<(GameAction action, double score)>();

            foreach (var attack in attacks)
            {
                double attackScore = 0;
                Point targetPos = attack.AttackPosition ?? attack.TargetPosition ?? new Point(0, 0);
                var target = state.GetUnitAt(targetPos);

                if (target != null)
                {
                    // Huge bonus for killing blows (estimated)
                    if (target.Health <= 35) // Likely to kill
                        attackScore += 1000;

                    // Prefer low health targets
                    attackScore += (100 - target.Health);

                    // Prefer move+attack over direct attack (better positioning)
                    if (attack.Type == ActionType.MoveAndAttack)
                        attackScore += 50;

                    scoredAttacks.Add((attack, attackScore));
                }
            }

            // Choose best attack with some randomness (80% best, 20% random)
            if (scoredAttacks.Count > 0)
            {
                if (random.NextDouble() > 0.2)
                {
                    // Pick best attack
                    return scoredAttacks.OrderByDescending(a => a.score).First().action;
                }
                else
                {
                    // Random attack for variety
                    return attacks[random.Next(attacks.Count)];
                }
            }

            return attacks[random.Next(attacks.Count)];
        }

        // If no good attacks, make smart moves
        if (moves.Count > 0)
        {
            var myUnits = state.GetFactionUnits(player);
            var enemyUnits = state.Units.Where(u => u.FactionColor != player).ToList();

            if (enemyUnits.Count > 0)
            {
                // Score moves by how close they get us to enemies
                var scoredMoves = new List<(GameAction action, double score)>();

                foreach (var move in moves)
                {
                    double moveScore = 0;
                    Point dest = move.TargetPosition ?? new Point(0, 0);

                    // Prefer moves that get us closer to enemies
                    double minDistToEnemy = enemyUnits.Min(e =>
                        Math.Abs(dest.X - e.Position.X) + Math.Abs(dest.Y - e.Position.Y));
                    moveScore -= minDistToEnemy * 10; // Negative distance = closer is better

                    // Bonus for moves that put us in attack range
                    var myUnit = state.GetUnitAt(move.UnitPosition);
                    if (myUnit != null)
                    {
                        foreach (var enemy in enemyUnits)
                        {
                            int distToEnemy = Math.Abs(dest.X - enemy.Position.X) + Math.Abs(dest.Y - enemy.Position.Y);
                            if (distToEnemy <= myUnit.AttackRange)
                            {
                                moveScore += 100; // Can attack next turn
                                if (enemy.Health <= 30)
                                    moveScore += 100; // Can finish off weak enemy
                            }
                        }
                    }

                    scoredMoves.Add((move, moveScore));
                }

                // Choose best move (with some randomness)
                if (scoredMoves.Count > 0 && random.NextDouble() > 0.3)
                {
                    return scoredMoves.OrderByDescending(m => m.score).First().action;
                }
            }
        }

        // Fallback: random action
        return actions[random.Next(actions.Count)];
    }

    /// <summary>
    /// Backpropagation phase - update node statistics
    /// </summary>
    private void Backpropagate(MCTSNode node, double score)
    {
        while (node != null)
        {
            node.Visits++;

            // Normalize score to -1 to 1 range for better MCTS behavior
            double normalizedScore = NormalizeScore(score);

            // If this is the opponent's node, invert the score
            if (node.PlayerColor != aiColor)
            {
                normalizedScore = -normalizedScore;
            }

            node.TotalScore += normalizedScore;
            node = node.Parent;
        }
    }

    /// <summary>
    /// Normalize score to -1 to 1 range
    /// </summary>
    private double NormalizeScore(double score)
    {
        // Use tanh to map scores to -1 to 1
        return Math.Tanh(score / 1000.0);
    }

    /// <summary>
    /// Determine which player should act next based on game state
    /// </summary>
    private Color DetermineCurrentPlayer(GameState state)
    {
        // Get all factions
        var factions = state.Units.Select(u => u.FactionColor).Distinct().ToList();

        if (factions.Count <= 1)
            return factions.Count > 0 ? factions[0] : aiColor;

        // Check which faction has units that are not passive
        foreach (var faction in factions)
        {
            var factionUnits = state.GetFactionUnits(faction);
            if (factionUnits.Any(u => u.State != HexBattleDemo.UnitState.Passive))
            {
                return faction;
            }
        }

        // All units passive - return first faction (turn will advance)
        return factions[0];
    }
}
