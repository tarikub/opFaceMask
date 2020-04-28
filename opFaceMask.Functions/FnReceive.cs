using FnOpFaceMask.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace FnOpFaceMask
{
    public static class FnReceive
    {
        [FunctionName(nameof(Receive))]
        public static async Task<IActionResult> Receive(
          [OrchestrationTrigger]IDurableOrchestrationContext context,
            [Table(nameof(Center))]CloudTable centerTable,
          ILogger log)
        {
            var twilioResponse = context.GetInput<TwilioResponse>();
            var centerClient = new DataAccess<Center>(centerTable);
            var msgProcessed = true;
            var responseMessage = "";
            try
            {
                var centerFilter = new TableQuery<Center>()
                .Where(TableQuery.GenerateFilterCondition("DonationPhoneNumber", QueryComparisons.Equal,
                twilioResponse.To));

                var centers = await centerClient.GetAllAsync(centerFilter);
                var center = centers.FirstOrDefault();
                var donatedItem = ConfigUtil.GetEnvironmentVariable("donatedItem");

                if (center != null)
                {
                    if (center.Active)
                    {
                        TwilioUtil.Notify(twilioResponse.From, string.Format(Messages.Receive, donatedItem, center.Address), log);
                    }
                    else
                    {
                        TwilioUtil.Notify(twilioResponse.From, Messages.Close, log);
                    }
                }
            }
            catch (Exception ex)
            {
                msgProcessed = false;
                responseMessage = $"{ex.Message} {ex.StackTrace} {ex?.InnerException?.StackTrace ?? ""}";
            }
            return msgProcessed ? new OkObjectResult(msgProcessed) : (IActionResult)new BadRequestObjectResult(responseMessage);
        }
    }
}