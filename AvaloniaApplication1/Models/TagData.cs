using System;

namespace AvaloniaApplication1.Models
{
    public sealed record TagData(
        string ConnId,
        string Address,
        object Value,
        TagQuality Quality = TagQuality.Good,
        DateTime? Timestamp = null
    );
}
