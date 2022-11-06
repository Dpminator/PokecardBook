using Microsoft.Azure.Cosmos.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

    public class PokemonCardCellEntity : CustomTableEntity
    {
        public string SetName { get; set; }
        public string CardNumber { get; set; }
        public string UserName { get; set; }


        public static async Task<TableQuerySegment<PokemonCardCellEntity>> GetPokemonCardCells(CloudTable pokeTable, string username)
        {
            var query = new TableQuery<PokemonCardCellEntity>()
                .Where(TableQuery.GenerateFilterCondition("UserName", QueryComparisons.Equal, username.ToLower()));
            return await pokeTable.ExecuteQuerySegmentedAsync(query, null);
        }

        public static async Task InjectUpdates(CloudTable pokeTable, string commaSeparatedUpdates, string username)
        {
            if (commaSeparatedUpdates == "") return;
            foreach (var change in commaSeparatedUpdates.Split(","))
            {
                var changeSplit = change.Split("_");
                var card = new PokemonCardCellEntity
                {
                    ETag = "*",
                    PartitionKey = changeSplit[0],
                    RowKey = changeSplit[1],
                    SetName = changeSplit[2],
                    CardNumber = changeSplit[3],
                    UserName = username
                };
                card.BeautifySetName();
                await pokeTable.ExecuteAsync(card.SetName == "NONE" ? TableOperation.Delete(card) : TableOperation.InsertOrMerge(card));
            }
        }

        protected void BeautifySetName()
        {
            SetName = (SetName) switch
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
                "lost-origin" => "Lost Origin",
                "silver-tempest" => "Silver Tempest",
                "crown-zenith" => "Crown Zenith",
                _ => SetName
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
    }

    public class PokemonUserEntity : CustomTableEntity
    {
        public string PageCount { get; set; }

        public static async Task<TableQuerySegment<PokemonUserEntity>> GetPokebookUser(CloudTable pokeTable, string username)
        {
            var query = new TableQuery<PokemonUserEntity>().Where(GetPartitionKeyFilter(username));
            return await pokeTable.ExecuteQuerySegmentedAsync(query, null);
        }
    }

}
