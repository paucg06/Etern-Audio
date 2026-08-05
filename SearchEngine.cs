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

                string catKey = TagEngine.NormalizeText(f.Category);
                if (!index.ContainsKey(catKey)) index[catKey] = new HashSet<string>();
                index[catKey].Add(f.Id);

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

            if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(query.Trim()))
            {
                foreach (var f in fileById.Values)
                    f.MatchScore = 10.0;
                candidates = fileById.Values;
            }
            else
            {
                string[] expanded = TagEngine.ExpandQuery(query);
                string rawNorm = TagEngine.NormalizeText(query.Trim());

                var scores = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

                foreach (var term in expanded)
                {
                    if (string.IsNullOrEmpty(term)) continue;

                    if (index.ContainsKey(term))
                    {
                        foreach (var id in index[term])
                        {
                            if (!scores.ContainsKey(id)) scores[id] = 0;
                            scores[id] += 8.0;
                        }
                    }

                    foreach (var kvp in index)
                    {
                        if (kvp.Key.Length < 2) continue;
                        double bonus = 0;
                        if (kvp.Key.Equals(term, StringComparison.OrdinalIgnoreCase))
                            bonus = 9.5;
                        else if (kvp.Key.StartsWith(term, StringComparison.OrdinalIgnoreCase))
                            bonus = 7.5;
                        else if (kvp.Key.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                            bonus = 5.0;

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

                foreach (var f in fileById.Values)
                {
                    string fnNorm = TagEngine.NormalizeText(f.FileName);
                    string dnNorm = TagEngine.NormalizeText(f.DisplayName);

                    if (dnNorm.Equals(rawNorm, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!scores.ContainsKey(f.Id)) scores[f.Id] = 0;
                        scores[f.Id] += 15.0;
                    }
                    else if (fnNorm.IndexOf(rawNorm, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (!scores.ContainsKey(f.Id)) scores[f.Id] = 0;
                        scores[f.Id] += 12.0;
                    }
                    else if (dnNorm.IndexOf(rawNorm, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (!scores.ContainsKey(f.Id)) scores[f.Id] = 0;
                        scores[f.Id] += 10.0;
                    }
                }

                if (scores.Count == 0)
                {
                    candidates = new List<SfxFile>();
                }
                else
                {
                    double maxRaw = scores.Values.Max();

                    foreach (var kvp in scores)
                    {
                        if (fileById.ContainsKey(kvp.Key))
                        {
                            double normalizedScore = Math.Min(10.0, Math.Max(1.0, Math.Round((kvp.Value / maxRaw) * 10.0, 1)));
                            fileById[kvp.Key].MatchScore = normalizedScore;
                        }
                    }

                    candidates = scores
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

            if (!string.IsNullOrEmpty(folderPathFilter))
            {
                result = result.Where(f => f.FilePath.StartsWith(folderPathFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (lengthFilter == 1)
                result = result.Where(f => f.IsShortSfx);
            else if (lengthFilter == 2)
                result = result.Where(f => !f.IsShortSfx);

            if (favoritesOnly)
                result = result.Where(f => f.IsFavorite);

            if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(query.Trim()))
                result = result.OrderBy(f => f.DisplayName);

            return result.ToList();
        }
    }
}
