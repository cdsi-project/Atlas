using CDSI.Agent.Core.Assets;
using CDSI.Agent.Core.Transfers;

namespace CDSI.Agent.WinForms;

internal sealed class AssetTransferConfirmationForm : Form
{
    public AssetTransferConfirmationForm(
        ManagedAssetTransferAction action,
        IReadOnlyCollection<AssetListItem> assets)
    {
        ArgumentNullException.ThrowIfNull(assets);
        var isMove = action == ManagedAssetTransferAction.Move;

        Text = isMove
            ? "确认移动到 CDSI 工作目录"
            : "确认复制到 CDSI 工作目录";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(620, 380);
        Size = new Size(760, 480);
        ShowInTaskbar = false;
        Font = new Font("Segoe UI", 9F);
        AutoScaleMode = AutoScaleMode.Dpi;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(20),
            BackColor = Color.White
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, isMove ? 58 : 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));

        layout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = isMove
                ? $"将 {assets.Count:N0} 个文件移动到 CDSI 工作目录"
                : $"将 {assets.Count:N0} 个文件复制到 CDSI 工作目录",
            Font = new Font("Segoe UI Semibold", 13F),
            ForeColor = Color.FromArgb(31, 37, 43),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        layout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = isMove
                ? "目标副本通过大小和 SHA-256 校验后，才会删除下列源文件。失败时源文件保持不变。"
                : "不会覆盖工作目录中内容不同的已有文件，源文件保持不变。",
            ForeColor = isMove
                ? Color.FromArgb(137, 49, 49)
                : Color.FromArgb(72, 81, 89),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 1);

        var paths = new ListBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            SelectionMode = SelectionMode.None,
            HorizontalScrollbar = true,
            IntegralHeight = false,
            AccessibleName = "将操作的文件"
        };
        paths.Items.AddRange(assets.Select(asset => asset.Path).Cast<object>().ToArray());
        layout.Controls.Add(paths, 0, 2);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 12, 0, 0)
        };
        var confirmButton = new Button
        {
            Text = isMove ? "确认移动" : "确认复制",
            DialogResult = DialogResult.OK,
            Size = new Size(104, 32),
            BackColor = isMove
                ? Color.FromArgb(137, 49, 49)
                : Color.FromArgb(24, 121, 78),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        confirmButton.FlatAppearance.BorderSize = 0;
        var cancelButton = new Button
        {
            Text = "取消",
            DialogResult = DialogResult.Cancel,
            Size = new Size(88, 32),
            Margin = new Padding(8, 0, 0, 0)
        };
        buttons.Controls.Add(confirmButton);
        buttons.Controls.Add(cancelButton);
        layout.Controls.Add(buttons, 0, 3);

        AcceptButton = confirmButton;
        CancelButton = cancelButton;
        Controls.Add(layout);
    }
}
