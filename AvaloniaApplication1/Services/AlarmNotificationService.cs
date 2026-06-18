using System;

namespace AvaloniaApplication1.Services
{
    public sealed class AlarmNotificationService : IAlarmNotificationService
    {
        private Action<string, string>? _handler;

        public void SetHandler(Action<string, string> handler) => _handler = handler;

        public void ShowAlarm(string title, string message) =>
            _handler?.Invoke(title, message);
    }
}
