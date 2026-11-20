using GitScope.Core;

namespace GitScope.App;

public sealed record Palette(Color Background, Color Surface, Color Ink, Color Muted, Color Border,
    Color Accent, Color AccentInk, Color AccentOn, Color Selection, Color Error)
{
    private static Color C(string value) => ColorTranslator.FromHtml(value);
    public static Palette Light { get; } = new(C("#F5F6F8"), C("#FFFFFF"), C("#171C27"), C("#626B7C"), C("#DCE1E8"), C("#002FA7"), C("#002FA7"), C("#FFFFFF"), C("#E8EEFF"), C("#B12535"));
    public static Palette Dark { get; } = new(C("#11151D"), C("#191F2A"), C("#F2F4F8"), C("#A5AFC0"), C("#343D4D"), C("#7298FF"), C("#95B2FF"), C("#0D1B40"), C("#293B61"), C("#FF9DA8"));
}

public sealed class BufferedPanel : Panel
{
    public BufferedPanel() { DoubleBuffered = true; }
}

// Keep native Button keyboard/accessibility behavior; render every state with our palette.
public sealed class ScopeButton : Button
{
    private bool _hovered, _pressed;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Palette Colors { get; set; } = Palette.Light;

    public ScopeButton()
    {
        FlatStyle = FlatStyle.Flat; UseVisualStyleBackColor = false; Cursor = Cursors.Hand;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var background = !Enabled ? Colors.Surface : _pressed ? FlatAppearance.MouseDownBackColor : _hovered ? FlatAppearance.MouseOverBackColor : BackColor;
        if (background.IsEmpty) background = BackColor;
        var ink = Enabled ? ForeColor : Colors.Muted;
        e.Graphics.Clear(background);
        using var border = new Pen(Enabled ? FlatAppearance.BorderColor : Colors.Border);
        e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        TextRenderer.DrawText(e.Graphics, Text, Font, Rectangle.Inflate(ClientRectangle, -4, -2), ink,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        if (Focused && ShowFocusCues && Enabled)
            ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -4, -4), ink, background);
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hovered = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hovered = false; Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) _pressed = true; Invalidate(); }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); _pressed = false; Invalidate(); }
    protected override void OnMouseCaptureChanged(EventArgs e) { base.OnMouseCaptureChanged(e); if (!Capture) _pressed = false; Invalidate(); }
    protected override void OnKeyDown(KeyEventArgs e) { base.OnKeyDown(e); if (e.KeyCode == Keys.Space) { _pressed = true; Invalidate(); } }
    protected override void OnKeyUp(KeyEventArgs e) { base.OnKeyUp(e); _pressed = false; Invalidate(); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); _pressed = _hovered = false; Cursor = Enabled ? Cursors.Hand : Cursors.Default; Invalidate(); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); _pressed = false; Invalidate(); }
}

public sealed class CommitGrid : DataGridView
{
    public event Action<int>? WheelRequested;
    protected override void OnMouseWheel(MouseEventArgs e) => WheelRequested?.Invoke(e.Delta);
}

public sealed class ScrollRail : Control
{
    public int Maximum { get; private set; }
    public int Value { get; private set; }
    private int _page = 1, _total = 1, _dragOffset;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Color TrackColor { get; set; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Color ThumbColor { get; set; }
    public event Action<int>? ValueChanged;
    public ScrollRail()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        Cursor = Cursors.Hand; TabStop = false; AccessibleName = "提交列表滚动条"; AccessibleRole = AccessibleRole.ScrollBar;
    }
    public void UpdateRange(int total, int page, int value)
    {
        _total = Math.Max(1, total); _page = Math.Max(1, page); Maximum = Math.Max(0, total - page); Value = Math.Clamp(value, 0, Maximum); Visible = Maximum > 0; Invalidate();
    }
    private Rectangle Thumb()
    {
        int height = Math.Min(Height, Math.Max(28, Height * _page / _total));
        int y = Maximum == 0 ? 0 : (Height - height) * Value / Maximum;
        return new Rectangle(3, y, Math.Max(3, Width - 6), height);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor); using var track = new SolidBrush(TrackColor); using var thumb = new SolidBrush(ThumbColor);
        e.Graphics.FillRectangle(track, Width / 2, 0, 1, Height); e.Graphics.FillRectangle(thumb, Thumb());
    }
    private void SetValue(int value) { Value = Math.Clamp(value, 0, Maximum); ValueChanged?.Invoke(Value); Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e); var thumb = Thumb();
        _dragOffset = thumb.Contains(e.Location) ? e.Y - thumb.Y : thumb.Height / 2;
        Capture = true; Drag(e.Y);
    }
    protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (Capture) Drag(e.Y); }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); Capture = false; }
    private void Drag(int y) { int span = Height - Thumb().Height; if (span > 0) SetValue((int)Math.Round((double)(y - _dragOffset) * Maximum / span)); }
}
