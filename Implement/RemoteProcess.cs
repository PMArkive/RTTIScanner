using EnvDTE80;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Process = System.Diagnostics.Process;

namespace RTTIScanner.Commands
{
    public class RemoteProcess
    {
        public static RemoteProcess Instance { get; set; }
        public Process currentProcess;
        public DTE2 dte;
        public EnvDTE.Debugger debugger;

        public RemoteProcess(DTE2 dte)
        {
            this.dte = dte;
            this.debugger = dte.Debugger;
        }

        public async Task<byte[]> ReadRemoteMemoryAsync(IntPtr address, int size)
        {
            if (address == IntPtr.Zero)
            {
                return null;
            }

            currentProcess ??= await GetCurrentDebugProcessAsync();

            if (currentProcess == null)
            {
                await VS.MessageBox.ShowWarningAsync("当前进程为空!");
                return null;
            }

            // get pointer.
            byte[] data = new byte[size];
            int bytesRead;
            try
            {
                if (!NativeAPI.ReadProcessMemory(currentProcess.Handle, address, data, size, out bytesRead))
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    await VS.MessageBox.ShowWarningAsync($"ReadProcessMemory failed!. Error reading process memory: {errorCode}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                await VS.MessageBox.ShowWarningAsync($"Catched error reading process memory: {ex.Message}");
                return null;
            }

            return data;
        }

        private async Task<Process> GetCurrentDebugProcessAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // a fkn weird way to get current process but i dont have no idea.
            // cuz debugger.CurrentProcess is null WTF!
            foreach (EnvDTE.Process process in debugger.DebuggedProcesses)
            {
                if (process != null)
                {
                    try
                    {
                        return Process.GetProcessById(process.ProcessID);
                    }
                    catch (Exception ex)
                    {
                        await VS.MessageBox.ShowWarningAsync($"Catched error getting process by id: {ex.Message}");
                        return null;
                    }
                }
            }

            return null;
        }

        public static async Task<IntPtr> ParseAddressAsync(string addressString)
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
