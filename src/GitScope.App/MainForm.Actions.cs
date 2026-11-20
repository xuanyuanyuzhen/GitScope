using GitScope.Core;
using System.Globalization;

namespace GitScope.App;

public sealed partial class MainForm
{
    private int _diffVersion;

    public async Task InitializeAsync()
    {
        try { await RefreshTargetsAndRecoveryAsync(); }
        catch (Exception e) { _recoveryScanFailed = true; DisplayError(e); }
        if (!string.IsNullOrWhiteSpace(_repositoryPath.Text)) await LoadRepositoryAsync(_repositoryPath.Text);
        UpdateActions();
    }

    private async Task BrowseRepositoryAsync()
    {
        using var dialog = new FolderBrowserDialog { Description = "选择本地 Git 仓库", UseDescriptionForTitle = true, SelectedPath = Directory.Exists(_repositoryPath.Text) ? _repositoryPath.Text : "" };
        if (dialog.ShowDialog(this) == DialogResult.OK) await LoadRepositoryAsync(dialog.SelectedPath);
    }

    public async Task LoadRepositoryAsync(string path)
    {
        if (_actionBusy) return;
        CancelQuery(); var version = _queryVersion; var cancellation = new CancellationTokenSource(); _queryCancellation = cancellation;
        _queryBusy = true; var token = cancellation.Token;
        bool restoringSameRepository = string.Equals(path.Trim().TrimEnd(Path.DirectorySeparatorChar), _saved.RepositoryPath.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
        var wantedReference = restoringSameRepository ? _saved.Reference : ""; var rememberedTargetId = _saved.TargetId; var rememberedKeeps = _saved.KeepPaths;
        _applyingValues = true; _repositoryPath.Text = path; _references.Items.Clear(); _applyingValues = false;
        _repository = null; _target = null; ClearHistory("正在识别仓库和引用…"); UpdateTargetSummary();
        SetStatus("loading", "读取本地仓库和引用…"); UpdateActions();
        try
        {
            var repository = await Task.Run(() => _repositories.InspectAsync(path.Trim(), token), token);
            var references = await Task.Run(() => _repositories.GetReferencesAsync(repository, token), token);
            var targets = await Task.Run(() => _targets.ListAsync(token), token);
            if (version != _queryVersion || IsDisposed) return;
            _repository = repository; _knownTargets = targets;
            _applyingValues = true;
            _repositoryPath.Text = repository.RootPath;
            foreach (var reference in references) _references.Items.Add(new ReferenceChoice(reference));
            int index = references.ToList().FindIndex(r => r.Name == wantedReference);
            bool missingSavedReference = references.Count > 0 && wantedReference.Length > 0 && index < 0;
            _references.SelectedIndex = references.Count == 0 || missingSavedReference ? -1 : Math.Max(0, index);
            _applyingValues = false;
            var remembered = targets.FirstOrDefault(t => t.TargetId == rememberedTargetId && t.RepositoryId == repository.Id);
            if (remembered is not null)
            {
                _target = remembered; _applyingValues = true; _targetPath.Text = remembered.Path;
                SetPreservationPaths(rememberedKeeps ?? remembered.Preservation.Paths); _applyingValues = false;
            }
            UpdateTargetSummary(); Persist();
            if (references.Count == 0)
            {
                ClearHistory("仓库还没有可查询的提交。"); SetStatus("empty", "这是空仓库；产生提交后再读取引用。");
            }
            else if (missingSavedReference)
            {
                ClearHistory("上次引用已失效，请明确选择另一个引用。\n" + wantedReference);
                SetStatus("failed", "上次引用已失效，已保留其记录。请选择引用后查询：" + wantedReference);
            }
            else
            {
                _queryBusy = false;
                await QueryCurrentAsync();
            }
        }
        catch (OperationCanceledException) { if (version == _queryVersion && !IsDisposed) SetStatus("cancelled", "已取消仓库读取。"); }
        catch (Exception e)
        {
            if (version != _queryVersion || IsDisposed) return;
            _repository = null; ClearHistory("暂时无法读取仓库\n" + e.Message); DisplayError(e);
        }
        finally
        {
            if (version == _queryVersion && !IsDisposed) { _queryBusy = false; UpdateActions(); }
        }
    }

    public async Task QueryCurrentAsync()
    {
        if (_actionBusy || _repository is null || _references.SelectedItem is not ReferenceChoice reference) return;
        var repository = _repository;
        var filter = new HistoryFilter(_startDate.Text, _endDate.Text, _timeZone.Text.Trim(), _firstParent.Checked ? HistoryMode.FirstParent : HistoryMode.All);
        CancelQuery(); var version = _queryVersion; _queryCancellation = new(); var token = _queryCancellation.Token; _queryBusy = true;
        ClearHistory("正在遍历本地提交…"); SetStatus("loading", "按冻结引用查询全部本地可达历史…"); UpdateActions();
        try
        {
            DateSemantics.Parse(filter);
            var tip = await Task.Run(() => _repositories.ResolveCommitAsync(repository, reference.Reference.Name, token), token);
            var history = await Task.Run(() => _histories.QueryAsync(repository, tip, filter, Guid.NewGuid().ToString("N"), token), token);
            if (version != _queryVersion || IsDisposed) return;
            _history = history; _displayZone = filter.TimeZoneId; _page = 0;
            _listTitle.Text = $"01  /  提交历史  {history.Items.Count}";
            _listScope.Text = $"{reference.Reference.Name} · {filter.TimeZoneId}";
            _tips.SetToolTip(_listScope, $"冻结提交：{history.TipOid}\n{filter.TimeZoneId} · 按提交者时间\n" + string.Join("\n", history.Warnings));
            ShowPage();
            if (history.Items.Count == 0)
            {
                _empty.Text = history.Coverage == HistoryCoverage.Shallow ? "本机已知历史无匹配\n这是浅仓库，历史范围受限。" : "这段日期没有匹配记录\n调整日期或引用后重试。";
                SetStatus("empty", history.Coverage == HistoryCoverage.Shallow ? "浅仓库：本机已知历史无匹配；没有自动加深历史。" : "查询完成，没有匹配的提交。");
            }
            else
            {
                SetStatus("ready", history.Warnings.Count > 0 ? string.Join("  ", history.Warnings) : $"已查询 {history.Items.Count} 条提交 · 起止日均包含 · 每页 50 条");
                await SelectIndexAsync(0);
            }
            Persist();
        }
        catch (OperationCanceledException) { if (version == _queryVersion && !IsDisposed) { ClearHistory("查询已取消，可重新查询。"); SetStatus("cancelled", "查询已取消。"); } }
        catch (Exception e) { if (version == _queryVersion && !IsDisposed) { ClearHistory("查询未完成\n" + e.Message); DisplayError(e); } }
        finally { if (version == _queryVersion && !IsDisposed) { _queryBusy = false; UpdateActions(); } }
    }

    private void CancelQuery()
    {
        _queryVersion++; _queryCancellation?.Cancel(); _queryBusy = false;
    }

    private void ClearHistory(string message)
    {
        _history = null; _pageItems = []; _selected = null; _page = 0; _previewVersion++; _diffVersion++;
        _previewCancellation?.Cancel(); _filling = true; _commits.Rows.Clear(); _filling = false; _files.Rows.Clear(); _changedFiles = [];
        _commits.Visible = false; _empty.Visible = true; _empty.Text = message; _scroll.Visible = false;
        _listTitle.Text = "01  /  提交历史"; _listScope.Text = ""; _pageInfo.Text = "—"; _pageNumber.Clear();
        _detailTitle.Text = "选择一条提交查看变化"; _detailMeta.Text = ""; _oid.Text = ""; _comparison.Text = "";
        _dependency.Text = "依赖声明变化将在选定目标后提示。"; ShowFiles(); InvalidatePlan();
    }

    public void SetDates(string start, string end, string? timeZone = null)
    {
        _applyingValues = true; _startDate.Text = start; _endDate.Text = end; if (timeZone is not null) _timeZone.Text = timeZone; _applyingValues = false;
        InvalidatePlan(); Persist();
    }

    public void NavigatePage(int page)
    {
        if (_history is null || _actionBusy || _queryBusy || page < 0 || page >= PageCount) return;
        _page = page; ShowPage(); SetStatus(_state, $"已跳至第 {_page + 1} / {PageCount} 页；所选提交保持不变。");
    }

    public bool JumpToPage(string value)
    {
        if (_actionBusy || _queryBusy || PageCount == 0) return false;
        if (!int.TryParse(value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int page) || page < 1 || page > PageCount)
        {
            SetStatus(_state, $"页码请输入 1–{PageCount} 的整数；当前仍在第 {_page + 1} 页。");
            _pageNumber.Focus(); _pageNumber.SelectAll(); return false;
        }
        NavigatePage(page - 1); return true;
    }

    private void ShowCalendar(TextBox input, TextBox other)
    {
        if (_actionBusy) return;
        _calendar?.Close();
        static DateTime? Parse(string text) => DateTime.TryParseExact(text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;
        var selected = Parse(input.Text); DateTime today;
        try { today = TimeZoneInfo.ConvertTime(DateTimeOffset.Now, DateSemantics.GetTimeZone(_timeZone.Text.Trim())).Date; }
        catch (ScopeException) { today = DateTime.Today; }
        _calendar = new DateCalendar(input == _startDate ? "选择开始日期" : "选择结束日期", selected, selected ?? Parse(other.Text) ?? today, today, _palette,
            date => input.Text = date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "") { Opacity = Opacity };
        _calendar.FormClosed += (_, _) => { if (!IsDisposed) input.Focus(); };
        var anchor = input.PointToScreen(new Point(0, input.Height + D(5)));
        _calendar.Show(this);
        var area = Screen.FromControl(input).WorkingArea;
        _calendar.Location = new Point(Math.Clamp(anchor.X, area.Left, Math.Max(area.Left, area.Right - _calendar.Width)),
            anchor.Y + _calendar.Height <= area.Bottom ? anchor.Y : Math.Max(area.Top, input.PointToScreen(Point.Empty).Y - _calendar.Height));
    }

    private void ShowPage()
    {
        if (_history is null) return;
        _pageItems = _history.Page(_page); _filling = true; _commits.Rows.Clear();
        var zone = DateSemantics.GetTimeZone(_displayZone);
        foreach (var item in _pageItems)
        {
            var time = TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeSeconds(item.CommitterTime), zone);
            _commits.Rows.Add(time.ToString("yyyy-MM-dd"), item.Subject, Short(item.Oid));
        }
        _commits.ClearSelection(); _commits.CurrentCell = null;
        int selectedIndex = _pageItems.ToList().FindIndex(c => c.Oid == _selected?.Oid);
        if (selectedIndex >= 0) { _commits.CurrentCell = _commits.Rows[selectedIndex].Cells[1]; _commits.Rows[selectedIndex].Selected = true; }
        _filling = false; _empty.Visible = _pageItems.Count == 0; _commits.Visible = !_empty.Visible;
        _pageInfo.Text = _history.Items.Count == 0 ? "0 条记录" : $"第 {_page + 1} / {(_history.Items.Count + 49) / 50} 页";
        _pageNumber.Text = PageCount == 0 ? "" : (_page + 1).ToString(CultureInfo.InvariantCulture);
        UpdateRail(); UpdateActions();
    }

    public async Task SelectIndexAsync(int index)
    {
        if (index < 0 || index >= _pageItems.Count || _actionBusy) return;
        _filling = true; _commits.ClearSelection(); _commits.CurrentCell = _commits.Rows[index].Cells[1]; _commits.Rows[index].Selected = true; _filling = false;
        await SelectCurrentAsync();
    }

    private async Task SelectCurrentAsync()
    {
        int index = _commits.CurrentRow?.Index ?? -1;
        if (index < 0 || index >= _pageItems.Count || _repository is null || _actionBusy) return;
        _selected = _pageItems[index]; var selected = _selected; var repository = _repository;
        _previewVersion++; _diffVersion++; int version = _previewVersion;
        _previewCancellation?.Cancel(); _previewCancellation = new(); var token = _previewCancellation.Token;
        InvalidatePlan(); ShowFiles(); _files.Rows.Clear(); _changedFiles = [];
        _detailTitle.Text = selected.Subject; _oid.Text = selected.Oid; _tips.SetToolTip(_detailTitle, selected.Subject);
        var zone = DateSemantics.GetTimeZone(_displayZone);
        string Format(long time) => TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeSeconds(time), zone).ToString("yyyy-MM-dd HH:mm:ss zzz");
        _detailMeta.Text = $"{selected.AuthorName}  ·  {Format(selected.CommitterTime)}";
        _tips.SetToolTip(_detailMeta, $"提交者时间：{Format(selected.CommitterTime)}\n作者时间：{Format(selected.AuthorTime)}\n时区：{_displayZone}");
        _comparison.Text = "正在读取变更文件…"; _dependency.Text = "正在比较依赖声明…";
        try
        {
            var preview = await Task.Run(() => _histories.PreviewAsync(repository, selected.Oid, token), token);
            if (version != _previewVersion || IsDisposed) return;
            _changedFiles = preview.Files; _comparison.Text = $"{preview.Comparison} · {preview.Files.Count} 个文件";
            foreach (var file in preview.Files)
            {
                int row = _files.Rows.Add(file.Status, file.Path, file.IsBinary ? "二进制" : "文本");
                _files.Rows[row].Cells[2].ToolTipText = $"原大小：{file.OldSize?.ToString() ?? "无"} B；新大小：{file.NewSize?.ToString() ?? "无"} B";
            }
            await UpdateDependencyAsync(version, token);
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            if (version != _previewVersion || IsDisposed) return;
            _comparison.Text = "预览失败：" + e.Message; _dependency.Text = "依赖状态未知，请自行检查。";
        }
        finally { if (!IsDisposed) UpdateActions(); }
    }

