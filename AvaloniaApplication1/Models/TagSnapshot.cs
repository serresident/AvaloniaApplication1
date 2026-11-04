using System;

namespace AvaloniaApplication1.Models
{
    /// <summary>
    /// Immutable point-in-time snapshot of an industrial tag telemetry state.
    /// Memory efficient readonly record struct.
    /// </summary>
    public readonly record struct TagSnapshot(
        string TagId,
        object? Value,
        TagQuality Quality,
        DateTime Timestamp,
        string? ErrorMessage = null,
        bool IsOverridden = false
    );
}
