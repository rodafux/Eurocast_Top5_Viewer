using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Threading;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Eurocast_Top5_Viewer.ViewModels
{
    public class ProductionHistoryEntry
    {
        public DateTime Timestamp { get; set; }
        public string Machine { get; set; } = "";
        public string Piece { get; set; } = "";
        public string Moule { get; set; } = "";
        public string Action { get; set; } = "";
    }

    public class DefectHistoryEntry
    {
        public Guid Id { get; set; }
        public string Machine { get; set; } = "";
        public string Piece { get; set; } = "";
        public string Moule { get; set; } = "";
        public string TypeDefaut { get; set; } = "";
        public string Gravite { get; set; } = "";
        public string Commentaire { get; set; } = "";
        public string NumeroNoyau { get; set; } = "";
        public string Date { get; set; } = "";
        public string Heure { get; set; } = "";
        public string Action { get; set; } = "";
    }

    public class DisplayItem : INotifyPropertyChanged
    {
        public bool IsHeader { get; set; }
        public bool IsNotHeader => !IsHeader;
        public string MachineInfo { get; set; } = "";
        public string State { get; set; } = "";
        public string CreationDateStr { get; set; } = "";
        public string DefectType { get; set; } = "";
        public string CoreNumber { get; set; } = "";
        public string Comment { get; set; } = "";
        public DateTime CreationDate { get; set; }

        // NOUVEAU : Variables pour l'alerte noyau
        public int CoreAlertCount { get; set; } = 0;
        public string CoreAlertText => CoreAlertCount > 1 ? $"⚠️ ALERTE : CASSÉ {CoreAlertCount} FOIS EN 7 JOURS" : "";

        private string _elapsedTime = "";
        public string ElapsedTime { get => _elapsedTime; set { _elapsedTime = value; OnPropertyChanged(); } }

        public string DefectDisplay => string.IsNullOrWhiteSpace(CoreNumber) ? DefectType : $"{DefectType} (Noyau {CoreNumber})";

        public string BackgroundColor => IsHeader ? "#1E293B" : (State == "NC" ? "#450A0A" : (State == "AA" ? "#422006" : (State == "B" ? "#064E3B" : "#0F172A")));
        public string TextColor => IsHeader ? "#F8FAFC" : (State == "NC" ? "#FECACA" : (State == "AA" ? "#FDE68A" : (State == "B" ? "#A7F3D0" : "#CBD5E1")));
        public string AccentColor => IsHeader ? "#38BDF8" : (State == "NC" ? "#EF4444" : (State == "AA" ? "#F59E0B" : (State == "B" ? "#10B981" : "#334155")));

        public event PropertyChangedEventHandler? PropertyChanged;

        public void RefreshTime()
        {
            if (IsHeader) return;

            if (State == "B")
            {
                ElapsedTime = "RÉSOLU";
            }
            else
            {
                var delta = DateTime.Now - CreationDate;
                ElapsedTime = delta.TotalDays >= 1
                    ? $"{(int)delta.TotalDays}j {delta.Hours:D2}h {delta.Minutes:D2}m"
                    : $"{delta.Hours}h {delta.Minutes:D2}m";
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string name = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class MainViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<DisplayItem> DashboardItems { get; set; } = new ObservableCollection<DisplayItem>();
        private List<List<DisplayItem>> _allPages = new List<List<DisplayItem>>();

        private string _lastUpdateText = "";
        public string LastUpdateText { get => _lastUpdateText; set { _lastUpdateText = value; OnPropertyChanged(); } }

        private string _pageIndicatorText = "";
        public string PageIndicatorText { get => _pageIndicatorText; set { _pageIndicatorText = value; OnPropertyChanged(); } }

        private DispatcherTimer _dataTimer;
        private DispatcherTimer _clockTimer;
        private DispatcherTimer _pagingTimer;

        private int _currentPageIndex = 0;
        private readonly int _itemsPerPage = 10;

        private readonly string _basePath = @"P:\Delle\Pot Commun\Informatique\NE PAS SUPPRIMER - TOP5_QUALITE\data";

        public MainViewModel()
        {
            LoadData();
            _dataTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
            _dataTimer.Tick += (s, e) => LoadData();
            _dataTimer.Start();

            _pagingTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
            _pagingTimer.Tick += (s, e) => NextPage();
            _pagingTimer.Start();

            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += (s, e) => { foreach (var item in DashboardItems) item.RefreshTime(); };
            _clockTimer.Start();
        }

        private void NextPage()
        {
            if (_allPages.Count == 0) return;
            _currentPageIndex = (_currentPageIndex + 1) % _allPages.Count;
            UpdatePageDisplay();
        }

        private void UpdatePageDisplay()
        {
            if (_allPages.Count <= 1) PageIndicatorText = "";
            else PageIndicatorText = $"PAGE {_currentPageIndex + 1}/{_allPages.Count}";

            DashboardItems.Clear();
            if (_allPages.Count > _currentPageIndex)
            {
                foreach (var item in _allPages[_currentPageIndex]) DashboardItems.Add(item);
            }
        }

        private void LoadData()
        {
            try
            {
                string prodPath = Path.Combine(_basePath, "historique_productions.json");
                string defDir = Path.Combine(_basePath, "HistoriqueDefauts");
                if (!File.Exists(prodPath)) return;

                var prodHistory = JsonSerializer.Deserialize<List<ProductionHistoryEntry>>(File.ReadAllText(prodPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (prodHistory == null || !prodHistory.Any()) return;

                var activeProds = prodHistory.GroupBy(h => h.Machine).ToDictionary(g => g.Key, g => g.OrderByDescending(h => h.Timestamp).First());

                var newPages = new List<List<DisplayItem>>();
                var currentPageList = new List<DisplayItem>();

                foreach (var prod in activeProds.Values.Where(p => p.Piece != "---"))
                {
                    string safePiece = string.Join("_", prod.Piece.Split(Path.GetInvalidFileNameChars()));
                    string safeMoule = string.Join("_", prod.Moule.Split(Path.GetInvalidFileNameChars()));
                    string defectFile = Path.Combine(defDir, $"Defauts_{safePiece}_{safeMoule}.json");

                    if (File.Exists(defectFile))
                    {
                        var history = JsonSerializer.Deserialize<List<DefectHistoryEntry>>(File.ReadAllText(defectFile), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        var activeMachineItems = new List<DisplayItem>();

                        if (history != null)
                        {
                            var groupedDefects = history.GroupBy(d => d.Id).ToList();

                            // 1. ANALYSE DES 7 DERNIERS JOURS : On compte le nombre de fois où chaque noyau a eu un défaut.
                            var coreAlertCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                            foreach (var group in groupedDefects)
                            {
                                var latest = group.Last();
                                var first = group.First();

                                if (latest.Action != "Suppression" && !string.IsNullOrWhiteSpace(latest.NumeroNoyau))
                                {
                                    if (DateTime.TryParseExact($"{first.Date} {first.Heure}", "dd/MM/yyyy HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime pDate))
                                    {
                                        if (pDate >= DateTime.Now.Date.AddDays(-7))
                                        {
                                            string core = latest.NumeroNoyau.Trim();
                                            if (!coreAlertCounts.ContainsKey(core)) coreAlertCounts[core] = 0;
                                            coreAlertCounts[core]++;
                                        }
                                    }
                                }
                            }

                            // 2. GÉNÉRATION DES DÉFAUTS ACTIFS
                            foreach (var group in groupedDefects)
                            {
                                var latest = group.Last();
                                var first = group.First();

                                DateTime lastActionDate = DateTime.Now;
                                if (DateTime.TryParseExact($"{latest.Date} {latest.Heure}", "dd/MM/yyyy HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime pDate))
                                {
                                    lastActionDate = pDate;
                                }

                                bool isRecentlyValidated = latest.Gravite == "B" && (DateTime.Now - lastActionDate).TotalHours <= 1;
                                bool isActive = latest.Gravite == "AA" || latest.Gravite == "NC";

                                if (latest.Action != "Suppression" && (isActive || isRecentlyValidated))
                                {
                                    DateTime.TryParseExact($"{first.Date} {first.Heure}", "dd/MM/yyyy HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime cDate);

                                    // Vérification si ce noyau précis a une alerte
                                    int alertCount = 0;
                                    string coreTrimmed = latest.NumeroNoyau?.Trim() ?? "";
                                    if (!string.IsNullOrEmpty(coreTrimmed) && coreAlertCounts.ContainsKey(coreTrimmed))
                                    {
                                        alertCount = coreAlertCounts[coreTrimmed];
                                    }

                                    var item = new DisplayItem
                                    {
                                        State = latest.Gravite,
                                        DefectType = latest.TypeDefaut,
                                        CoreNumber = latest.NumeroNoyau,
                                        Comment = latest.Commentaire,
                                        CreationDate = cDate,
                                        CreationDateStr = cDate.ToString("dd/MM HH:mm"),
                                        CoreAlertCount = alertCount // Transmission au visuel
                                    };
                                    item.RefreshTime();
                                    activeMachineItems.Add(item);
                                }
                            }
                        }

                        if (activeMachineItems.Any())
                        {
                            var sortedDefects = activeMachineItems.OrderBy(x => x.State == "NC" ? 0 : (x.State == "AA" ? 1 : 2)).ThenBy(x => x.CreationDate).ToList();
                            string baseHeaderTitle = $"{prod.Machine}  ▸  Pièce: {prod.Piece}  ({prod.Moule})";

                            if (currentPageList.Count + 1 + sortedDefects.Count <= _itemsPerPage)
                            {
                                currentPageList.Add(new DisplayItem { IsHeader = true, MachineInfo = baseHeaderTitle });
                                currentPageList.AddRange(sortedDefects);
                            }
                            else if (1 + sortedDefects.Count <= _itemsPerPage)
                            {
                                if (currentPageList.Count > 0)
                                {
                                    newPages.Add(currentPageList);
                                    currentPageList = new List<DisplayItem>();
                                }
                                currentPageList.Add(new DisplayItem { IsHeader = true, MachineInfo = baseHeaderTitle });
                                currentPageList.AddRange(sortedDefects);
                            }
                            else
                            {
                                if (currentPageList.Count + 2 > _itemsPerPage && currentPageList.Count > 0)
                                {
                                    newPages.Add(currentPageList);
                                    currentPageList = new List<DisplayItem>();
                                }

                                currentPageList.Add(new DisplayItem { IsHeader = true, MachineInfo = baseHeaderTitle });

                                foreach (var defect in sortedDefects)
                                {
                                    if (currentPageList.Count >= _itemsPerPage)
                                    {
                                        newPages.Add(currentPageList);
                                        currentPageList = new List<DisplayItem>();
                                        currentPageList.Add(new DisplayItem { IsHeader = true, MachineInfo = baseHeaderTitle + "  (Suite)" });
                                    }
                                    currentPageList.Add(defect);
                                }
                            }
                        }
                    }
                }

                if (currentPageList.Any()) newPages.Add(currentPageList);

                _allPages = newPages;
                if (_currentPageIndex >= _allPages.Count) _currentPageIndex = 0;

                UpdatePageDisplay();
                LastUpdateText = $"SYNC: {DateTime.Now:HH:mm}";
            }
            catch { LastUpdateText = "ERREUR RÉSEAU"; }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}