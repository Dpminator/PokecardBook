﻿using Microsoft.Azure.Cosmos.Table;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Pokebook
{
    public abstract class CustomTableEntity : TableEntity
    {
        protected static string GetPartitionKeyFilter(string partitionKey) =>
            TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, partitionKey);

        protected static string GetRowKeyFilter(string rowKey) =>
            TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, rowKey);
    }

    public class PokecardSlot : CustomTableEntity
    {
        public string SetName { get; set; }
        public string CardNumber { get; set; }

        public string GetBookName() => PartitionKey;

        public int GetPageNumber() => int.Parse(RowKey.Split('-')[0]);
        public int GetSlotNumber() => int.Parse(RowKey.Split('-')[1]);

        public static async Task<TableQuerySegment<PokecardSlot>> GetPokecards(CloudTable pokecardSlotTable, string bookName)
        {
            var query = new TableQuery<PokecardSlot>().Where(GetPartitionKeyFilter(bookName));
            return await pokecardSlotTable.ExecuteQuerySegmentedAsync(query, null);
        }

        public static async Task InjectUpdates(CloudTable pokecardSlotTable, string commaSeparatedUpdates, string bookName)
        {
            if (commaSeparatedUpdates == "") return;
            foreach (var change in commaSeparatedUpdates.Split(","))
            {
                var changeSplit = change.Split("_");
                var card = new PokecardSlot
                {
                    ETag = "*",
                    PartitionKey = bookName,
                    RowKey = $"{changeSplit[0]}-{changeSplit[1]}",
                    SetName = BeautifySetName(changeSplit[2]),
                    CardNumber = changeSplit[3]
                };
                await pokecardSlotTable.ExecuteAsync(card.SetName == "NONE" ? TableOperation.Delete(card) : TableOperation.InsertOrMerge(card));
            }
        }

        public static string BeautifySetName(string setName)
        {
            return (setName) switch
            {
                "none" => "NONE",
                "" => "NONE",
                "promo" => "Promo",
                "sword-and-shield" => "Sword and Shield",
                "rebel-clash" => "Rebel Clash",
                "darkness-ablaze" => "Darkness Ablaze",
                "champions-path" => "Champion's Path",
                "vivid-voltage" => "Vivid Voltage",
                "shining-fates" => "Shining Fates",
                "battle-styles" => "Battle Styles",
                "chilling-reign" => "Chilling Reign",
                "evolving-skies" => "Evolving Skies",
                "celebrations" => "Celebrations",
                "celebrations-classic" => "Celebrations Classic",
                "fusion-strike" => "Fusion Strike",
                "brilliant-stars" => "Brilliant Stars",
                "astral-radiance" => "Astral Radiance",
                "pokemon-go" => "Pokemon Go",
                "lost-origin" => "Lost Origin",
                "silver-tempest" => "Silver Tempest",
                "crown-zenith" => "Crown Zenith",
                _ => setName
            };
        }

        public static string GetPokecardImageUrl(string cardSet, string cardNumber)
        {
            if (string.IsNullOrEmpty(cardSet) || string.IsNullOrEmpty(cardNumber) || cardSet == "NONE" || cardNumber == "0")
                return "https://images.pokemoncard.io/images/assets/CardBack.jpg";

            cardNumber = cardNumber.ToUpper().Substring(cardNumber.StartsWith("SWSH") ? 4 : 0).TrimStart('0').Replace("-", "/");

            var setId = cardSet.ToLower().Replace(" ", "-").Replace("'", "") switch
            {
                "promo" => "swshp",
                "base" => "swsh1",
                "sword-and-shield" => "swsh1",
                "sword-shield" => "swsh1",
                "rebel-clash" => "swsh2",
                "darkness-ablaze" => "swsh3",
                "champions-path" => "swsh35",
                "vivid-voltage" => "swsh4",
                "shining-fates" => "swsh45",
                "battle-styles" => "swsh5",
                "chilling-reign" => "swsh6",
                "evolving-skies" => "swsh7",
                "celebrations" => "cel25",
                "celebrations-classic" => "cel25c",
                "fusion-strike" => "swsh8",
                "brilliant-stars" => "swsh9",
                "astral-radiance" => "swsh10",
                "pokemon-go" => "pgo",
                "lost-origin" => "swsh11",
                "silver-tempest" => "swsh12",
                "crown-zenith" => "swsh125",
                _ => throw new Exception($"'{cardSet}' is not a valid set!")
            };

            if (cardNumber.StartsWith("TG"))
            {
                setId += "tg";
                cardNumber = "TG" + cardNumber.Substring(cardNumber.StartsWith("TG") ? 2 : 0).PadLeft(2, '0');
            }
            if (cardNumber.StartsWith("SV"))
            {
                setId += "sv";
                cardNumber = "SV" + cardNumber.Substring(cardNumber.StartsWith("SV") ? 2 : 0).PadLeft(3, '0');
            }

            var cardNumberSplit = cardNumber.Split('/');
            cardNumber = cardNumberSplit[0];

            if (setId == "cel25c")
            {
                if (cardNumber == "15")
                {
                    if (cardNumberSplit.Length == 1) throw new Exception("CardNumber is missing its denominator");
                    cardNumber += cardNumberSplit[1] switch
                    {
                        "102" => "_A1",
                        "82" => "_A2",
                        "132" => "_A3",
                        "106" => "_A4",
                        _ => throw new Exception($"'{cardNumberSplit[1]}' is not a valid denominator for the cel25c set!")
                    };
                }
                else cardNumber += "_A";
            }

            if (setId == "swshp") cardNumber = "SWSH" + cardNumber.PadLeft(3, '0');

            return $"https://images.pokemoncard.io/images/{setId}/{setId}-{cardNumber}_hiresopt.jpg";
        }

        public static string GetPokecardImageUrl2(string cardSet, string cardNumber)
        {
            if (string.IsNullOrEmpty(cardSet) || string.IsNullOrEmpty(cardNumber) || cardSet == "NONE" || cardNumber == "0")
                return "https://images.pokemoncard.io/images/assets/CardBack.jpg";

            cardNumber = cardNumber.ToUpper().Substring(cardNumber.StartsWith("SWSH") ? 4 : 0).TrimStart('0').Replace("-", "/").Split("/")[0];

            var setId = cardSet.ToLower().Replace(" ", "-").Replace("'", "") switch
            {
                "promo" => "swshpromos",
                "base" => "swordshield",
                "sword-and-shield" => "swordshield",
                "sword-shield" => "swordshield",
                "rebel-clash" => "rebelclash",
                "darkness-ablaze" => "darknessablaze",
                "champions-path" => "champion'spath",
                "vivid-voltage" => "vividvoltage",
                "shining-fates" => "shiningfates",
                "battle-styles" => "battlestyles",
                "chilling-reign" => "chillingreign",
                "evolving-skies" => "evolvingskies",
                "celebrations" => "celebrations",
                "celebrations-classic" => "celebrationsclassic",
                "fusion-strike" => "fusionstrike",
                "brilliant-stars" => "brilliantstars",
                "astral-radiance" => "astralradiance",
                "pokemon-go" => "pokemongo",
                "lost-origin" => "lostorigin",
                "silver-tempest" => "silvertempest",
                "crown-zenith" => "crownzenith",
                _ => throw new Exception($"'{cardSet}' is not a valid set!")
            };

            if (cardNumber.StartsWith("TG") || cardNumber.StartsWith("SV") || setId == "celebrationsclassic")
            {
                if (setId == "celebrationsclassic") setId = "celebrations";
                cardNumber = "h" + cardNumber.Substring(setId == "celebrations" ? 0 : 2).TrimStart('0');
            }

            return $"https://www.serebii.net/card/{setId}/{cardNumber}.jpg";
        }

        public static string GetPokecardImageUrlZenith(string cardSet, string cardNumber)
        {
            if (cardSet == "Promo" && (cardNumber.Contains("282") || cardNumber.Contains("283") || cardNumber.Contains("284")))
            {
                cardNumber = cardNumber.ToLower();
                if (cardNumber.StartsWith("swsh")) cardNumber = cardNumber.Split("swsh")[1];
                if (!int.TryParse(cardNumber, out int cn)) return "";
                return cn switch
                {
                    282 => "https://images.squarespace-cdn.com/content/v1/5cf4cfa4382ac0000123aa1b/1667538693127-3437AP6P3L1IDBLRPS9D/IMG_2953.png",
                    283 => "https://images.squarespace-cdn.com/content/v1/5cf4cfa4382ac0000123aa1b/1667538677264-L5TUK3EFQ6CK1U6KOODJ/IMG_2952.png",
                    284 => "https://images.squarespace-cdn.com/content/v1/5cf4cfa4382ac0000123aa1b/1667538683191-FKALAQS7LI9FFLGASSFN/IMG_2954.png",
                    _ => "",
                };
            }

            if (cardSet != "Crown Zenith") return "";

            if (cardNumber.ToLower().StartsWith("gg"))
            {
                if (!int.TryParse(cardNumber.ToLower().Split("gg")[1], out int cn)) return "";

                var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Mozilla", "5.0"));
                httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("(Windows NT 10.0; Win64; x64)"));

                var html = httpClient.GetAsync("https://www.justinbasil.com/visual/ss125").GetAwaiter().GetResult()
                    .Content
                    .ReadAsStringAsync().GetAwaiter().GetResult();

                return html.Split("<div class=\"sqs-gallery\">")[13].Split("<noscript><img src=\"")[cn].Split("\"")[0];
            }
            else
            {
                if (!int.TryParse(cardNumber, out int cn)) return "";
                return cn switch
                {
                    147 => "https://images.squarespace-cdn.com/content/v1/5cf4cfa4382ac0000123aa1b/1672783150367-351N529R8A01THQPG816/IMG_3432.jpeg",
                    148 => "https://images.squarespace-cdn.com/content/v1/5cf4cfa4382ac0000123aa1b/1669912687661-01M1CHWY45VR286DDNMC/249.jpg",
                    149 => "https://images.squarespace-cdn.com/content/v1/5cf4cfa4382ac0000123aa1b/1669912686925-W8EMP0V2SFUBSV8OQPPG/247.jpg",
                    150 => "https://images.squarespace-cdn.com/content/v1/5cf4cfa4382ac0000123aa1b/1669912687247-MC3RAHDZRJMJK06AFTOZ/248-1.jpg",
                    151 => "https://images.squarespace-cdn.com/content/v1/5cf4cfa4382ac0000123aa1b/1669912686230-YUVGTL0QAGGGTAAOKPR7/245.jpg",
                    _ => "",
                };
            }
        }
    }

    public class PokecardBook : CustomTableEntity
    {
        public string PageCount { get; set; }

        public string BookName => PartitionKey;

        public static async Task<TableQuerySegment<PokecardBook>> GetPokebookUser(CloudTable pokecardBookTable, string bookName)
        {
            var query = new TableQuery<PokecardBook>().Where(GetPartitionKeyFilter(bookName));
            return await pokecardBookTable.ExecuteQuerySegmentedAsync(query, null);
        }
    }
}
