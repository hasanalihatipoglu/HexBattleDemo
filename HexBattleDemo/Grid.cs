using HexBattleDemo;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace HexBattleDemo;

/// <summary>
/// Represents a hexagonal grid for Windows Forms applications
/// </summary>
public class HexGrid : Control
{
    private int hexSize = 30;
    private int gridWidth = 10;
    private int gridHeight = 10;
    private HexOrientation orientation = HexOrientation.PointyTop;
    private Color gridColor = Color.Black;
    private Color hexFillColor = Color.LightGray;
    private float gridLineWidth = 1.5f;

    private Dictionary<Point, Color> hexColors = new Dictionary<Point, Color>();
    private Dictionary<Point, Unit> units = new Dictionary<Point, Unit>();
    private Point? selectedHex = null;
    private Unit selectedUnit = null;
    private HashSet<Point> highlightedHexes = new HashSet<Point>();
    private HashSet<Point> attackableHexes = new HashSet<Point>();
    private PathFinder pathFinder;
    private CombatManager combatManager;
    private UnitActionState actionState = UnitActionState.None;
    private Point? pendingMovePosition = null;
    private Point? hoveredHex = null;
    private List<Point> hoveredPath = new List<Point>();
    private int currentTurn = 1;

    public event EventHandler<HexClickEventArgs> HexClicked;
    public event EventHandler<TurnEventArgs> TurnChanged;

    public HexGrid()
    {
        this.DoubleBuffered = true;
        this.ResizeRedraw = true;
        this.BackColor = Color.White;
        this.pathFinder = new PathFinder(gridWidth, gridHeight);
        this.combatManager = new CombatManager(pathFinder);

        // Subscribe to combat events
        combatManager.CombatResolved += CombatManager_CombatResolved;
    }

    #region Properties

    /// <summary>
    /// Size of each hexagon (radius)
    /// </summary>
    public int HexSize
    {
        get { return hexSize; }
        set
        {
            hexSize = value;
            Invalidate();
        }
    }

    /// <summary>
    /// Number of hexagons in width
    /// </summary>
    public int GridWidth
    {
        get { return gridWidth; }
        set
        {
            gridWidth = value;
            pathFinder = new PathFinder(gridWidth, gridHeight);
            combatManager = new CombatManager(pathFinder);
            Invalidate();
        }
    }

    /// <summary>
    /// Number of hexagons in height
    /// </summary>
    public int GridHeight
    {
        get { return gridHeight; }
        set
        {
            gridHeight = value;
            pathFinder = new PathFinder(gridWidth, gridHeight);
            combatManager = new CombatManager(pathFinder);
            Invalidate();
        }
    }

    /// <summary>
    /// Orientation of hexagons (FlatTop or PointyTop)
    /// </summary>
    public HexOrientation Orientation
    {
        get { return orientation; }
        set
        {
            orientation = value;
            Invalidate();
        }
    }

    /// <summary>
    /// Color of grid lines
    /// </summary>
    public Color GridColor
    {
        get { return gridColor; }
        set
        {
            gridColor = value;
            Invalidate();
        }
    }

    /// <summary>
    /// Default fill color for hexagons
    /// </summary>
    public Color HexFillColor
    {
        get { return hexFillColor; }
        set
        {
            hexFillColor = value;
            Invalidate();
        }
    }

    /// <summary>
    /// Width of grid lines
    /// </summary>
    public float GridLineWidth
    {
        get { return gridLineWidth; }
        set
        {
            gridLineWidth = value;
            Invalidate();
        }
    }

