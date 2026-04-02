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
    // ============================================================================
    // 1. MODÈLES DE LECTURE
    // ============================================================================
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

    // ============================================================================
    // 2. MODÈLE D'AFFICHAGE VISUEL
    // ============================================================================
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
        public string ElapsedTime { get; set; } = "";

        public string DefectDisplay => string.IsNullOrWhiteSpace(CoreNumber) ? DefectType : $"{DefectType}\n▶ Noyau {CoreNumber}";

        public string BackgroundColor => IsHeader ? "#3C3C3C" : (State == "NC" ? "#4D1919" : (State == "AA" ? "#4D3B0A" : "#1C1C1C"));
        public string TextColor => IsHeader ? "#FFFFFF" : (State == "NC" ? "#FFCDD2" : (State == "AA" ? "#FFECB3" : "#B0B0B0"));

        public event PropertyChangedEventHandler? PropertyChanged;
        public void RefreshTime()
        {
            if (IsHeader) return;
            var delta = DateTime.Now - CreationDate;
            ElapsedTime = delta.TotalDays >= 1
                ? $"{(int)delta.TotalDays}j {delta.Hours:D2}h {delta.Minutes:D2}m"
                : $"{delta.Hours}h {delta.Minutes:D2}m";
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ElapsedTime)));
        }
    }

    // ============================================================================
    // 3. LE CERVEAU (Moteur de rafraîchissement & Pagination Intelligente)
    // ============================================================================
    public class MainViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<DisplayItem> DashboardItems { get; set; } = new ObservableCollection<DisplayItem>();

        // NOUVEAU : La mémoire contient désormais une liste de "Pages" pré-calculées
        private List<List<DisplayItem>> _allPages = new List<List<DisplayItem>>();

        private string _lastUpdateText = "";
        public string LastUpdateText { get => _lastUpdateText; set { _lastUpdateText = value; OnPropertyChanged(); } }

        private string _pageIndicatorText = "";
        public string PageIndicatorText { get => _pageIndicatorText; set { _pageIndicatorText = value; OnPropertyChanged(); } }

        private DispatcherTimer _dataTimer;
        private DispatcherTimer _clockTimer;
        private DispatcherTimer _pagingTimer;

        // PARAMÈTRES DE PAGINATION
        private int _currentPageIndex = 0;
        private readonly int _itemsPerPage = 11; // 11 lignes par écran

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
            _clockTimer.Tick += (s, e) =>
            {
                foreach (var item in DashboardItems) item.RefreshTime();
            };
            _clockTimer.Start();
        }

        // ================== MOTEUR DE CARROUSEL ==================
        private void NextPage()
        {
            if (_allPages.Count == 0) return;

            _currentPageIndex++;
            if (_currentPageIndex >= _allPages.Count)
            {
                _currentPageIndex = 0; // Retour à la page 1
            }

            UpdatePageDisplay();
        }

        private void UpdatePageDisplay()
        {
            int totalPages = _allPages.Count;

            if (totalPages <= 1) PageIndicatorText = "";
            else PageIndicatorText = $"Page {_currentPageIndex + 1} / {totalPages}";

            DashboardItems.Clear();
            if (totalPages > 0 && _currentPageIndex < totalPages)
            {
                foreach (var item in _allPages[_currentPageIndex])
                {
                    DashboardItems.Add(item);
                }
            }
        }
        // ===========================================================

        private void LoadData()
        {
            try
            {
                string prodPath = Path.Combine(_basePath, "historique_productions.json");
                string defDir = Path.Combine(_basePath, "HistoriqueDefauts");

                if (!File.Exists(prodPath) || !Directory.Exists(defDir))
                {
                    LastUpdateText = $"En attente de la BDD... ({DateTime.Now:HH:mm})";
                    return;
                }

                var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var prodHistory = JsonSerializer.Deserialize<List<ProductionHistoryEntry>>(File.ReadAllText(prodPath), jsonOptions);
                if (prodHistory == null || prodHistory.Count == 0) return;

                var activeProds = prodHistory.GroupBy(h => h.Machine)
                                             .ToDictionary(g => g.Key, g => g.OrderByDescending(h => h.Timestamp).First());

                // --- DEBUT DE LA PAGINATION INTELLIGENTE ---
                var newPages = new List<List<DisplayItem>>();
                var currentPageList = new List<DisplayItem>();

                foreach (var kvp in activeProds)
                {
                    var prod = kvp.Value;
                    if (prod.Piece == "---" || string.IsNullOrWhiteSpace(prod.Piece)) continue;

                    string safePiece = string.Join("_", prod.Piece.Split(Path.GetInvalidFileNameChars()));
                    string safeMoule = string.Join("_", prod.Moule.Split(Path.GetInvalidFileNameChars()));
                    string defectFile = Path.Combine(defDir, $"Defauts_{safePiece}_{safeMoule}.json");

                    var activeMachineItems = new List<DisplayItem>();

                    if (File.Exists(defectFile))
                    {
                        var history = JsonSerializer.Deserialize<List<DefectHistoryEntry>>(File.ReadAllText(defectFile), jsonOptions);
                        if (history != null)
                        {
                            var groupedDefects = history.GroupBy(d => d.Id);

                            foreach (var group in groupedDefects)
                            {
                                var latest = group.Last();
                                var first = group.First();

                                if (latest.Action != "Suppression" && (latest.Gravite == "AA" || latest.Gravite == "NC"))
                                {
                                    DateTime creationDate = DateTime.Now;
                                    if (DateTime.TryParseExact($"{first.Date} {first.Heure}", "dd/MM/yyyy HH:mm:ss",
                                        System.Globalization.CultureInfo.InvariantCulture,
                                        System.Globalization.DateTimeStyles.None, out DateTime parsedDate))
                                    {
                                        creationDate = parsedDate;
                                    }

                                    activeMachineItems.Add(new DisplayItem
                                    {
                                        IsHeader = false,
                                        State = latest.Gravite,
                                        DefectType = latest.TypeDefaut,
                                        CoreNumber = latest.NumeroNoyau,
                                        Comment = latest.Commentaire,
                                        CreationDate = creationDate,
                                        CreationDateStr = creationDate.ToString("dd/MM/yyyy à HH:mm")
                                    });
                                }
                            }
                        }
                    }

                    // On a trouvé des défauts pour cette machine, on les place sur les pages
                    if (activeMachineItems.Any())
                    {
                        var sortedDefects = activeMachineItems.OrderBy(x => x.State == "NC" ? 0 : 1).ThenBy(x => x.CreationDate).ToList();

                        string baseHeaderTitle = $"{prod.Machine}  |  Pièce : {prod.Piece}  |  Moule : {prod.Moule}";
                        var headerItem = new DisplayItem { IsHeader = true, MachineInfo = baseHeaderTitle };

                        // RÈGLE 1 : ANTI-ORPHELIN
                        // S'il reste 0 ou 1 place sur la page actuelle, on passe à la page suivante
                        if (currentPageList.Count >= _itemsPerPage - 1)
                        {
                            if (currentPageList.Count > 0) newPages.Add(currentPageList);
                            currentPageList = new List<DisplayItem>();
                        }

                        currentPageList.Add(headerItem);

                        foreach (var defect in sortedDefects)
                        {
                            // RÈGLE 2 : RAPPEL DE CONTEXTE
                            // Si la page est pleine, on coupe, on sauvegarde la page, et on remet le titre sur la nouvelle page
                            if (currentPageList.Count >= _itemsPerPage)
                            {
                                newPages.Add(currentPageList);
                                currentPageList = new List<DisplayItem>();
                                currentPageList.Add(new DisplayItem { IsHeader = true, MachineInfo = baseHeaderTitle + " (Suite)" });
                            }
                            currentPageList.Add(defect);
                        }
                    }
                }

                // Ne pas oublier d'ajouter la dernière page en cours de remplissage
                if (currentPageList.Count > 0)
                {
                    newPages.Add(currentPageList);
                }

                _allPages = newPages;

                // Sécurité : Si des défauts ont été corrigés et qu'il y a moins de pages qu'avant
                if (_currentPageIndex >= _allPages.Count) _currentPageIndex = 0;

                UpdatePageDisplay();

                LastUpdateText = $"Dernière synchro à {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                LastUpdateText = $"Problème de réseau à {DateTime.Now:HH:mm:ss}";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}