    private async Task UpdateDependencyAsync(int version, CancellationToken token)
    {
        if (_repository is null || _selected is null) return;
        var targetId = _target?.TargetId; var previous = _target?.LastCommitOid; var repository = _repository; var selected = _selected.Oid;
        var notice = await Task.Run(() => _histories.DependencyNoticeAsync(repository, previous, selected, token), token);
        if (version != _previewVersion || IsDisposed || _target?.TargetId != targetId || _target?.LastCommitOid != previous) return;
        _dependency.Text = notice.Message; _tips.SetToolTip(_dependency, notice.Message + (notice.ChangedPaths.Count == 0 ? "" : "\n" + string.Join("\n", notice.ChangedPaths)));
    }

    private async Task LoadDiffAsync()
    {
        int index = _files.CurrentRow?.Index ?? -1;
        if (_repository is null || _selected is null || index < 0 || index >= _changedFiles.Count) return;
        var file = _changedFiles[index]; var repository = _repository; var selected = _selected.Oid;
        int version = ++_diffVersion; int previewVersion = _previewVersion;
        _diff.Visible = true; _files.Visible = false; _showFiles.Visible = true;
        if (file.IsBinary)
        {
            _diff.Text = $"二进制文件：{file.Path}\r\n原大小：{file.OldSize?.ToString() ?? "无"} B\r\n新大小：{file.NewSize?.ToString() ?? "无"} B\r\n\r\n二进制内容不作为文本显示。"; return;
        }
        _diff.Text = "读取文本差异…";
        try
        {
            var diff = await Task.Run(() => _histories.DiffAsync(repository, selected, file.Path, _previewCancellation?.Token ?? CancellationToken.None));
            if (version == _diffVersion && previewVersion == _previewVersion && !IsDisposed) _diff.Text = diff;
        }
        catch (OperationCanceledException) { }
        catch (Exception e) { if (version == _diffVersion && !IsDisposed) _diff.Text = "差异读取失败：" + e.Message; }
    }

