using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Reflection;
using System.Drawing;
using System.Text;
using EnvDTE80;
using EnvDTE;
using Process = System.Diagnostics.Process;
using System;
using Newtonsoft.Json.Linq;


namespace vsix_demo1
{
    [Command(PackageIds.MyCommand)]
    internal sealed class MyCommand : BaseCommand<MyCommand>
    {
        private TextBox editableTextBox;
        private TextBox readonlyTextBox;
        private DTE2 dte;
        private EnvDTE.Debugger debugger;
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await Package.JoinableTaskFactory.SwitchToMainThreadAsync();

            dte = (DTE2)Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(DTE));
            debugger = dte.Debugger;

            // 创建可编辑的文本框
            editableTextBox = new System.Windows.Forms.TextBox();
            editableTextBox.Multiline = false;
            editableTextBox.Dock = DockStyle.Fill;
            editableTextBox.Padding = new Padding(10); // 添加一些内边距
            editableTextBox.Font = new Font("Consolas", 12, FontStyle.Regular); // 设置字体
            editableTextBox.KeyPress += OnKeyPress_Enter;

            // 创建不可编辑的文本框
            readonlyTextBox = new System.Windows.Forms.TextBox();
            readonlyTextBox.Multiline = true;
            readonlyTextBox.ReadOnly = true;
            readonlyTextBox.Dock = DockStyle.Fill;
            readonlyTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            readonlyTextBox.Font = new Font("Consolas", 12, FontStyle.Regular); // 设置字体

            // 创建一个 TableLayoutPanel 来布局这两个文本框
            var tableLayoutPanel = new TableLayoutPanel();
            tableLayoutPanel.RowCount = 2;
            tableLayoutPanel.ColumnCount = 1;
            tableLayoutPanel.Dock = DockStyle.Fill;
            tableLayoutPanel.Controls.Add(editableTextBox, 0, 0);
            tableLayoutPanel.Controls.Add(readonlyTextBox, 0, 1);

            // 创建一个新的工具窗口
            var toolWindow = new Form();
            toolWindow.Text = "RTTI Scanner";
            string projectName = Assembly.GetExecutingAssembly().GetName().Name.ToString();
            toolWindow.Icon = new Icon(Assembly.GetExecutingAssembly().GetManifestResourceStream(projectName + ".Resources" + ".ReClass.ico"));
            toolWindow.Controls.Add(tableLayoutPanel);

            // 显示工具窗口
            toolWindow.Show();
        }

        private async void OnKeyPress_Enter(object sender, KeyPressEventArgs e)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (e.KeyChar == (char)Keys.Enter)
            {
                // 禁用系统的默认回车键行为, 如 取消提示音
                e.Handled = true;

                string context = editableTextBox.Text;
                if (context.Length == 0)
                {
                    return;
                }

                if (debugger.CurrentMode == dbgDebugMode.dbgDesignMode)
                {
                    await VS.MessageBox.ShowWarningAsync("调试器没有启动!");
                    return;
                }

                DoAddressScan(context);
            }
        }

        private void AppendResult(string text)
        {
            // 将可编辑文本框的内容复制到不可编辑文本框, 自动换行
            readonlyTextBox.Text += (readonlyTextBox.Text.Length > 0 ? Environment.NewLine : "") + text;
        }

        private async void DoAddressScan(string sAddress)
        {
            Process currentProcess = await GetCurrentDebugProcessAsync();

            if (currentProcess == null)
            {
                await VS.MessageBox.ShowWarningAsync("当前进程为空!");
                return;
            }

            IntPtr address = await ParseAddressAsync(sAddress);

            if (address == IntPtr.Zero)
            {
                return;
            }

            // get pointer.
            byte[] pointerBuffer = new byte[8];
            int bytesRead;
            bool success = false;
            try
            {
                success = Win32API.ReadProcessMemory(currentProcess.Handle, address, pointerBuffer, 8, out bytesRead);
                if (!success)
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    await VS.MessageBox.ShowWarningAsync($"ReadProcessMemory failed!. Error reading process memory: {errorCode}");
                    return;
                }
            }
            catch (Exception ex)
            {
                await VS.MessageBox.ShowWarningAsync($"Catched error reading process memory: {ex.Message}");
                return;
            }

            if (success && bytesRead == pointerBuffer.Length)
            {
                long pointer = BitConverter.ToInt64(pointerBuffer, 0);
                string hexValue = "0x" + pointer.ToString("X16");
                AppendResult(hexValue);
            }
        }

        private async System.Threading.Tasks.Task<Process> GetCurrentDebugProcessAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // a fkn weird way to get current process but i dont have other idea.
            // cuz dte.Debugger.CurrentProcess always return null WTF!
            foreach (EnvDTE.Process process in debugger.DebuggedProcesses)
            {
                if (process != null)
                {
                    try
                    {
                        return Process.GetProcessById(process.ProcessID);
                    } catch (Exception ex)
                    {
                        await VS.MessageBox.ShowWarningAsync($"Catched error getting process by id: {ex.Message}");
                        return null;
                    }
                }
            }

            return null;
        }

        private async System.Threading.Tasks.Task<IntPtr> ParseAddressAsync(string addressString)
        {
            // 从字符串中解析地址
            if (addressString.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                addressString = addressString.Substring(2);
            }

            try
            {
                return new IntPtr(long.Parse(addressString, System.Globalization.NumberStyles.HexNumber));
            }
            catch (FormatException ex)
            {
                await VS.MessageBox.ShowWarningAsync($"Error parsing address: {ex.Message}");
                return IntPtr.Zero;
            }
            catch (OverflowException ex)
            {
                await VS.MessageBox.ShowWarningAsync($"Error parsing address: {ex.Message}");
                return IntPtr.Zero;
            }
            catch (Exception ex)
            {
                await VS.MessageBox.ShowWarningAsync($"Error parsing address: {ex.Message}");
                return IntPtr.Zero;
            }
        }
    }
}