    /// <summary>
    /// Current turn number
    /// </summary>
    public int CurrentTurn
    {
        get { return currentTurn; }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Set the color of a specific hexagon
    /// </summary>
    public void SetHexColor(int q, int r, Color color)
    {
        hexColors[new Point(q, r)] = color;
        Invalidate();
    }

    /// <summary>
    /// Get the color of a specific hexagon
    /// </summary>
    public Color GetHexColor(int q, int r)
    {
        Point key = new Point(q, r);
        return hexColors.ContainsKey(key) ? hexColors[key] : hexFillColor;
    }

    /// <summary>
    /// Clear all custom hex colors
    /// </summary>
    public void ClearHexColors()
    {
        hexColors.Clear();
        Invalidate();
    }

    /// <summary>
    /// Select a hexagon at the given coordinates
    /// </summary>
    public void SelectHex(int q, int r)
    {
        selectedHex = new Point(q, r);
        Invalidate();
    }

    /// <summary>
    /// Clear hex selection
    /// </summary>
    public void ClearSelection()
    {
        selectedHex = null;
        Invalidate();
    }

    /// <summary>
    /// Add a unit to the grid at specified position
    /// </summary>
    public void AddUnit(Unit unit, int q, int r)
    {
        unit.MoveTo(q, r);
        units[new Point(q, r)] = unit;
        Invalidate();
    }

    /// <summary>
    /// Remove unit from specified position
    /// </summary>
    public void RemoveUnit(int q, int r)
    {
        units.Remove(new Point(q, r));
        Invalidate();
    }

    /// <summary>
    /// Get unit at specified position
    /// </summary>
    public Unit GetUnit(int q, int r)
    {
        Point key = new Point(q, r);
        return units.ContainsKey(key) ? units[key] : null;
    }

    /// <summary>
    /// Move unit from one position to another
    /// </summary>
    public bool MoveUnit(int fromQ, int fromR, int toQ, int toR)
    {
        Point fromPos = new Point(fromQ, fromR);
        Point toPos = new Point(toQ, toR);

        if (!units.ContainsKey(fromPos))
            return false;

        // Check if destination is within bounds
        if (toQ < 0 || toQ >= gridWidth || toR < 0 || toR >= gridHeight)
            return false;

        // Check if destination is occupied
        if (units.ContainsKey(toPos))
            return false;

        Unit unit = units[fromPos];
        units.Remove(fromPos);
        unit.MoveTo(toQ, toR);
        units[toPos] = unit;
        Invalidate();

        return true;
    }

    /// <summary>
    /// Clear all units from the grid
    /// </summary>
    public void ClearUnits()
    {
        units.Clear();
        Invalidate();
    }

    /// <summary>
    /// Select a unit and highlight available movement hexes
    /// </summary>
    public void SelectUnit(int q, int r)
    {
        Unit unit = GetUnit(q, r);
        if (unit != null && unit.IsAlive && unit.State != UnitState.Passive)
        {
            selectedUnit = unit;
            selectedHex = new Point(q, r);
            actionState = UnitActionState.SelectingAction;

            // Find all occupied positions (blocked) - only block by friendly units and self
            HashSet<Point> blockedPositions = new HashSet<Point>();
            foreach (var kvp in units)
            {
                // Block only if it's the same faction (friendly) or the current unit
                if (kvp.Value.IsAlive && kvp.Key != new Point(q, r))
                {
                    // Only block if same faction - enemies don't block movement
                    if (kvp.Value.FactionColor == unit.FactionColor)
                    {
                        blockedPositions.Add(kvp.Key);
                    }
                }
            }

            // Determine available movement based on state
            int availableMovement = unit.State == UnitState.Ready ? 0 : unit.MovementRange;
            
            if (availableMovement > 0)
            {
                // Find available movement range
                List<Point> reachableHexes = pathFinder.FindMovementRange(new Point(q, r), availableMovement, blockedPositions);

                // Remove enemy-occupied hexes from available destinations
                highlightedHexes = new HashSet<Point>();
                foreach (Point hex in reachableHexes)
                {
                    Unit occupant = GetUnit(hex.X, hex.Y);
                    if (occupant == null || occupant.FactionColor == unit.FactionColor)
                    {
                        highlightedHexes.Add(hex);
                    }
                }
            }
            else
            {
                highlightedHexes.Clear();
            }

            // Find direct attackable enemies (if unit is Active or Ready)
            if (unit.State == UnitState.Active || unit.State == UnitState.Ready)
            {
                attackableHexes = new HashSet<Point>(
                    combatManager.GetAttackablePositions(new Point(q, r), unit.FactionColor, units, unit.AttackRange)
                );
            }
            else
            {
                attackableHexes.Clear();
            }

            Invalidate();
        }
    }

    /// <summary>
    /// Clear unit selection and highlighted hexes
    /// </summary>
    public void ClearUnitSelection()
    {
        selectedUnit = null;
        selectedHex = null;
        highlightedHexes.Clear();
        attackableHexes.Clear();
        actionState = UnitActionState.None;
        pendingMovePosition = null;
        hoveredHex = null;
        hoveredPath.Clear();
        Invalidate();
    }

    /// <summary>
    /// Get currently selected unit
    /// </summary>
    public Unit GetSelectedUnit()
    {
        return selectedUnit;
    }

    /// <summary>
    /// Check if a hex is highlighted as available for movement
    /// </summary>
    public bool IsHexHighlighted(int q, int r)
    {
        return highlightedHexes.Contains(new Point(q, r));
    }

    /// <summary>
    /// Reset all units to Active state (e.g., start of new turn)
    /// </summary>
    public void ResetAllUnitStates()
    {
        foreach (var unit in units.Values)
        {
            unit.ResetState();
        }
        Invalidate();
    }

    /// <summary>
    /// Check if all units are passive and advance to next turn if so
    /// </summary>
    private void CheckAndAdvanceTurn()
    {
        // Check if all alive units are passive
        bool allPassive = true;
        foreach (var unit in units.Values)
        {
            if (unit.IsAlive && unit.State != UnitState.Passive)
            {
                allPassive = false;
                break;
            }
        }

        // If all units are passive, advance to next turn
        if (allPassive && units.Count > 0)
        {
            AdvanceToNextTurn();
        }
    }

    /// <summary>
    /// Advance to the next turn and reset all unit states
    /// </summary>
    private void AdvanceToNextTurn()
    {
        currentTurn++;
        ResetAllUnitStates();
        
        // Raise turn changed event
        TurnChanged?.Invoke(this, new TurnEventArgs(currentTurn));
        
        Invalidate();
    }

    #endregion

    #region Event Handlers

    private void CombatManager_CombatResolved(object sender, CombatEventArgs e)
    {
        // Remove dead units from the grid
        if (e.Result.DefenderEliminated)
        {
            RemoveUnit(e.Result.DefenderPosition.X, e.Result.DefenderPosition.Y);
        }

        if (e.Result.AttackerEliminated)
        {
            RemoveUnit(e.Result.AttackerPosition.X, e.Result.AttackerPosition.Y);
        }

        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Invalidate();
    }

    #endregion

    #region Drawing

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using (Pen gridPen = new Pen(gridColor, gridLineWidth))
        using (SolidBrush fillBrush = new SolidBrush(hexFillColor))
        using (SolidBrush highlightBrush = new SolidBrush(Color.FromArgb(100, Color.LightGreen)))
        using (SolidBrush attackBrush = new SolidBrush(Color.FromArgb(120, Color.Red)))
        {
            for (int q = 0; q < gridWidth; q++)
            {
                for (int r = 0; r < gridHeight; r++)
                {
                    PointF center = HexToPixel(q, r);
                    PointF[] hexPoints = GetHexagonPoints(center);

                    // Fill hexagon
                    Color fillColor = GetHexColor(q, r);
                    fillBrush.Color = fillColor;
                    g.FillPolygon(fillBrush, hexPoints);

                    // Highlight available movement hexes (green)
                    if (highlightedHexes.Contains(new Point(q, r)))
                    {
                        g.FillPolygon(highlightBrush, hexPoints);
                    }

                    // Highlight attackable hexes (red)
                    if (attackableHexes.Contains(new Point(q, r)))
                    {
                        g.FillPolygon(attackBrush, hexPoints);
                    }

                    // Draw outline
                    g.DrawPolygon(gridPen, hexPoints);

                    // Highlight selected hex
                    if (selectedHex.HasValue && selectedHex.Value.X == q && selectedHex.Value.Y == r)
                    {
                        using (Pen highlightPen = new Pen(Color.Yellow, gridLineWidth + 2))
                        {
                            g.DrawPolygon(highlightPen, hexPoints);
                        }
                    }
                }
            }

            // Draw path lines when hovering over a valid destination
            if (hoveredPath.Count > 1)
            {
                // Determine if this is an attack path (ends on enemy) or movement path
                bool isAttackPath = false;
                Point endPoint = hoveredPath[hoveredPath.Count - 1];
                Unit targetUnit = GetUnit(endPoint.X, endPoint.Y);

                if (targetUnit != null && selectedUnit != null &&
                    targetUnit.FactionColor != selectedUnit.FactionColor)
                {
                    isAttackPath = true;
                }

                // Use red for attack paths, blue for movement paths
                Color pathColor = isAttackPath ? Color.Red : Color.Blue;

                using (Pen pathPen = new Pen(Color.FromArgb(200, pathColor), 3))
                {
                    pathPen.StartCap = LineCap.Round;
                    pathPen.EndCap = LineCap.ArrowAnchor;
                    pathPen.LineJoin = LineJoin.Round;

                    // Draw lines connecting the path
                    for (int i = 0; i < hoveredPath.Count - 1; i++)
                    {
                        PointF start = HexToPixel(hoveredPath[i].X, hoveredPath[i].Y);
                        PointF end = HexToPixel(hoveredPath[i + 1].X, hoveredPath[i + 1].Y);
                        g.DrawLine(pathPen, start, end);
                    }
                }

                // Draw small circles at each waypoint (excluding start and end)
                if (hoveredPath.Count > 2)
                {
                    using (SolidBrush waypointBrush = new SolidBrush(Color.FromArgb(150, pathColor)))
                    {
                        for (int i = 1; i < hoveredPath.Count - 1; i++)
                        {
                            PointF waypoint = HexToPixel(hoveredPath[i].X, hoveredPath[i].Y);
                            float waypointSize = 6;
                            g.FillEllipse(waypointBrush,
                                waypoint.X - waypointSize / 2,
                                waypoint.Y - waypointSize / 2,
                                waypointSize,
                                waypointSize);
                        }
                    }
                }
            }

            // Draw units on top of hexagons
            foreach (var kvp in units)
            {
                Point pos = kvp.Key;
                Unit unit = kvp.Value;

                if (unit.IsAlive)
                {
                    PointF center = HexToPixel(pos.X, pos.Y);
                    float unitRadius = hexSize * 0.6f; // Unit is 60% of hex size
                    unit.Draw(g, center, unitRadius);
                }
            }
        }
    }