    private void ShowFiles() { _diffVersion++; _diff.Visible = false; _files.Visible = true; _showFiles.Visible = false; _diff.Clear(); }
    private static string Short(string oid) => oid[..Math.Min(9, oid.Length)];

    private void UpdateRail()
    {
        if (_commits.Height <= 0) return;
        int page = Math.Max(1, (_commits.Height - _commits.ColumnHeadersHeight) / Math.Max(1, _commits.RowTemplate.Height));
        _scroll.UpdateRange(_commits.Visible ? _commits.Rows.Count : 0, page, Math.Max(0, _commits.FirstDisplayedScrollingRowIndex));
    }
    private void ScrollTo(int row)
    {
        if (_commits.Rows.Count == 0) return;
        int page = Math.Max(1, (_commits.Height - _commits.ColumnHeadersHeight) / _commits.RowTemplate.Height);
        _commits.FirstDisplayedScrollingRowIndex = Math.Clamp(row, 0, Math.Max(0, _commits.Rows.Count - page)); UpdateRail();
    }

    private void BrowseTarget()
    {
        using var dialog = new FolderBrowserDialog { Description = "选择固定输出目录；首次请使用空专用目录", UseDescriptionForTitle = true, SelectedPath = Directory.Exists(_targetPath.Text) ? _targetPath.Text : "" };
        if (dialog.ShowDialog(this) == DialogResult.OK) _targetPath.Text = dialog.SelectedPath;
    }

