using System;
using System.Collections.Concurrent;
using System.Threading;

namespace AATool.Utilities
{
    /// <summary>
    /// OpenGL calls are only valid on the thread that owns the context, so background
    /// threads (asset loading, web requests) use this to run graphics work on the main thread.
    /// </summary>
    public static class MainThread
    {
        private static readonly ConcurrentQueue<Action> Pending = new ();
        private static int Id = -1;

        public static bool IsCurrent => Thread.CurrentThread.ManagedThreadId == Id;

        public static void Initialize() => Id = Thread.CurrentThread.ManagedThreadId;

        /// <summary>
        /// Run an action on the main thread and wait for it to finish.
        /// Callers must not hold any lock the main thread could be waiting on.
        /// </summary>
        public static void Invoke(Action action)
        {
            if (IsCurrent || Id is -1)
            {
                action();
                return;
            }

            Exception exception = null;
            using var done = new ManualResetEventSlim(false);
            Pending.Enqueue(() => {
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    exception = e;
                }
                finally
                {
                    done.Set();
                }
            });
            done.Wait();

            if (exception is not null)
                throw exception;
        }

        public static T Invoke<T>(Func<T> function)
        {
            T result = default;
            Invoke(() => { result = function(); });
            return result;
        }

        public static void RunPending()
        {
            while (Pending.TryDequeue(out Action action))
                action();
        }
    }
}
