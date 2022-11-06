using Microsoft.Azure.Cosmos.Table;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Pokebook
{
    public class PokemonBookPage
    {
        private readonly CloudTable PokemonCardCellTable;
        private readonly CloudTable PokemonUserTable;
        private readonly Dictionary<(string, string), PokemonCardCellEntity> PokemonCardCells;
        private readonly string UserName;
        private readonly int PageCount;

        public PokemonBookPage(CloudTable pokemonCardCellTable, CloudTable pokemonUserTable, string username)
        {
            PokemonCardCellTable = pokemonCardCellTable;
            PokemonUserTable = pokemonUserTable;
            PokemonCardCells = new Dictionary<(string, string), PokemonCardCellEntity>();
            UserName = string.IsNullOrEmpty(username) ? "" : username.ToLower();
            PageCount = 12;

            var pokebookUsers = PokemonUserEntity.GetPokebookUser(PokemonUserTable, UserName).GetAwaiter().GetResult();
            if (pokebookUsers.Results.Count != 0)
            {
                PageCount = int.Parse(pokebookUsers.Results[0].PageCount);
                foreach (var cardCell in PokemonCardCellEntity.GetPokemonCardCells(PokemonCardCellTable, UserName).GetAwaiter().GetResult())
                {
                    PokemonCardCells.Add((cardCell.PartitionKey, cardCell.RowKey), cardCell);
                }
            }
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
            var newUser = UserName == "";

            var content = $"<script>username = \"{UserName}\"; pageCount = {PageCount};</script>";

            content += "<div style='float:left;width:300;'>";

            content += "<h3>Page Selector:</h3>";
            content += "<div style='display:flex;'>";
            content += "<button disabled id='PagePreviousButton' onclick=\"MovePage('previous')\">Previous</button>";
            content += "<div id='PageNumber' style='width:100;text-align: center;'>Page <b>1</b></div>";
            content += "<button id='PageNextButton' onclick=\"MovePage('next')\">Next</button>";
            content += "</div><br><br><br>";

            content += "<div>";
            content += "<h3>User Details:</h3>";
            content += $"Username: <input id='UsernameInput' style='width:150;' onkeyup=\"UserDetailSelectorOnChange()\" {(newUser ? "" : "disabled")} value='{(newUser ? "" : UserName)}'><br>";
            content += $"Page Count: <input id='PageCountInput' style='width:140;' onkeyup=\"UserDetailSelectorOnChange()\" onChange=\"UserDetailSelectorOnChange()\" type='number' min='2' max='100' value='{(newUser ? "12" : $"{PageCount}")}'><br>";
            content += "</div><br><br><br>";

            content += "<div>";
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
            content += "<option value='Astral Radiance'>Astral Radiance</option>";
            content += "<option value='Lost Origin'>Lost Origin</option>";
            content += "<option value='Silver Tempest'>Silver Tempest</option>";
            content += "<option value='Crown Zenith'>Crown Zenith</option>";
            content += "</select><br>";
            content += "Card Number: <input id='CardNumberInput' onkeyup=\"SelectorOnChange()\" style='width:125;' disabled><br>";
            content += "<button id='ClearSelection' onclick='ClearSelection()' disabled>Clear Selection</button><br><br>";
            content += "<button id='UpdateCard' onclick='UpdateCard()' disabled>Update Card</button>";
            content += "</div><br><br><br>";

            content += "<div>";
            content += "<h3>Updated Cards:</h3>";
            content += "<ul id='UpdatedCardsList'></ul><br>";
            content += "<button id='UndoUpdates' onclick='UndoAllUpdates()' disabled>Undo All Updates</button><br><br>";
            content += "<button id='SaveUpdates' onclick='SaveUpdates()' disabled>Save Updates</button><br><br>";
            content += "</div><br><br><br>";

            content += "</div>";


            content += "<div style='float:left'>";
            for (int page = 0; page <= PageCount + 1; page++)
            {
                content += $"<div class='PokemonCardPage' id='PokemonCardPage{page}' style='display:inline-block;{(page == 0 || page == PageCount + 1 ? "opacity:0.3" : "")}'><table>";
                for (int row = 0; row < 3; row++)
                {
                    content += "<tr>";
                    for (int column = 0; column < 3; column++)
                    {
                        var slot = (3 * row + column) + 1;
                        var pageSlot = ($"{page}", $"{slot}");
                        var set = "NONE";
                        var cardNumber = "0";

                        if (page != PageCount + 1 && PokemonCardCells.ContainsKey(pageSlot))
                        {
                            set = PokemonCardCells[pageSlot].SetName;
                            cardNumber = PokemonCardCells[pageSlot].CardNumber;
                        }

                        var url = $"/api/card-image/{set}/{cardNumber.Replace("/", "-")}";
                        var clickAction = page == 0 || page == PageCount + 1 ? "" : $"onclick=\"SelectCard({page}, {slot}, '{set}', '{cardNumber}')\"";
                        content += $"<td><div class='PokemonCardCell' id='Cell_{page}_{slot}' style='background-image:url(\"{url}\")' {clickAction}></div></td>";
                    }
                    content += "</tr>";
                }
                content += "</table></div>";
            }
            content += "</div>";



            return content;
        }
    }
}