    public async Task BindTargetAsync(string path)
    {
        if (_actionBusy || _repository is null) return;
        var repository = _repository; StartAction("binding", "校验输出目录身份与归属…");
        try
        {
            var target = await Task.Run(() => _targets.RegisterAsync(repository, path.Trim(), _actionCancellation!.Token));
            _target = target; _applyingValues = true; _targetPath.Text = target.Path; SetPreservationPaths(target.Preservation.Paths); _applyingValues = false;
            InvalidatePlan(); UpdateTargetSummary(); Persist(); SetStatus("ready", "目标已绑定。选中保留项后检查更新计划。");
            if (_selected is not null) await UpdateDependencyAsync(_previewVersion, CancellationToken.None);
        }
        catch (OperationCanceledException) { SetStatus("cancelled", "目录绑定已取消。"); }
        catch (Exception e) { _target = null; InvalidatePlan(); UpdateTargetSummary(); DisplayError(e); }
        finally { FinishAction(); }
    }

    public void SetPreservationPaths(IEnumerable<string> paths)
    {
        _fillingKeeps = true;
        var selected = paths.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        _keepList.Items.Clear();
        foreach (var path in new[] { "node_modules", ".env" }.Concat(selected).Distinct(StringComparer.OrdinalIgnoreCase))
            _keepList.Items.Add(path, selected.Contains(path, StringComparer.OrdinalIgnoreCase));
        _fillingKeeps = false; InvalidatePlan();
        if (!_applyingValues) Persist();
    }

