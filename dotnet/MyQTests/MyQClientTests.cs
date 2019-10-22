using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace MyQTests
{
    public class MyQClientTests : IClassFixture<MyQClientTestFixture>
    {
        private ITestOutputHelper Output { get; set; }

        public MyQClientTests(ITestOutputHelper output)
        {
            this.Output = output;
        }

        [Fact]
        public async Task LoginWorks()
        {
            var securityToken = await MyQClient.MyQClient.Instance.LoginAsync();

            Assert.NotEmpty(securityToken);

            this.Output.WriteLine($@"Resulting security token: {securityToken}");
        }

        [Fact]
        public async Task GetSystemDetailGetsStuff()
        {
            var systemDetail = await MyQClient.MyQClient.Instance.GetSystemDetailAsync();

            Assert.NotEmpty(systemDetail);

            this.Output.WriteLine($@"Resulting system detail: {systemDetail}");
        }

        [Fact]
        public async Task FindsOneOrMoreGarageDoors()
        {
            var doors = await MyQClient.MyQClient.Instance.GetGarageDoorsAsync();

            Assert.True(doors.Count() > 0);
        }

        [Fact]
        public async Task CanGetDoorState()
        {
            var targetDoor = (await MyQClient.MyQClient.Instance.GetGarageDoorsAsync()).First();

            var doorState = await MyQClient.MyQClient.Instance.GetDoorStatusAsync(targetDoor.DeviceId);

            this.Output.WriteLine($@"Door State: {doorState}");
        }

        [Fact]
        public async Task CanOpenDoor()
        {
            var targetDoor = (await MyQClient.MyQClient.Instance.GetGarageDoorsAsync()).First();

            Assert.True(await MyQClient.MyQClient.Instance.OpenDoorAsync(targetDoor));
        }

        [Fact]
        public async Task CanCloseDoor()
        {
            var targetDoor = (await MyQClient.MyQClient.Instance.GetGarageDoorsAsync()).First();

            Assert.True(await MyQClient.MyQClient.Instance.CloseDoorAsync(targetDoor));
        }
    }

    public sealed class MyQClientTestFixture : IDisposable
    {
        public MyQClientTestFixture()
        {
            Environment.SetEnvironmentVariable("MyQApplicationId", "JVM/G9Nwih5BwKgNCjLxiFUQxQijAebyyg8QUHr7JOrP+tuPb8iHfRHKwTmDzHOu");
            Environment.SetEnvironmentVariable("MyQCulture", "en");
            Environment.SetEnvironmentVariable("MyQBaseAddress", "https://myqexternal.myqdevice.com");
            Environment.SetEnvironmentVariable("LoginEndpoint", "api/v4/User/Validate");
            Environment.SetEnvironmentVariable("GetSystemDetailEndpoint", "api/v4/UserDeviceDetails/Get");
            Environment.SetEnvironmentVariable("SetStateEndpoint", "api/v4/DeviceAttribute/PutDeviceAttribute");

            Environment.SetEnvironmentVariable("MyQUsername", "<your username here>");
            Environment.SetEnvironmentVariable("MyQPassword", "<your password here>");
        }

        public void Dispose()
        {
            // teardown
        }
    }
}
