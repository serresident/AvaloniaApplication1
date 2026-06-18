using System;
using System.Threading.Tasks;

namespace AvaloniaApplication1.Services
{
    internal static class AsyncHelper
    {
        /// <summary>
        /// Запускает задачу "fire-and-forget" с логированием необработанных исключений.
        /// </summary>
        internal static void FireAndForget(
            this Task task,
            Action<Exception>? onError = null,
            string context = "")
        {
            task.ContinueWith(t =>
            {
                if (t.Exception != null)
                {
                    var ex = t.Exception.InnerException ?? t.Exception;
                    var msg = $"[FireAndForget]{(string.IsNullOrEmpty(context) ? "" : $"[{context}]")} {ex.Message}";
                    Console.WriteLine(msg);
                    onError?.Invoke(ex);
                }
            }, TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}
