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

        this.Controls.Add(hexGrid);
    }

    private void HexGrid_HexClicked(object sender, HexClickEventArgs e)
    {
        // Get unit at clicked position
        Unit unit = hexGrid.GetUnit(e.Q, e.R);
        Unit selectedUnit = hexGrid.GetSelectedUnit();

        if (unit != null && selectedUnit == null)
        {
            // Show unit info when selecting
            this.Text = $"Hex Grid - {unit.FactionColor.Name} unit at ({e.Q}, {e.R}) - Health: {unit.Health}/{unit.MaxHealth} - Green=Move, Red=Attack";
        }
        else if (selectedUnit != null)
        {
            this.Text = $"Hex Grid - {selectedUnit.FactionColor.Name} unit selected - Green=Move, Red=Attack";
        }
        else
        {
            this.Text = $"Hex Grid with Combat - Click on a unit to select it";
        }
    }
}