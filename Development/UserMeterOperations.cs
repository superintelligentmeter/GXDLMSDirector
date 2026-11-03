// superintelligentmeter contributors, 2026-11-02. Serialize manual work after dashboard polling. GPL-2.0-only.
using System;
using System.Threading;
using System.Threading.Tasks;
using Gurux.Common;

namespace GXDLMSDirector
{
    public partial class MainForm
    {
        private int pendingUserMeterOperations;

        private sealed class UserMeterOperation : IDisposable
        {
            internal readonly Task Ready;
            private Action release;
            internal UserMeterOperation(Task ready, Action release) { Ready = ready; this.release = release; }
            public void Dispose() => Interlocked.Exchange(ref release, null)?.Invoke();
        }

        private UserMeterOperation BeginUserMeterOperation()
        {
            Interlocked.Increment(ref pendingUserMeterOperations);
            try
            {
                Task ready = InvokeRequired
                    ? (Task)Invoke(new Func<Task>(PauseDashboardRead))
                    : PauseDashboardRead();
                return new UserMeterOperation(ready, () => Interlocked.Decrement(ref pendingUserMeterOperations));
            }
            catch { Interlocked.Decrement(ref pendingUserMeterOperations); throw; }
        }

        // Macro callbacks are synchronous: their worker may wait, but the UI thread must never
        // wait for Poll's UI continuation. A synchronous UI caller receives a retryable error.
        private void WaitForDashboardRead(UserMeterOperation operation)
        {
            if (!operation.Ready.IsCompleted && !InvokeRequired)
                throw new InvalidOperationException("The current dashboard request is finishing. Please retry the meter operation.");
            operation.Ready.GetAwaiter().GetResult();
        }

        private GXAsyncWork CreateUserWork(object sender, AsyncStateChangeEventHandler state,
            AsyncTransaction command, Gurux.Common.ErrorEventHandler error, string text, object[] parameters)
        {
            return new GXAsyncWork(sender, state, (owner, work, args) =>
            {
                using var operation = BeginUserMeterOperation();
                while (!operation.Ready.IsCompleted)
                {
                    if (work.IsCanceled) return;
                    operation.Ready.Wait(100);
                }
                if (!work.IsCanceled && !IsDisposed) command(owner, work, args);
            }, error, text, parameters);
        }
    }
}
