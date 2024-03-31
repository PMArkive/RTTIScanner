using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio;
using System.Threading.Tasks;

namespace RTTIScanner.Implement
{
    public class MinidumpParser
    {
        public static MinidumpParser Instance { get; set; }
        public async Task<byte[]> ReadMemoryFromVSAsync(IntPtr pointer, int size)
        {
            string expression = "0x" + pointer.ToString("X");
            if (DebuggerIfaces.Instance.mainThread == null)
            {
                await VS.MessageBox.ShowWarningAsync("获取主线程失败!");
                return null;
            }

            IDebugStackFrame2 debugStackFrame = GetTopStackFrame(DebuggerIfaces.Instance.mainThread);
            if (debugStackFrame == null)
            {
                await VS.MessageBox.ShowWarningAsync("获取栈帧失败!");
                return null;
            }

            IDebugExpressionContext2 expressionContext;
            if (debugStackFrame.GetExpressionContext(out expressionContext) != VSConstants.S_OK)
            {
                await VS.MessageBox.ShowWarningAsync("获取IDebugExpressionContext2失败!");
                return null;
            }

            IDebugExpression2 debugExpression;
            if (expressionContext.ParseText(expression, enum_PARSEFLAGS.PARSE_EXPRESSION, 16, out debugExpression, out _, out _) != VSConstants.S_OK)
            {
                await VS.MessageBox.ShowWarningAsync("获取IDebugExpression2失败!");
                return null;
            }

            IDebugProperty2 debugProperty;
            if (debugExpression.EvaluateSync(enum_EVALFLAGS.EVAL_NOSIDEEFFECTS, uint.MaxValue, null, out debugProperty) != VSConstants.S_OK)
            {
                await VS.MessageBox.ShowWarningAsync("获取IDebugProperty2失败!");
                return null;
            }

            IDebugMemoryContext2 memoryContext;
            if (debugProperty.GetMemoryContext(out memoryContext) != VSConstants.S_OK)
            {
                await VS.MessageBox.ShowWarningAsync("获取IDebugMemoryContext2失败!");
                return null;
            }

            if (DebuggerIfaces.Instance.program == null)
            {
                await VS.MessageBox.ShowWarningAsync("获取program失败!");
                return null;
            }

            IDebugMemoryBytes2 debugMemoryBytes2;
            if (DebuggerIfaces.Instance.program.GetMemoryBytes(out debugMemoryBytes2) != VSConstants.S_OK)
            {
                await VS.MessageBox.ShowWarningAsync("获取IDebugMemoryBytes2失败!");
                return null;
            }

            byte[] data = new byte[size];
            uint unreadableBytes = 0;
            if (debugMemoryBytes2.ReadAt(memoryContext, (uint)size, data, out _, ref unreadableBytes) != VSConstants.S_OK)
            {
                await VS.MessageBox.ShowWarningAsync("从VS读取内存失败!");
                return null;
            }

            return data;
        }

        private IDebugStackFrame2 GetTopStackFrame(IDebugThread2 thread)
        {
            IEnumDebugFrameInfo2 enumFrameInfo;
            thread.EnumFrameInfo(enum_FRAMEINFO_FLAGS.FIF_FRAME, 0, out enumFrameInfo);
            FRAMEINFO[] frameInfo = new FRAMEINFO[1];
            uint fetched = 0;
            enumFrameInfo.Next(1, frameInfo, ref fetched);
            return fetched > 0 ? frameInfo[0].m_pFrame : null;
        }
    }
}
