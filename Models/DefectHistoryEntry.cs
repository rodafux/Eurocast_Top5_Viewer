using System;

namespace Eurocast_Top5_Viewer.Models
{
    public class DefectHistoryEntry
    {
        public Guid Id { get; set; }
        public string Machine { get; set; } = string.Empty;
        public string Piece { get; set; } = string.Empty;
        public string Moule { get; set; } = string.Empty;
        public string TypeDefaut { get; set; } = string.Empty;
        public string Gravite { get; set; } = string.Empty;
        public string Commentaire { get; set; } = string.Empty;
        public string NumeroNoyau { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string Heure { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
    }
}