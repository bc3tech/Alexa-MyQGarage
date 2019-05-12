using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace MyQClient
{
    public class MyQClient
    {
        private static readonly HttpClient _httpClient = new HttpClient { BaseAddress = new Uri(Environment.GetEnvironmentVariable(@"MyQBaseAddress")) };
        private static MyQClient _instance;

        private MyQClient()
        {
            _httpClient.DefaultRequestHeaders.Add(@"MyQApplicationId", Environment.GetEnvironmentVariable(@"MyQApplicationId"));
        }

        public static MyQClient Instance => _instance ?? (_instance = new MyQClient());

        private string _securityToken;

        private async Task<string> GetTokenAsync()
        {
            if (!string.IsNullOrWhiteSpace(_securityToken)) return _securityToken;

            for (int i = 0; i < 10; i++)
            {
                try
                {
                    var payload = JObject.FromObject(new
                    {
                        username = Environment.GetEnvironmentVariable(@"MyQUsername"),
                        password = Environment.GetEnvironmentVariable(@"MyQPassword")
                    });

                    var loginResponse = await _httpClient.PostAsync(Environment.GetEnvironmentVariable(@"LoginEndpoint"),
                        new StringContent(payload.ToString(), Encoding.Default, @"application/json"));

                    string loginResultString = await loginResponse.Content.ReadAsStringAsync();
                    var loginResult = JObject.Parse(loginResultString);

                    if (loginResult.Value<string>(@"ReturnCode") == "0"
                        && string.IsNullOrWhiteSpace(loginResult.Value<string>(@"ErrorMessage"))
                        && !string.IsNullOrWhiteSpace(loginResult.Value<string>(@"SecurityToken")))
                    {
                        _httpClient.DefaultRequestHeaders.Remove(@"SecurityToken");

                        _securityToken = loginResult.Value<string>(@"SecurityToken");
                        _httpClient.DefaultRequestHeaders.Add(@"SecurityToken", _securityToken);
                        return _securityToken;
                    }
                }
                catch (Exception ex)
                {
                    Trace.TraceError(ex.ToString());

                    await Task.Delay(1000);
                }

            }

            _securityToken = null;
            throw new AccessViolationException($@"Didn't get a successful login within 10 seconds. Check your username & password in Application Settings and try again.");
        }

        private Task<string> ResetTokenAsync()
        {
            _securityToken = null;
            return GetTokenAsync();
        }

        private JObject EnsureSuccessfulResponse(string responseBody)
        {
            var jObject = JObject.Parse(responseBody);

            if (jObject.Value<string>(@"ReturnCode") != "0"
                || !string.IsNullOrWhiteSpace(jObject.Value<string>(@"ErrorMessage")))
            {
                throw new ApplicationException($@"Response request failed: {responseBody}");
            }

            return jObject;
        }

        public Task<string> LoginAsync() => GetTokenAsync();

        public async Task<string> GetSystemDetailAsync()
        {
            for (int i = 0; i < 10; i++)
            {
                var systemDetailResponse = await _httpClient.GetStringAsync(Environment.GetEnvironmentVariable(@"GetSystemDetailEndpoint"));
                try
                {
                    return EnsureSuccessfulResponse(systemDetailResponse).ToString();
                }
                catch (ApplicationException)
                {
                    await ResetTokenAsync();
                    await Task.Delay(1000);
                }
            }

            throw new AccessViolationException($@"Didn't get a successful login within 10 seconds. Check your username & password in Application Settings and try again.");
        }

        private static readonly string[] SUPPORTED_DEVICE_TYPE_NAMES = new[] { @"GarageDoorOpener", "Garage Door Opener WGDO", "VGDO" };

        public async Task<IEnumerable<GarageDoor>> GetGarageDoorsAsync()
        {
            var allDevices = await GetSystemDetailAsync();

            var allDevicesOjb = JObject.Parse(allDevices).Value<JArray>(@"Devices");
            var doors = allDevicesOjb
                .Where(device => SUPPORTED_DEVICE_TYPE_NAMES.Contains(device.Value<string>(@"MyQDeviceTypeName")))
                .Select(door => new GarageDoor
                {
                    DeviceId = door.Value<string>(@"MyQDeviceId"),
                    Name = door.Value<JArray>(@"Attributes").FirstOrDefault(a => a.Value<string>("AttributeDisplayName") == @"desc")?.Value<string>(@"Value"),
                    RawValue = door
                });

            return doors;
        }

        public async Task<DoorState> GetDoorStatusAsync(string deviceId)
        {
            var door = (await GetGarageDoorsAsync()).Single(d => d.DeviceId == deviceId);

            var rawStateValue = door.RawValue.Value<JArray>(@"Attributes").FirstOrDefault(a => a.Value<string>(@"AttributeDisplayName") == "doorstate")?.Value<string>(@"Value");

            return (DoorState)int.Parse(rawStateValue);
        }

        private async Task<bool> SetDoorStateAsync(GarageDoor door, DoorState desiredState)
        {
            var payload = JObject.FromObject(new
            {
                attributeName = "desireddoorstate",
                myQDeviceId = door.DeviceId,
                AttributeValue = ((int)desiredState).ToString()
            });

            var setStateResponse = await _httpClient.PutAsync(Environment.GetEnvironmentVariable(@"SetStateEndpoint"),
                new StringContent(payload.ToString(), Encoding.Default, @"application/json"));

            return setStateResponse.IsSuccessStatusCode;
        }

        public Task<bool> OpenDoorAsync(GarageDoor door) => SetDoorStateAsync(door, DoorState.Open);
        public Task<bool> CloseDoorAsync(GarageDoor door) => SetDoorStateAsync(door, DoorState.Closed);

        public class GarageDoor
        {
            public string Name { get; set; }
            public string DeviceId { get; set; }
            public JToken RawValue { get; internal set; }
        }

        public enum DoorState
        {
            Closed = 0,
            Open = 1,
            OtherClosed = 2,
            Opening = 4,
            Closing = 5,
            InTransition = 8,
            OtherOpen = 9
        }
    }
}
