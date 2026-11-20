using System.Diagnostics;
using System.Runtime.InteropServices;
using GitScope.Core;
using Microsoft.Win32;

namespace GitScope.App;

public sealed partial class MainForm : Form
{
    private readonly IRepositoryService _repositories;
    private readonly IHistoryService _histories;
    private readonly ITargetService _targets;
    private readonly AppSettingsStore _store;
    private readonly Func<bool> _systemDark;
    private readonly bool _autoLoad;
    private readonly ToolTip _tips = new() { AutoPopDelay = 12000 };
    private AppSettings _saved;
    private Palette _palette = Palette.Light;
    private ThemeMode _theme;
    private RepositoryInfo? _repository;
    private HistorySnapshot? _history;
    private ManagedTarget? _target;
    private ExportPlan? _plan;
    private CommitSummary? _selected;
    private IReadOnlyList<ManagedTarget> _knownTargets = [];
    private IReadOnlyList<RecoveryItem> _pending = [];
    private IReadOnlyList<ChangedFile> _changedFiles = [];
    private IReadOnlyList<CommitSummary> _pageItems = [];
    private CancellationTokenSource? _queryCancellation, _previewCancellation, _actionCancellation;
    private int _queryVersion, _previewVersion, _page;
    private bool _filling, _fillingKeeps, _applyingValues, _actionBusy, _closingAfterAction, _allowClose, _initialized;
    private bool _recoveryScanFailed, _queryBusy;
    private string _settingsError = "", _displayZone = "", _state = "idle";
    private string? _activeRequestId;
    private DateCalendar? _calendar;

    private readonly Label _brand = LabelOf("GitScope", 23, FontStyle.Bold, "Segoe UI");
    private readonly Label _tagline = LabelOf("本地版本 · 可恢复导出", 10);
    private readonly Label _logo = LabelOf("G", 18, FontStyle.Bold, "Segoe UI");
    private readonly Panel _rule = new();
    private readonly Button _light, _dark, _system, _browseRepository, _loadRepository, _query, _reset, _previous, _next, _copy, _showFiles, _browseTarget, _bindTarget, _addKeep, _prepare, _export, _open, _cancel, _recover;
    private readonly Button _firstPage, _lastPage, _jumpPage, _chooseStart, _chooseEnd;
    private readonly TextBox _pageNumber = Input("跳转页码", "页码");
    private readonly Label _dateHelp = LabelOf("按提交日期筛选（含首尾日），留空不限；导出仍是所选提交的完整代码。", 9);
    private readonly TextBox _repositoryPath = Input("仓库路径", "选择或输入一个本地 Git 仓库");
    private readonly ComboBox _references = new() { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, DrawMode = DrawMode.OwnerDrawFixed, AccessibleName = "引用选择" };
    private readonly TextBox _startDate = Input("开始日期", "yyyy-MM-dd");
    private readonly TextBox _endDate = Input("结束日期", "yyyy-MM-dd");
    private readonly TextBox _timeZone = Input("IANA 时区", "例如 Asia/Shanghai");
    private readonly CheckBox _firstParent = new() { Text = "仅主线", AutoSize = false, AccessibleName = "仅第一父提交历史" };
    private readonly Label _repositoryCaption = LabelOf("本地仓库", 9, FontStyle.Bold), _referenceCaption = LabelOf("分支 / 引用", 9, FontStyle.Bold),
        _startCaption = LabelOf("开始日期", 9), _endCaption = LabelOf("结束日期（包含当天）", 9), _zoneCaption = LabelOf("时区 · IANA", 9);
    private readonly BufferedPanel _listPanel = new(), _previewPanel = new(), _outputPanel = new(), _recoveryPanel = new();
    private readonly Label _listTitle = LabelOf("01  /  提交历史", 12, FontStyle.Bold), _listScope = LabelOf("选择仓库后查询本地历史", 9),
        _pageInfo = LabelOf("—", 9), _empty = LabelOf("选择仓库，找到一个版本。", 15),
        _detailLabel = LabelOf("所选提交", 9, FontStyle.Bold), _detailTitle = LabelOf("选择一条提交查看变化", 15, FontStyle.Bold),
        _detailMeta = LabelOf("", 9), _comparison = LabelOf("", 9), _dependency = LabelOf("依赖声明变化将在选定目标后提示。", 9);
    private readonly TextBox _oid = new() { ReadOnly = true, BorderStyle = BorderStyle.None, Font = new Font("Consolas", 9), AccessibleName = "完整提交 ID" };
    private readonly CommitGrid _commits = new();
    private readonly ScrollRail _scroll = new();
    private readonly DataGridView _files = new();
    private readonly TextBox _diff = new() { Multiline = true, ReadOnly = true, WordWrap = false, ScrollBars = ScrollBars.Both, BorderStyle = BorderStyle.None, Font = new Font("Consolas", 9), AccessibleName = "文本差异预览", Visible = false };
    private readonly Label _outputTitle = LabelOf("02  /  固定输出", 12, FontStyle.Bold), _lastExport = LabelOf("上次导出：尚未绑定目标", 9),
        _targetCaption = LabelOf("输出目录", 9), _keepCaption = LabelOf("勾选保留 · 未选项随旧内容移除", 9, FontStyle.Bold),
        _planSummary = LabelOf("绑定专用目录并选择提交，再检查更新计划。", 10),
        _safetyNote = LabelOf("更新前请停止目标中的开发服务器和安装器。", 9);
    private readonly TextBox _targetPath = Input("固定输出目录", "首次请使用新建或空的专用目录");
    private readonly CheckedListBox _keepList = new() { CheckOnClick = true, BorderStyle = BorderStyle.None, IntegralHeight = false, AccessibleName = "保留环境和已安装依赖" };
    private readonly TextBox _keepPath = Input("添加保留路径", "精确相对路径，如 apps/web/node_modules");
    private readonly Label _status = LabelOf("就绪", 9), _settingsNotice = LabelOf("", 9), _recoveryLabel = LabelOf("存在待恢复的目录更新", 9, FontStyle.Bold);
    private readonly ComboBox _recoveryItems = new() { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, DrawMode = DrawMode.OwnerDrawFixed, AccessibleName = "待恢复目录" };

