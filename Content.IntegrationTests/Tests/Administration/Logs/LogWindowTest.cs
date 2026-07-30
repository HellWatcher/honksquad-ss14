using System.Linq;
using Content.Client.Administration.UI;
using Content.Client.Administration.UI.CustomControls;
using Content.Client.Administration.UI.Logs;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.MenuBar.Widgets;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.Administration.Commands;
using Content.Server.Administration.Logs;
using Content.Shared.Database;

namespace Content.IntegrationTests.Tests.Administration.Logs;

public sealed class LogWindowTest : InteractionTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true, AdminLogsEnabled = true, DummyTicker = false };

    [Test]
    public async Task TestAdminLogsWindow()
    {
        // First, generate a new log
        var log = Server.Resolve<IAdminLogManager>();
        var guid = Guid.NewGuid();
        await Server.WaitPost(() => log.Add(LogType.Unknown, $"{SPlayer} test log 1: {guid}"));

        // Click the admin button in the menu bar
        await ClickWidgetControl<GameTopMenuBar, MenuButton>(nameof(GameTopMenuBar.AdminButton));
        var adminWindow = GetWindow<AdminMenuWindow>();

        // Find and click the "open logs" button.
        Assert.That(TryGetControlFromChildren<CommandButton>(x => x.Command == OpenAdminLogsCommand.Cmd, adminWindow, out var btn));
        await ClickControl(btn!);
        var logWindow = GetWindow<AdminLogsWindow>();

        // Find the log search field and refresh buttons
        var search = logWindow.Logs.LogSearch;
        var refresh = logWindow.Logs.RefreshButton;
        var cont = logWindow.Logs.LogsContainer;

        async Task<AdminLogLabel[]> SearchForLog(Guid logGuid)
        {
            await Client.WaitPost(() => search.Text = logGuid.ToString());
            await ClickControl(refresh);

            await RunUntilSynced();
            await RunTicks(10);

            //HONK START - deflake: log requests are serviced asynchronously (Task.Run) and replied
            // over the network, so a fixed wait isn't always enough for the reply to arrive and
            // populate the container. Poll until the expected log shows up instead.
            AdminLogLabel[] result = [];
            for (var i = 0; i < 30; i++)
            {
                result = cont.Children
                    .Where(x => x.Visible && x is AdminLogLabel)
                    .Cast<AdminLogLabel>()
                    .ToArray();
                if (result.Length == 1 && result[0].Log.Message.Contains(logGuid.ToString()))
                    break;

                await RunTicks(5);
            }

            return result;
            //HONK END
        }

        // Search for the log we added earlier.
        var searchResult = await SearchForLog(guid);
        Assert.That(searchResult, Has.Length.EqualTo(1));
        Assert.That(searchResult[0].Log.Message, Contains.Substring($" test log 1: {guid}"));

        // Add a new log
        guid = Guid.NewGuid();
        await Server.WaitPost(() => log.Add(LogType.Unknown, $"{SPlayer} test log 2: {guid}"));

        // Update the search and refresh
        searchResult = await SearchForLog(guid);
        Assert.That(searchResult, Has.Length.EqualTo(1));
        Assert.That(searchResult[0].Log.Message, Contains.Substring($" test log 2: {guid}"));
    }
}