    #endregion

    #region Hexagon Math

    private PointF HexToPixel(int q, int r)
    {
        float x, y;
        float gridPixelWidth, gridPixelHeight;

        if (orientation == HexOrientation.FlatTop)
        {
            x = hexSize * (3f / 2f * q);
            y = hexSize * ((float)Math.Sqrt(3) / 2f * q + (float)Math.Sqrt(3) * r);

            // Calculate grid dimensions for centering
            gridPixelWidth = hexSize * (3f / 2f * (gridWidth - 1)) + hexSize * 2;
            gridPixelHeight = hexSize * (float)Math.Sqrt(3) * gridHeight;
        }
        else // PointyTop - rectangular layout
        {
            // Rectangular layout: columns are side by side, rows offset by half width
            x = hexSize * (float)Math.Sqrt(3) * q;
            y = hexSize * 1.5f * r;

            // Offset odd rows by half width
            if (r % 2 == 1)
            {
                x += hexSize * (float)Math.Sqrt(3) / 2f;
            }

            // Calculate grid dimensions for centering
            gridPixelWidth = hexSize * (float)Math.Sqrt(3) * (gridWidth - 1) + hexSize * (float)Math.Sqrt(3);
            if (gridHeight > 1)
            {
                gridPixelWidth += hexSize * (float)Math.Sqrt(3) / 2f; // Account for offset
            }
            gridPixelHeight = hexSize * 1.5f * (gridHeight - 1) + hexSize * 2f;
        }

        // Center the grid
        float offsetX = (this.Width - gridPixelWidth) / 2f;
        float offsetY = (this.Height - gridPixelHeight) / 2f;

        return new PointF(x + offsetX, y + offsetY);
    }