    public string State => _state;
    public string StatusText => _status.Text;
    public string? SelectedOid => _selected?.Oid;
    public string? LastExportedOid => _target?.LastCommitOid;
    public string? TargetId => _target?.TargetId;
    public ThemeMode Theme => _theme;
    public bool IsDark { get; private set; }
    public int TotalCount => _history?.Items.Count ?? 0;
    public int VisibleCount => _pageItems.Count;
    public int PageIndex => _page;
    public int PageCount => (TotalCount + 49) / 50;
    public bool CanExport => _export.Enabled;
    public bool HasPreparedPlan => _plan is not null;
    public string PlanText => _planSummary.Text;
    public string RepositoryText => _repositoryPath.Text;
    public string StartDateText => _startDate.Text;
    public string TimeZoneText => _timeZone.Text;
    public int RecoveryCount => _pending.Count;
    public Color GridInk => _commits.DefaultCellStyle.ForeColor;
    public Color InputBackground => _repositoryPath.BackColor;
    public IReadOnlyList<string> SelectedPreservationPaths => _keepList.CheckedItems.Cast<string>().ToArray();

    public MainForm(IRepositoryService repositories, IHistoryService histories, ITargetService targets,
        AppSettingsStore store, string? repositoryPath = null, Func<bool>? systemDark = null, bool autoLoad = true)
    {
        _repositories = repositories; _histories = histories; _targets = targets; _store = store; _systemDark = systemDark ?? ReadSystemDark; _autoLoad = autoLoad;
        try { _saved = store.Read(); }
        catch (ScopeException e) { _saved = new(); _settingsError = e.Message; }
        _theme = _saved.Theme;
        Text = "GitScope · 历史代码"; Font = new Font("Microsoft YaHei UI", 9.5f); StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1280, 900); MinimumSize = new Size(1100, 850); AutoScaleMode = AutoScaleMode.Dpi; DoubleBuffered = true;
        _light = ButtonOf("浅色", () => SetTheme(ThemeMode.Light)); _dark = ButtonOf("夜间", () => SetTheme(ThemeMode.Dark)); _system = ButtonOf("跟随系统", () => SetTheme(ThemeMode.System));
        _browseRepository = AsyncButton("选择仓库", BrowseRepositoryAsync); _loadRepository = AsyncButton("读取引用", () => LoadRepositoryAsync(_repositoryPath.Text));
        _query = AsyncButton("查询历史", QueryCurrentAsync); _reset = AsyncButton("清空日期", async () => { SetDates("", "", _timeZone.Text); await QueryCurrentAsync(); });
        _chooseStart = ButtonOf("日历", () => ShowCalendar(_startDate, _endDate)); _chooseStart.AccessibleName = "选择开始日期";
        _chooseEnd = ButtonOf("日历", () => ShowCalendar(_endDate, _startDate)); _chooseEnd.AccessibleName = "选择结束日期";
        _previous = ButtonOf("上一页", () => NavigatePage(_page - 1)); _next = ButtonOf("下一页", () => NavigatePage(_page + 1));
        _firstPage = ButtonOf("首页", () => NavigatePage(0)); _lastPage = ButtonOf("末页", () => NavigatePage(PageCount - 1));
        _jumpPage = ButtonOf("跳转", () => JumpToPage(_pageNumber.Text)); _pageNumber.TextAlign = HorizontalAlignment.Center;
        _copy = ButtonOf("复制 ID", CopyOid); _showFiles = ButtonOf("返回文件", ShowFiles);
        _browseTarget = ButtonOf("选择目录", BrowseTarget); _bindTarget = AsyncButton("绑定目录", () => BindTargetAsync(_targetPath.Text));
        _addKeep = ButtonOf("添加", AddKeepPath); _prepare = AsyncButton("检查更新计划", PrepareExportAsync); _export = AsyncButton("导出到此目录", ExecutePlanAsync);
        _open = ButtonOf("打开目录", OpenTarget); _cancel = ButtonOf("取消任务", RequestCancel); _recover = AsyncButton("恢复旧目录", RecoverSelectedAsync);
        _logo.TextAlign = ContentAlignment.MiddleCenter; _empty.TextAlign = ContentAlignment.MiddleCenter; _detailTitle.AutoEllipsis = true;
        _lastExport.TextAlign = ContentAlignment.MiddleRight; _pageInfo.TextAlign = ContentAlignment.MiddleLeft;
        _planSummary.AutoEllipsis = false; _recoveryPanel.Visible = false;
        _showFiles.Visible = false; _settingsNotice.AutoEllipsis = true;
        Controls.AddRange([_brand, _tagline, _logo, _rule, _light, _dark, _system, _repositoryCaption, _repositoryPath, _browseRepository, _referenceCaption,
            _references, _loadRepository, _startCaption, _startDate, _chooseStart, _endCaption, _endDate, _chooseEnd, _zoneCaption, _timeZone, _firstParent, _query, _reset, _dateHelp,
            _listPanel, _previewPanel, _outputPanel, _status, _settingsNotice, _recoveryPanel]);
        _listPanel.Controls.AddRange([_listTitle, _listScope, _commits, _scroll, _pageInfo, _firstPage, _previous, _pageNumber, _jumpPage, _next, _lastPage, _empty]);
        _previewPanel.Controls.AddRange([_detailLabel, _detailTitle, _detailMeta, _oid, _copy, _comparison, _showFiles, _files, _diff, _dependency]);
        _outputPanel.Controls.AddRange([_outputTitle, _lastExport, _targetCaption, _targetPath, _browseTarget, _bindTarget, _keepCaption, _keepList,
            _keepPath, _addKeep, _planSummary, _safetyNote, _cancel, _open, _prepare, _export]);
        _recoveryPanel.Controls.AddRange([_recoveryLabel, _recoveryItems, _recover]);
        ConfigureGrids();
        foreach (var combo in new[] { _references, _recoveryItems })
        {
            combo.ItemHeight = D(22);
            combo.DrawItem += (_, e) =>
            {
                bool selected = e.State.HasFlag(DrawItemState.Selected);
                using var background = new SolidBrush(selected ? _palette.Selection : _palette.Surface);
                e.Graphics.FillRectangle(background, e.Bounds);
                if (e.Index >= 0 && e.Index < combo.Items.Count)
                    TextRenderer.DrawText(e.Graphics, combo.Items[e.Index]?.ToString() ?? "", combo.Font, e.Bounds,
                        _palette.Ink, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
                if (e.State.HasFlag(DrawItemState.Focus)) e.DrawFocusRectangle();
            };
        }
        foreach (var panel in new[] { _listPanel, _previewPanel, _outputPanel, _recoveryPanel })
            panel.Paint += (_, e) => { using var pen = new Pen(_palette.Border); e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1); };
        _applyingValues = true;
        _repositoryPath.Text = repositoryPath ?? _saved.RepositoryPath; _startDate.Text = _saved.StartDate; _endDate.Text = _saved.EndDate; _timeZone.Text = _saved.TimeZoneId;
        _targetPath.Text = _saved.TargetPath; _firstParent.Checked = _saved.HistoryMode == HistoryMode.FirstParent;
        SetPreservationPaths(_saved.KeepPaths ?? []); _applyingValues = false;
        WireEvents(); AssignTabOrder(); ApplyTheme(); Arrange(); _initialized = true; UpdateActions();
        Shown += async (_, _) => { _repositoryPath.Focus(); if (_autoLoad) await InitializeAsync(); };
        Resize += (_, _) => Arrange();
        SystemEvents.UserPreferenceChanged += SystemPreferenceChanged;
        FormClosing += HandleClosing;
        _tips.SetToolTip(_timeZone, "使用 IANA 时区；起止日都包含。日界无法唯一转换时会明确报错。");
        _tips.SetToolTip(_startDate, "筛选提交记录的起点，包含当天；留空不限。可手动输入，或点击日历（Alt+↓）。");
        _tips.SetToolTip(_endDate, "筛选提交记录的终点，包含当天；不会裁剪导出的代码。留空不限，Alt+↓打开日历。");
        _tips.SetToolTip(_pageNumber, "输入页码，按 Enter 或点击跳转。翻页不会改变已选提交。");
        _tips.SetToolTip(_keepList, "仅精确相对路径。已跟踪的环境文件、package.json 和锁文件不能作为保留项。");
        _tips.SetToolTip(_lastExport, "上次成功导出的提交；目录可能已有本地修改。");
    }

    private static Label LabelOf(string text, float size, FontStyle style = FontStyle.Regular, string family = "Microsoft YaHei UI") =>
        new() { Text = text, Font = new Font(family, size, style), AutoSize = false, UseMnemonic = false };
    private static TextBox Input(string name, string hint) => new() { BorderStyle = BorderStyle.FixedSingle, PlaceholderText = hint, AccessibleName = name };
    private Button ButtonOf(string text, Action action)
    {
        var button = new ScopeButton { Text = text, Font = new Font("Microsoft YaHei UI", 9), TabStop = true };
        button.FlatAppearance.BorderSize = 1; button.Click += (_, _) => { try { action(); } catch (Exception e) { DisplayError(e); } }; return button;
    }
    private Button AsyncButton(string text, Func<Task> action)
    {
        var button = new ScopeButton { Text = text, Font = new Font("Microsoft YaHei UI", 9), TabStop = true };
        button.FlatAppearance.BorderSize = 1; button.Click += async (_, _) => { try { await action(); } catch (Exception e) { DisplayError(e); } }; return button;
    }
    private void WireEvents()
    {
        _repositoryPath.TextChanged += (_, _) =>
        {
            if (_applyingValues) return;
            CancelQuery(); _repository = null; _target = null; _references.Items.Clear(); ClearHistory("仓库路径已更改，请读取引用。"); UpdateTargetSummary(); Persist();
        };
        _references.SelectedIndexChanged += (_, _) =>
        {
            if (_applyingValues) return;
            CancelQuery(); ClearHistory("引用已更改，请查询历史。"); Persist();
        };
        foreach (var input in new[] { _startDate, _endDate, _timeZone })
        {
            input.TextChanged += (_, _) => { if (!_applyingValues) { InvalidatePlan("查询条件已更改，请重新检查计划。"); Persist(); } };
            input.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await QueryCurrentAsync(); } };
        }
        _firstParent.CheckedChanged += (_, _) => { if (!_applyingValues) { InvalidatePlan(); Persist(); } };
        _targetPath.TextChanged += (_, _) => { if (!_applyingValues) { _target = null; InvalidatePlan(); UpdateTargetSummary(); Persist(); } };
        _keepList.ItemCheck += (_, _) =>
        {
            if (_fillingKeeps) return;
            InvalidatePlan("保留选择已更改，请重新检查计划。");
            if (IsHandleCreated) BeginInvoke(() => Persist());
        };
        _keepPath.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; AddKeepPath(); } };
        _repositoryPath.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await LoadRepositoryAsync(_repositoryPath.Text); } };
        _recoveryItems.SelectedIndexChanged += (_, _) => UpdateRecoveryButton();
        _pageNumber.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; JumpToPage(_pageNumber.Text); } };
        foreach (var date in new[] { _startDate, _endDate })
        {
            date.DoubleClick += (_, _) => ShowCalendar(date, date == _startDate ? _endDate : _startDate);
            date.KeyDown += (_, e) => { if (e.Alt && e.KeyCode == Keys.Down) { e.SuppressKeyPress = true; ShowCalendar(date, date == _startDate ? _endDate : _startDate); } };
        }
    }
    private void AssignTabOrder()
    {
        int i = 0;
        foreach (var control in new Control[] { _repositoryPath, _browseRepository, _references, _loadRepository, _startDate, _chooseStart, _endDate, _chooseEnd, _timeZone, _firstParent, _query, _reset, _listPanel, _previewPanel, _outputPanel, _light, _dark, _system, _recoveryPanel }) control.TabIndex = i++;
        i = 0; foreach (var control in new Control[] { _commits, _firstPage, _previous, _pageNumber, _jumpPage, _next, _lastPage }) control.TabIndex = i++;
        _oid.TabIndex = 0; _copy.TabIndex = 1; _files.TabIndex = 2; _showFiles.TabIndex = 3; _diff.TabIndex = 4;
        i = 0; foreach (var control in new Control[] { _targetPath, _browseTarget, _bindTarget, _keepList, _keepPath, _addKeep, _cancel, _open, _prepare, _export }) control.TabIndex = i++;
        _recoveryItems.TabIndex = 0; _recover.TabIndex = 1;
    }
    private void ConfigureGrids()
    {
        foreach (var grid in new DataGridView[] { _commits, _files })
        {
            grid.AllowUserToAddRows = false; grid.AllowUserToDeleteRows = false; grid.AllowUserToResizeRows = false; grid.ReadOnly = true;
            grid.MultiSelect = false; grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect; grid.RowHeadersVisible = false;
            grid.BorderStyle = BorderStyle.None; grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None; grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            grid.RowTemplate.Height = 39; grid.ColumnHeadersHeight = 31; grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.EnableHeadersVisualStyles = false; grid.AutoGenerateColumns = false; grid.DataError += (_, e) => e.ThrowException = false;
        }
        _commits.ScrollBars = ScrollBars.None; _commits.AccessibleName = "提交列表"; _files.AccessibleName = "改动文件列表，回车查看文本差异";
        _commits.Columns.Add(new DataGridViewTextBoxColumn { Name = "Date", HeaderText = "提交日期", Width = 109, SortMode = DataGridViewColumnSortMode.NotSortable });
        _commits.Columns.Add(new DataGridViewTextBoxColumn { Name = "Subject", HeaderText = "提交说明", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 130, SortMode = DataGridViewColumnSortMode.NotSortable });
        _commits.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", HeaderText = "ID", Width = 90, SortMode = DataGridViewColumnSortMode.NotSortable });
        _commits.SelectionChanged += async (_, _) => { if (!_filling) await SelectCurrentAsync(); };
        _commits.Scroll += (_, _) => UpdateRail(); _commits.WheelRequested += delta => ScrollTo(_commits.FirstDisplayedScrollingRowIndex - Math.Sign(delta) * 3);
        _scroll.ValueChanged += ScrollTo;
        _files.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "变更", Width = 55, SortMode = DataGridViewColumnSortMode.NotSortable });
        _files.Columns.Add(new DataGridViewTextBoxColumn { Name = "Path", HeaderText = "文件 · 双击或回车查看", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 170, SortMode = DataGridViewColumnSortMode.NotSortable });
        _files.Columns.Add(new DataGridViewTextBoxColumn { Name = "Kind", HeaderText = "内容", Width = 85, SortMode = DataGridViewColumnSortMode.NotSortable });
        _files.CellDoubleClick += async (_, e) => { if (e.RowIndex >= 0) await LoadDiffAsync(); };
        _files.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await LoadDiffAsync(); } };
    }
    private void InvalidatePlan(string? message = null)
    {
        _plan = null;
        if (!_actionBusy) _planSummary.Text = message ?? "先检查更新计划，确认本次保留与移除内容。";
        UpdateActions();
    }
    private void UpdateActions()
    {
        if (_export is null) return;
        bool available = !_actionBusy;
        foreach (var control in new Control[] { _repositoryPath, _browseRepository, _loadRepository, _references, _startDate, _endDate, _chooseStart, _chooseEnd, _timeZone, _firstParent, _reset,
            _targetPath, _browseTarget, _keepList, _keepPath, _addKeep, _commits }) control.Enabled = available;
        _query.Enabled = available && _repository is not null; _bindTarget.Enabled = available && _repository is not null && _targetPath.Text.Trim().Length > 0;
        bool recoveryBlocked = _recoveryScanFailed || (_target is not null && _pending.Any(p => p.TargetId == _target.TargetId && !p.CleanupOnly));
        _prepare.Enabled = available && !_queryBusy && _repository is not null && _selected is not null && _target is not null && !recoveryBlocked;
        _export.Enabled = _prepare.Enabled && _plan is not null;
        _export.BackColor = _export.Enabled ? _palette.Accent : _palette.Surface;
        _export.ForeColor = _export.Enabled ? _palette.AccentOn : _palette.Muted;
        _export.FlatAppearance.BorderColor = _export.Enabled ? _palette.Accent : _palette.Border;
        _open.Enabled = available && _target?.LastCommitOid is not null;
        _copy.Enabled = _selected is not null; _cancel.Enabled = _actionBusy || _queryBusy;
        bool canPage = available && !_queryBusy && PageCount > 0;
        _firstPage.Enabled = _previous.Enabled = canPage && _page > 0;
        _lastPage.Enabled = _next.Enabled = canPage && _page + 1 < PageCount;
        _pageNumber.Enabled = _jumpPage.Enabled = canPage;
        _recover.Enabled = available && _recoveryItems.SelectedItem is RecoveryChoice;
        _recoveryItems.Enabled = available;
    }
    private void DisplayError(Exception error)
    {
        _state = "failed"; _status.Text = error is ScopeException scope ? $"{scope.Code} · {scope.Message}" : error.Message;
        _status.ForeColor = _palette.Error;
    }
    private void SetStatus(string state, string text)
    {
        _state = state; _status.Text = text; _status.ForeColor = _palette.Muted;
    }
    private void Persist()
    {
        if (!_initialized || _applyingValues) return;
        _saved = new AppSettings(1, _theme, _repositoryPath.Text, (_references.SelectedItem as ReferenceChoice)?.Reference.Name ?? _saved.Reference,
            _startDate.Text, _endDate.Text, _timeZone.Text, _target?.TargetId ?? "", _targetPath.Text, SelectedPreservationPaths.ToArray(),
            _firstParent.Checked ? HistoryMode.FirstParent : HistoryMode.All);
        try { _store.Save(_saved); if (_settingsError.Length == 0) _settingsNotice.Text = "本地对象导出 · 远程更新将在后续阶段接入"; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ScopeException) { _settingsError = e.Message; _settingsNotice.Text = _settingsError; _tips.SetToolTip(_settingsNotice, _settingsError); }
    }
    private void CopyOid()
    {
        if (_selected is null) return;
        try { Clipboard.SetText(_selected.Oid); SetStatus("ready", "完整提交 ID 已复制。"); }
        catch (ExternalException) { DisplayError(new InvalidOperationException("剪贴板暂时不可用，请重试。")); }
    }
    private void OpenTarget()
    {
        if (_target?.LastCommitOid is null) return;
        if (!Directory.Exists(_target.Path)) { DisplayError(new DirectoryNotFoundException("输出目录已失效，请重新核对目标。")); return; }
        Process.Start(new ProcessStartInfo { FileName = _target.Path, UseShellExecute = true });
    }
    private void RequestCancel()
    {
        _actionCancellation?.Cancel(); _queryCancellation?.Cancel();
        SetStatus("cancelling", _actionBusy ? "已请求取消；目录切换开始后会先完成安全收尾。" : "正在取消查询…");
    }
    private void HandleClosing(object? sender, FormClosingEventArgs e)
    {
        if (_allowClose) return;
        if (_actionBusy)
        {
            e.Cancel = true; _closingAfterAction = true; RequestCancel(); return;
        }
        _queryCancellation?.Cancel(); _previewCancellation?.Cancel(); Persist();
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            SystemEvents.UserPreferenceChanged -= SystemPreferenceChanged;
            _queryCancellation?.Cancel(); _previewCancellation?.Cancel(); _actionCancellation?.Cancel();
            _tips.Dispose();
            _calendar?.Dispose();
        }
        base.Dispose(disposing);
    }
    private sealed record ReferenceChoice(RevisionInfo Reference)
    {
        public override string ToString() => Reference.Name;
    }
    private sealed record RecoveryChoice(RecoveryItem Item)
    {
        public override string ToString() => $"{Item.TargetPath} · {(Item.CleanupOnly ? "待清理" : "待恢复")}";
    }
}
