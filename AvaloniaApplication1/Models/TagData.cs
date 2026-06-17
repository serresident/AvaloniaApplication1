namespace AvaloniaApplication1.Models
{
    public class TagData
    {
        public string ConnId { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public object Value { get; set; } = null!;
        
        public TagData(string connId, string address, object value)
        {
            ConnId = connId;
            Address = address;
            Value = value;
        }
    }
}
