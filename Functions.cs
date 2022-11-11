using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using System.Net.Http;
using Microsoft.Azure.Cosmos.Table;

namespace Pokebook
{
    public static class Functions
    {
        [FunctionName("GetPokemonCardImage")]
        public static async Task<FileContentResult> GetPokemonCardImage(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "card-image/{set}/{number}")] HttpRequest req, Microsoft.Azure.WebJobs.ExecutionContext exeCon,
            string set, string number)
        {
            var url = PokecardSlot.GetPokecardImageUrl(set, number);
            var backupUrl = "https://www.mypokecard.com/en/Gallery/my/galery/5c4Z3OUCKurc.jpg";

            var imageResponse = await new HttpClient().GetAsync(url);
            if (imageResponse.IsSuccessStatusCode) 
                return new FileContentResult(await (imageResponse).Content.ReadAsByteArrayAsync(), "image/jpeg");
            return new FileContentResult(await (await new HttpClient().GetAsync(backupUrl)).Content.ReadAsByteArrayAsync(), "image/jpeg"); 
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
           [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "update-book")] HttpRequest req, Microsoft.Azure.WebJobs.ExecutionContext exeCon,
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