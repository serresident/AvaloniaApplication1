namespace AvaloniaApplication1.Models
{
    /// <summary>
    /// Industrial telemetry quality flag inspired by Rapid SCADA v6 and OPC DA/UA.
    /// </summary>
    public enum TagQuality : byte
    {
        Good = 0,
        Bad = 1,
        CommFailure = 2,
        Timeout = 3,
        Stale = 4,
        OutOfRange = 5,
        Overridden = 6,
        Uninitialized = 7
    }
}
