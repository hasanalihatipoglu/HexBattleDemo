using HexBattleDemo;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace HexBattleDemo;

public partial class Form1 : Form
{
    private HexGrid hexGrid;

    public Form1()
    {
        InitializeComponent();
        InitializeHexGrid();
    }


    private void InitializeHexGrid()
    {
        hexGrid = new HexGrid();
        hexGrid.Dock = DockStyle.Fill;
        hexGrid.GridWidth = 5;
        hexGrid.GridHeight = 5;
        hexGrid.HexSize = 30;
        hexGrid.Orientation = HexOrientation.PointyTop;
        hexGrid.HexFillColor = Color.LightGray;
        hexGrid.GridColor = Color.Black;
        hexGrid.GridLineWidth = 1.5f;

        // Create red unit (Red faction, 100 health) at top-left corner
        Unit redUnit = new Unit(Color.Red, 100, 100, movementRange: 2, attackRange: 2);
        hexGrid.AddUnit(redUnit, 0, 0);

        // Create blue unit (Blue faction, 75 health) at bottom-right corner
        Unit blueUnit = new Unit(Color.Blue, 75, 100, movementRange: 2, attackRange: 2);
        hexGrid.AddUnit(blueUnit, hexGrid.GridWidth - 1, hexGrid.GridHeight - 1);

        // Handle hex clicks
        hexGrid.HexClicked += HexGrid_HexClicked;
        
        // Handle turn changes
        hexGrid.TurnChanged += HexGrid_TurnChanged;

        this.Controls.Add(hexGrid);
        
        // Set initial title
        UpdateTitle();
    }

    private void HexGrid_TurnChanged(object sender, TurnEventArgs e)
    {
        UpdateTitle();
    }

    private void UpdateTitle()
    {
        var activeFactions = hexGrid.GetActiveFactions();

        if (activeFactions.Count > 0)
        {
            string factionsText = string.Join(" & ", activeFactions);
            this.Text = $"Hex Grid Battle - Turn {hexGrid.CurrentTurn} - {factionsText} can act";
        }
        else
        {
            this.Text = $"Hex Grid Battle - Turn {hexGrid.CurrentTurn} - All units passive";
        }
    }

    private void HexGrid_HexClicked(object sender, HexClickEventArgs e)
    {
        // Get unit at clicked position
        Unit unit = hexGrid.GetUnit(e.Q, e.R);
        Unit selectedUnit = hexGrid.GetSelectedUnit();
        var activeFactions = hexGrid.GetActiveFactions();
        string factionsText = activeFactions.Count > 0 ? string.Join(" & ", activeFactions) : "None";

        if (unit != null && selectedUnit == null)
        {
            // Show unit info when selecting
            this.Text = $"Turn {hexGrid.CurrentTurn} ({factionsText}) - {unit.FactionColor.Name} unit at ({e.Q}, {e.R}) - Health: {unit.Health}/{unit.MaxHealth} - State: {unit.State}";
        }
        else if (selectedUnit != null)
        {
            this.Text = $"Turn {hexGrid.CurrentTurn} ({factionsText}) - {selectedUnit.FactionColor.Name} unit selected - State: {selectedUnit.State} - Green=Move, Red=Attack";
        }
        else
        {
            UpdateTitle();
        }
    }
}
