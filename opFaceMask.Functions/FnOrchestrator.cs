using FnOpFaceMask.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace FnOpFaceMask
{
    public static class FnOrchestrator
    {
        [FunctionName(nameof(Run))]
        public static async Task<IActionResult> Run(
           [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
           [DurableClient]IDurableClient client,
            ILogger log)
        {
            var res = TwilioUtil.Get(req);
            var msgProcessed = true;
            var responseMessage = "";
            try
            {
                switch (res.Operation)
                {
                    case Operations.None:
                        {
                            await client.StartNewAsync(nameof(FnOrchestrator.Help), res);
                            break;
                        }
                    case Operations.Donate:
                        {
                            await client.StartNewAsync(nameof(FnDonate.Donate), res);
                            break;
                        }
                    case Operations.DonateLocation:
                        {
                            await client.StartNewAsync(nameof(FnDonate.DonateLocation), res);
                            break;
                        }
                    case Operations.Start:
                        {
                            await client.StartNewAsync(nameof(FnDonate.Start), res);
                            break;
                        }
                    case Operations.Receive:
                        {
                            await client.StartNewAsync(nameof(FnReceive.Receive), res);
                            break;
                        }
                    case Operations.Close:
                        {
                            await client.StartNewAsync(nameof(FnDonate.Close), res);
                            break;
                        }
                    default:
                        {
                            await client.StartNewAsync(nameof(FnOrchestrator.Help), res);
                            break;
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

        [FunctionName(nameof(Help))]
        public static async Task<IActionResult> Help(
         [OrchestrationTrigger]IDurableOrchestrationContext context,
         [DurableClient]IDurableClient client,
         ILogger log)
        {
            var twilioResponse = context.GetInput<TwilioResponse>();
            var msgProcessed = true;
            var responseMessage = "";
            try
            {
                var operationMessages = @"
                    text 'start' to start a donation drive \n
                    text 'close' to end a donation drive \n
                    text 'donate' to donate items \n
                    text 'receive' to receive donated items \n
                ";
                TwilioUtil.Notify(twilioResponse.From, operationMessages, log);
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