using System.Globalization;

namespace GitScope.App;

// A themed, non-modal calendar. The text fields remain the canonical optional filter values.
internal sealed class DateCalendar : Form
{
    private readonly Palette _colors;
    private readonly Action<DateTime?> _choose;
    private readonly DateTime? _selected;
    private readonly DateTime _today;
    private DateTime _month, _focusedDate;
    private readonly Label _heading, _message;
    private readonly TextBox _monthInput;
    private readonly ScopeButton _previous, _next;
    private readonly ScopeButton[] _days = new ScopeButton[42];
    private readonly DateTime?[] _dates = new DateTime?[42];

    internal DateCalendar(string title, DateTime? selected, DateTime initial, DateTime today, Palette colors, Action<DateTime?> choose)
    {
        _colors = colors; _choose = choose; _selected = selected?.Date; _today = today.Date;
        _focusedDate = (selected ?? initial).Date; _month = new DateTime(_focusedDate.Year, _focusedDate.Month, 1);
        Text = title; AccessibleName = title; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual;
        FormBorderStyle = FormBorderStyle.FixedToolWindow; MaximizeBox = false; MinimizeBox = false;
        AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(336, 386); Font = new Font("Microsoft YaHei UI", 9); BackColor = colors.Surface; ForeColor = colors.Ink;
        _heading = LabelAt("", 74, 12, 188, 28); _heading.TextAlign = ContentAlignment.MiddleCenter;
        _previous = ButtonAt("上月", "上一个月", 12, 12, 58, () => ChangeMonth(-1));
        _next = ButtonAt("下月", "下一个月", 266, 12, 58, () => ChangeMonth(1));
        string[] weekdays = ["一", "二", "三", "四", "五", "六", "日"];
        for (int i = 0; i < 7; i++) { var label = LabelAt(weekdays[i], 14 + i * 44, 48, 42, 24); label.TextAlign = ContentAlignment.MiddleCenter; label.ForeColor = colors.Muted; }
        for (int i = 0; i < _days.Length; i++)
        {
            int index = i;
            _days[i] = ButtonAt("", "日期", 14 + i % 7 * 44, 76 + i / 7 * 32, 42, () => Choose(_dates[index]));
            _days[i].Height = 30;
            _days[i].GotFocus += (_, _) => { if (_dates[index] is DateTime date) _focusedDate = date; };
        }
        LabelAt("月份", 14, 278, 42, 25).ForeColor = colors.Muted;
        _monthInput = new TextBox { Text = "", AccessibleName = "跳转月份", PlaceholderText = "yyyy-MM", BorderStyle = BorderStyle.FixedSingle, BackColor = colors.Background, ForeColor = colors.Ink };
        _monthInput.SetBounds(60, 278, 184, 28); Controls.Add(_monthInput);
        ButtonAt("转到", "跳转到指定月份", 252, 276, 70, GoToMonth);
        _monthInput.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; GoToMonth(); } };
        _message = LabelAt("方向键选日期 · Enter 确认 · Esc 关闭", 14, 308, 310, 24); _message.ForeColor = colors.Muted;
        ButtonAt("今天", "选择今天", 14, 342, 92, () => Choose(_today));
        ButtonAt("清空日期", "清空当前日期筛选", 114, 342, 108, () => Choose(null));
        ButtonAt("关闭", "关闭日历", 230, 342, 92, Close);
        RenderMonth();
        Shown += (_, _) => FocusDay();
        Deactivate += (_, _) => { if (!IsDisposed) Close(); };
    }

    private Label LabelAt(string text, int x, int y, int width, int height)
    {
        var label = new Label { Text = text, AutoSize = false, ForeColor = _colors.Ink, BackColor = _colors.Surface };
        label.SetBounds(x, y, width, height); Controls.Add(label); return label;
    }
    private ScopeButton ButtonAt(string text, string name, int x, int y, int width, Action click)
    {
        var button = new ScopeButton { Text = text, AccessibleName = name, Colors = _colors, BackColor = _colors.Surface, ForeColor = _colors.Ink, Font = Font };
        button.FlatAppearance.BorderColor = _colors.Border; button.FlatAppearance.MouseOverBackColor = _colors.Selection; button.FlatAppearance.MouseDownBackColor = _colors.Selection;
        button.SetBounds(x, y, width, 30); button.Click += (_, _) => click(); Controls.Add(button); return button;
    }
    private void Choose(DateTime? date) { _choose(date); Close(); }
    private void ChangeMonth(int delta)
    {
        if ((delta < 0 && _month.Year == 1 && _month.Month == 1) || (delta > 0 && _month.Year == 9999 && _month.Month == 12)) return;
        _month = _month.AddMonths(delta);
        _focusedDate = new DateTime(_month.Year, _month.Month, Math.Min(_focusedDate.Day, DateTime.DaysInMonth(_month.Year, _month.Month)));
        RenderMonth(); FocusDay();
    }
    private void GoToMonth()
    {
        if (!DateTime.TryParseExact(_monthInput.Text.Trim(), "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var month))
        { _message.Text = "月份请填写 yyyy-MM，例如 2026-11。"; _message.ForeColor = _colors.Error; _monthInput.Focus(); return; }
        _month = month; _focusedDate = month; RenderMonth(); FocusDay();
    }
    private void RenderMonth()
    {
        _heading.Text = _month.ToString("yyyy 年 M 月", CultureInfo.InvariantCulture);
        _monthInput.Text = _month.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        _message.Text = "方向键选日期 · Enter 确认 · Esc 关闭"; _message.ForeColor = _colors.Muted;
        _previous.Enabled = _month.Year > 1 || _month.Month > 1; _next.Enabled = _month.Year < 9999 || _month.Month < 12;
        int offset = ((int)_month.DayOfWeek + 6) % 7;
        for (int i = 0; i < _days.Length; i++)
        {
            long ticks = _month.Ticks + (long)(i - offset) * TimeSpan.TicksPerDay;
            DateTime? date = ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks ? null : new DateTime(ticks);
            _dates[i] = date; var button = _days[i]; button.Enabled = date.HasValue;
            button.Text = date?.Day.ToString(CultureInfo.InvariantCulture) ?? "";
            button.AccessibleName = date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "无日期";
            bool selected = date.HasValue && date == _selected;
            button.BackColor = selected ? _colors.Accent : _colors.Surface;
            button.ForeColor = selected ? _colors.AccentOn : date?.Month == _month.Month ? _colors.Ink : _colors.Muted;
            button.FlatAppearance.BorderColor = selected || date == _today ? _colors.Accent : _colors.Border;
            button.FlatAppearance.MouseOverBackColor = selected ? _colors.Accent : _colors.Selection;
            button.FlatAppearance.MouseDownBackColor = button.FlatAppearance.MouseOverBackColor;
            button.TabStop = date == _focusedDate; button.Invalidate();
        }
    }
    private void FocusDay()
    {
        int index = Array.FindIndex(_dates, date => date == _focusedDate);
        if (index >= 0) _days[index].Focus();
    }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape) { Close(); return true; }
        if (_days.Any(day => day.Focused))
        {
            int delta = keyData switch { Keys.Left => -1, Keys.Right => 1, Keys.Up => -7, Keys.Down => 7, _ => 0 };
            if (delta != 0)
            {
                long ticks = _focusedDate.Ticks + (long)delta * TimeSpan.TicksPerDay;
                if (ticks >= DateTime.MinValue.Ticks && ticks <= DateTime.MaxValue.Ticks)
                { _focusedDate = new DateTime(ticks); _month = new DateTime(_focusedDate.Year, _focusedDate.Month, 1); RenderMonth(); FocusDay(); }
                return true;
            }
            if (keyData is Keys.PageUp or Keys.PageDown) { ChangeMonth(keyData == Keys.PageUp ? -1 : 1); return true; }
            if (keyData == Keys.Enter) { Choose(_focusedDate); return true; }
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}
