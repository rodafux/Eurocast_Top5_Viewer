using System;

namespace Eurocast_Top5_Viewer.Models
{
    public class FlashDetail
    {
        public string Id { get; set; } = string.Empty;
        public string DateStr { get; set; } = string.Empty;
        public string Origine { get; set; } = string.Empty;
        public string Client { get; set; } = string.Empty;
        public string Reference { get; set; } = string.Empty;
        public string Designation { get; set; } = string.Empty;
        public string LibelleDefaut { get; set; } = string.Empty;
        public string Consequences { get; set; } = string.Empty;
        public string PlanReaction { get; set; } = string.Empty;
        public string RefImageUrl { get; set; } = string.Empty;
        public string DefectImageUrl { get; set; } = string.Empty;
    }
}