using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace HexBattleDemo;

/// <summary>
/// Represents a unit with faction color and health
/// </summary>
public class Unit
{
    private Color factionColor;
    private int health;
    private int maxHealth;
    private Point gridPosition;
    private int movementRange;
    private int attackRange;

    public Unit(Color factionColor, int health, int maxHealth = 100, int movementRange = 2, int attackRange = 1)
    {
        this.factionColor = factionColor;
        this.health = health;
        this.maxHealth = maxHealth;
        this.movementRange = movementRange;
        this.attackRange = attackRange;
        this.gridPosition = new Point(0, 0);
    }

    #region Properties

    /// <summary>
    /// Color representing the unit's faction
    /// </summary>
    public Color FactionColor
    {
        get { return factionColor; }
        set { factionColor = value; }
    }

    /// <summary>
    /// Current health of the unit
    /// </summary>
    public int Health
    {
        get { return health; }
        set
        {
            health = Math.Max(0, Math.Min(value, maxHealth));
        }
    }

    /// <summary>
    /// Maximum health of the unit
    /// </summary>
    public int MaxHealth
    {
        get { return maxHealth; }
        set { maxHealth = value; }
    }

    /// <summary>
    /// Grid position (Q, R coordinates)
    /// </summary>
    public Point GridPosition
    {
        get { return gridPosition; }
        set { gridPosition = value; }
    }

    /// <summary>
    /// Movement range of the unit (how many hexes it can move)
    /// </summary>
    public int MovementRange
    {
        get { return movementRange; }
        set { movementRange = value; }
    }

    /// <summary>
    /// Attack range of the unit (how many hexes away it can attack)
    /// </summary>
    public int AttackRange
    {
        get { return attackRange; }
        set { attackRange = value; }
    }

    /// <summary>
    /// Check if unit is alive
    /// </summary>
    public bool IsAlive
    {
        get { return health > 0; }
    }

    #endregion

    #region Methods

    /// <summary>
    /// Apply damage to the unit
    /// </summary>
    public void TakeDamage(int damage)
    {
        Health -= damage;
    }

    /// <summary>
    /// Heal the unit
    /// </summary>
    public void Heal(int amount)
    {
        Health += amount;
    }

    /// <summary>
    /// Draw the unit at the specified center position
    /// </summary>
    public void Draw(Graphics g, PointF center, float radius)
    {
        if (!IsAlive)
            return;

        // Enable anti-aliasing for smooth circles
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Calculate circle bounds
        float diameter = radius * 2;
        RectangleF circleBounds = new RectangleF(
            center.X - radius,
            center.Y - radius,
            diameter,
            diameter
        );

        // Draw circle with faction color
        using (SolidBrush brush = new SolidBrush(factionColor))
        {
            g.FillEllipse(brush, circleBounds);
        }

        // Draw border
        using (Pen borderPen = new Pen(Color.Black, 2))
        {
            g.DrawEllipse(borderPen, circleBounds);
        }

        // Draw health number in the center
        string healthText = health.ToString();
        using (Font font = new Font("Arial", radius * 0.5f, FontStyle.Bold))
        using (StringFormat sf = new StringFormat())
        {
            sf.Alignment = StringAlignment.Center;
            sf.LineAlignment = StringAlignment.Center;

            // Draw text shadow for better visibility
            using (SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(128, Color.Black)))
            {
                g.DrawString(healthText, font, shadowBrush, center.X + 1, center.Y + 1, sf);
            }

            // Draw text in white or black depending on background brightness
            Color textColor = GetContrastColor(factionColor);
            using (SolidBrush textBrush = new SolidBrush(textColor))
            {
                g.DrawString(healthText, font, textBrush, center, sf);
            }
        }
    }

    /// <summary>
    /// Get contrasting color (white or black) based on background brightness
    /// </summary>
    private Color GetContrastColor(Color backgroundColor)
    {
        // Calculate perceived brightness
        double brightness = (0.299 * backgroundColor.R +
                           0.587 * backgroundColor.G +
                           0.114 * backgroundColor.B) / 255;

        // Return black for light backgrounds, white for dark backgrounds
        return brightness > 0.5 ? Color.Black : Color.White;
    }

    /// <summary>
    /// Move unit to new grid position
    /// </summary>
    public void MoveTo(int q, int r)
    {
        gridPosition = new Point(q, r);
    }

    /// <summary>
    /// Move unit to new grid position
    /// </summary>
    public void MoveTo(Point position)
    {
        gridPosition = position;
    }

    #endregion

    public override string ToString()
    {
        return $"Unit at ({gridPosition.X}, {gridPosition.Y}) - Health: {health}/{maxHealth} - Faction: {factionColor.Name}";
    }
}