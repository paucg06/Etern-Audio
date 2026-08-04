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
            // lengthFilter: 0 = All, 1 = Short (<30s), 2 = Long (>=30s)
            IEnumerable<SfxFile> candidates;

            if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(query.Trim()))
            {
                candidates = fileById.Values;
            }
            else
            {
                string[] expanded = TagEngine.ExpandQuery(query);
                string rawNorm = TagEngine.NormalizeText(query.Trim());

                var scores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                foreach (var term in expanded)
                {
                    if (string.IsNullOrEmpty(term)) continue;

                    if (index.ContainsKey(term))
                    {
                        foreach (var id in index[term])
                        {
                            if (!scores.ContainsKey(id)) scores[id] = 0;
                            scores[id] += 10;
                        }
                    }

                    foreach (var kvp in index)
                    {
                        if (kvp.Key.Length < 2) continue;
                        int bonus = 0;
                        if (kvp.Key.StartsWith(term, StringComparison.OrdinalIgnoreCase))
                            bonus = 7;
                        else if (kvp.Key.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                            bonus = 4;
                        else if (term.Length >= 3 && term.StartsWith(kvp.Key, StringComparison.OrdinalIgnoreCase))
                            bonus = 5;

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
                    if (fnNorm.IndexOf(rawNorm, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (!scores.ContainsKey(f.Id)) scores[f.Id] = 0;
                        scores[f.Id] += 15;
                    }
                    else if (dnNorm.IndexOf(rawNorm, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (!scores.ContainsKey(f.Id)) scores[f.Id] = 0;
                        scores[f.Id] += 12;
                    }
                }

                if (scores.Count == 0)
                {
                    candidates = new List<SfxFile>();
                }
                else
                {
                    candidates = scores
                        .OrderByDescending(kvp => kvp.Value)
                        .ThenBy(kvp => fileById.ContainsKey(kvp.Key) ? fileById[kvp.Key].DisplayName : "")
                        .Where(kvp => fileById.ContainsKey(kvp.Key))
                        .Select(kvp => fileById[kvp.Key]);
                }
            }

            var result = candidates;

            // Apply category / folder filters
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
