using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using System.Net.Http;
using Microsoft.Azure.Cosmos.Table;
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
            var urlNumber = 0;
            while (urlNumber <= 3)
            { 
                urlNumber++;
                var url = urlNumber switch
                {
                    1 => PokecardSlot.GetPokecardImageUrl(set, number),
                    2 => PokecardSlot.GetPokecardImageUrl2(set, number),
                    3 => "https://www.mypokecard.com/en/Gallery/my/galery/5c4Z3OUCKurc.jpg",
                    _ => ""
                };
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

        [FunctionName("MortgageCalculator")]
        public static async Task<ContentResult> MortgageCalculator([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "mortgage")] HttpRequest req, ExecutionContext exeCon)
        {
            var propertyPrice = req.Query["propertyprice"].Count > 0 ? int.Parse(req.Query["propertyprice"].ToString()) : 630000;
            var savings = req.Query["savings"].Count > 0 ? int.Parse(req.Query["savings"].ToString()) : 70000;
            var interestRatePercent = req.Query["interestrate"].Count > 0 ? double.Parse(req.Query["interestrate"].ToString()) : 5.2;
            var loanInYears = req.Query["loanlength"].Count > 0 ? int.Parse(req.Query["loanlength"].ToString()) : 30;

            var output = $"Property Price: ${propertyPrice}<br>";
            output += $"Current Savings: ${savings}<br>";
            output += $"Interest Rate: {interestRatePercent}%<br>";
            output += $"Loan Length: {loanInYears} Years<br><br><br>";

            double calculateStampDuty()
            {
                if (propertyPrice < 600000) return 0;
                var a = 0.0000004;
                var b = 0.6214/3;
                var x = propertyPrice - 600000;
                return a*(x*x) + (b*x);
            }
            var stampDuty = calculateStampDuty();
            output += $"Stamp Duty: ${Math.Round(stampDuty, 2)}<br>";

            double calculateTransferFee()
            {
                var paperLodgementFee = 101.7;
                var considerationFactor = 2.34;
                var maxPaper = 3612d;

                return Math.Min((Math.Floor(propertyPrice / 1000d) * considerationFactor) + paperLodgementFee, Math.Round(maxPaper));
            }
            var transferFee = calculateTransferFee();
            output += $"Transfer Fee: ${Math.Ceiling(transferFee)}<br>";

            var governmentFees = 124.00;
            output += $"Government Fees: ${governmentFees}<br><br>";

            var availableDeposit = savings - stampDuty - transferFee - governmentFees;
            output += $"Available Deposit: ${Math.Ceiling(availableDeposit)}<br>";

            var depositPercent = Math.Round((availableDeposit / propertyPrice) * 100, 2);
            output += $"Deposit percentage: {depositPercent}%<br>";

            double calculateLmi()
            {
                var loanAmount = propertyPrice - availableDeposit;

                var lvr = Math.Floor(100 * loanAmount / propertyPrice);
                if (lvr < 80) return 0;
                if (lvr > 95) return 100000;

                var variableScale = (lvr-80)/15;
                var a = variableScale * 3.343881;
                var b = 1200 * -0.0000000001630831 / variableScale;
                var c = -0.00001561 * variableScale /20;
                var d = 0.000015601 * variableScale /23;

                var multiplier =  (a - (b / c) * (1 - Math.Exp(d * loanAmount)));

                return multiplier / 100 * loanAmount * 0.978;
            }
            var lmiEstimate = calculateLmi();
            output += $"LMI: ${Math.Round(lmiEstimate, 2)}<br><br>";


            var loanAmount = propertyPrice - availableDeposit + lmiEstimate;
            output += $"Loan Amount: ${Math.Ceiling(loanAmount)}<br><br>";

            double calculateMonthlyMortgage()
            {
                var monthlyInterest = (interestRatePercent / 100) / 12;
                var NumberOfPayments = loanInYears*12;
                return loanAmount * (monthlyInterest * Math.Pow(1 + monthlyInterest, NumberOfPayments)) / (Math.Pow(1 + monthlyInterest, NumberOfPayments) - 1);
            }
            var MonthlyMortgage = Math.Round(calculateMonthlyMortgage(),2);
            output += $"Monthly Mortgage: ${MonthlyMortgage}<br>";

            var MonthlyMortgagePrincipal = loanAmount/(loanInYears*12);
            output += $"Monthly Mortgage Principal: ${Math.Round(MonthlyMortgagePrincipal, 2)}<br>";

            var MonthlyMortgageInterest = MonthlyMortgage - MonthlyMortgagePrincipal;
            output += $"Monthly Mortgage Interest: ${Math.Round(MonthlyMortgageInterest, 2)}<br>";

            return new ContentResult { Content = output, ContentType = "text/html" };
        }


        [FunctionName("Anime")]
        public static async Task<ContentResult> Anime([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "anime/{titleCode}")] HttpRequest req, ExecutionContext exeCon, string titleCode)
        {
            await AnimeHelper.SetUpDatabaseTables();
            var html = await AnimeHelper.GetLandingPageHtml(titleCode);

            return new ContentResult { Content = html, ContentType = "text/html" };
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

            var currentEpochTicks = DateTime.UtcNow.Ticks - DateTime.Parse("1970-01-01 00:00:00").Ticks;
            var receivedNonce = req.Query["nonce"].Count > 0 && long.TryParse(req.Query["nonce"].ToString(), out long nonce) ? nonce : 0;
            if (receivedNonce > (currentEpochTicks - 90000000) && receivedNonce < (currentEpochTicks + 90000000) || 
                (req.Query["nonce"].Count > 0 && req.Query["nonce"].ToString() == bookName))
            {
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
            }
            else
            {


                Console.WriteLine("Nonce failed");
            } 

            var html = $"<!DOCTYPE html><html><head><meta http-equiv=\"refresh\" content=\"0; url=http{(req.IsHttps ? "s" : "")}://{req.Host.Value}/api/pokemon-book?book={bookName}\"/></head></html>";
            return new ContentResult { Content = html, ContentType = "text/html" };
        }
    }
}
