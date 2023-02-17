using Microsoft.Azure.Cosmos.Table;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Pokebook
{
    public class TimeZoneHelper
    {
        private static readonly TimeZoneInfo _aest = TimeZoneInfo.FindSystemTimeZoneById("AUS Eastern Standard Time");

        public static DateTime ConvertToUtc(DateTime aestDateTime) => TimeZoneInfo.ConvertTimeToUtc(aestDateTime, _aest);
        public static DateTime ConvertToAest(DateTime utcDateTime)
        {
            return (utcDateTime.Kind != DateTimeKind.Utc) ? TimeZoneInfo.ConvertTime(utcDateTime, _aest) : TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, _aest);
        }
    }

    public class AnimeHelper
    {
        public class AnimeItem
        {
            public int AnimeId { get; set; }
            public string KaaAnimeId { get; set; }
            public string Title { get; set; }
            public string TitleCode { get; set; }
            public string UploadWeekday { get; set; }
        }

        public class AnimeEpisodeItem
        {
            public int AnimeEpisodeId { get; set; }
            public int AnimeId { get; set; }
            public string SeasonEpisode { get; set; }
            public string UrlSlug { get; set; }
            public DateTime DateTimeUploaded { get; set; }
        }

        public class WatchedEpisodeItem
        {
            public int WacthedEpisodeId { get; set; }
            public int AnimeEpisodeId { get; set; }
            public string Username { get; set; }
        }

        public class CurrentlyWatchingAnimeItem
        {
            public int CurrentlyWatchingAnimeId { get; set; }
            public int AnimeId { get; set; }
            public string Username { get; set; }
        }

        private static readonly string sqlConnString = Environment.GetEnvironmentVariable("AnimeDbConnString");
        private static readonly string kickassAnimeBaseUrl = $"https://www2.kickassanime.ro";
        private static readonly List<string> sqlTableName = new List<string>() { "Anime", "AnimeEpisode", "WatchedEpisode", "CurrentlyWatchingAnime" };

        private static DataTable QuerySql(string query)
        {
            //Console.WriteLine(query);
            DataTable dataTable = new();
            using SqlConnection sqlConnection = new(sqlConnString);
            using SqlCommand sqlCommand = sqlConnection.CreateCommand();
            sqlCommand.CommandText = query;
            sqlCommand.CommandType = CommandType.Text;
            sqlConnection.Open();
            var rows_returned = (new SqlDataAdapter(sqlCommand)).Fill(dataTable);
            sqlConnection.Close();
            return dataTable;
        }
       

        public static async Task SetUpDatabaseTables()
        {
            var queryTemplate = "IF EXISTS (SELECT * FROM information_schema.tables WHERE table_name = '{{TABLENAME}}') SELECT 1 ELSE SELECT 0";
            var primaryColumns = new Dictionary<string, string>();
            
            foreach (var tableName in sqlTableName)
            {
                if (QuerySql(queryTemplate.Replace("{{TABLENAME}}", tableName)).AsEnumerable().First().ItemArray[0].ToString() != "1")
                {
                    var foreignKeyColumns = new List<string>();
                    var createQuery = "SET ANSI_NULLS ON\nGO\nSET QUOTED_IDENTIFIER ON\nGO\n";
                    createQuery += $"CREATE TABLE [dbo].[{tableName}](\n";

                    var classType = Type.GetType($"Pokebook.AnimeHelper+{tableName}Item");
                    var primary = !primaryColumns.ContainsValue(tableName);
                    foreach (var property in classType.GetProperties())
                    {
                        var properyName = property.Name;
                        if (primaryColumns.ContainsKey(properyName)) foreignKeyColumns.Add(properyName);
                        var propertyType = property.PropertyType.Name switch
                        {
                            "Int32" => "[int]",
                            "String" => "[varchar](4096)",
                            "DateTime" => "[datetime]",
                            _ => throw new NotImplementedException()
                        };
                        createQuery += $"{(!primary ? ",\n" : "")}\t[{properyName}] {propertyType} {(primary ? "IDENTITY(1,1)" : "")}{(propertyType == "[datetime]" ? "" :" NOT NULL")}";
                        if (primary) primaryColumns.Add(properyName, tableName);
                        primary = false;
                    }
                    createQuery += "\n) ON [PRIMARY]\nGO\n";
                    createQuery += $"ALTER TABLE [dbo].[{tableName}] ADD PRIMARY KEY CLUSTERED \n(\n\t[{primaryColumns.First(x => x.Value == tableName).Key}] ASC\n)";
                    createQuery += "WITH(STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON[PRIMARY]\nGO\n";

                    foreach (var foreignKeyColumn in foreignKeyColumns)
                    {
                        createQuery += $"ALTER TABLE [dbo].[{tableName}]  WITH CHECK ADD FOREIGN KEY([{foreignKeyColumn}])\n";
                        createQuery += $"REFERENCES [dbo].[{primaryColumns[foreignKeyColumn]}] ([{foreignKeyColumn}])\nGO";
                    }

                    Console.WriteLine(createQuery);
                    foreach (var query in createQuery.Split("GO"))
                    {
                        if (query.Trim() == "") continue;
                        QuerySql(query);
                    }
                }
            }
        }

        public static async Task UpdateAnimeList(bool hourly = false)
        {
            var currentAuTime = TimeZoneHelper.ConvertToAest(DateTime.UtcNow);
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Mozilla", "5.0"));
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("(Windows NT 10.0; Win64; x64)"));
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("AppleWebKit", "537.36"));
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("(KHTML, like Gecko)"));
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Chrome", "109.0.0.0"));
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Safari", "537.36"));

            List<JsonElement> animeList = new();
            for (var i = 0; i < (hourly ? 1 : 100); i++)
            {
                var kaaResponse = await httpClient.GetAsync($"{kickassAnimeBaseUrl.TrimEnd('/')}/api/recent_update?episodeType=all&perPage=100&page={i}");
                if (kaaResponse.StatusCode.ToString() == "InternalServerError") break;
                var content = kaaResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                var kaaJsonRoot = JsonDocument.Parse(content).RootElement;
                var kaaJsonList = kaaJsonRoot.EnumerateArray().ToList();
                animeList.AddRange(kaaJsonList.Where(x => !hourly || (x.TryGetProperty("isSimulcast", out var item) && item.GetBoolean())).DistinctBy(x => x.GetProperty("title").GetString()).ToList());
            }
            animeList = animeList.Where(x => !hourly || x.GetProperty("updatedString").GetString().Contains("minute") || x.GetProperty("updatedString").GetString() == "1 hour ago").ToList();

            foreach (var anime in animeList)
            {
                var title = anime.GetProperty("title").GetString();

                var getAnimeQuery = $"select top 1 AnimeId, KaaAnimeId from Anime where Title = '{title.Replace("'", "''")}'";
                var animeDataTable = QuerySql(getAnimeQuery);
                if (animeDataTable.Rows.Count == 0)
                {
                    var url = $"{kickassAnimeBaseUrl.TrimEnd('/')}/api/search";
                    var searchTitle = title;
                    if (searchTitle.Contains("(") && searchTitle.EndsWith(")")) searchTitle = searchTitle.Substring(0, searchTitle.IndexOf("("));
                    if (searchTitle.Contains("?") && !searchTitle.EndsWith("?")) searchTitle = searchTitle.Substring(0, searchTitle.IndexOf("?"));
                    if (searchTitle.Length < 3) continue;
                    searchTitle = searchTitle.Replace("\"", "\\\"");
                    var jsonBody = $"{{\"query\":\"{searchTitle}\"}}";
                    var kaaResponse = await httpClient.PostAsync(url, new StringContent(jsonBody, Encoding.UTF8, "application/json"));
                    var kaaJson = JsonDocument.Parse(kaaResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult()).RootElement.EnumerateArray().ToList();
                    var kaaAnime = kaaJson.Where(x => x.GetProperty("title").GetString() == title).ToList();
                    if (kaaAnime.Count == 0) continue;
                    var kaaId = kaaAnime.First().GetProperty("_id").GetString();
                    var titleCode = title.ToLower();
                    foreach (var character in new List<string>() { " ", ":", ";", "/", ".", "~" }) titleCode = titleCode.Replace(character, "-");
                    foreach (var character in new List<string>() { "'", ",", "!", "(", ")", "?" }) titleCode = titleCode.Replace(character, "");
                    titleCode = titleCode.Replace("--", "-").TrimEnd('-');

                    var query = $"insert into Anime (KaaAnimeId, Title, TitleCode, UploadWeekday) values ('{kaaId}', '{title.Replace("'", "''")}', '{titleCode}', 'Unknown')";
                    QuerySql(query);
                    animeDataTable = QuerySql(getAnimeQuery);
                }

                var dbAnimeId = animeDataTable.Rows[0].ItemArray[0].ToString();
                var kaaAnimeId = animeDataTable.Rows[0].ItemArray[1].ToString();
                var kaaResponse2 = await httpClient.GetAsync($"{kickassAnimeBaseUrl.TrimEnd('/')}/api/episodes/{kaaAnimeId}");
                var kaaJson2 = JsonDocument.Parse(kaaResponse2.Content.ReadAsStringAsync().GetAwaiter().GetResult()).RootElement.EnumerateArray().ToList();
                var kaaAnimeEpisodes = kaaJson2.Where(x => x.GetProperty("lang").GetString() == "ja-JP").Reverse().ToList();

                foreach (var episode in kaaAnimeEpisodes)
                {
                    var episodeNumber = int.TryParse(episode.GetProperty("episodeNumber").ToString(), out var epNum) ? epNum : -1;
                    var seasonNumber = int.TryParse(episode.GetProperty("seasonNumber").ToString(), out var seNum) ? seNum : -1;
                    if (episodeNumber == -1 || seasonNumber == -1) continue;
                    var seasonEpisode = $"S{seasonNumber:D3}E{episodeNumber:D4}";
                    var urlSlug = episode.GetProperty("slug").ToString();
                    var datetime = hourly ? $"'{currentAuTime.ToString("yyyy-MM-dd hh:00:00.000")}'" : "NULL";

                    if (QuerySql($"select * from AnimeEpisode where AnimeId = '{dbAnimeId}' and SeasonEpisode = '{seasonEpisode}'").Rows.Count == 0)
                    {
                        var query = $"insert into AnimeEpisode (AnimeId, SeasonEpisode, DateTimeUploaded, UrlSlug) values ('{dbAnimeId}', '{seasonEpisode}', {datetime}, '{urlSlug}')";
                        QuerySql(query);
                    }
                    else break;
                }
            }
        }
    }
}