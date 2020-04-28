using FnOpFaceMask.Entities;
using FnOpFaceMask.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace FnOpFaceMask
{
    public static class FnDonate
    {
        [FunctionName(nameof(Start))]
        public static async Task<IActionResult> Start(
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
                 .Where(TableQuery.GenerateFilterCondition("HostPhoneNumber", QueryComparisons.Equal,
                 twilioResponse.From));

                var centers = await centerClient.GetAllAsync(centerFilter);
                var center = centers.FirstOrDefault();

                if (center == null)
                {
                    await centerClient.InsertAsync(new Center
                    {
                        Active = false,
                        Address = string.Empty,
                        Lat = string.Empty,
                        HostPhoneNumber = twilioResponse.From,
                        DonationPhoneNumber = string.Empty
                    });

                    TwilioUtil.Notify(twilioResponse.From, Messages.Host, log);
                }
                else
                {
                    TwilioUtil.Notify(twilioResponse.From, string.Format(Messages.EventFound), log);
                }
            }
            catch (Exception ex)
            {
                msgProcessed = false;
                responseMessage = $"{ex.Message} {ex.StackTrace} {ex?.InnerException?.StackTrace ?? ""}";
            }
            return msgProcessed ? new OkObjectResult(msgProcessed) : (IActionResult)new BadRequestObjectResult(responseMessage);
        }

        [FunctionName(nameof(Donate))]
        public static async Task<IActionResult> Donate(
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
                if (center != null)
                {
                    if (center.Active)
                    {
                        // Notify donor
                        TwilioUtil.Notify(twilioResponse.From, string.Format(Messages.Donate, center.Address), log);
                        // Notify host
                        TwilioUtil.Notify(center.HostPhoneNumber, string.Format(Messages.DonationReceipt, twilioResponse.From, twilioResponse.Body), log);
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

        [FunctionName(nameof(DonateLocation))]
        public static async Task<IActionResult> DonateLocation(
          [OrchestrationTrigger]IDurableOrchestrationContext context,
          [SignalR(HubName = "center")]IAsyncCollector<SignalRMessage> signalRMessages,
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
                  .Where(TableQuery.GenerateFilterCondition("HostPhoneNumber", QueryComparisons.Equal,
                  twilioResponse.From));
                var centers = await centerClient.GetAllAsync(centerFilter);
                var center = centers.FirstOrDefault();
                var donatedItem = ConfigUtil.GetEnvironmentVariable("donatedItem");

                if (center != null)
                {
                    var geoAddress = await GetLocationAsync(twilioResponse.Body);
                    if (geoAddress.results.Length > 0)
                    {
                        var address = geoAddress.results.FirstOrDefault();
                        if (string.IsNullOrEmpty(center.DonationPhoneNumber))
                        {
                            var areaCode = int.Parse(twilioResponse.From.Substring(1, 3));
                            var newTwilioNumber = TwilioUtil.GetTwilioNumber(areaCode)?.PhoneNumber?.ToString();

                            var simulated = bool.Parse(ConfigUtil.GetEnvironmentVariable("Simulated"));
                            if (!simulated)
                            {
                                var phoneSID = TwilioUtil.ProvisionNumber(newTwilioNumber);
                                center.DonationPhoneNumberSID = phoneSID;
                            }
                            center.DonationPhoneNumber = newTwilioNumber;
                        }
                        center.Address = address.formatted_address;
                        center.Active = true;
                        center.Lat = address.geometry.location.lat.ToString();
                        center.Lng = address.geometry.location.lng.ToString();

                        await centerClient.ReplaceAsync(center);

                        TwilioUtil.Notify(twilioResponse.From, string.Format(Messages.DonationAddress, center.Address, donatedItem, center.DonationPhoneNumber), log);

                        var broadcastStat = new { Lat = center.Lat, Lng = center.Lng, PhoneNumber = center.DonationPhoneNumber };
                        await signalRMessages.AddAsync(
                          new SignalRMessage
                          {
                              Target = "BroadcastMessage",
                              Arguments = new[] { broadcastStat }
                          });
                    }
                    else
                    {
                        TwilioUtil.Notify(twilioResponse.From, string.Format(Messages.DonationAddressNotFound, center.Address), log);
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

        [FunctionName(nameof(Close))]
        public static async Task<IActionResult> Close(
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

                if (center != null && center.HostPhoneNumber == twilioResponse.From)
                {
                    center.Active = false;
                    var simulated = bool.Parse(ConfigUtil.GetEnvironmentVariable("Simulated"));
                    if (!simulated)
                    {
                        TwilioUtil.DeleteNumber(center.DonationPhoneNumberSID);
                    }
                    await centerClient.ReplaceAsync(center);
                }
            }
            catch (Exception ex)
            {
                msgProcessed = false;
                responseMessage = $"{ex.Message} {ex.StackTrace} {ex?.InnerException?.StackTrace ?? ""}";
            }
            return msgProcessed ? new OkObjectResult(msgProcessed) : (IActionResult)new BadRequestObjectResult(responseMessage);
        }

        [FunctionName("negotiate")]
        public static SignalRConnectionInfo Negotiate(
        [HttpTrigger(AuthorizationLevel.Anonymous)]HttpRequest req,
        [SignalRConnectionInfo
        (HubName = "center")]
        SignalRConnectionInfo connectionInfo)
        {
            // connectionInfo contains an access key token with a name identifier claim set to the authenticated user
            return connectionInfo;
        }

        #region "Private Methods"

        private static async Task<Rootobject> GetLocationAsync(string address)
        {
            Rootobject location = null;
            var googleApiKey = ConfigUtil.GetEnvironmentVariable("googleApiKey");
            var requestUrl = $"https://maps.googleapis.com/maps/api/geocode/json?address={address}&key={googleApiKey}";
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(requestUrl);
                var result = await client.GetAsync("");
                var resultContent = await result.Content.ReadAsStringAsync();

                location = JsonConvert.DeserializeObject<Rootobject>(resultContent);
            }
            return location;
        }

        #endregion "Private Methods"
    }
}