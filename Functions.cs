using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using System.Net.Http;
using Microsoft.Azure.Cosmos.Table;
using System.Collections.Generic;
using System.Linq;
using System;

namespace Pokebook
{
    public static class Functions
    {
        [FunctionName("GetPokemonCardImage")]
        public static async Task<FileContentResult> GetPokemonCardImage(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "card-image/{set}/{number}")] HttpRequest req, ExecutionContext exeCon,
            string set, string number)
        {
            var urls = new List<string>()
            {
                PokecardSlot.GetPokecardImageUrl(set, number),
                PokecardSlot.GetPokecardImageUrl2(set, number),
                PokecardSlot.GetPokecardImageUrlZenith(set, number),
                "https://www.mypokecard.com/en/Gallery/my/galery/5c4Z3OUCKurc.jpg"
            };

            foreach (var url in urls)
            {
                if (url == "") continue;
                var imageResponse = await new HttpClient().GetAsync(url);
                if (imageResponse.IsSuccessStatusCode) 
                    return new FileContentResult(await (imageResponse).Content.ReadAsByteArrayAsync(), "image/jpeg");
            }

            return new FileContentResult(Array.Empty<byte>(), "image/jpeg"); 
        }

        [FunctionName("GetPokemonEbayPrices")]
        public static async Task<ActionResult> GetPokemonCardEbayResults(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "ebay-prices/{set}/{number}")] HttpRequest req, ExecutionContext exeCon,
            string set, string number)
        {
            var json = (await EbayHelper.GetPokecardEbayPrices(set, number))
                .Take(req.Query["resultsCount"].Count > 0 && int.TryParse(req.Query["resultsCount"].ToString(), out int rc) ? rc : 5);

            if (req.Query["json"].Count > 0) return new OkObjectResult(json);

            return new ContentResult { Content = string.Join("", json.Select(x => $"<p>${x.Price:N2} <a href='{x.Link}' target='_blank'>{x.Name}</a></p>")), ContentType = "text/html" };
        }

        [FunctionName("GetLucasProperties")]
        public static async Task CheckLucasPropertiesUpdates([TimerTrigger("0 */30 * * * *")] TimerInfo myTimer)
        {
            try
            {
                await LucasHelper.SetUpDatabaseTables();
                await LucasHelper.UpdateLucasProperties();
            } catch (Exception e)
            {
                await LucasHelper.PingDiscord($"An Error has occured:\\n{e}");
            }
        }

        [FunctionName("DomTest")]
        public static async Task<OkObjectResult> Test([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "dom-test")] HttpRequest req, ExecutionContext exeCon)
        {
            var results = await LucasHelper.UpdateLucasProperties();
            return new OkObjectResult(results == "" ? "No changes" : results);
        }

        [FunctionName("PokemonBook")]
        public static async Task<ContentResult> ProducePokemonBookHtmlPage(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "pokemon-book")] HttpRequest req,
            [Table("PokecardSlot")] CloudTable pokecardSlotTable, [Table("PokecardBook")] CloudTable pokecardBookTable)
        {
            var htmlHelper = new PokemonBookPage(pokecardSlotTable, pokecardBookTable, req.Query["book"].ToString());
            var pageContent = await htmlHelper.GetHtml();
            return new ContentResult { Content = pageContent, ContentType = "text/html" };
        }

        [FunctionName("UpdateBook")]
        public static async Task<ContentResult> UpdateBook(
           [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "update-book")] HttpRequest req, ExecutionContext exeCon,
            [Table("PokecardSlot")] CloudTable pokecardSlotTable, [Table("PokecardBook")] CloudTable pokecardBookTable)
        {
            var bookName = req.Query["book"].ToString(); 
            var listOfUpdates = req.Query["updates"].ToString().TrimEnd(',');
            await PokecardSlot.InjectUpdates(pokecardSlotTable, listOfUpdates, bookName);

            var newPageCount = req.Query["pages"].ToString();
            if (newPageCount != "")
            {
                await pokecardBookTable.ExecuteAsync(TableOperation.InsertOrMerge(new PokecardBook
                {
                    ETag = "*",
                    PartitionKey = bookName,
                    RowKey = bookName,
                    PageCount = newPageCount
                }));
            }

            var html = $"<!DOCTYPE html><html><head><meta http-equiv=\"refresh\" content=\"0; url=http{(req.IsHttps ? "s" : "")}://{req.Host.Value}/api/pokemon-book?book={bookName}\"/></head></html>";
            return new ContentResult { Content = html, ContentType = "text/html" };
        }
    }
}