    private Point? PixelToHex(float x, float y)
    {
        float gridPixelWidth, gridPixelHeight;

        if (orientation == HexOrientation.FlatTop)
        {
            gridPixelWidth = hexSize * (3f / 2f * (gridWidth - 1)) + hexSize * 2;
            gridPixelHeight = hexSize * (float)Math.Sqrt(3) * gridHeight;
        }
        else // PointyTop
        {
            gridPixelWidth = hexSize * (float)Math.Sqrt(3) * (gridWidth - 1) + hexSize * (float)Math.Sqrt(3);
            if (gridHeight > 1)
            {
                gridPixelWidth += hexSize * (float)Math.Sqrt(3) / 2f;
            }
            gridPixelHeight = hexSize * 1.5f * (gridHeight - 1) + hexSize * 2f;
        }

        // Adjust for centering offset
        float offsetX = (this.Width - gridPixelWidth) / 2f;
        float offsetY = (this.Height - gridPixelHeight) / 2f;
        x -= offsetX;
        y -= offsetY;

        if (orientation == HexOrientation.FlatTop)
        {
            float q = (2f / 3f * x) / hexSize;
            float r = (-1f / 3f * x + (float)Math.Sqrt(3) / 3f * y) / hexSize;
            return HexRound(q, r);
        }
        else // PointyTop - rectangular layout
        {
            // Calculate row first
            int row = (int)Math.Round(y / (hexSize * 1.5f));

            // Adjust x for row offset
            float adjustedX = x;
            if (row % 2 == 1)
            {
                adjustedX -= hexSize * (float)Math.Sqrt(3) / 2f;
            }

            // Calculate column
            int col = (int)Math.Round(adjustedX / (hexSize * (float)Math.Sqrt(3)));

            // Check if within bounds
            if (col >= 0 && col < gridWidth && row >= 0 && row < gridHeight)
            {
                // Verify the click is actually inside the hexagon
                PointF center = HexToPixel(col, row);
                float dx = x + offsetX - center.X;
                float dy = y + offsetY - center.Y;
                float distance = (float)Math.Sqrt(dx * dx + dy * dy);

                if (distance <= hexSize)
                {
                    return new Point(col, row);
                }
            }

            return null;
        }
    }

