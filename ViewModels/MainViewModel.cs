using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Threading;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Eurocast_Top5_Viewer.Interfaces;
using Eurocast_Top5_Viewer.Models;
using Eurocast_Top5_Viewer.Services;

namespace Eurocast_Top5_Viewer.ViewModels
{
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        public RelayCommand(Action execute) { _execute = execute; }
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute();
    }

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
        public string MachineColor { get; set; } = "Transparent";
        public string State { get; set; } = "";
        public string CreationDateStr { get; set; } = "";
        public string DefectType { get; set; } = "";
        public string CoreNumber { get; set; } = "";
        public string Comment { get; set; } = "";
        public DateTime CreationDate { get; set; }
        public int CoreAlertCount { get; set; } = 0;
        public string CoreAlertText => CoreAlertCount > 1 ? $"⚠️ ALERTE : CASSÉ {CoreAlertCount} FOIS EN 7 JOURS" : "";

        private string _elapsedTime = "";
        public string ElapsedTime { get => _elapsedTime; set { _elapsedTime = value; OnPropertyChanged(); } }

        public string DefectDisplay => string.IsNullOrWhiteSpace(CoreNumber) ? DefectType : $"{DefectType} (Noyau {CoreNumber})";

        private string _backgroundColor = "Transparent";
        public string BackgroundColor { get => _backgroundColor; set { _backgroundColor = value; OnPropertyChanged(); } }

        private string _accentColor = "Transparent";
        public string AccentColor { get => _accentColor; set { _accentColor = value; OnPropertyChanged(); } }

        public string TextColor => "#F8FAFC";
        private bool _blinkToggle = false;

        public event PropertyChangedEventHandler? PropertyChanged;

        public void RefreshTime()
        {
            if (IsHeader) { BackgroundColor = "Transparent"; AccentColor = "Transparent"; return; }

            var delta = DateTime.Now - CreationDate;

            if (State == "B") ElapsedTime = "VALIDÉ";
            else ElapsedTime = delta.TotalDays >= 1 ? $"{(int)delta.TotalDays}j {delta.Hours:D2}h" : $"{delta.Hours}h {delta.Minutes:D2}m";

            // LOGIQUE MISE À JOUR : Clignotement si < 30 minutes pour "NC" (Non conforme) et "AA" (À améliorer)
            if ((State == "NC" || State == "AA") && delta.TotalMinutes < 30)
            {
                _blinkToggle = !_blinkToggle;

                if (State == "NC")
                {
                    // Clignotement d'alerte ROUGE pour Non conforme
                    BackgroundColor = _blinkToggle ? "#7F1D1D" : "#0A3B66";
                    AccentColor = _blinkToggle ? "Transparent" : "#E53935";
                }
                else // State == "AA"
                {
                    // Clignotement d'alerte ORANGE/AMBRE pour À améliorer
                    BackgroundColor = _blinkToggle ? "#78350F" : "#0A3B66";
                    AccentColor = _blinkToggle ? "Transparent" : "#FFB300";
                }
            }
            else
            {
                // Couleurs par défaut (sans clignotement)
                BackgroundColor = "#0A3B66";
                AccentColor = State == "NC" ? "#E53935" : (State == "AA" ? "#FFB300" : (State == "B" ? "#10B981" : "Transparent"));
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string name = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly IQualityService _qualityService;
        public ObservableCollection<DisplayItem> DashboardItems { get; set; } = new ObservableCollection<DisplayItem>();
        private List<List<DisplayItem>> _allPages = new List<List<DisplayItem>>();
        private List<string> _flashUrls = new List<string>();

        public List<string> DisplayModes { get; } = new List<string>
        {
            "Afficher l'ensemble des informations",
            "Afficher uniquement les défauts pièces",
            "Afficher uniquement les Flash Qualité"
        };

        private int _selectedDisplayMode = 0;
        public int SelectedDisplayMode
        {
            get => _selectedDisplayMode;
            set
            {
                if (_selectedDisplayMode != value)
                {
                    _selectedDisplayMode = value;
                    OnPropertyChanged();
                    ApplyDisplayMode();
                }
            }
        }

        public string LastUpdateText { get => _lastUpdateText; set { _lastUpdateText = value; OnPropertyChanged(); } }
        private string _lastUpdateText = "";

        public string PageIndicatorText { get => _pageIndicatorText; set { _pageIndicatorText = value; OnPropertyChanged(); } }
        private string _pageIndicatorText = "";

        private bool _isFlashVisible;
        public bool IsFlashVisible { get => _isFlashVisible; set { _isFlashVisible = value; OnPropertyChanged(); } }

        private bool _isDefectsVisible = true;
        public bool IsDefectsVisible { get => _isDefectsVisible; set { _isDefectsVisible = value; OnPropertyChanged(); } }

        public bool IsWindowed { get => _isWindowed; set { _isWindowed = value; OnPropertyChanged(); } }
        private bool _isWindowed = true;

        public bool IsPaused { get => _isPaused; set { _isPaused = value; OnPropertyChanged(); OnPropertyChanged(nameof(PausePlayText)); } }
        private bool _isPaused = false;
        public string PausePlayText => IsPaused ? "▶ LECTURE" : "⏸ PAUSE";

        public ICommand TogglePauseCommand { get; }
        public ICommand NextPageCommand { get; }
        public ICommand PrevPageCommand { get; }

        public FlashDetail? CurrentFlashDetail { get => _currentFlashDetail; set { _currentFlashDetail = value; OnPropertyChanged(); } }
        private FlashDetail? _currentFlashDetail;

        public event EventHandler? FlashAppeared;

        private DispatcherTimer _dataTimer;
        private DispatcherTimer _clockTimer;
        private DispatcherTimer _pagingTimer;

        private int _currentPageIndex = 0;
        private int _currentFlashIndex = 0;
        private readonly int _itemsPerPage = 10;
        private readonly string _basePath = @"P:\Delle\Pot Commun\Informatique\NE PAS SUPPRIMER - TOP5_QUALITE\data";

        private readonly string[] _machineColorPalette = new string[] { "#00E5FF", "#D500F9", "#FFEA00", "#00B0FF", "#F8FAFC", "#76FF03", "#FF9100", "#F50057", "#1DE9B6", "#651FFF", "#FF3D00", "#C6FF00" };

        public MainViewModel()
        {
            _qualityService = new QualityService();
            TogglePauseCommand = new RelayCommand(() => IsPaused = !IsPaused);
            NextPageCommand = new RelayCommand(async () => { await NextPageAsync(); RestartTimer(); });
            PrevPageCommand = new RelayCommand(async () => { await PrevPageAsync(); RestartTimer(); });

            _dataTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
            _pagingTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };

            LoadData();
            _ = LoadFlashesAsync();

            _dataTimer.Tick += async (s, e) => { LoadData(); await LoadFlashesAsync(); };
            _dataTimer.Start();

            _pagingTimer.Tick += async (s, e) => { if (!IsPaused) await NextPageAsync(); };
            _pagingTimer.Start();

            _clockTimer.Tick += (s, e) => { foreach (var item in DashboardItems) item.RefreshTime(); };
            _clockTimer.Start();
        }

        private void RestartTimer()
        {
            if (_pagingTimer != null)
            {
                _pagingTimer.Stop();
                _pagingTimer.Start();
            }
        }

        private async Task LoadFlashesAsync()
        {
            try
            {
                var urls = await _qualityService.GetRecentFlashUrlsAsync();
                _flashUrls = urls ?? new List<string>();
            }
            catch
            {
                _flashUrls = new List<string>();
            }
        }

        private async void ApplyDisplayMode()
        {
            _currentPageIndex = 0;
            _currentFlashIndex = 0;

            if (SelectedDisplayMode == 1)
            {
                IsFlashVisible = false;
                IsDefectsVisible = true;
                UpdatePageDisplay();
            }
            else if (SelectedDisplayMode == 2)
            {
                IsDefectsVisible = false;
                IsFlashVisible = true;
                if (_flashUrls != null && _flashUrls.Count > 0) await ShowFlashAsync(0);
                else PageIndicatorText = "AUCUN FLASH QUALITÉ";
            }
            else
            {
                IsFlashVisible = false;
                IsDefectsVisible = true;
                UpdatePageDisplay();
            }
            RestartTimer();
        }

        private async Task NextPageAsync()
        {
            if (_flashUrls == null) _flashUrls = new List<string>();
            if (_allPages == null) _allPages = new List<List<DisplayItem>>();

            if (SelectedDisplayMode == 1)
            {
                _currentPageIndex++;
                if (_currentPageIndex >= _allPages.Count) _currentPageIndex = 0;
                UpdatePageDisplay();
                return;
            }

            if (SelectedDisplayMode == 2)
            {
                _currentFlashIndex++;
                if (_currentFlashIndex >= _flashUrls.Count) _currentFlashIndex = 0;
                if (_flashUrls.Count > 0) await ShowFlashAsync(_currentFlashIndex);
                return;
            }

            if (IsFlashVisible)
            {
                _currentFlashIndex++;
                if (_currentFlashIndex >= _flashUrls.Count) { IsFlashVisible = false; IsDefectsVisible = true; _currentPageIndex = 0; UpdatePageDisplay(); }
                else await ShowFlashAsync(_currentFlashIndex);
            }
            else
            {
                _currentPageIndex++;
                if (_currentPageIndex >= _allPages.Count || _allPages.Count == 0)
                {
                    if (_flashUrls.Count > 0) { IsFlashVisible = true; IsDefectsVisible = false; _currentFlashIndex = 0; await ShowFlashAsync(_currentFlashIndex); }
                    else { _currentPageIndex = 0; UpdatePageDisplay(); }
                }
                else UpdatePageDisplay();
            }
        }

        private async Task PrevPageAsync()
        {
            if (_flashUrls == null) _flashUrls = new List<string>();
            if (_allPages == null) _allPages = new List<List<DisplayItem>>();

            if (SelectedDisplayMode == 1)
            {
                _currentPageIndex--;
                if (_currentPageIndex < 0) _currentPageIndex = _allPages.Count > 0 ? _allPages.Count - 1 : 0;
                UpdatePageDisplay();
                return;
            }

            if (SelectedDisplayMode == 2)
            {
                _currentFlashIndex--;
                if (_currentFlashIndex < 0) _currentFlashIndex = _flashUrls.Count > 0 ? _flashUrls.Count - 1 : 0;
                if (_flashUrls.Count > 0) await ShowFlashAsync(_currentFlashIndex);
                return;
            }

            if (IsFlashVisible)
            {
                _currentFlashIndex--;
                if (_currentFlashIndex < 0)
                {
                    IsFlashVisible = false;
                    IsDefectsVisible = true;
                    if (_allPages.Count > 0) { _currentPageIndex = _allPages.Count - 1; UpdatePageDisplay(); }
                    else if (_flashUrls.Count > 0) { IsFlashVisible = true; IsDefectsVisible = false; _currentFlashIndex = _flashUrls.Count - 1; await ShowFlashAsync(_currentFlashIndex); }
                }
                else await ShowFlashAsync(_currentFlashIndex);
            }
            else
            {
                _currentPageIndex--;
                if (_currentPageIndex < 0)
                {
                    if (_flashUrls.Count > 0) { IsFlashVisible = true; IsDefectsVisible = false; _currentFlashIndex = _flashUrls.Count - 1; await ShowFlashAsync(_currentFlashIndex); }
                    else if (_allPages.Count > 0) { _currentPageIndex = _allPages.Count - 1; UpdatePageDisplay(); }
                    else _currentPageIndex = 0;
                }
                else UpdatePageDisplay();
            }
        }

        private async Task ShowFlashAsync(int index)
        {
            if (_flashUrls == null) _flashUrls = new List<string>();
            if (index < 0 || index >= _flashUrls.Count) return;

            if (_pagingTimer != null)
            {
                _pagingTimer.Interval = TimeSpan.FromSeconds(25);
            }

            var url = _flashUrls[index];
            CurrentFlashDetail = await _qualityService.GetFlashDetailAsync(url);
            PageIndicatorText = $"FLASH QUALITÉ {index + 1}/{_flashUrls.Count}";

            FlashAppeared?.Invoke(this, EventArgs.Empty);
        }

        private void UpdatePageDisplay()
        {
            if (_pagingTimer != null)
            {
                _pagingTimer.Interval = TimeSpan.FromSeconds(15);
            }

            if (_flashUrls == null) _flashUrls = new List<string>();
            if (_allPages == null) _allPages = new List<List<DisplayItem>>();
            if (DashboardItems == null) DashboardItems = new ObservableCollection<DisplayItem>();

            if (_allPages.Count <= 1 && _flashUrls.Count == 0) PageIndicatorText = "";
            else if (_allPages.Count > 0) PageIndicatorText = $"PAGE {_currentPageIndex + 1}/{_allPages.Count}";

            DashboardItems.Clear();

            if (_currentPageIndex >= 0 && _currentPageIndex < _allPages.Count)
            {
                var currentPage = _allPages[_currentPageIndex];
                if (currentPage != null)
                {
                    foreach (var item in currentPage)
                    {
                        DashboardItems.Add(item);
                    }
                }
            }
        }

        private void LoadData()
        {
            try
            {
                string prodPath = Path.Combine(_basePath, "historique_productions.json");
                string defDir = Path.Combine(_basePath, "HistoriqueDefauts");
                if (!File.Exists(prodPath)) return;

                var prodHistory = JsonSerializer.Deserialize<List<ProductionHistoryEntry>>(File.ReadAllText(prodPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<ProductionHistoryEntry>();
                if (!prodHistory.Any()) return;

                var activeProds = prodHistory.GroupBy(h => h.Machine).ToDictionary(g => g.Key, g => g.OrderByDescending(h => h.Timestamp).First());
                var newPages = new List<List<DisplayItem>>();
                var currentPageList = new List<DisplayItem>();
                var machineColors = new Dictionary<string, string>();
                int colorIndex = 0;

                foreach (var prod in activeProds.Values.Where(p => p.Piece != "---"))
                {
                    if (!machineColors.ContainsKey(prod.Machine)) { machineColors[prod.Machine] = _machineColorPalette[colorIndex % _machineColorPalette.Length]; colorIndex++; }
                    string currentMachineColor = machineColors[prod.Machine];
                    string safePiece = string.Join("_", prod.Piece.Split(Path.GetInvalidFileNameChars()));
                    string safeMoule = string.Join("_", prod.Moule.Split(Path.GetInvalidFileNameChars()));
                    string defectFile = Path.Combine(defDir, $"Defauts_{safePiece}_{safeMoule}.json");

                    if (File.Exists(defectFile))
                    {
                        var history = JsonSerializer.Deserialize<List<DefectHistoryEntry>>(File.ReadAllText(defectFile), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<DefectHistoryEntry>();
                        var activeMachineItems = new List<DisplayItem>();
                        if (history.Any())
                        {
                            var groupedDefects = history.GroupBy(d => d.Id).ToList();
                            var coreAlertCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                            foreach (var group in groupedDefects)
                            {
                                var latest = group.Last(); var first = group.First();
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
                            foreach (var group in groupedDefects)
                            {
                                var latest = group.Last(); var first = group.First();
                                DateTime.TryParseExact($"{latest.Date} {latest.Heure}", "dd/MM/yyyy HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime lastActionDate);
                                bool isActive = latest.Gravite == "AA" || latest.Gravite == "NC";
                                if (latest.Action != "Suppression" && (isActive || (latest.Gravite == "B" && (DateTime.Now - lastActionDate).TotalHours <= 1)))
                                {
                                    DateTime.TryParseExact($"{first.Date} {first.Heure}", "dd/MM/yyyy HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime cDate);
                                    int alertCount = 0; string coreTrimmed = latest.NumeroNoyau?.Trim() ?? "";
                                    if (!string.IsNullOrEmpty(coreTrimmed) && coreAlertCounts.ContainsKey(coreTrimmed)) alertCount = coreAlertCounts[coreTrimmed];
                                    var item = new DisplayItem { State = latest.Gravite, DefectType = latest.TypeDefaut, CoreNumber = latest.NumeroNoyau, Comment = latest.Commentaire, CreationDate = cDate, CreationDateStr = cDate.ToString("dd/MM HH:mm"), CoreAlertCount = alertCount };
                                    item.RefreshTime(); activeMachineItems.Add(item);
                                }
                            }
                        }
                        if (activeMachineItems.Any())
                        {
                            var sortedDefects = activeMachineItems.OrderBy(x => x.State == "NC" ? 0 : (x.State == "AA" ? 1 : 2)).ThenBy(x => x.CreationDate).ToList();
                            string baseHeaderTitle = $"{prod.Machine} ▸ {prod.Piece} ({prod.Moule})";
                            if (currentPageList.Count + 1 + sortedDefects.Count <= _itemsPerPage)
                            {
                                currentPageList.Add(new DisplayItem { IsHeader = true, MachineInfo = baseHeaderTitle, MachineColor = currentMachineColor });
                                currentPageList.AddRange(sortedDefects);
                            }
                            else
                            {
                                if (currentPageList.Count > 0) { newPages.Add(currentPageList); currentPageList = new List<DisplayItem>(); }
                                currentPageList.Add(new DisplayItem { IsHeader = true, MachineInfo = baseHeaderTitle, MachineColor = currentMachineColor });
                                foreach (var defect in sortedDefects)
                                {
                                    if (currentPageList.Count >= _itemsPerPage) { newPages.Add(currentPageList); currentPageList = new List<DisplayItem>(); currentPageList.Add(new DisplayItem { IsHeader = true, MachineInfo = baseHeaderTitle + " (Suite)", MachineColor = currentMachineColor }); }
                                    currentPageList.Add(defect);
                                }
                            }
                        }
                    }
                }
                if (currentPageList.Any()) newPages.Add(currentPageList);
                _allPages = newPages;

                if (SelectedDisplayMode == 0 || SelectedDisplayMode == 1)
                {
                    if (!IsFlashVisible && _currentPageIndex >= _allPages.Count) _currentPageIndex = 0;
                    if (!IsFlashVisible) UpdatePageDisplay();
                }

                LastUpdateText = $"SYNC: {DateTime.Now:HH:mm}";
            }
            catch
            {
                LastUpdateText = "ERREUR RÉSEAU";
                if (_allPages == null) _allPages = new List<List<DisplayItem>>();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}