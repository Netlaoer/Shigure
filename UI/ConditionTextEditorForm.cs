using System.Drawing;

namespace Shigure;

/// <summary>直接编辑主条件表达式，保留可视化表格无法完整表示的文本。</summary>
internal sealed class ConditionTextEditorForm : Form
{
    private readonly TextBox _conditionBox = new();

    public string ConditionText { get; private set; } = string.Empty;

    public ConditionTextEditorForm(string condition)
    {
        Text = "高级编辑条件";
        Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = UiTheme.Surface;
        ForeColor = UiTheme.Text;
        UiTheme.ConfigureResizableDialog(this, 760, 320, 540, 240);
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.Surface,
            Padding = new Padding(UiTheme.CardPadding, 12, UiTheme.CardPadding, 12),
            ColumnCount = 1,
            RowCount = 2
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        Controls.Add(root);

        var editorCard = new UiCardPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(UiTheme.CardPadding),
            Margin = new Padding(0, 0, 0, UiTheme.PageGap),
            ColumnCount = 1,
            RowCount = 3
        };
        editorCard.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        editorCard.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        editorCard.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        editorCard.Controls.Add(UiTheme.CreateSectionTitle(Font, "主条件表达式"), 0, 0);
        editorCard.Controls.Add(new Label
        {
            Text = "谨慎使用",
            Dock = DockStyle.Fill,
            ForeColor = UiTheme.Muted,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = Padding.Empty
        }, 0, 1);

        UiTheme.StyleTextBox(_conditionBox);
        _conditionBox.Dock = DockStyle.Fill;
        _conditionBox.Multiline = true;
        _conditionBox.AcceptsReturn = true;
        _conditionBox.WordWrap = true;
        _conditionBox.ScrollBars = ScrollBars.Vertical;
        _conditionBox.Text = condition;
        _conditionBox.AccessibleName = "主条件表达式";
        editorCard.Controls.Add(_conditionBox, 0, 2);
        root.Controls.Add(editorCard, 0, 0);

        var actionCard = new UiCardPanel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = new Padding(UiTheme.CardPadding, 10, UiTheme.CardPadding, 10),
            ColumnCount = 2,
            RowCount = 1
        };
        actionCard.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        actionCard.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 184));
        actionCard.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        var saveButton = UiTheme.CreateButton("保存", UiTheme.ButtonKind.Primary);
        UiTheme.StyleActionButton(saveButton, 80);
        saveButton.Margin = new Padding(8, 0, 0, 0);
        saveButton.Click += (_, _) =>
        {
            ConditionText = _conditionBox.Text.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ').Trim();
            DialogResult = DialogResult.OK;
        };
        var cancelButton = UiTheme.CreateButton("取消", UiTheme.ButtonKind.Secondary);
        UiTheme.StyleActionButton(cancelButton, 80);
        cancelButton.Margin = new Padding(8, 0, 0, 0);
        cancelButton.Click += (_, _) => DialogResult = DialogResult.Cancel;
        buttons.Controls.Add(saveButton);
        buttons.Controls.Add(cancelButton);
        actionCard.Controls.Add(buttons, 1, 0);
        root.Controls.Add(actionCard, 0, 1);

        AcceptButton = saveButton;
        CancelButton = cancelButton;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        UiTheme.ApplyDarkTitleBar(this);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _conditionBox.Focus();
    }
}