    private Point? HexRound(float q, float r)
    {
        float s = -q - r;

        int rq = (int)Math.Round(q);
        int rr = (int)Math.Round(r);
        int rs = (int)Math.Round(s);

        float q_diff = Math.Abs(rq - q);
        float r_diff = Math.Abs(rr - r);
        float s_diff = Math.Abs(rs - s);

        if (q_diff > r_diff && q_diff > s_diff)
        {
            rq = -rr - rs;
        }
        else if (r_diff > s_diff)
        {
            rr = -rq - rs;
        }

        // Check if within bounds
        if (rq >= 0 && rq < gridWidth && rr >= 0 && rr < gridHeight)
        {
            return new Point(rq, rr);
        }

        return null;
    }

    private PointF[] GetHexagonPoints(PointF center)
    {
        PointF[] points = new PointF[6];
        float angleOffset = orientation == HexOrientation.FlatTop ? 0 : 30;

        for (int i = 0; i < 6; i++)
        {
            float angleDeg = 60 * i + angleOffset;
            float angleRad = (float)(Math.PI / 180 * angleDeg);
            points[i] = new PointF(
                center.X + hexSize * (float)Math.Cos(angleRad),
                center.Y + hexSize * (float)Math.Sin(angleRad)
            );
        }

        return points;
    }

    #endregion

    #region Mouse Events

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        Point? hex = PixelToHex(e.X, e.Y);

        // Only show paths if a unit is selected and we're hovering over a valid destination
        if (selectedUnit != null && hex.HasValue)
        {
            // Check if the hovered hex changed
            if (hoveredHex != hex.Value)
            {
                hoveredHex = hex.Value;
                hoveredPath.Clear();

                // Check if hovering over a movement destination (green hex)
                if (highlightedHexes.Contains(hex.Value))
                {
                    // Get blocked positions - only friendly units block movement
                    HashSet<Point> blockedPositions = new HashSet<Point>();
                    foreach (var kvp in units)
                    {
                        if (kvp.Value.IsAlive && kvp.Key != selectedUnit.GridPosition)
                        {
                            // Only block if same faction - enemies don't block movement
                            if (kvp.Value.FactionColor == selectedUnit.FactionColor)
                            {
                                blockedPositions.Add(kvp.Key);
                            }
                        }
                    }

                    // Calculate path from selected unit to hovered hex
                    List<Point> path = pathFinder.FindPath(
                        selectedUnit.GridPosition,
                        hex.Value,
                        blockedPositions
                    );

                    if (path.Count > 0)
                    {
                        // Add the starting position to complete the path
                        hoveredPath.Add(selectedUnit.GridPosition);
                        hoveredPath.AddRange(path);
                    }
                }
                // Check if hovering over an attackable enemy (red hex)
                else if (attackableHexes.Contains(hex.Value))
                {
                    // For attacks, calculate path to enemy through hex centers
                    // Use pathfinding to find route (enemies don't block for visualization)
                    HashSet<Point> blockedPositions = new HashSet<Point>();

                    // Only block by friendly units for path visualization
                    foreach (var kvp in units)
                    {
                        if (kvp.Value.IsAlive &&
                            kvp.Key != selectedUnit.GridPosition &&
                            kvp.Key != hex.Value) // Don't block the target
                        {
                            if (kvp.Value.FactionColor == selectedUnit.FactionColor)
                            {
                                blockedPositions.Add(kvp.Key);
                            }
                        }
                    }

                    // Find path to target
                    List<Point> attackPath = pathFinder.FindPath(
                        selectedUnit.GridPosition,
                        hex.Value,
                        blockedPositions
                    );

                    if (attackPath.Count > 0)
                    {
                        hoveredPath.Add(selectedUnit.GridPosition);
                        hoveredPath.AddRange(attackPath);
                    }
                    else
                    {
                        // Fallback: direct line if pathfinding fails
                        hoveredPath.Add(selectedUnit.GridPosition);
                        hoveredPath.Add(hex.Value);
                    }
                }

                Invalidate();
            }
        }
        else if (hoveredHex.HasValue)
        {
            // Clear hover if we're not in selection mode
            hoveredHex = null;
            hoveredPath.Clear();
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);

