using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace EternAudio
{
    public class SearchEngine
    {
        private Dictionary<string, HashSet<string>> index =
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        private Dictionary<string, SfxFile> fileById =
            new Dictionary<string, SfxFile>(StringComparer.OrdinalIgnoreCase);

        public int TotalFiles { get { return fileById.Count; } }

        public void BuildIndex(List<SfxFile> files)
        {
            index.Clear();
            fileById.Clear();

            foreach (var f in files)
            {
                fileById[f.Id] = f;

                foreach (var tag in f.Tags)
                {
                    string key = TagEngine.NormalizeText(tag);
                    if (!index.ContainsKey(key)) index[key] = new HashSet<string>();
                    index[key].Add(f.Id);
                }

                foreach (var tok in TagEngine.TokenizeFilename(f.FileName))
                {
                    if (!index.ContainsKey(tok)) index[tok] = new HashSet<string>();
                    index[tok].Add(f.Id);
                }

                if (!string.IsNullOrEmpty(f.OriginalRawName))
                {
                    foreach (var tok in TagEngine.TokenizeFilename(f.OriginalRawName))
                    {
                        if (!index.ContainsKey(tok)) index[tok] = new HashSet<string>();
                        index[tok].Add(f.Id);
                    }
                }

                string catKey = TagEngine.NormalizeText(f.Category);
                if (!index.ContainsKey(catKey)) index[catKey] = new HashSet<string>();
                index[catKey].Add(f.Id);

                string subCatKey = TagEngine.NormalizeText(f.SubCategory);
                if (!index.ContainsKey(subCatKey)) index[subCatKey] = new HashSet<string>();
                index[subCatKey].Add(f.Id);

                var dnTokens = f.DisplayName.Split(new char[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var tok in dnTokens)
                {
                    string k = TagEngine.NormalizeText(tok);
                    if (k.Length < 2) continue;
                    if (!index.ContainsKey(k)) index[k] = new HashSet<string>();
                    index[k].Add(f.Id);
                }
            }
        }

        public List<SfxFile> Search(string query, string categoryFilter, bool favoritesOnly, string folderPathFilter, int lengthFilter = 0)
        {
            IEnumerable<SfxFile> candidates;
            bool isGlobalSearchQuery = !string.IsNullOrEmpty(query) && !string.IsNullOrEmpty(query.Trim());

            if (!isGlobalSearchQuery)
            {
                foreach (var f in fileById.Values)
                    f.MatchScore = 10.0;
                candidates = fileById.Values;
            }
            else
            {
                string rawNorm = TagEngine.NormalizeText(query.Trim());
                string[] expanded = TagEngine.ExpandQuery(rawNorm);

                var scores = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

                foreach (var term in expanded)
                {
                    if (string.IsNullOrEmpty(term)) continue;

                    // Direct index hit
                    if (index.ContainsKey(term))
                    {
                        foreach (var id in index[term])
                        {
                            if (!scores.ContainsKey(id)) scores[id] = 0;
                            scores[id] += 10.0;
                        }
                    }

                    // Partial / Substring hits across index
                    foreach (var kvp in index)
                    {
                        if (kvp.Key.Length < 2) continue;
                        double bonus = 0;
                        if (kvp.Key.Equals(term, StringComparison.OrdinalIgnoreCase))
                            bonus = 9.5;
                        else if (kvp.Key.StartsWith(term, StringComparison.OrdinalIgnoreCase))
                            bonus = 7.5;
                        else if (kvp.Key.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 term.IndexOf(kvp.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                            bonus = 5.5;

                        if (bonus > 0)
                        {
                            foreach (var id in kvp.Value)
                            {
                                if (!scores.ContainsKey(id)) scores[id] = 0;
                                scores[id] += bonus;
                            }
                        }
                    }
                }

                // Title & FilePath Vector Bonus
                foreach (var f in fileById.Values)
                {
                    string fnNorm = TagEngine.NormalizeText(f.FileName);
                    string dnNorm = TagEngine.NormalizeText(f.DisplayName);
                    string rawNormFile = TagEngine.NormalizeText(f.OriginalRawName ?? "");
                    string fpNorm = TagEngine.NormalizeText(f.FilePath);

                    if (dnNorm.Equals(rawNorm, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!scores.ContainsKey(f.Id)) scores[f.Id] = 0;
                        scores[f.Id] += 20.0;
                    }
                    else if (fnNorm.IndexOf(rawNorm, StringComparison.OrdinalIgnoreCase) >= 0 ||
                             dnNorm.IndexOf(rawNorm, StringComparison.OrdinalIgnoreCase) >= 0 ||
                             rawNormFile.IndexOf(rawNorm, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (!scores.ContainsKey(f.Id)) scores[f.Id] = 0;
                        scores[f.Id] += 15.0;
                    }
                    else if (fpNorm.IndexOf(rawNorm, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (!scores.ContainsKey(f.Id)) scores[f.Id] = 0;
                        scores[f.Id] += 8.0;
                    }
                }

                // Filter out zero / noise scores (must have score >= 4.0 to be included)
                var validScores = scores.Where(kvp => kvp.Value >= 4.0).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                if (validScores.Count == 0)
                {
                    candidates = new List<SfxFile>();
                }
                else
                {
                    double maxRaw = validScores.Values.Max();

                    foreach (var kvp in validScores)
                    {
                        if (fileById.ContainsKey(kvp.Key))
                        {
                            double normalizedScore = Math.Min(10.0, Math.Max(1.0, Math.Round((kvp.Value / maxRaw) * 10.0, 1)));
                            fileById[kvp.Key].MatchScore = normalizedScore;
                        }
                    }

                    candidates = validScores
                        .OrderByDescending(kvp => kvp.Value)
                        .Where(kvp => fileById.ContainsKey(kvp.Key))
                        .Select(kvp => fileById[kvp.Key]);
                }
            }

            var result = candidates;

            if (!string.IsNullOrEmpty(categoryFilter) && categoryFilter != "Todos los audios")
            {
                if (categoryFilter == "EFX / Cortos (<30s)")
                    result = result.Where(f => f.IsShortSfx);
                else if (categoryFilter == "Música / Largos (>=30s)")
                    result = result.Where(f => !f.IsShortSfx);
                else
                    result = result.Where(f => f.Category.IndexOf(categoryFilter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                               f.SubCategory.IndexOf(categoryFilter, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            if (!isGlobalSearchQuery && !string.IsNullOrEmpty(folderPathFilter))
            {
                result = result.Where(f => f.FilePath.StartsWith(folderPathFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (lengthFilter == 1)
                result = result.Where(f => f.IsShortSfx);
            else if (lengthFilter == 2)
                result = result.Where(f => !f.IsShortSfx);

            if (favoritesOnly)
                result = result.Where(f => f.IsFavorite);

            if (!isGlobalSearchQuery)
                result = result.OrderBy(f => f.DisplayName);

            return result.ToList();
        }
    }
}
