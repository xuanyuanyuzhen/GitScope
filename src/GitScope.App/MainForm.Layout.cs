using System.Runtime.InteropServices;
using GitScope.Core;
using Microsoft.Win32;

namespace GitScope.App;

public sealed partial class MainForm
{
    private int D(int value) => LogicalToDeviceUnits(value);
    private void Arrange()
    {
        if (_light is null) return;
        int w = ClientSize.Width, h = ClientSize.Height, pad = D(24), gap = D(12), content = w - pad * 2;
        _logo.SetBounds(pad, D(19), D(36), D(36)); _brand.SetBounds(pad + D(47), D(14), D(172), D(42));
        _tagline.SetBounds(pad + D(223), D(31), Math.Max(D(100), w - D(580)), D(23));
        _system.SetBounds(w - pad - D(105), D(22), D(105), D(32)); _dark.SetBounds(w - pad - D(173), D(22), D(68), D(32)); _light.SetBounds(w - pad - D(241), D(22), D(68), D(32));
        _rule.SetBounds(pad, D(73), content, 1);
        int recoveryOffset = _recoveryPanel.Visible ? D(46) : 0;
        _recoveryPanel.SetBounds(pad, D(81), content, D(38)); _recoveryLabel.SetBounds(D(12), D(10), D(183), D(20));
        _recoveryItems.SetBounds(D(200), D(6), Math.Max(D(250), content - D(351)), D(28)); _recover.SetBounds(content - D(135), D(5), D(123), D(28));
        int referenceWidth = Math.Clamp(content / 4, D(240), D(340));
        int referenceX = w - pad - D(104) - gap - referenceWidth;
        int repositoryWidth = referenceX - gap - D(100) - gap - pad;
        _repositoryCaption.SetBounds(pad, D(87) + recoveryOffset, D(150), D(19));
        _repositoryPath.SetBounds(pad, D(108) + recoveryOffset, repositoryWidth, D(31));
        _browseRepository.SetBounds(pad + repositoryWidth + gap, D(108) + recoveryOffset, D(100), D(31));
        _referenceCaption.SetBounds(referenceX, D(87) + recoveryOffset, referenceWidth, D(19));
        _references.SetBounds(referenceX, D(108) + recoveryOffset, referenceWidth, D(31));
        _loadRepository.SetBounds(w - pad - D(104), D(108) + recoveryOffset, D(104), D(31));
        int sx = pad, ex = sx + D(184), zx = ex + D(184), fx = zx + D(196);
        _startCaption.SetBounds(sx, D(151) + recoveryOffset, D(172), D(19)); _startDate.SetBounds(sx, D(172) + recoveryOffset, D(124), D(31));
        _chooseStart.SetBounds(sx + D(130), D(171) + recoveryOffset, D(42), D(29));
        _endCaption.SetBounds(ex, D(151) + recoveryOffset, D(172), D(19)); _endDate.SetBounds(ex, D(172) + recoveryOffset, D(124), D(31));
        _chooseEnd.SetBounds(ex + D(130), D(171) + recoveryOffset, D(42), D(29));
        _zoneCaption.SetBounds(zx, D(151) + recoveryOffset, D(184), D(19)); _timeZone.SetBounds(zx, D(172) + recoveryOffset, D(184), D(31));
        _firstParent.SetBounds(fx, D(172) + recoveryOffset, D(102), D(31));
        _query.SetBounds(fx + D(110), D(172) + recoveryOffset, D(105), D(31)); _reset.SetBounds(fx + D(227), D(172) + recoveryOffset, D(96), D(31));
        _dateHelp.SetBounds(pad, D(207) + recoveryOffset, content, D(22));
        int middleY = D(242) + recoveryOffset, outputY = h - D(292), middleHeight = outputY - D(16) - middleY;
        int left = (content - D(16)) / 2;
        _listPanel.SetBounds(pad, middleY, left, middleHeight); _previewPanel.SetBounds(pad + left + D(16), middleY, content - left - D(16), middleHeight);
        _listTitle.SetBounds(D(16), D(12), left - D(32), D(25)); _listScope.SetBounds(D(16), D(39), left - D(32), D(20)); _listScope.AutoEllipsis = true;
        _commits.SetBounds(1, D(65), left - D(14), middleHeight - D(145)); _scroll.SetBounds(left - D(12), D(96), D(11), middleHeight - D(176));
        _empty.SetBounds(D(20), D(72), left - D(40), middleHeight - D(152));
        _pageInfo.SetBounds(D(16), middleHeight - D(70), left - D(32), D(22));
        int pageX = D(16), pageY = middleHeight - D(39);
        foreach (var item in new (Control Control, int Width)[] { (_firstPage, 52), (_previous, 70), (_pageNumber, 72), (_jumpPage, 54), (_next, 70), (_lastPage, 52) })
        { item.Control.SetBounds(pageX, pageY, D(item.Width), D(30)); pageX += D(item.Width + 8); }
        int previewWidth = _previewPanel.Width;
        _detailLabel.SetBounds(D(16), D(12), previewWidth - D(32), D(20)); _detailTitle.SetBounds(D(16), D(34), previewWidth - D(32), D(31));
        _detailMeta.SetBounds(D(16), D(69), previewWidth - D(32), D(21));
        _oid.SetBounds(D(16), D(93), previewWidth - D(119), D(22)); _copy.SetBounds(previewWidth - D(91), D(89), D(75), D(27));
        _comparison.SetBounds(D(16), D(121), previewWidth - D(132), D(20)); _comparison.AutoEllipsis = true;
        _showFiles.SetBounds(previewWidth - D(108), D(117), D(92), D(27));
        _files.SetBounds(1, D(147), previewWidth - 2, Math.Max(D(15), middleHeight - D(197)));
        _diff.SetBounds(D(14), D(147), previewWidth - D(28), Math.Max(D(15), middleHeight - D(197)));
        _dependency.SetBounds(D(16), middleHeight - D(42), previewWidth - D(32), D(38)); _dependency.AutoEllipsis = true;
        bool compactPreview = middleHeight < D(260);
        _detailLabel.Visible = !compactPreview;
        if (compactPreview)
        {
            _detailTitle.SetBounds(D(16), D(12), previewWidth - D(32), D(29));
            _detailMeta.SetBounds(D(16), D(44), previewWidth - D(32), D(20));
            _oid.SetBounds(D(16), D(67), previewWidth - D(119), D(20)); _copy.SetBounds(previewWidth - D(91), D(64), D(75), D(25));
            _comparison.SetBounds(D(16), D(91), previewWidth - D(132), D(18)); _showFiles.SetBounds(previewWidth - D(108), D(87), D(92), D(25));
            _files.SetBounds(1, D(114), previewWidth - 2, middleHeight - D(154));
            _diff.SetBounds(D(14), D(114), previewWidth - D(28), middleHeight - D(154));
            _dependency.SetBounds(D(16), middleHeight - D(34), previewWidth - D(32), D(30));
        }
        _outputPanel.SetBounds(pad, outputY, content, D(240));
        _outputTitle.SetBounds(D(16), D(12), D(230), D(25)); _lastExport.SetBounds(Math.Max(D(250), content - D(600)), D(15), Math.Min(D(584), content - D(266)), D(21));
        _targetCaption.SetBounds(D(16), D(54), D(66), D(22));
        _targetPath.SetBounds(D(88), D(48), content - D(324), D(31));
        _browseTarget.SetBounds(content - D(224), D(48), D(98), D(31)); _bindTarget.SetBounds(content - D(114), D(48), D(98), D(31));
        int keepWidth = Math.Min(D(360), content * 34 / 100), actionX = keepWidth + D(40), actionWidth = content - actionX - D(16);
        _keepCaption.SetBounds(D(16), D(92), keepWidth, D(21)); _keepList.SetBounds(D(16), D(117), keepWidth, D(73));
        _keepPath.SetBounds(D(16), D(202), keepWidth - D(65), D(28)); _addKeep.SetBounds(D(16) + keepWidth - D(56), D(202), D(56), D(28));
        _planSummary.SetBounds(actionX, D(95), actionWidth, D(70)); _safetyNote.SetBounds(actionX, D(169), actionWidth, D(22));
        int exportX = content - D(16 + 150);
        _export.SetBounds(exportX, D(198), D(150), D(34));
        _prepare.SetBounds(exportX - D(142 + 12), D(198), D(142), D(34));
        _open.SetBounds(_prepare.Left - D(102 + 12), D(198), D(102), D(34));
        _cancel.SetBounds(_open.Left - D(96 + 12), D(198), D(96), D(34));
        _status.SetBounds(pad, h - D(43), content, D(23)); _status.AutoEllipsis = true;
        _settingsNotice.SetBounds(pad, h - D(21), content, D(18));
        _commits.RowTemplate.Height = D(39); _commits.ColumnHeadersHeight = D(31); _files.RowTemplate.Height = D(31); _files.ColumnHeadersHeight = D(27);
        _commits.Columns["Date"]!.Width = D(109); _commits.Columns["Id"]!.Width = D(90);
        _files.Columns["Status"]!.Width = D(55); _files.Columns["Kind"]!.Width = D(85);
        UpdateRail();
    }

