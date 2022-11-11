using Microsoft.Azure.Cosmos.Table;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
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
            var content = $"<script>bookName = \"{BookName}\"; pageCount = {PageCount};</script>";

            content += "<div style='float:left;width:300;margin-right:15px;'>";

            content += "<h3>Page Selector:</h3>";
            content += "<div style='display:flex;'>";
            content += "<button disabled id='PagePreviousButton' onclick=\"MovePage('previous')\">Previous</button>";
            content += "<div id='PageNumber' style='width:100;text-align: center;'>Page <b>1</b></div>";
            content += "<button id='PageNextButton' onclick=\"MovePage('next')\">Next</button>";
            content += "</div><br><br><br>";

            content += "<div class ='Boxdesign'>";
            content += "<h3>Book Details:</h3>";
            content += $"Book Name: <input id='BookNameInput' style='width:150;' onkeyup=\"BookDetailSelectorOnChange()\" {(BookName == "" ? "" : "disabled")} value='{BookName}'><br>";
            content += $"Page Count: <input id='PageCountInput' style='width:140;' onkeyup=\"BookDetailSelectorOnChange()\" onChange=\"BookDetailSelectorOnChange()\" type='number' min='2' max='100' value='{(NewBookFromUrl ? "" : $"{PageCount}")}'><br>";
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
            content += "Card Number: <input id='CardNumberInput' onkeyup=\"SelectorOnChange()\" style='width:125;' disabled><br>";
            content += "<button class ='buttonlayout' id='ClearSelection' onclick='ClearSelection()' disabled>Clear Selection</button><br><br>";
            content += "<button class ='buttonlayout' id='UpdateCard' onclick='UpdateCard()' disabled>Update Card</button><br><br>";
            content += "<button class ='buttonlayout' id='SwapCard' onclick='SwapCardButton()' disabled>Swap Card</button><br><br>";
            content += "</div><br><br><br>";

            content += "<div class ='Boxdesign'>";
            content += "<h3>Updated Cards:</h3>";
            content += "<ul id='UpdatedCardsList'></ul><br>";
            content += "<button class ='buttonlayout' id='UndoUpdates' onclick='UndoAllUpdates()' disabled>Undo All Updates</button><br><br>";
            content += "<button class ='buttonlayout' id='SaveUpdates' onclick='SaveUpdates()' disabled>Save Updates</button><br><br>";
            content += "</div><br><br><br>";

            content += "</div>";


            content += "<div style='float:left'>";
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
                        content += $"<td><img class='PokemonCardCell' id='Cell_{page}_{slot}' src=\"{url.Replace("'", "&#39;")}\" {clickAction}></td>";
                    }
                    content += "</tr>";
                }
                content += "</table></div>";
            }
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

        public static async Task<List<EbayItem>> GetPokecardEbayResults(string cardSet, string cardNumber)
        {
            var ebayItems = new List<EbayItem>();

            cardSet = PokecardSlot.BeautifySetName(cardSet);
            if (string.IsNullOrEmpty(cardSet) || string.IsNullOrEmpty(cardNumber) || cardSet.ToUpper() == "NONE" || cardNumber == "0") return ebayItems;

            cardNumber = cardNumber.ToUpper().Substring(cardNumber.StartsWith("SWSH") ? 4 : 0).TrimStart('0').Replace("-", "/");
            if (cardSet == "Promo") cardNumber = "SWSH" + cardNumber;

            var url = $"https://www.ebay.com.au/sch/i.html?_from=R40&_nkw={cardSet.Replace(" ", "+")}+{cardNumber}+-digital+-grade+-graded+-PSA+-DSG&_sacat=0&LH_TitleDesc=0&LH_BIN=1&_sop=15&rt=nc&LH_PrefLoc=1";
            var ebayResponse = await new HttpClient().GetAsync(url);
            var ebayHtml = ebayResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            var itemCount = 0;
            foreach (var listedItem in ebayHtml.Split("s-item s-item__pl-on-bottom s-item--watch-at-corner"))
            {
                if (itemCount == 5) break;
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
                        itemCount++;
                    }
                }
            }
            return ebayItems;
        }

    }

}