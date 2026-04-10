using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Eurocast_Top5_Viewer.Interfaces;
using Eurocast_Top5_Viewer.Models;

namespace Eurocast_Top5_Viewer.Services
{
    public class QualityService : IQualityService
    {
        private readonly string _baseUrl = "http://10.6.2.3";
        private readonly string _listUrl = "http://10.6.2.3/cgi-bin/Qualite.exe/ListeFlash?IDCLIENT=-1&IDREFERENCE=-1&ANNEE=2026&ORIGINEINCIDENT=-1";
        private readonly HttpClient _httpClient;

        public QualityService()
        {
            _httpClient = new HttpClient();
            // Indispensable sous .NET Core / 5+ pour supporter les anciens encodages comme ISO-8859-1
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public async Task<List<string>> GetRecentFlashUrlsAsync()
        {
            var validUrls = new List<string>();
            var currentDate = DateTime.Now;
            var thresholdDate = currentDate.AddMonths(-1);

            try
            {
                // Lecture forcée en ISO-8859-1 pour éviter les problèmes d'accents
                var response = await _httpClient.GetAsync(_listUrl);
                var bytes = await response.Content.ReadAsByteArrayAsync();
                string htmlContent = Encoding.GetEncoding("iso-8859-1").GetString(bytes);

                var tbodyMatch = Regex.Match(htmlContent, @"<tbody>(.*?)</tbody>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (!tbodyMatch.Success) return validUrls;

                var rowRegex = new Regex(@"<tr[^>]*>(.*?)</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                var rowMatches = rowRegex.Matches(tbodyMatch.Groups[1].Value);

                foreach (Match rowMatch in rowMatches)
                {
                    string rowHtml = rowMatch.Groups[1].Value;
                    var colRegex = new Regex(@"<td[^>]*>(.*?)</td>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                    var colMatches = colRegex.Matches(rowHtml);

                    if (colMatches.Count >= 5)
                    {
                        string idColHtml = colMatches[0].Groups[1].Value;
                        string dateColHtml = colMatches[4].Groups[1].Value;

                        string dateText = Regex.Replace(dateColHtml, @"<[^>]+>|&nbsp;", "").Trim();
                        var urlMatch = Regex.Match(idColHtml, @"href=""([^""]+)""", RegexOptions.IgnoreCase);

                        if (urlMatch.Success && DateTime.TryParseExact(dateText, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime flashDate))
                        {
                            if (flashDate >= thresholdDate && flashDate <= currentDate)
                            {
                                string fullUrl = _baseUrl + urlMatch.Groups[1].Value;
                                validUrls.Add(fullUrl);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.Log($"Erreur GetRecentFlashUrlsAsync : {ex.Message}");
            }

            return validUrls;
        }

        public async Task<FlashDetail?> GetFlashDetailAsync(string url)
        {
            try
            {
                // Lecture avec le bon encodage
                var response = await _httpClient.GetAsync(url);
                var bytes = await response.Content.ReadAsByteArrayAsync();
                string htmlContent = Encoding.GetEncoding("iso-8859-1").GetString(bytes);

                var detail = new FlashDetail();

                // Regex assouplie pour contourner les &nbsp; et les sauts de ligne imprévisibles
                var titleMatch = Regex.Match(htmlContent, @"FLASH QUALITE N°\s*(\d+)[^0-9]+([0-9/]+)[^\(]*\((.*?)\)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (titleMatch.Success)
                {
                    detail.Id = titleMatch.Groups[1].Value.Trim();
                    detail.DateStr = titleMatch.Groups[2].Value.Trim();
                    detail.Origine = WebUtility.HtmlDecode(titleMatch.Groups[3].Value).Trim();
                }

                var clientMatch = Regex.Match(htmlContent, @"CLIENT\s*:\s*(.*?)<\/div>", RegexOptions.IgnoreCase);
                if (clientMatch.Success) detail.Client = CleanHtmlText(clientMatch.Groups[1].Value);

                var refMatch = Regex.Match(htmlContent, @"REFERENCE\s*:\s*(.*?)<\/div>", RegexOptions.IgnoreCase);
                if (refMatch.Success) detail.Reference = CleanHtmlText(refMatch.Groups[1].Value);

                var desigMatch = Regex.Match(htmlContent, @"<td width=""33%""><div align=""center"">(.*?)<\/div>", RegexOptions.IgnoreCase);
                if (desigMatch.Success)
                {
                    string rawDesig = CleanHtmlText(desigMatch.Groups[1].Value);
                    // Suppression de la redondance "DESIGNATION :" si elle est déjà dans le texte source
                    detail.Designation = Regex.Replace(rawDesig, @"^DESIGNATION\s*:\s*", "", RegexOptions.IgnoreCase);
                }

                detail.LibelleDefaut = ExtractTextBlock(htmlContent, @"Lib&eacute;ll&eacute; du d&eacute;faut.*?<span[^>]*>(.*?)<\/span>");
                detail.Consequences = ExtractTextBlock(htmlContent, @"Cons&eacute;quences.*?<span[^>]*>(.*?)<\/span>");
                detail.PlanReaction = ExtractTextBlock(htmlContent, @"Plan de r&eacute;action.*?<span[^>]*>(.*?)<\/span>");

                var imgRefMatch = Regex.Match(htmlContent, @"<img src=""(/outillage/[^""]+)""", RegexOptions.IgnoreCase);
                if (imgRefMatch.Success) detail.RefImageUrl = _baseUrl + imgRefMatch.Groups[1].Value;

                var imgVueMatch = Regex.Match(htmlContent, @"<img src=""(/Qualite/[^""]+Vue[^""]+)""", RegexOptions.IgnoreCase);
                if (imgVueMatch.Success) detail.DefectImageUrl = _baseUrl + imgVueMatch.Groups[1].Value;

                return detail;
            }
            catch (Exception ex)
            {
                LoggerService.Log($"Erreur GetFlashDetailAsync : {ex.Message}");
                return null;
            }
        }

        private string ExtractTextBlock(string html, string pattern)
        {
            var match = Regex.Match(html, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (match.Success) return CleanHtmlText(match.Groups[1].Value);
            return string.Empty;
        }

        private string CleanHtmlText(string rawHtml)
        {
            string clean = Regex.Replace(rawHtml, @"<[^>]+>", ""); // Supprime les balises (ex: <h1>)
            clean = Regex.Replace(clean, @"&nbsp;", " "); // Remplace les espaces insécables
            return WebUtility.HtmlDecode(clean).Trim(); // Décode les entités et nettoie les bords
        }
    }
}