    private void AddKeepPath()
    {
        var path = _keepPath.Text.Trim(); if (path.Length == 0) return;
        // Backend owns normalization and security validation; the UI keeps the user's exact input for an actionable preflight error.
        int index = _keepList.Items.Cast<string>().ToList().FindIndex(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        if (index >= 0) _keepList.SetItemChecked(index, true); else _keepList.Items.Add(path, true);
        _keepPath.Clear(); InvalidatePlan(); Persist();
    }

    public async Task PrepareExportAsync()
    {
        if (!_prepare.Enabled || _repository is null || _selected is null || _target is null) return;
        var repository = _repository; var commit = _selected.Oid; var target = _target; var keeps = SelectedPreservationPaths;
        StartAction("planning", "检查目标、Git 树和保留项冲突…"); _plan = null;
        try
        {
            var plan = await Task.Run(() => _targets.PlanAsync(repository, commit, target.TargetId, keeps, Guid.NewGuid().ToString("N"), _actionCancellation!.Token));
            SetPreservationPaths(plan.Preservation.Paths); _plan = plan;
            _target = plan.Target with { Preservation = plan.Preservation };
            int present = plan.Roots.Count(r => r.Exists); int missing = plan.Roots.Count - present;
            string preserve = present == 0 ? "不保留本地内容" : $"保留 {present} 项：" + string.Join("、", plan.Roots.Where(r => r.Exists).Take(3).Select(r => r.RelativePath)) + (present > 3 ? "…" : "");
            _planSummary.Text = $"就绪 · {Short(commit)} · {plan.Entries.Count} 个跟踪文件\n{preserve}{(missing > 0 ? $"；{missing} 项不存在，将跳过" : "")}\n未选择保留的旧文件、修改和目录将被移除。";
            _tips.SetToolTip(_planSummary, _planSummary.Text + "\n目标：" + plan.Target.Path + "\n保留根：" + string.Join("、", plan.Roots.Select(r => r.RelativePath)) + "\n" + string.Join("\n", plan.Warnings));
            Persist(); SetStatus("planned", plan.Warnings.Count > 0 ? string.Join("  ", plan.Warnings) : "检查通过。确认上方目录和保留清单后，点击“导出到此目录”。");
        }
        catch (OperationCanceledException) { _planSummary.Text = "检查已取消，输出目录尚未更新。"; SetStatus("cancelled", "已取消计划检查。"); }
        catch (Exception e) { _planSummary.Text = "检查未通过：" + e.Message; DisplayError(e); }
        finally { FinishAction(); }
    }

    public async Task ExecutePlanAsync()
    {
        if (!_export.Enabled || _plan is null) return;
        var plan = _plan; StartAction("exporting", "正在准备完整代码…"); _activeRequestId = plan.RequestId;
        var progress = new Progress<OperationProgress>(p =>
        {
            if (IsDisposed || p.RequestId != _activeRequestId) return;
            string count = p.Completed.HasValue && p.Total.HasValue ? $" ({p.Completed}/{p.Total})" : "";
            _status.Text = p.Message + count; _planSummary.Text = StageCaption(p.Stage) + "\n" + p.Message + count;
        });
        try
        {
            var result = await Task.Run(() => _targets.ExecuteAsync(plan, progress, _actionCancellation!.Token));
            _plan = null; ApplyResult(result);
            if (result.Outcome == OperationOutcome.Success && _target is not null && result.CommitOid is not null)
                _target = _target with { LastCommitOid = result.CommitOid };
            try
            {
                await RefreshTargetsAndRecoveryAsync(); UpdateTargetSummary();
                if (_selected is not null) await UpdateDependencyAsync(_previewVersion, CancellationToken.None);
            }
            catch (Exception e)
            {
                _recoveryScanFailed = true;
                _status.Text = result.Message + " 后续状态刷新失败，请重启核对：" + e.Message;
            }
        }
        catch (Exception e)
        {
            _plan = null; DisplayError(e); _planSummary.Text = "操作未完成，请检查恢复状态。";
            try { await RefreshTargetsAndRecoveryAsync(); } catch { _recoveryScanFailed = true; }
        }
        finally { _activeRequestId = null; FinishAction(); Persist(); }
    }

    private static string StageCaption(string stage) => stage.ToUpperInvariant() switch
    {
        "VALIDATING" => "正在校验更新条件", "STAGING" => "正在准备新版本", "VERIFIED" => "新版本校验完成",
        "OLD_MOVED" => "正在安全替换目录", "PRESERVING" => "正在保留环境与依赖", "NEW_PROMOTED" => "正在核对更新结果",
        "COMMITTED" => "代码已更新", "COMMITTED_CLEANUP_PENDING" => "代码已更新，旧备份待清理", "CLEANED" => "更新完成",
        _ => "正在完成目录操作"
    };

    private void ApplyResult(ExportResult result)
    {
        string message = result.Message + (result.CleanupPending ? " 代码已更新，旧备份可从恢复入口继续清理。" : "");
        switch (result.Outcome)
        {
            case OperationOutcome.Success: SetStatus("success", message); _planSummary.Text = "操作成功\n" + message; break;
            case OperationOutcome.Cancelled: SetStatus("cancelled", message); _planSummary.Text = "已取消\n" + message; break;
            case OperationOutcome.RecoveryRequired: SetStatus("recoveryRequired", message); _planSummary.Text = "需要恢复\n" + message; _status.ForeColor = _palette.Error; break;
            default: SetStatus("failed", message); _planSummary.Text = "更新未完成\n" + message; _status.ForeColor = _palette.Error; break;
        }
        UpdateTargetSummary();
    }

    private async Task RefreshTargetsAndRecoveryAsync()
    {
        var targetTask = Task.Run(() => _targets.ListAsync()); var pendingTask = Task.Run(() => _targets.PendingAsync());
        await Task.WhenAll(targetTask, pendingTask);
        if (IsDisposed) return;
        _knownTargets = targetTask.Result; _pending = pendingTask.Result; _recoveryScanFailed = false;
        if (_target is not null) _target = _knownTargets.FirstOrDefault(t => t.TargetId == _target.TargetId);
        _recoveryItems.Items.Clear(); foreach (var pending in _pending) _recoveryItems.Items.Add(new RecoveryChoice(pending));
        if (_pending.Count > 0) _recoveryItems.SelectedIndex = 0;
        _recoveryPanel.Visible = _pending.Count > 0; UpdateRecoveryButton(); UpdateTargetSummary(); Arrange(); UpdateActions();
    }

    private void UpdateRecoveryButton()
    {
        if (_recover is null) return;
        _recover.Text = _recoveryItems.SelectedItem is RecoveryChoice { Item.CleanupOnly: true } ? "继续清理" : "恢复旧目录";
    }

    public async Task RecoverSelectedAsync()
    {
        if (_actionBusy || _recoveryItems.SelectedItem is not RecoveryChoice choice) return;
        StartAction("recovering", "根据事务记录核对目录与保留项身份…");
        var progress = new Progress<OperationProgress>(p => { if (!IsDisposed && _actionBusy) _status.Text = p.Message; });
        try
        {
            var result = await Task.Run(() => _targets.RecoverAsync(choice.Item.TargetId, progress, _actionCancellation!.Token));
            _plan = null; ApplyResult(result);
            try { await RefreshTargetsAndRecoveryAsync(); UpdateTargetSummary(); }
            catch (Exception e) { _recoveryScanFailed = true; _status.Text = result.Message + " 后续状态刷新失败，请重启核对：" + e.Message; }
        }
        catch (OperationCanceledException) { SetStatus("cancelled", "恢复请求已取消，未决事务仍可从上方入口继续处理。"); }
        catch (Exception e) { DisplayError(e); _recoveryScanFailed = true; }
        finally { FinishAction(); Persist(); }
    }

    private void UpdateTargetSummary()
    {
        _lastExport.Text = _target is null ? "上次导出：尚未绑定目标" : _target.LastCommitOid is null ? "上次导出：尚未导出" : $"上次导出：{Short(_target.LastCommitOid)} · 目录可能已有本地修改";
        if (_target is not null) _tips.SetToolTip(_lastExport, $"{_target.Path}\n上次成功提交：{_target.LastCommitOid ?? "无"}\n目录可能已有本地修改。");
        UpdateActions();
    }

    private void StartAction(string state, string text)
    {
        CancelQuery(); _actionBusy = true; _actionCancellation = new(); SetStatus(state, text); UpdateActions();
    }
    private void FinishAction()
    {
        _actionBusy = false; _actionCancellation?.Dispose(); _actionCancellation = null; UpdateActions();
        if (_closingAfterAction && !IsDisposed) { _allowClose = true; Persist(); BeginInvoke(Close); }
    }
}
