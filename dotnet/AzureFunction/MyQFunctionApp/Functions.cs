using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace MyQFunctionApp
{
    public static class Functions
    {
        private static readonly HttpClient _client = new HttpClient();

        [FunctionName(nameof(ControlDoor))]
        public static async Task<IActionResult> ControlDoor(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            var kvClient = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(new AzureServiceTokenProvider().KeyVaultTokenCallback), _client);

            var usernameUri = req.Headers[@"x-keyvaultUsernameUri"].ToString();
            var username = (await kvClient.GetSecretAsync(usernameUri)).Value;
            if (string.IsNullOrWhiteSpace(username))
            {
                log.LogError($@"No username found at {usernameUri}");
                return new ObjectResult(@"No username found at keyvault uri given for username")
                {
                    StatusCode = StatusCodes.Status400BadRequest
                };
            }

            var pwdUri = req.Headers[@"x-keyvaultPwdUri"].ToString();
            var pwd = (await kvClient.GetSecretAsync(pwdUri)).Value;
            if (string.IsNullOrWhiteSpace(pwd))
            {
                log.LogError($@"No password found at {pwdUri}");
                return new ObjectResult(@"No password found at keyvault uri given for username")
                {
                    StatusCode = StatusCodes.Status400BadRequest
                };
            }

            try
            {
                var loginResult = await MyQClient.MyQClient.Instance.LoginAsync(username, pwd);
                if (!string.IsNullOrWhiteSpace(loginResult))
                {
                    var body = JObject.Parse(await req.ReadAsStringAsync());
                    var op = body.Value<string>(@"op");
                    if (op.Equals(@"open", StringComparison.OrdinalIgnoreCase))
                    {
                        if (bool.Parse(Environment.GetEnvironmentVariable(@"CanOpen") ?? "false"))  //default secure
                        {
                            var targetDoor = (await MyQClient.MyQClient.Instance.GetGarageDoorsAsync()).First();
                            await MyQClient.MyQClient.Instance.OpenDoorAsync(targetDoor);
                            log.LogInformation(@"Door open request sent.");
                        }
                        else
                        {
                            log.LogWarning(@"Open request denied by application setting");
                            return new ObjectResult(@"Opening is disabled. Set the 'CanOpen' application setting to 'true' to enable.")
                            {
                                StatusCode = StatusCodes.Status401Unauthorized
                            };
                        }
                    }
                    else if (op.Equals(@"close", StringComparison.OrdinalIgnoreCase))
                    {
                        var targetDoor = (await MyQClient.MyQClient.Instance.GetGarageDoorsAsync()).First();
                        await MyQClient.MyQClient.Instance.CloseDoorAsync(targetDoor);
                        log.LogInformation(@"Door close request sent.");
                    }
                }

                return new AcceptedResult();
            }
            catch
            {
                return new BadRequestResult();
            }
        }
    }
}
