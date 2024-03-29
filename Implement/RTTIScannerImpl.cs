using EnvDTE;
using EnvDTE80;
using RTTIScanner.ClassExtensions;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;


namespace RTTIScanner.Commands
{
    [Command(PackageIds.Window)]
    internal sealed class RTTIScannerImpl : BaseCommand<RTTIScannerImpl>
    {
        public static RTTIScannerImpl Instance { get; private set; }
        private TextBox AddressInputBox;
        private TextBox RTTIShowBox;
        private Form toolWindow;


        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await Package.JoinableTaskFactory.SwitchToMainThreadAsync();

            RemoteProcess.Instance = new RemoteProcess((DTE2)Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(DTE)));

            AddressInputBox = new TextBox();
            AddressInputBox.Multiline = false;
            AddressInputBox.Dock = DockStyle.Fill;
            AddressInputBox.Padding = new Padding(10); // 添加一些内边距
            AddressInputBox.Font = new Font("Consolas", 12, FontStyle.Regular); // 设置字体
            AddressInputBox.KeyPress += OnKeyPress_Enter;

            RTTIShowBox = new TextBox();
            RTTIShowBox.Multiline = true;
            RTTIShowBox.ReadOnly = true;
            RTTIShowBox.Dock = DockStyle.Fill;
            RTTIShowBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            RTTIShowBox.Font = new Font("Consolas", 12, FontStyle.Regular); // 设置字体
            RTTIShowBox.TextChanged += RTTIShowBox_TextChanged;

            var tableLayoutPanel = new TableLayoutPanel();
            tableLayoutPanel.RowCount = 2;
            tableLayoutPanel.ColumnCount = 1;
            tableLayoutPanel.Dock = DockStyle.Fill;
            tableLayoutPanel.Controls.Add(AddressInputBox, 0, 0);
            tableLayoutPanel.Controls.Add(RTTIShowBox, 0, 1);

            toolWindow = new Form();
            toolWindow.Text = "RTTI Scanner";
            string projectName = Assembly.GetExecutingAssembly().GetName().Name.ToString();
            toolWindow.Icon = new Icon(Assembly.GetExecutingAssembly().GetManifestResourceStream(projectName + ".Resources" + ".ReClass.ico"));
            toolWindow.Controls.Add(tableLayoutPanel);
            toolWindow.TopMost = true;

            toolWindow.Show();
        }

        private async void OnKeyPress_Enter(object sender, KeyPressEventArgs e)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (e.KeyChar == (char)Keys.Enter)
            {
                // 禁用系统的默认回车键行为, 如 取消提示音
                e.Handled = true;

                string context = AddressInputBox.Text;
                if (context.Length == 0)
                {
                    return;
                }

                if (RemoteProcess.Instance.debugger.CurrentMode == dbgDebugMode.dbgDesignMode)
                {
                    await VS.MessageBox.ShowWarningAsync("调试器没有启动!");
                    return;
                }

                IntPtr pointer = await RemoteProcess.ParseAddressAsync(context);
                if (!pointer.MayBeValid())
                {
                    await VS.MessageBox.ShowWarningAsync("指针不合法!");
                    return;
                }

                try
                {
                    byte[] data = await RemoteProcess.Instance.ReadRemoteMemoryAsync(pointer, 8);
                    IntPtr remotePtr = (IntPtr)BitConverter.ToInt64(data, 0);
                    string rtti = await RTTIParser.ReadRemoteRuntimeTypeInformationAsync(remotePtr);
                    if (string.IsNullOrEmpty(rtti))
                    {
                        await VS.MessageBox.ShowWarningAsync("获取RTTI信息失败!");
                        return;
                    }

                    string[] rttis = rtti.Split(separator: new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                    RTTIShowBox.Clear();
                    foreach (string className in rttis)
                    {
                        AppendResult(className);
                    }
                }
                catch (Exception ex)
                {
                    await VS.MessageBox.ShowWarningAsync($"Catched error reading process memory: {ex.Message}");
                }
            }
        }

        private void RTTIShowBox_TextChanged(object sender, EventArgs e)
        {
            // 获取 RTTIShowBox 的最佳大小
            var preferredSize = RTTIShowBox.GetPreferredSize(new Size(int.MaxValue, int.MaxValue));

            // 设置工具窗口的大小
            toolWindow.Size = new Size(
                Math.Max(toolWindow.Width, preferredSize.Width + 20), // 宽度加上一些边距
                Math.Max(toolWindow.Height, preferredSize.Height + 20) // 高度加上一些边距
            );
        }

        public void AppendResult(string text)
        {
            RTTIShowBox.Text += (RTTIShowBox.Text.Length > 0 ? Environment.NewLine : "") + text;
        }
    }
}
