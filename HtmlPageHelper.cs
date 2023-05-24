using Microsoft.Azure.Cosmos.Table;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Pokebook
{
    public class PokemonBookPage
    {
        private readonly CloudTable PokecardSlotTable;
        private readonly CloudTable PokemonUserTable;
        private readonly Dictionary<(string, string), PokecardSlot> PokecardSlots;
        private readonly string BookName;
        private readonly int PageCount;
        private readonly bool NewBookFromUrl = false;

        public PokemonBookPage(CloudTable pokecardSlotTable, CloudTable pokecardBookTable, string bookName)
        {
            PokecardSlotTable = pokecardSlotTable;
            PokemonUserTable = pokecardBookTable;
            PokecardSlots = new Dictionary<(string, string), PokecardSlot>();
            BookName = bookName ?? "";
            PageCount = 12;

            var pokebookUsers = PokecardBook.GetPokebookUser(PokemonUserTable, BookName).GetAwaiter().GetResult();
            if (pokebookUsers.Results.Count != 0)
            {
                PageCount = int.Parse(pokebookUsers.Results[0].PageCount);
                foreach (var cardSlot in PokecardSlot.GetPokecards(PokecardSlotTable, BookName).GetAwaiter().GetResult())
                {
                    var rowKeySplit = cardSlot.RowKey.Split("-");
                    PokecardSlots.Add((rowKeySplit[0], rowKeySplit[1]), cardSlot);
                }
            }
            else NewBookFromUrl = true;
        }

        public async Task<string> GetHtml()
        {
            var htmlFile = "pokemonBookTemplate.html";

            var htmlTemplate = File.ReadAllText(File.Exists(htmlFile) ? htmlFile : $"C:/home/site/wwwroot/{htmlFile}");

            var htmlInnerContent = await GetPokemonBookHtml();
            var finalHtml = htmlTemplate.Replace("{{CONTENT GOES HERE}}", htmlInnerContent).Replace("{{FOOTER CONTENT GOES HERE}}", "");
            return finalHtml;
        }

        private async Task<string> GetPokemonBookHtml()
        {
            var content = $"<script>bookName = \"{(NewBookFromUrl ? "" : BookName)}\"; pageCount = {PageCount};</script>";

            content += "<div style='float:left;width:300;margin-top:10px;'>";

            content += "<div class ='Boxdesign'>";
            content += "<h3>Page Selector:</h3>";
            content += "<div style='display:flex;justify-content:space-between;'>";
            content += "<button disabled id='PagePreviousButton' onclick=\"MovePage('previous')\">Previous</button>";
            content += "<div id='PageNumber' style='text-align: center;'>Page <b>1</b></div>";
            content += "<button id='PageNextButton' onclick=\"MovePage('next')\">Next</button>";
            content += "</div></div><br><br><br>";

            content += "<div class ='Boxdesign'>";
            content += "<h3>Book Details:</h3>";
            content += $"Book Name: <input id='BookNameInput' style='width:150;' onkeyup=\"BookDetailSelectorOnChange()\" {(BookName == "" ? "" : "disabled")} value='{BookName}'><br>";
            content += $"Page Count: <input id='PageCountInput' style='width:152;' onkeyup=\"BookDetailSelectorOnChange()\" onChange=\"BookDetailSelectorOnChange()\" type='number' min='2' max='100' value='{(NewBookFromUrl ? "" : $"{PageCount}")}'><br>";
            content += "</div><br><br><br>";

            content += "<div class ='Boxdesign'>";
            content += "<h3>Selected Card:</h3>";
            content += "Page Number: <input id='SelectedPageNumber' style='width:124;' disabled value='N/A'><br>";
            content += "Slot Number: <input id='SelectedSlotNumber' style='width:129;' disabled value='N/A'><br>";
            content += "Set Name: <select id='SetNameSelector' onChange=\"SelectorOnChange()\" disabled>";
            content += "<option value='NONE'>NONE</option>";
            content += "<option value='Promo'>Black Star Promo</option>";
            content += "<option value='Sword and Shield'>Sword & Shield</option>";
            content += "<option value='Rebel Clash'>Rebel Clash</option>";
            content += "<option value='Darkness Ablaze'>Darkness Ablaze</option>";
            content += "<option value=\"Champion's Path\">Champion's Path</option>";
            content += "<option value='Vivid Voltage'>Vivid Voltage</option>";
            content += "<option value='Shining Fates'>Shining Fates</option>";
            content += "<option value='Battle Styles'>Battle Styles</option>";
            content += "<option value='Chilling Reign'>Chiling Reign</option>";
            content += "<option value='Evolving Skies'>Evolving Skies</option>";
            content += "<option value='Celebrations'>Celebrations</option>";
            content += "<option value='Celebrations Classic'>Celebrations: Classic</option>";
            content += "<option value='Fusion Strike'>Fusion Strike</option>";
            content += "<option value='Brilliant Stars'>Brilliant Stars</option>";
            content += "<option value='Pokemon Go'>Pokemon Go</option>";
            content += "<option value='Astral Radiance'>Astral Radiance</option>";
            content += "<option value='Lost Origin'>Lost Origin</option>";
            content += "<option value='Silver Tempest'>Silver Tempest</option>";
            content += "<option value='Crown Zenith'>Crown Zenith</option>";
            content += "</select><br>";
            content += "Card Number: <input id='CardNumberInput' onkeyup=\"SelectorOnChange()\" style='width:125;' disabled><br><br>";
            content += "<button class ='buttonlayout' id='ClearSelection' onclick='ClearSelection()' disabled>Clear Selection</button><br>";
            content += "<button class ='buttonlayout' id='UpdateCard' onclick='UpdateCard()' disabled>Update Card</button><br>";
            content += "<button class ='buttonlayout' id='SwapCard' onclick='SwapCardButton()' disabled>Swap Card</button><br>";
            content += "</div><br><br><br>";

            content += "<div class ='Boxdesign' style='visibility:visible' id='CardDetailsDiv'>";
            content += "<h3>Card Details:</h3>";
            content += "Set Name: <input id='CardDetailSetName' style='width:140;' disabled value='NONE'><br>";
            content += "Card Number: <input id='CardDetailCardNumber' style='width:116;' disabled value='0'><br><br>";
            content += "<button class ='buttonlayout' id='OpenCardImage' onclick='OpenCardImage()'>Open Card's Image</button><br>";
            content += "<button class ='buttonlayout' id='OpenCardEbayPrices' onclick='OpenCardEbayPrices()'>See Card's Ebay Prices</button><br>";
            content += "<div id ='EbayPricesDiv'></div>";
            content += "</div>";

            content += "</div>";


            content += "<div style='float:left;margin-left:15px;margin-right:10px;margin-top:5px'>";
            for (int page = 0; page <= PageCount + 1; page++)
            {
                content += $"<div class='{(page == 0 || page == PageCount + 1 ? "PokemonCardPageBlank" : "PokemonCardPage")}' id='PokemonCardPage{page}' style='display:inline-block;{(page == 0 || page == PageCount + 1 ? "opacity:0.3" : "")}'><table>";
                for (int row = 0; row < 3; row++)
                {
                    content += "<tr>";
                    for (int column = 0; column < 3; column++)
                    {
                        var slot = (3 * row + column) + 1;
                        var pageSlot = ($"{page}", $"{slot}");
                        var set = "NONE";
                        var cardNumber = "0";

                        if (page != PageCount + 1 && PokecardSlots.ContainsKey(pageSlot))
                        {
                            set = PokecardSlots[pageSlot].SetName;
                            cardNumber = PokecardSlots[pageSlot].CardNumber;
                        }

                        var url = $"/api/card-image/{set}/{cardNumber.Replace("/", "-")}";
                        var clickAction = page == 0 || page == PageCount + 1 ? "" : $"onclick=\"SelectCard({page}, {slot}, '{set.Replace("'", "\\'")}', '{cardNumber}')\"";
                        content += $"<td><img class='PokemonCardCell' id='Cell_{page}_{slot}' data-src=\"{url.Replace("'", "&#39;")}\" {clickAction}></td>";
                    }
                    content += "</tr>";
                }
                content += "</table></div>";
            }
            content += "</div>";


            content += "<div style='float:left;width:300;margin-top:10px;'>";

            content += $"<div class ='Boxdesign' style='visibility:{(NewBookFromUrl ? "visible" : "hidden")}' id='UpdatedCardsDiv'>";
            content += $"<h3>Updated{(NewBookFromUrl ? " Details and " : " ")}Cards:</h3>";
            content += $"<div id='NewBookName' style='display:{(BookName == "" ? "block" : "none")}'><b>Book Name:</b>{BookName}</div>";
            content += $"<div id='NewPageCount' style='display:{(NewBookFromUrl ? "block" : "none")}'><b>Page Count:</b></div>";
            content += $"<div id='CardChanges' style='display:none'><b>Card Changes:</b></div>";
            content += "<ul id='UpdatedCardsList'></ul><br>";
            content += "<button class ='buttonlayout' id='SaveUpdates' onclick='SaveUpdates()' disabled>Save Updates</button><br>";
            content += "<button class ='buttonlayout' id='UndoUpdates' onclick='UndoAllUpdates()' style='color:red'>Undo All Updates</button><br>";
            content += "</div>";

            content += "</div>";


            return content;
        }
    }

    public class EbayHelper
    {
        public class EbayItem
        { 
            public string Name { get; set; }
            public double Price { get; set; }
            public string Link { get; set; }
        }

        public static async Task<List<EbayItem>> GetPokecardEbayPrices(string cardSet, string cardNumber)
        {
            var ebayItems = new List<EbayItem>();

            cardSet = PokecardSlot.BeautifySetName(cardSet);
            if (string.IsNullOrEmpty(cardSet) || string.IsNullOrEmpty(cardNumber) || cardSet.ToUpper() == "NONE" || cardNumber == "0") return ebayItems;

            cardNumber = cardNumber.ToUpper().Substring(cardNumber.StartsWith("SWSH") ? 4 : 0).TrimStart('0').Replace("-", "/");
            if (cardSet == "Promo") cardNumber = "SWSH" + cardNumber;

            var url = $"https://www.ebay.com.au/sch/i.html?_from=R40&_nkw={cardSet.Replace(" ", "+")}+{cardNumber}+-digital+-online+-grade+-graded+-PSA+-DSG&LH_TitleDesc=0&LH_BIN=1&_sop=15&rt=nc&LH_PrefLoc=1";
            var ebayResponse = await new HttpClient().GetAsync(url);
            var ebayHtml = ebayResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            foreach (var listedItem in ebayHtml.Split("s-item s-item__pl-on-bottom"))
            {
                if (listedItem.Contains("<span role=heading aria-level=3>"))
                {
                    var name = listedItem.Split("<span role=heading aria-level=3>")[1].Split("</span>")[0];
                    if (name.Contains(cardSet) && name.Contains(cardNumber))
                    {
                        var itemPrice = double.Parse(listedItem.Split("<span class=s-item__price>")[1].Split("</span>")[0].Split("AU $")[1]);
                        var shippingCost = 0.0d;
                        if (!listedItem.Contains("Free postage"))
                        {
                            shippingCost = double.Parse(listedItem.Split("<span class=\"s-item__shipping s-item__logisticsCost\">")[1].Split("</span>")[0].Split("AU $")[1].Split(" ")[0]);
                        }
                        var totalPrice = Math.Round(itemPrice + shippingCost, 2);
                        var ebayLinkToItem = listedItem.Split("class=s-item__link href=")[1].Split("?")[0];

                        ebayItems.Add(new EbayItem { Name = name, Price = totalPrice, Link = ebayLinkToItem});
                    }
                }
            }
            return ebayItems;
        }
    }

    public class LucasHelper
    {
        public class LucasItem
        {
            public int PropertyId { get; set; }
            public string Address { get; set; }
            public string Suburb { get; set; }
            public string WebsiteLink { get; set; }
            public string Status { get; set; }
            public int MinimumPrice { get; set; }
            public int MaximumPrice { get; set; }
            public int BedroomCount { get; set; }
            public int BathroomCount { get; set; }
            public int CarparkCount { get; set; }
            public int AreaSquareMeters { get; set; }
            public int CouncilRates { get; set; }
            public int WaterRates { get; set; }
            public int StrataFess { get; set; }
        }

        private static readonly string sqlConnString = Environment.GetEnvironmentVariable("LucasDbConnString");
        private static readonly int urlMinnimumPrice = 400000;
        private static readonly int urlMaximumPrice = 650000;
        private static readonly int urlMinnimumBedrooms = 2;
        private static readonly int urlMinnimumBathrooms = 1;
        private static readonly string lucasResultsUrl = $"https://www.lucasre.com.au/pages/real-estate/results?listing_sale_method=Sale&status=&display_sale_method=BUY&listing_suburb_search=Docklands%2C+VIC+3008%3B+&listing_category=&listing_price_from={urlMinnimumPrice}&listing_price_to={urlMaximumPrice}&listing_bedrooms={urlMinnimumBedrooms}&listing_bathrooms={urlMinnimumBathrooms}&surrounds=false";

        private static DataTable QuerySql(string query)
        {
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

        private static int GetFeesPerAnnum(string html, string feeName)
        {
            var fee = 0;
            if (html.Contains($"<span class=\"detail-title\">{feeName}</span>"))
            {
                var feeText = html.Split($"{feeName}</span>\n<span class=\"detail-text\">$")[1].Split("</span>")[0];
                fee = int.Parse(html.Split($"{feeName}</span>\n<span class=\"detail-text\">$")[1].Split(" per ")[0].Replace(",", "").Split(".")[0]);

                if (feeText.ToLower().Contains("annum")) return fee;
                if (feeText.ToLower().Contains("quarter")) return fee * 4;
                if (feeText.ToLower().Contains("month")) return fee * 12;

                Console.WriteLine($"feeText '{feeText}' is not accounted for yet!");
            }
            return fee;
        }

        public static async Task SetUpDatabaseTables()
        {
            var queryTemplate = "IF EXISTS (SELECT * FROM information_schema.tables WHERE table_name = '{{TABLENAME}}') SELECT 1 ELSE SELECT 0";

            var propertyTableExists = QuerySql(queryTemplate.Replace("{{TABLENAME}}", "Property")).AsEnumerable().First().ItemArray[0].ToString() == "1";
            var priceHistoryTableExists = QuerySql(queryTemplate.Replace("{{TABLENAME}}", "PriceHistory")).AsEnumerable().First().ItemArray[0].ToString() == "1";
            var statusHistoryTableExists = QuerySql(queryTemplate.Replace("{{TABLENAME}}", "StatusHistory")).AsEnumerable().First().ItemArray[0].ToString() == "1";

            if (!propertyTableExists || !priceHistoryTableExists || !statusHistoryTableExists)
            {
                throw new Exception("Database tables not properly set up!");
            }
        }

        public static async Task<string> UpdateLucasProperties()
        {
            var changedSqlProperties = new List<(bool newProperty, int oldMin, int newMin, int oldMax, int newMax, string oldStatus, string newStatus, LucasItem property)>();
            var sqlProperties = new List<LucasItem>();

            var getAllActiveProperties = "SELECT * FROM Property p INNER JOIN StatusHistory sh ON p.PropertyId = sh.PropertyId AND sh.StatusHistoryId = (SELECT MAX(StatusHistoryId) FROM StatusHistory WHERE PropertyId = p.PropertyId) INNER JOIN PriceHistory ph ON p.PropertyId = ph.PropertyId AND ph.PriceHistoryId = (SELECT MAX(PriceHistoryId) FROM PriceHistory WHERE PropertyId = p.PropertyId) where sh.[Status] != 'Inactive'";

            var dt = QuerySql(getAllActiveProperties);
            foreach (var sqlResult in dt.AsEnumerable())
            {
                sqlProperties.Add(
                    new LucasItem
                    {
                        PropertyId = int.Parse(sqlResult.ItemArray[dt.Columns.IndexOf("PropertyId")].ToString()),
                        Address = sqlResult.ItemArray[dt.Columns.IndexOf("Address")].ToString(),
                        Suburb = sqlResult.ItemArray[dt.Columns.IndexOf("Suburb")].ToString(),
                        WebsiteLink = sqlResult.ItemArray[dt.Columns.IndexOf("WebsiteLink")].ToString(),
                        Status = sqlResult.ItemArray[dt.Columns.IndexOf("Status")].ToString(),
                        MinimumPrice = int.Parse(sqlResult.ItemArray[dt.Columns.IndexOf("MinimumPrice")].ToString()),
                        MaximumPrice = int.Parse(sqlResult.ItemArray[dt.Columns.IndexOf("MaximumPrice")].ToString()),
                        BedroomCount = int.Parse(sqlResult.ItemArray[dt.Columns.IndexOf("Bedrooms")].ToString()),
                        BathroomCount = int.Parse(sqlResult.ItemArray[dt.Columns.IndexOf("Bathrooms")].ToString()),
                        CarparkCount = int.Parse(sqlResult.ItemArray[dt.Columns.IndexOf("CarSpaces")].ToString()),
                        AreaSquareMeters = int.Parse(sqlResult.ItemArray[dt.Columns.IndexOf("AreaSquareMeters")].ToString()),
                        CouncilRates = int.Parse(sqlResult.ItemArray[dt.Columns.IndexOf("CouncilRates")].ToString()),
                        WaterRates = int.Parse(sqlResult.ItemArray[dt.Columns.IndexOf("WaterRates")].ToString()),
                        StrataFess = int.Parse(sqlResult.ItemArray[dt.Columns.IndexOf("StrataFees")].ToString())
                    }
                );
            }

            var pageCount = 1;
            for (int currentPage = 1; currentPage <= pageCount; currentPage++)
            {
                var url = $"{lucasResultsUrl}&pg={currentPage}";

                var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Mozilla", "5.0"));
                httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("(Windows NT 10.0; Win64; x64)"));

                var lucasResponse = await httpClient.GetAsync(url);
                var lucasHtml = lucasResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                if (lucasHtml.Contains("load more")) pageCount++;

                foreach (var listedItem in lucasHtml.Split("<div class=\"listing-wrapper\">")[1].Split("<a href=\""))
                {
                    if (!listedItem.Contains("class=\"listing\"")) continue;

                    var lines = listedItem.Split("\n");
                    var lineOffset = lines[3].Contains("badge") ? 1 : 0;

                    var link = "https://www.lucasre.com.au" + lines[0].Split("\"")[0];
                    var address = lines[6 + lineOffset];
                    var suburb = lines[9 + lineOffset];
                    var sqlProperty = sqlProperties.SingleOrDefault(x => x.Address == address && x.Suburb == suburb, null);
                    var newProperty = (sqlProperty == null);
                    if (!newProperty) sqlProperties.Remove(sqlProperty);
                    var propertyId = newProperty ? 0 : sqlProperty.PropertyId;
                    var oldMinPrice = 0;
                    var oldMaxPrice = 0;
                    var minPrice = -1;
                    var maxPrice = -1;
                    if (lines[12 + lineOffset] != "Contact Agent")
                    {
                        minPrice = int.Parse(lines[12 + lineOffset].Split("-")[0].Trim().TrimStart('$').Replace(",", ""));
                        maxPrice = lines[12 + lineOffset].Contains("-") ? int.Parse(lines[12 + lineOffset].Split("-")[1].Trim().TrimStart('$').Replace(",", "")) : minPrice;
                    }
                    var priceChange = (!newProperty && (sqlProperty.MinimumPrice != minPrice || sqlProperty.MaximumPrice != maxPrice));
                    if (priceChange)
                    {
                        oldMinPrice = sqlProperty.MinimumPrice;
                        oldMaxPrice = sqlProperty.MaximumPrice;
                        sqlProperty.MinimumPrice = minPrice;
                        sqlProperty.MaximumPrice = maxPrice;
                    }
                    var bedroomCount = int.Parse(lines[17+ lineOffset].Split("</span>")[1]);
                    var bathroomCount = int.Parse(lines[20+ lineOffset].Split("</span>")[1]);
                    var carCount = 0;
                    if (listedItem.Contains("<span class=\"icon-car\"></span>"))
                        carCount = int.Parse(lines[23+ lineOffset].Split("</span>")[1]);

                    var lucasResponse2 = await httpClient.GetAsync(link);
                    var lucasHtml2 = lucasResponse2.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                    var oldSize = 0;
                    var oldCouncilRates = 0;
                    var oldWaterRates = 0;
                    var OldStrata = 0;
                    var sizeMetresSquared = 0;
                    if (lucasHtml2.Contains("<span class=\"detail-title\">Total Size</span>"))
                        sizeMetresSquared = int.Parse(lucasHtml2.Split("Total Size</span>\n<span class=\"detail-text\">")[1].Split("m<sup>")[0].Replace(",", ""));
                    var councilRates = GetFeesPerAnnum(lucasHtml2, "Council Rates");
                    var waterRates = GetFeesPerAnnum(lucasHtml2, "Water Rates"); 
                    var strataFees = GetFeesPerAnnum(lucasHtml2, "Strata Fees");
                    if (!newProperty)
                    {
                        oldSize = sqlProperty.AreaSquareMeters;
                        oldCouncilRates = sqlProperty.CouncilRates;
                        oldWaterRates = sqlProperty.WaterRates;
                        OldStrata = sqlProperty.StrataFess;
                    }
                    var feesUpdate = !newProperty && (oldSize != sizeMetresSquared || oldCouncilRates != councilRates || oldWaterRates != waterRates || OldStrata != strataFees);

                    var oldStatus = "";
                    var status = "Active";
                    if (lucasHtml2.Contains("<div class='main-badge large'>"))
                    {
                        status = $"Badge:{lucasHtml2.Split("<div class='main-badge large'>")[1].Split("</div>")[0].Trim()}";
                    }else if (sizeMetresSquared == 0 || councilRates == 0 || waterRates == 0 || strataFees == 0)
                    {
                        var missingInfoText = "";
                        if (sizeMetresSquared == 0) missingInfoText += "size,";
                        if (councilRates == 0) missingInfoText += "council,";
                        if (waterRates == 0) missingInfoText += "water,";
                        if (strataFees == 0) missingInfoText += "strata";
                        status = $"MissingInfo:{missingInfoText.TrimEnd(',')}";
                    }

                    var statusUpdate = (!newProperty && status != sqlProperty.Status);
                    if (statusUpdate)
                    {
                        oldStatus = sqlProperty.Status;
                        sqlProperty.Status = status;
                    }

                    if (newProperty) sqlProperty =
                        new LucasItem
                        {
                            PropertyId = propertyId,
                            Address = address,
                            Suburb = suburb,
                            WebsiteLink = link,
                            Status = status,
                            MinimumPrice = minPrice,
                            MaximumPrice = maxPrice,
                            BedroomCount = bedroomCount,
                            BathroomCount = bathroomCount,
                            CarparkCount = carCount,
                            AreaSquareMeters = sizeMetresSquared,
                            CouncilRates = councilRates,
                            WaterRates = waterRates,
                            StrataFess = strataFees
                        };

                    if (newProperty || priceChange || statusUpdate)
                    {
                        if (newProperty)
                        {
                            priceChange = true;
                            statusUpdate = true;

                            var query = $"insert into Property values ('{address}', '{suburb}', '{link}', {bedroomCount}, {bathroomCount}, {carCount}, {sizeMetresSquared}, {councilRates}, {waterRates}, {strataFees})";
                            QuerySql(query);

                            query = $"select PropertyId from Property where Address = '{address}' and Suburb = '{suburb}'";
                            propertyId = int.Parse(QuerySql(query).AsEnumerable().First().ItemArray[0].ToString());
                        }
                        if (priceChange) QuerySql($"insert into PriceHistory values ({minPrice}, {maxPrice}, {propertyId}, current_timestamp)");
                        if (statusUpdate) QuerySql($"insert into StatusHistory values ('{status}', {propertyId}, current_timestamp)");
                        changedSqlProperties.Add((newProperty, oldMinPrice, minPrice, oldMaxPrice, maxPrice, oldStatus, status, sqlProperty));
                    }

                    if (feesUpdate)
                    {
                        QuerySql($"update Property set AreaSquareMeters = {sizeMetresSquared}, CouncilRates = {councilRates}, WaterRates = {waterRates}, StrataFees = {strataFees} where propertyId = {sqlProperty.PropertyId}");
                    }
                }
            }

            foreach (var unlistedProperty in sqlProperties)
            {
                QuerySql($"insert into StatusHistory values ('Inactive', {unlistedProperty.PropertyId}, current_timestamp)");
                changedSqlProperties.Add((false, 0, 0, 0, 0, unlistedProperty.Status, "Inactive", unlistedProperty));
            }

            var changes = "";
            if (changedSqlProperties.Count > 0)
            {
                if (changedSqlProperties.Exists(x => x.newStatus == "Inactive")) changes += "\\n\\nNow Inactive:\\n\\n";
                foreach (var (_, _, _, _, _, _, _, property) in changedSqlProperties.Where(x => x.newStatus == "Inactive"))
                {
                    changes += $"{property.Address}, {property.Suburb}: ${property.MinimumPrice} - ${property.MaximumPrice} ({property.WebsiteLink})\\n\\n";
                }

                if (changedSqlProperties.Exists(x => x.newStatus != "Inactive" && !x.newProperty)) changes += "\\n\\nUpdated Properties:\\n\\n";
                foreach (var (_, oldMin, newMin, oldMax, newMax, oldStatus, newStatus, property) in changedSqlProperties.Where(x => x.newStatus != "Inactive" && !x.newProperty))
                {
                    changes += $"{property.Address}, {property.Suburb}: ";
                    if (oldStatus != "")
                    {
                        changes += $"From status '{oldStatus}' to '{newStatus}'";
                    }
                    if (oldMin != 0)
                    {
                        if (oldStatus != "") changes += " and ";
                        changes += $"From price range ${oldMin} - ${oldMax} to ${newMin} - ${newMax}";
                    }
                    changes += $" ({property.WebsiteLink})\\n\\n";
                }

                if (changedSqlProperties.Exists(x => x.newStatus != "Inactive" && x.newProperty)) changes += "\\n\\nNew Properties:\\n\\n";
                foreach (var (_, _, newMin, _, newMax, _, newStatus, property) in changedSqlProperties.Where(x => x.newStatus != "Inactive" && x.newProperty))
                {
                    changes += $"{property.Address}, {property.Suburb}: ${newMin} - ${newMax} for {property.AreaSquareMeters} square meters ({property.WebsiteLink})\\n\\n";
                }

                changes += $"\\nCheck out all the properties here: {lucasResultsUrl}";

                await PingDiscord($"there have been changes:{changes}");
            }
            return changes;
        }

        public static async Task PingDiscord(string message)
        {
            var url = Environment.GetEnvironmentVariable("DiscordWebhookUrl"); ;
            var jsonBody = $"{{\"content\":\"<@165680911189409792>, {message}\"}}";
            var output = await new HttpClient().PostAsync(url, new StringContent(jsonBody, Encoding.UTF8, "application/json"));
            //Console.WriteLine(output);
        }
    }

}