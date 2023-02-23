using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace Pokebook
{
    public class AnimeHelper
    {
        public class AnimeItem
        {
            public int AnimeId { get; set; }
            public string KaaAnimeId { get; set; }
            public string Title { get; set; }
            public string TitleCode { get; set; }
        }

        public class AnimeEpisodeItem
        {
            public int AnimeEpisodeId { get; set; }
            public int AnimeId { get; set; }
            public int SeasonNumber { get; set; }
            public int? EpisodeNumber { get; set; }
            public string KaaSeasonId { get; set; }
            public string UrlSlug { get; set; }
            public DateTime? DateTimeUploaded { get; set; }
        }

        public class WatchedEpisodeItem
        {
            public int WacthedEpisodeId { get; set; }
            public int AnimeEpisodeId { get; set; }
        }

        public class UpdateHistoryItem
        { 
            public int UpdateHistoryId { get; set; }
            public DateTime DateTimeUpdated { get; set; }
        }

        private static readonly string sqlConnString = Environment.GetEnvironmentVariable("AnimeDbConnString");
        private static readonly string kickassAnimeBaseUrl = $"https://www2.kickassanime.ro";
        private static readonly List<string> sqlTableName = new() { "Anime", "AnimeEpisode", "WatchedEpisode", "UpdateHistory" };

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
                        var properyTypeName = property.PropertyType.Name;
                        var nullable = (properyTypeName == "Nullable`1");
                        if (nullable)  properyTypeName = property.PropertyType.FullName.Split("[System.")[1].Split(",")[0];
                        var propertyType = properyTypeName switch
                        {
                            "Int32" => "[int]",
                            "String" => "[varchar](4096)",
                            "DateTime" => "[datetime]",
                            _ => throw new NotImplementedException()
                        };
                        createQuery += $"{(!primary ? ",\n" : "")}\t[{properyName}] {propertyType} {(primary ? "IDENTITY(1,1)" : "")}{(nullable ? "" :" NOT NULL")}";
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

        public static async Task<string> GetLandingPageHtml(string titleCode)
        {
            await UpdateAnimeList();

            var animeTitle = QuerySql($"select Title from Anime where TitleCode = '{titleCode}'").Rows[0].ItemArray[0].ToString();

            var animeEpisodes = new List<AnimeEpisodeItem>();
            var columns = string.Join(", ", Type.GetType(animeEpisodes.GetType().FullName.Split("[[")[1].Split(",")[0]).GetProperties().Select(x => x.Name));
            var query2 = $"select {columns} from AnimeEpisode where AnimeId in (select AnimeId from Anime where TitleCode = '{titleCode}') order by SeasonNumber desc, EpisodeNumber desc";
            var queryDataRows = QuerySql(query2).Rows;
            for (var i = 0; i < queryDataRows.Count; i++)
                animeEpisodes.Add(new()
                {
                    AnimeEpisodeId = int.Parse(queryDataRows[i].ItemArray[0].ToString()),
                    AnimeId = int.Parse(queryDataRows[i].ItemArray[1].ToString()),
                    SeasonNumber = int.Parse(queryDataRows[i].ItemArray[2].ToString()),
                    EpisodeNumber = int.TryParse(queryDataRows[i].ItemArray[3].ToString(), out var ep) ? ep : null,
                    KaaSeasonId = queryDataRows[i].ItemArray[4].ToString(),
                    UrlSlug = queryDataRows[i].ItemArray[5].ToString(),
                    DateTimeUploaded = DateTime.TryParse(queryDataRows[i].ItemArray[6].ToString().TrimEnd('Z'), out var dt) ? dt : null,
                });

            
            var htmlInnerContent = $"<h1>{animeTitle}</h1>";
            foreach (var seasonNumber in animeEpisodes.Select(x => x.SeasonNumber).Distinct())
            {
                htmlInnerContent += $"<h2>Season {seasonNumber}</h2>";
                foreach (var episode in animeEpisodes.Where(x => x.SeasonNumber == seasonNumber))
                {
                    htmlInnerContent += $"<a href=\"{kickassAnimeBaseUrl}/watch/{episode.UrlSlug}\">Episode {episode.EpisodeNumber}</a>";
                    htmlInnerContent += "<br>";
                }
                htmlInnerContent += $"<br><br>";
            }

            var htmlFile = "animeLandingPageTemplate.html";
            var htmlTemplate = File.ReadAllText(File.Exists(htmlFile) ? htmlFile : $"C:/home/site/wwwroot/{htmlFile}");
            var finalHtml = htmlTemplate.Replace("{{CONTENT GOES HERE}}", htmlInnerContent).Replace("{{SCRIPTS GO HERE}}", $"document.title = '{animeTitle.Replace("'", "\\'")}'");

            return finalHtml;
        }

        public static async Task UpdateAnimeList()
        {
            var currentUtcTime = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.FFF");
            var lastUpdateUtcTime = DateTime.Parse(QuerySql("select top 1 DateTimeUpdated from UpdateHistory order by UpdateHistoryId desc").Rows[0].ItemArray[0].ToString());

            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Mozilla", "5.0"));
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("(Windows NT 10.0; Win64; x64)"));
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("AppleWebKit", "537.36"));
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("(KHTML, like Gecko)"));
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Chrome", "109.0.0.0"));
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Safari", "537.36"));

            List<JsonElement> animeList = new();
            for (var i = 0; i < 100; i++)
            {
                var kaaResponse = await httpClient.GetAsync($"{kickassAnimeBaseUrl.TrimEnd('/')}/api/recent_update?perPage=100&page={i}");
                if (kaaResponse.StatusCode.ToString() == "InternalServerError") continue;
                if (kaaResponse.StatusCode.ToString() == "Forbidden") return;
                var content = kaaResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                if (content == "[]") break;
                var kaaJsonRoot = JsonDocument.Parse(content).RootElement;
                var kaaJsonList = kaaJsonRoot.EnumerateArray().ToList();
                animeList.AddRange(kaaJsonList.DistinctBy(x => x.GetProperty("title").GetString()).ToList());
                if (DateTime.Parse(kaaJsonList.Last().GetProperty("lastUpdate").ToString().TrimEnd('Z')) < lastUpdateUtcTime) break;
            }

            animeList = animeList.Where(x => DateTime.Parse(x.GetProperty("lastUpdate").ToString().TrimEnd('Z')) > lastUpdateUtcTime).ToList();
            foreach (var anime in animeList)
            {
                var title = anime.GetProperty("title").GetString();
                var latestEpisodeSlug = anime.GetProperty("slug").GetString();
                var latestEpisodeUpdateTime = anime.GetProperty("lastUpdate").GetString();
                var language = "ja-JP";

                var getAnimeQuery = $"select AnimeId, KaaAnimeId from Anime where Title = '{title.Replace("'", "''")}'";
                var animeDataTable = QuerySql(getAnimeQuery);
                if (animeDataTable.Rows.Count == 0)
                {
                    var url = $"{kickassAnimeBaseUrl.TrimEnd('/')}/api/watch/{anime.GetProperty("slug").GetString()}";
                    var kaaResponse = await httpClient.GetAsync(url);
                    var kaaAnime = JsonDocument.Parse(kaaResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult()).RootElement;
                    var kaaId = kaaAnime.GetProperty("anime_id").GetString();
                    
                    var titleCode = title.ToLower();
                    titleCode = titleCode.Replace("&", "and");
                    titleCode = titleCode.Replace("@", "a");
                    foreach (var character in new List<string>() { "'", "’", ",", "!", "(", ")", "-", "<", "#", "$", "+", "{", "}" }) titleCode = titleCode.Replace(character, "");
                    foreach (var character in new List<string>() { " ", ":", ";", "/", ".", "~", ">", "^", "\"", "?"}) titleCode = titleCode.Replace(character, "-");
                    titleCode = titleCode.Replace("--", "-").Replace("--", "-").Trim('-');

                    var query = $"insert into Anime (KaaAnimeId, Title, TitleCode) values ('{kaaId}', '{title.Replace("'", "''")}', '{titleCode}')";
                    QuerySql(query);
                    animeDataTable = QuerySql(getAnimeQuery);
                }

                var dbAnimeId = animeDataTable.Rows[0].ItemArray[0].ToString();
                var kaaAnimeId = animeDataTable.Rows[0].ItemArray[1].ToString();

                var kaaResponse2 = await httpClient.GetAsync($"{kickassAnimeBaseUrl.TrimEnd('/')}/api/season/{kaaAnimeId}");
                var kaaJson2 = JsonDocument.Parse(kaaResponse2.Content.ReadAsStringAsync().GetAwaiter().GetResult()).RootElement.EnumerateArray().ToList();
                var kaaAnimeSeasons = kaaJson2.Where(x => x.GetProperty("languages").EnumerateArray().Select(x => x.ToString()).Contains(language)).Reverse().ToList();
                foreach (var season in kaaAnimeSeasons)
                {
                    var kaaSeasonId = season.GetProperty("id").ToString();
                    var kaaSeasonNumber = season.GetProperty("number").GetInt32();

                    var kaaResponse3 = await httpClient.GetAsync($"{kickassAnimeBaseUrl.TrimEnd('/')}/api/episodes/{kaaSeasonId}?lh={language}");
                    var kaaAnimeEpisodes = JsonDocument.Parse(kaaResponse3.Content.ReadAsStringAsync().GetAwaiter().GetResult()).RootElement.GetProperty("result").EnumerateArray().ToList();

                    bool breakLoop = false;
                    foreach (var episode in kaaAnimeEpisodes)
                    {
                        var episodeNumber = int.TryParse(episode.GetProperty("episodeNumber").ToString(), out var epNum) ? $"{epNum}" : "NULL";
                        var urlSlug = episode.GetProperty("slug").ToString();
                        var datetime = (urlSlug == latestEpisodeSlug) ? $"'{latestEpisodeUpdateTime}'" : "NULL";

                        if (QuerySql($"select * from AnimeEpisode where UrlSlug = '{urlSlug}'").Rows.Count == 0)
                        {
                            var query = $"insert into AnimeEpisode (AnimeId, KaaSeasonId, EpisodeNumber, SeasonNumber, DateTimeUploaded, UrlSlug) values ('{dbAnimeId}', '{kaaSeasonId}', {episodeNumber}, {kaaSeasonNumber}, {datetime}, '{urlSlug}')";
                            QuerySql(query);
                        }
                        else
                        {
                            breakLoop = true;
                            break;
                        } 
                    }
                    if (breakLoop) break;
                }
            }

            QuerySql($"insert into UpdateHistory (DateTimeUpdated) values ('{currentUtcTime}')");
        }
    }
}