        // Clear hover when mouse leaves control
        if (hoveredHex.HasValue)
        {
            hoveredHex = null;
            hoveredPath.Clear();
            Invalidate();
        }
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);

        Point? hex = PixelToHex(e.X, e.Y);

        if (!hex.HasValue)
            return;

        Unit clickedUnit = GetUnit(hex.Value.X, hex.Value.Y);

        switch (actionState)
        {
            case UnitActionState.None:
                // No unit selected - select a unit if clicked
                if (clickedUnit != null && clickedUnit.IsAlive)
                {
                    SelectUnit(hex.Value.X, hex.Value.Y);
                }
                break;

            case UnitActionState.SelectingAction:
                // Unit is selected, waiting for action
                if (clickedUnit == selectedUnit)
                {
                    // Clicked same unit - deselect
                    ClearUnitSelection();
                }
                else if (attackableHexes.Contains(hex.Value))
                {
                    // Direct attack without moving
                    PerformAttack(selectedUnit.GridPosition, hex.Value);
                    ClearUnitSelection();
                }
                else if (highlightedHexes.Contains(hex.Value))
                {
                    // Check if destination is occupied by an enemy
                    Unit destinationUnit = GetUnit(hex.Value.X, hex.Value.Y);
                    if (destinationUnit != null && destinationUnit.FactionColor != selectedUnit.FactionColor)
                    {
                        // Cannot move onto an enemy unit
                        return;
                    }

                    // Check if this is within 1 hex (move + attack option)
                    int distance = pathFinder.GetDistance(selectedUnit.GridPosition, hex.Value);

                    if (distance == 1)
                    {
                        // This is a 1-hex move - show both move and attack options
                        pendingMovePosition = hex.Value;

                        // Get attackable enemies from new position
                        attackableHexes = new HashSet<Point>(
                            combatManager.GetAttackablePositions(hex.Value, selectedUnit.FactionColor, units, selectedUnit.AttackRange)
                        );

                        if (attackableHexes.Count > 0)
                        {
                            // There are enemies to attack - show attack options
                            // BUT keep green hexes visible too!
                            actionState = UnitActionState.SelectingAttackAfterMove;
                            // highlightedHexes stays intact - don't clear!
                        }
                        else
                        {
                            // No enemies to attack - just move
                            PerformMove(selectedUnit.GridPosition, hex.Value);
                            ClearUnitSelection();
                        }
                    }
                    else
                    {
                        // Move 2 hexes - no attack option
                        PerformMove(selectedUnit.GridPosition, hex.Value);
                        ClearUnitSelection();
                    }
                }
                else if (clickedUnit != null && clickedUnit.IsAlive)
                {
                    // Clicked different friendly unit - select it instead
                    if (clickedUnit.FactionColor == selectedUnit.FactionColor)
                    {
                        SelectUnit(hex.Value.X, hex.Value.Y);
                    }
                }
                else
                {
                    // Clicked empty space - deselect
                    ClearUnitSelection();
                }
                break;

            case UnitActionState.SelectingAttackAfterMove:
                // After clicking 1-hex move, user can attack OR move somewhere else
                if (attackableHexes.Contains(hex.Value))
                {
                    // Chose to attack - perform move then attack
                    PerformMove(selectedUnit.GridPosition, pendingMovePosition.Value);
                    PerformAttack(pendingMovePosition.Value, hex.Value);
                    ClearUnitSelection();
                }
                else if (highlightedHexes.Contains(hex.Value))
                {
                    // Chose to move to a different position instead
                    // Check if destination is occupied by an enemy
                    Unit destinationUnit = GetUnit(hex.Value.X, hex.Value.Y);
                    if (destinationUnit != null && destinationUnit.FactionColor != selectedUnit.FactionColor)
                    {
                        // Cannot move onto an enemy unit
                        return;
                    }

                    // Just move to the clicked position, ignore pending move
                    PerformMove(selectedUnit.GridPosition, hex.Value);
                    ClearUnitSelection();
                }
                else if (clickedUnit == selectedUnit)
                {
                    // Clicked on same unit - cancel and go back to selection
                    pendingMovePosition = null;
                    attackableHexes.Clear();
                    actionState = UnitActionState.SelectingAction;
                    Invalidate();
                }
                else
                {
                    // Clicked elsewhere - cancel move+attack, go back to action selection
                    pendingMovePosition = null;
                    attackableHexes.Clear();
                    actionState = UnitActionState.SelectingAction;
                    Invalidate();
                }
                break;
        }

        Invalidate();
        HexClicked?.Invoke(this, new HexClickEventArgs(hex.Value.X, hex.Value.Y, e.Button));
    }

    private void PerformMove(Point from, Point to)
    {
        Unit unit = GetUnit(from.X, from.Y);
        if (unit == null) return;

        // Calculate distance moved
        int distance = pathFinder.GetDistance(from, to);

        // Move the unit
        MoveUnit(from.X, from.Y, to.X, to.Y);

        // Update unit state based on distance
        if (distance >= unit.MovementRange)
        {
            // Moved maximum distance - unit is now passive
            unit.State = UnitState.Passive;
        }
        else if (distance == 1)
        {
            // Moved 1 hex - check if enemies in attack range
            List<Point> attackableEnemies = combatManager.GetAttackablePositions(
                to, unit.FactionColor, units, unit.AttackRange);

            if (attackableEnemies.Count > 0)
            {
                // Can still attack - unit is ready
                unit.State = UnitState.Ready;
            }
            else
            {
                // No enemies to attack - unit is passive
                unit.State = UnitState.Passive;
            }
        }
        else
        {
            // Moved less than max but not 1 hex - passive
            unit.State = UnitState.Passive;
        }

        // Check if all units are passive and advance turn if needed
        CheckAndAdvanceTurn();
    }

    private void PerformAttack(Point attackerPos, Point defenderPos)
    {
        Unit attacker = GetUnit(attackerPos.X, attackerPos.Y);
        Unit defender = GetUnit(defenderPos.X, defenderPos.Y);

        if (attacker != null && defender != null)
        {
            combatManager.ResolveCombat(attacker, defender, attackerPos, defenderPos);

            // After attacking, unit becomes passive
            attacker.State = UnitState.Passive;

            // Check if all units are passive and advance turn if needed
            CheckAndAdvanceTurn();
        }
    }

    #endregion
}

#region Supporting Classes

/// <summary>
/// Hexagon orientation types
/// </summary>
public enum HexOrientation
{
    FlatTop,
    PointyTop
}

/// <summary>
/// Event arguments for hex click events
/// </summary>
public class HexClickEventArgs : EventArgs
{
    public int Q { get; private set; }
    public int R { get; private set; }
    public MouseButtons Button { get; private set; }

    public HexClickEventArgs(int q, int r, MouseButtons button)
    {
        Q = q;
        R = r;
        Button = button;
    }
}

/// <summary>
/// Action state for unit interaction
/// </summary>
public enum UnitActionState
{
    None,                           // No unit selected
    SelectingAction,                // Unit selected, choosing move or attack
    SelectingAttackAfterMove        // Moved 1 hex, now selecting attack target
}

/// <summary>
/// Event arguments for turn change events
/// </summary>
public class TurnEventArgs : EventArgs
{
    public int TurnNumber { get; private set; }

    public TurnEventArgs(int turnNumber)
    {
        TurnNumber = turnNumber;
    }
}

#endregion
