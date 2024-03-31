using Microsoft.VisualStudio.Debugger.Interop;

namespace RTTIScanner.Implement
{
    public class DebuggerIfaces
    {
        public static DebuggerIfaces Instance { get; set; }
        public IDebugEngine2 engine;
        public IDebugProcess2 process;
        public IDebugProgram2 program;
        public IDebugThread2 mainThread;
        public IDebugEvent2 debugEvent;
        public void Update(IDebugEngine2 engine, IDebugProcess2 process, IDebugProgram2 program, IDebugThread2 thread, IDebugEvent2 debugEvent) 
        { 
            if (engine != null) this.engine = engine;
            if (process != null) this.process = process;
            if (program != null) this.program = program;
            if (thread != null) mainThread ??= thread;
            if (debugEvent != null) this.debugEvent = debugEvent;
        }
    }
}
