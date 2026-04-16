using System;

namespace Eurocast_Top5_Viewer.Models
{
    public class ProductionHistoryEntry
    {
        public DateTime Timestamp { get; set; }
        public string Machine { get; set; } = string.Empty;
        public string Piece { get; set; } = string.Empty;
        public string Moule { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
    }
}