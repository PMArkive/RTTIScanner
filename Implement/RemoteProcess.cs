using EnvDTE80;
using RTTIScanner.ClassExtensions;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Process = System.Diagnostics.Process;

namespace RTTIScanner.Implement
{
    [StructLayout(LayoutKind.Sequential)]
    public struct MEMORY_BASIC_INFORMATION
    {
        public IntPtr BaseAddress;
        public IntPtr AllocationBase;
        public uint AllocationProtect;
        public IntPtr RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
    }

    public static class MemoryProtectionConstants
    {
        public const uint PAGE_NOACCESS = 0x01;
        public const uint PAGE_READONLY = 0x02;
        public const uint PAGE_READWRITE = 0x04;
        public const uint PAGE_WRITECOPY = 0x08;
        public const uint PAGE_EXECUTE = 0x10;
        public const uint PAGE_EXECUTE_READ = 0x20;
        public const uint PAGE_EXECUTE_READWRITE = 0x40;
        public const uint PAGE_EXECUTE_WRITECOPY = 0x80;
        public const uint PAGE_GUARD = 0x100;
        public const uint PAGE_NOCACHE = 0x200;
        public const uint PAGE_WRITECOMBINE = 0x400;
    }

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

        public byte[] ReadRemoteMemory(IntPtr address, int size)
        {
            if (!address.MayBeValid())
            {
                return null;
            }

            if (MinidumpParser.Instance != null)
            {
                return MinidumpParser.Instance.ReadMemoryFromVS(address, size);
            }

            if (currentProcess == null)
            {
                RTTIScannerImpl.ErrorResult("当前进程为空!");
                return null;
            }

            byte[] data = new byte[size];
            try
            {
                int bytesRead;
                MEMORY_BASIC_INFORMATION memInfo = new MEMORY_BASIC_INFORMATION();
                if (NativeAPI.VirtualQueryEx(currentProcess.Handle, address, out memInfo, (uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION))) == 0)
                {
                    RTTIScannerImpl.ErrorResult($"VirtualQueryEx failed!. Error reading process memory");
                    return null;
                }

                if (!NativeAPI.ReadProcessMemory(currentProcess.Handle, address, data, size, out bytesRead))
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    RTTIScannerImpl.ErrorResult($"ReadProcessMemory failed!. Error reading process memory: {errorCode}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                RTTIScannerImpl.ErrorResult($"Catched error reading process memory: {ex.Message}");
                return null;
            }

            return data;
        }

        public async Task<Process> GetCurrentDebugProcessAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (MinidumpParser.Instance != null)
            {
                return Process.GetCurrentProcess();
            }
            
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
                        RTTIScannerImpl.ErrorResult($"Catched error getting process by id: {ex.Message}");
                        return null;
                    }
                }
            }

            return null;
        }

        public static IntPtr ParseAddress(string addressString)
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
            catch (Exception ex)
            {
                RTTIScannerImpl.ErrorResult($"Error parsing address: {ex.Message}");
                return IntPtr.Zero;
            }
        }
    }
}
