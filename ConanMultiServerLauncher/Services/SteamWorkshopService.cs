using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ConanMultiServerLauncher.Services
{
    public static class SteamWorkshopService
    {
        private static readonly HttpClient Http = new HttpClient();
        private static readonly Regex AnyIdRegex = new("(?<!\\d)(\\d{6,12})(?!\\d)", RegexOptions.Compiled);
        private static readonly Regex CollectionUrlRegex = new(
            @"steamcommunity\\.com/(?:workshop/)?filedetails/\\?id=(\\d+)|steamcommunity\\.com/workshop/browse/\\?\\S*collection=(\\d+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static bool TryExtractCollectionId(string text, out long collectionId)
        {
            collectionId = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;

            var matches = CollectionUrlRegex.Matches(text);
            foreach (Match m in matches)
            {
                if (!m.Success) continue;
                for (int i = 1; i < m.Groups.Count; i++)
                {
                    var val = m.Groups[i]?.Value;
                    if (!string.IsNullOrEmpty(val) && long.TryParse(val, out var id))
                    {
                        collectionId = id;
                        return true;
                    }
                }
            }

            // Fallback: if text is just a number
            var idStr = AnyIdRegex.Matches(text).Cast<Match>().Select(x => x.Value).FirstOrDefault();
            return long.TryParse(idStr, out collectionId);
        }

        public static async Task<List<long>> GetCollectionChildrenAsync(long collectionId)
        {
            var url = "https://api.steampowered.com/ISteamRemoteStorage/GetCollectionDetails/v1/";
            using var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("collectioncount", "1"),
                new KeyValuePair<string, string>("publishedfileids[0]", collectionId.ToString())
            });

            using var resp = await Http.PostAsync(url, form).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement.GetProperty("response");
            var details = root.GetProperty("collectiondetails");
            if (details.GetArrayLength() == 0) return new List<long>();
            var first = details[0];
            if (!first.TryGetProperty("children", out var childrenElem)) return new List<long>();

            var children = childrenElem
                .EnumerateArray()
                .Select(e => e.GetProperty("publishedfileid").GetString())
                .Where(s => long.TryParse(s, out _))
                .Select(long.Parse)
                .Distinct()
                .ToList();

            return children;
        }

        public static async Task<List<long>> FilterToConanAsync(IEnumerable<long> ids)
        {
            var list = ids?.Distinct().ToList() ?? new();
            if (list.Count == 0) return list;

            var result = new List<long>();
            const int batch = 100;
            for (int i = 0; i < list.Count; i += batch)
            {
                var slice = list.Skip(i).Take(batch).ToList();
                var url = "https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/";
                var kv = new List<KeyValuePair<string, string>> { new("itemcount", slice.Count.ToString()) };
                for (int j = 0; j < slice.Count; j++)
                    kv.Add(new($"publishedfileids[{j}]", slice[j].ToString()));

                using var form = new FormUrlEncodedContent(kv);
                using var resp = await Http.PostAsync(url, form).ConfigureAwait(false);
                resp.EnsureSuccessStatusCode();
                var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                using var doc = JsonDocument.Parse(json);
                var details = doc.RootElement.GetProperty("response").GetProperty("publishedfiledetails").EnumerateArray();
                foreach (var d in details)
                {
                    var idStr = d.GetProperty("publishedfileid").GetString();
                    var appId = d.TryGetProperty("consumer_app_id", out var app) ? app.GetInt32() : 0;
                    if (appId == 440900 && long.TryParse(idStr, out var id))
                        result.Add(id);
                }
            }
            return result.Distinct().ToList();
        }
    }
}
