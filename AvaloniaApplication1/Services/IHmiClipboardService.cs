using System;

namespace AvaloniaApplication1.Services
{
    /// <summary>
    /// Изолированный внутренний буфер обмена HMI/SCADA для безопасной передачи
    /// рассчитанных значений в задатчики уставок без использования системного буфера ОС
    /// (гарантирует 100% стабильность на Linux Debian ARM32/ARM64 в kiosk-режиме).
    /// </summary>
    public interface IHmiClipboardService
    {
        string? CurrentValue { get; set; }
        bool HasValue { get; }
        event Action<string?>? ValueChanged;
        void Clear();
    }
}
