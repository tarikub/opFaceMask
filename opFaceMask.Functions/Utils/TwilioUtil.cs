using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Text;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Rest.Api.V2010.Account.AvailablePhoneNumberCountry;

namespace FnOpFaceMask.Utils
{
    public static class TwilioUtil
    {
        public static TwilioResponse Get(HttpRequest request)
        {
            var rtn = new TwilioResponse
            {
                Body = request.Form["Body"],
                DateCreated = request.Form["DateCreated"],
                MessageSid = request.Form["MessageSid"],
                From = request.Form["From"].ToString().Replace("+", ""),
                To = request.Form["To"].ToString().Replace("+", ""),
                Operation = DetermineIntent(request.Form["Body"])
            };

            if (!string.IsNullOrEmpty(request.Form["NumMedia"]) && request.Form["NumMedia"] != "0")
            {
                rtn.Medias = "";
                var numMedia = int.Parse(request.Form["NumMedia"]);
                var strBuilder = new StringBuilder();
                for (int resourceId = 0; resourceId < numMedia; resourceId++)
                {
                    var mediaUrl = request.Form[$"MediaUrl{resourceId}"];
                    strBuilder.Append($"{mediaUrl} ");
                }
                rtn.Medias = strBuilder.ToString();
            }

            return rtn;
        }

        private static Operations DetermineIntent(string message)
        {
            var intent = Operations.None;

            var ops = Enum.GetNames(typeof(Operations));

            for (var i = 0; i < ops.Length; i++)
            {
                if (message.ToLower().Contains(ops[i].ToLower()))
                {
                    return (Operations)Enum.Parse(typeof(Operations), ops[i]);
                }
            }

            if (intent == Operations.None && !string.IsNullOrEmpty(message))
            {
                return Operations.DonateLocation;
            }

            return intent;
        }

        public static LocalResource GetTwilioNumber(int areaCode)
        {
            var accountSid = ConfigUtil.GetEnvironmentVariable("accountSid");
            var authToken = ConfigUtil.GetEnvironmentVariable("authToken");
            TwilioClient.Init(accountSid, authToken);
            var local = LocalResource.Read(areaCode: areaCode, pathCountryCode: "US", limit: 1, smsEnabled: true);

            return local.FirstOrDefault();
        }

        public static string ProvisionNumber(string phoneNumber)
        {
            var accountSid = ConfigUtil.GetEnvironmentVariable("accountSid");
            var authToken = ConfigUtil.GetEnvironmentVariable("authToken");
            TwilioClient.Init(accountSid, authToken);

            var incomingPhoneNumber = IncomingPhoneNumberResource.Create(
                phoneNumber: new Twilio.Types.PhoneNumber(phoneNumber)
            );

            UpdateNumber(incomingPhoneNumber);

            return incomingPhoneNumber.Sid;
        }

        public static void UpdateNumber(IncomingPhoneNumberResource phoneNumber)
        {
            var accountSid = ConfigUtil.GetEnvironmentVariable("accountSid");
            var authToken = ConfigUtil.GetEnvironmentVariable("authToken");
            var webHookUrl = ConfigUtil.GetEnvironmentVariable("webHookUrl");
            var voiceHookUrl = ConfigUtil.GetEnvironmentVariable("voiceUrl");
            TwilioClient.Init(accountSid, authToken);

            IncomingPhoneNumberResource.Update(
                accountSid: phoneNumber.AccountSid,
                pathSid: phoneNumber.Sid,
                smsUrl: new Uri(webHookUrl),
                voiceUrl: new Uri(voiceHookUrl)
            );
        }

        public static void DeleteNumber(string phoneSID)
        {
            var accountSid = ConfigUtil.GetEnvironmentVariable("accountSid");
            var authToken = ConfigUtil.GetEnvironmentVariable("authToken");
            TwilioClient.Init(accountSid, authToken);

            IncomingPhoneNumberResource.Delete(
                pathSid: phoneSID
            );
        }

        public static void Notify(string to, string body, ILogger log, string appPhone = "")
        {
            var simulated = bool.Parse(ConfigUtil.GetEnvironmentVariable("Simulated"));
            if (simulated)
            {
                Console.WriteLine($"{to} --- {body}");
                return;
            }

            var accountSid = ConfigUtil.GetEnvironmentVariable("accountSid");
            var authToken = ConfigUtil.GetEnvironmentVariable("authToken");
            if (string.IsNullOrEmpty(appPhone))
            {
                appPhone = ConfigUtil.GetEnvironmentVariable("appPhone");
            }

            TwilioClient.Init(accountSid, authToken);

            var message = MessageResource.Create(
            body: body,
            from: new Twilio.Types.PhoneNumber(appPhone),
            to: new Twilio.Types.PhoneNumber(to)
        );
            if (!string.IsNullOrEmpty(message.Sid))
            {
                log.Log(LogLevel.Information, $"Message {body} sent to {to}");
            }
            else
            {
                log.Log(LogLevel.Information, $"Failed to send Message {body} sent to {to}");
            }
        }
    }
}