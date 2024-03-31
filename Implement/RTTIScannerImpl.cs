using EnvDTE;
using EnvDTE80;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using RTTIScanner.ClassExtensions;
using RTTIScanner.Implement;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio;


namespace RTTIScanner.Impl
{
    [Command(PackageIds.Window)]
    internal sealed class RTTIScannerImpl : BaseCommand<RTTIScannerImpl>
    {
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
            RTTIShowBox.TextChanged += RTTIShowBox_OnTextChanged;

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

                // minidump和调试模式都会走进来.
                if (RemoteProcess.Instance.debugger.CurrentMode == dbgDebugMode.dbgDesignMode)
                {
                    await VS.MessageBox.ShowWarningAsync("调试器没有启动!");
                    return;
                }

                // 不是断点模式的话CurrentProcess会一直为null, 很弱智
                // 这里用来判断是不是在处理minidump
                if (RemoteProcess.Instance.debugger.CurrentProcess != null)
                {
                    string filePath = RemoteProcess.Instance.debugger.CurrentProcess.Name;
                    if (filePath.EndsWith(".mdmp"))
                    {
                        MinidumpParser.Instance ??= new MinidumpParser();
                    }
                }

                IntPtr pointer = await RemoteProcess.ParseAddressAsync(context);
                if (!pointer.MayBeValid())
                {
                    await VS.MessageBox.ShowWarningAsync("地址不合法!");
                    return;
                }

                string rtti;

                try
                {
                    byte[] data = await RemoteProcess.Instance.ReadRemoteMemoryAsync(pointer, 8);
                    IntPtr remotePtr = (IntPtr)BitConverter.ToInt64(data, 0);
                    rtti = await RTTIParser.ReadRemoteRuntimeTypeInformationAsync(remotePtr);
                    if (string.IsNullOrEmpty(rtti))
                    {
                        await VS.MessageBox.ShowWarningAsync("获取RTTI信息失败!");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    await VS.MessageBox.ShowWarningAsync($"Catched error reading process memory: {ex.Message}");
                    return;
                }

                string[] rttis = rtti.Split(separator: new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                RTTIShowBox.Clear();
                foreach (string className in rttis)
                {
                    AppendResult(className);
                }
            }
        }

        

        private void RTTIShowBox_OnTextChanged(object sender, EventArgs e)
        {
            var preferredSize = RTTIShowBox.GetPreferredSize(new Size(int.MaxValue, int.MaxValue));

            toolWindow.Size = new Size(
                Math.Max(toolWindow.Width, preferredSize.Width + 20), 
                Math.Max(toolWindow.Height, preferredSize.Height + 20)
            );
        }

        public void AppendResult(string text)
        {
            RTTIShowBox.Text += (RTTIShowBox.Text.Length > 0 ? Environment.NewLine : "") + text;
        }
    }
}
