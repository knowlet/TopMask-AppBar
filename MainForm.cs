using System.Drawing;

namespace TopMaskAppBar;

internal sealed class MainForm : Form
{
    private readonly ComboBox _displayList = new();
    private readonly NumericUpDown _heightInput = new();
    private readonly Button _applyButton = new();
    private readonly Button _stopButton = new();
    private readonly Label _status = new();
    private readonly AppBarOverlay _overlay = new();
    private bool _loadingDisplays;

    public MainForm()
    {
        Text = "TopMask AppBar — 螢幕上方遮罩";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(560, 330);
        Size = new Size(680, 390);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 10F);

        BuildLayout();
        LoadDisplays();
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == NativeMethods.WmDisplayChange && !IsDisposed)
        {
            _overlay.StopMask();
            _applyButton.Text = "啟用黑色遮罩";
            _stopButton.Enabled = false;
            _status.Text = "顯示器設定已變更；遮罩已停止，正在更新螢幕清單。";
            _status.ForeColor = Color.DarkOrange;
            BeginInvoke((Action)(() => LoadDisplays()));
        }

        base.WndProc(ref message);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _overlay.StopMask();
        _overlay.Dispose();
        base.OnFormClosing(e);
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(22),
            ColumnCount = 1,
            RowCount = 6,
            BackColor = SystemColors.Control
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        root.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "以黑色 AppBar 遮住螢幕上緣",
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 8)
        }, 0, 0);

        root.Controls.Add(new Label
        {
            AutoSize = true,
            MaximumSize = new Size(610, 0),
            Text = "選擇要遮住的螢幕並設定實體像素高度。啟用後，Windows 會從該螢幕的可用工作區扣除遮罩高度。按「停止」或關閉本程式即可解除。",
            Margin = new Padding(0, 0, 0, 18)
        }, 0, 1);

        var displayRow = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Margin = new Padding(0, 0, 0, 14) };
        displayRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
        displayRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        displayRow.Controls.Add(new Label { Text = "目標螢幕", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 8, 0) }, 0, 0);
        _displayList.DropDownStyle = ComboBoxStyle.DropDownList;
        _displayList.Dock = DockStyle.Top;
        _displayList.SelectedIndexChanged += (_, _) =>
        {
            if (!_loadingDisplays && _overlay.IsMaskActive)
                ApplySettings();
            UpdateHeightLimit();
        };
        displayRow.Controls.Add(_displayList, 1, 0);
        root.Controls.Add(displayRow, 0, 2);

        var heightRow = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3, Margin = new Padding(0, 0, 0, 18) };
        heightRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
        heightRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
        heightRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        heightRow.Controls.Add(new Label { Text = "遮罩高度", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 8, 0) }, 0, 0);
        _heightInput.Minimum = 1;
        _heightInput.Maximum = 6000;
        _heightInput.Value = 80;
        _heightInput.Width = 108;
        _heightInput.ValueChanged += (_, _) =>
        {
            if (!_loadingDisplays && _overlay.IsMaskActive)
                ApplySettings();
        };
        heightRow.Controls.Add(_heightInput, 1, 0);
        heightRow.Controls.Add(new Label { Text = "實體像素 (px)", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(8, 6, 0, 0) }, 2, 0);
        root.Controls.Add(heightRow, 0, 3);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 0, 0, 12) };
        _applyButton.Text = "啟用黑色遮罩";
        _applyButton.AutoSize = true;
        _applyButton.Padding = new Padding(8, 3, 8, 3);
        _applyButton.Click += (_, _) => ApplySettings();
        _stopButton.Text = "停止並還原工作區";
        _stopButton.AutoSize = true;
        _stopButton.Padding = new Padding(8, 3, 8, 3);
        _stopButton.Enabled = false;
        _stopButton.Click += (_, _) => StopMask("遮罩已停止，Windows 工作區已還原。");
        buttons.Controls.Add(_applyButton);
        buttons.Controls.Add(_stopButton);
        root.Controls.Add(buttons, 0, 4);

        _status.AutoSize = true;
        _status.MaximumSize = new Size(610, 0);
        _status.Text = "正在讀取螢幕清單…";
        _status.Margin = new Padding(0, 2, 0, 0);
        root.Controls.Add(_status, 0, 5);
    }

    private void LoadDisplays()
    {
        try
        {
            _loadingDisplays = true;
            var displays = DisplayCatalog.GetDisplays();
            _displayList.Items.Clear();
            foreach (var display in displays)
                _displayList.Items.Add(display);

            if (displays.Count == 0)
            {
                _status.Text = "目前找不到可用的螢幕。請確認 Windows 已偵測到顯示器。";
                _status.ForeColor = Color.Firebrick;
                _applyButton.Enabled = false;
                return;
            }

            var selection = displays.FirstOrDefault(display => display.IsInternal)
                ?? displays.FirstOrDefault(display => display.IsPrimary)
                ?? displays[0];
            _displayList.SelectedItem = selection;
            _applyButton.Enabled = true;
            UpdateHeightLimit();

            if (displays.Any(display => display.IsInternal))
            {
                _status.Text = "已辨識並預選內建螢幕。變更高度或螢幕時，作用中的遮罩會即時更新。";
                _status.ForeColor = SystemColors.ControlText;
            }
            else
            {
                _status.Text = "Windows 未回報內建面板連線類型；請依名稱與解析度從清單選擇筆電螢幕。";
                _status.ForeColor = Color.DarkOrange;
            }
        }
        catch (Exception exception)
        {
            _status.Text = $"讀取螢幕清單失敗：{exception.Message}";
            _status.ForeColor = Color.Firebrick;
            _applyButton.Enabled = false;
        }
        finally
        {
            _loadingDisplays = false;
        }
    }

    private void UpdateHeightLimit()
    {
        if (_displayList.SelectedItem is not DisplayTarget display)
            return;

        var maximum = Math.Max(1, Math.Min(6000, display.Bounds.Height - 1));
        _heightInput.Maximum = maximum;
        if (_heightInput.Value > maximum)
            _heightInput.Value = maximum;
    }

    private void ApplySettings()
    {
        if (_displayList.SelectedItem is not DisplayTarget display)
        {
            _status.Text = "請先選擇一個螢幕。";
            _status.ForeColor = Color.Firebrick;
            return;
        }

        var height = decimal.ToInt32(_heightInput.Value);
        if (!_overlay.StartOrUpdate(display, height, out var error))
        {
            _status.Text = error;
            _status.ForeColor = Color.Firebrick;
            _applyButton.Text = "啟用黑色遮罩";
            _stopButton.Enabled = false;
            return;
        }

        _status.Text = $"遮罩作用中：{display.FriendlyName}，上方 {height} px 已從可用工作區保留。關閉本程式會移除 AppBar 並還原工作區。";
        _status.ForeColor = Color.DarkGreen;
        _applyButton.Text = "套用目前設定";
        _stopButton.Enabled = true;
    }

    private void StopMask(string message)
    {
        _overlay.StopMask();
        _applyButton.Text = "啟用黑色遮罩";
        _stopButton.Enabled = false;
        _status.Text = message;
        _status.ForeColor = SystemColors.ControlText;
    }
}