    public void SetTheme(ThemeMode mode)
    {
        _calendar?.Close(); _theme = mode; ApplyTheme(); Persist();
    }
    public void RefreshSystemTheme() { if (_theme == ThemeMode.System) ApplyTheme(); }
    private static bool ReadSystemDark()
    {
        try { using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"); return key?.GetValue("AppsUseLightTheme") is int value && value == 0; }
        catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException) { return false; }
    }
    private void SystemPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (_theme != ThemeMode.System || IsDisposed || !IsHandleCreated) return;
        try { BeginInvoke(RefreshSystemTheme); } catch (InvalidOperationException) { }
    }
    private void ApplyTheme()
    {
        IsDark = _theme == ThemeMode.Dark || (_theme == ThemeMode.System && _systemDark()); _palette = IsDark ? Palette.Dark : Palette.Light;
        BackColor = _palette.Background; ForeColor = _palette.Ink;
        void Apply(Control parent)
        {
            foreach (Control control in parent.Controls)
            {
                control.ForeColor = _palette.Ink;
                control.BackColor = control.Parent is BufferedPanel || control is BufferedPanel or TextBox or ComboBox or CheckedListBox ? _palette.Surface : _palette.Background;
                if (control is Button button)
                {
                    if (button is ScopeButton themed) themed.Colors = _palette;
                    button.BackColor = _palette.Surface; button.FlatAppearance.BorderColor = _palette.Border;
                    button.FlatAppearance.MouseOverBackColor = _palette.Selection; button.FlatAppearance.MouseDownBackColor = _palette.Selection;
                }
                if (control is Panel) Apply(control);
            }
        }
        Apply(this);
        foreach (var label in new[] { _tagline, _startCaption, _endCaption, _zoneCaption, _listScope, _detailMeta, _comparison, _dependency, _pageInfo, _lastExport,
            _targetCaption, _safetyNote, _status, _settingsNotice, _dateHelp }) label.ForeColor = _palette.Muted;
        _detailLabel.ForeColor = _palette.AccentInk;
        _logo.BackColor = _palette.Accent; _logo.ForeColor = _palette.AccentOn; _rule.BackColor = _palette.Border;
        foreach (var button in new[] { _query, _export })
        {
            button.BackColor = _palette.Accent; button.ForeColor = _palette.AccentOn; button.FlatAppearance.BorderColor = _palette.Accent;
            button.FlatAppearance.MouseOverBackColor = IsDark ? ColorTranslator.FromHtml("#95B2FF") : ColorTranslator.FromHtml("#183FAF");
            button.FlatAppearance.MouseDownBackColor = IsDark ? ColorTranslator.FromHtml("#ABC1FF") : ColorTranslator.FromHtml("#00247D");
        }
        foreach (var pair in new[] { (_light, ThemeMode.Light), (_dark, ThemeMode.Dark), (_system, ThemeMode.System) })
        {
            bool selected = pair.Item2 == _theme;
            pair.Item1.BackColor = selected ? _palette.Ink : _palette.Background; pair.Item1.ForeColor = selected ? _palette.Surface : _palette.Muted;
            pair.Item1.FlatAppearance.MouseOverBackColor = selected ? (IsDark ? ColorTranslator.FromHtml("#DCE1E8") : ColorTranslator.FromHtml("#343D4D")) : _palette.Selection;
            pair.Item1.FlatAppearance.MouseDownBackColor = selected ? (IsDark ? ColorTranslator.FromHtml("#BEC8D8") : ColorTranslator.FromHtml("#11151D")) : _palette.Selection;
        }
        foreach (var grid in new DataGridView[] { _commits, _files })
        {
            grid.BackgroundColor = _palette.Surface; grid.GridColor = _palette.Border;
            grid.DefaultCellStyle = new DataGridViewCellStyle { BackColor = _palette.Surface, ForeColor = _palette.Ink, SelectionBackColor = _palette.Selection,
                SelectionForeColor = _palette.Ink, Padding = new Padding(D(8), 0, D(6), 0), Font = new Font("Microsoft YaHei UI", 9) };
            grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = _palette.Surface, ForeColor = _palette.Muted, SelectionBackColor = _palette.Surface,
                SelectionForeColor = _palette.Muted, Font = new Font("Microsoft YaHei UI", 8.5f), Padding = new Padding(D(8), 0, 0, 0) };
        }
        _commits.Columns["Id"]!.DefaultCellStyle.Font = new Font("Consolas", 9);
        _scroll.BackColor = _palette.Surface; _scroll.TrackColor = _palette.Border; _scroll.ThumbColor = _palette.Muted;
        _recoveryLabel.ForeColor = _palette.AccentInk;
        _settingsNotice.Text = _settingsError.Length > 0 ? _settingsError : "本地对象导出 · 远程更新将在后续阶段接入";
        if (_state is "failed" or "recoveryRequired") _status.ForeColor = _palette.Error;
        if (IsHandleCreated) { int dark = IsDark ? 1 : 0; DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int)); }
        UpdateActions(); Invalidate(true);
    }
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e); int dark = IsDark ? 1 : 0; DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
    }
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
