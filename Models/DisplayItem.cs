using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Eurocast_Top5_Viewer.Models
{
    public class DisplayItem : INotifyPropertyChanged
    {
        public bool IsHeader { get; set; }
        public bool IsNotHeader => !IsHeader;
        public string MachineInfo { get; set; } = string.Empty;
        public string MachineColor { get; set; } = "Transparent";
        public string State { get; set; } = string.Empty;
        public string CreationDateStr { get; set; } = string.Empty;
        public string DefectType { get; set; } = string.Empty;
        public string CoreNumber { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
        public DateTime CreationDate { get; set; }
        public int CoreAlertCount { get; set; } = 0;

        public string CoreAlertText => CoreAlertCount > 1 ? $"⚠️ ALERTE : CASSÉ {CoreAlertCount} FOIS EN 7 JOURS" : string.Empty;

        private string _elapsedTime = string.Empty;
        public string ElapsedTime
        {
            get => _elapsedTime;
            set { _elapsedTime = value; OnPropertyChanged(); }
        }

        public string DefectDisplay => string.IsNullOrWhiteSpace(CoreNumber) ? DefectType : $"{DefectType} (Noyau {CoreNumber})";

        private string _backgroundColor = "Transparent";
        public string BackgroundColor
        {
            get => _backgroundColor;
            set { _backgroundColor = value; OnPropertyChanged(); }
        }

        private string _accentColor = "Transparent";
        public string AccentColor
        {
            get => _accentColor;
            set { _accentColor = value; OnPropertyChanged(); }
        }

        public string TextColor => "#F8FAFC";
        private bool _blinkToggle = false;

        public event PropertyChangedEventHandler? PropertyChanged;

        public void RefreshTime()
        {
            if (IsHeader)
            {
                BackgroundColor = "Transparent";
                AccentColor = "Transparent";
                return;
            }

            var delta = DateTime.Now - CreationDate;

            if (State == "B")
            {
                ElapsedTime = "VALIDÉ";
            }
            else
            {
                ElapsedTime = delta.TotalDays >= 1 ? $"{(int)delta.TotalDays}j {delta.Hours:D2}h" : $"{delta.Hours}h {delta.Minutes:D2}m";
            }

            if ((State == "NC" || State == "AA") && delta.TotalMinutes < 30)
            {
                _blinkToggle = !_blinkToggle;

                if (State == "NC")
                {
                    BackgroundColor = _blinkToggle ? "#7F1D1D" : "#0A3B66";
                    AccentColor = _blinkToggle ? "Transparent" : "#E53935";
                }
                else
                {
                    BackgroundColor = _blinkToggle ? "#78350F" : "#0A3B66";
                    AccentColor = _blinkToggle ? "Transparent" : "#FFB300";
                }
            }
            else
            {
                BackgroundColor = "#0A3B66";
                AccentColor = State == "NC" ? "#E53935" : (State == "AA" ? "#FFB300" : (State == "B" ? "#10B981" : "Transparent"));
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string name = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}