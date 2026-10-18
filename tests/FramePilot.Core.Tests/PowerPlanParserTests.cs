using FramePilot.Core.Power;

namespace FramePilot.Core.Tests;

public class PowerPlanParserTests
{
    private const string ListOutput = """
Existing Power Schemes (* Active)
-----------------------------------
Power Scheme GUID: 381b4222-f694-41f0-9685-ff5bb260df2e  (Balanced) *
Power Scheme GUID: 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c  (High performance)
Power Scheme GUID: a1841308-3541-4fab-bc81-f71556f20b4a  (Power saver)
""";

    [Fact]
    public void ParseList_FindsAllPlans()
    {
        var plans = PowerPlanParser.ParseList(ListOutput);

        Assert.Equal(3, plans.Count);
        Assert.Equal("Balanced", plans[0].Name);
        Assert.Equal("High performance", plans[1].Name);
        Assert.Equal("Power saver", plans[2].Name);
    }

    [Fact]
    public void ParseList_MarksTheActivePlan()
    {
        var plans = PowerPlanParser.ParseList(ListOutput);

        Assert.True(plans[0].IsActive);
        Assert.False(plans[1].IsActive);
        Assert.False(plans[2].IsActive);
    }

    [Fact]
    public void ParseList_IgnoresLinesWithoutGuid()
    {
        var plans = PowerPlanParser.ParseList(ListOutput + Environment.NewLine + "some other text");

        Assert.Equal(3, plans.Count);
    }

    [Fact]
    public void ParseList_WithEmptyOutput_ReturnsEmptyList()
    {
        Assert.Empty(PowerPlanParser.ParseList(string.Empty));
        Assert.Empty(PowerPlanParser.ParseList(null));
    }

    [Fact]
    public void ParseList_WithLocalizedOutput_StillFindsThePlans()
    {
        const string german = """
Vorhandene Energieschemas (* Aktiv)
-----------------------------------
Energieschema-GUID: 381b4222-f694-41f0-9685-ff5bb260df2e  (Ausbalanciert) *
Energieschema-GUID: 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c  (Höchstleistung)
""";

        var plans = PowerPlanParser.ParseList(german);

        Assert.Equal(2, plans.Count);
        Assert.True(plans[0].IsActive);
        Assert.Equal("Ausbalanciert", plans[0].Name);
    }

    [Fact]
    public void ParseActivePlanGuid_ReadsTheGuid()
    {
        var output = "Power Scheme GUID: 381b4222-f694-41f0-9685-ff5bb260df2e  (Balanced)";

        var guid = PowerPlanParser.ParseActivePlanGuid(output);

        Assert.NotNull(guid);
        Assert.Equal(Guid.Parse("381b4222-f694-41f0-9685-ff5bb260df2e"), guid!.Value);
    }

    [Fact]
    public void ParseActivePlanName_ReadsTheName()
    {
        var output = "Power Scheme GUID: 381b4222-f694-41f0-9685-ff5bb260df2e  (Balanced)";

        Assert.Equal("Balanced", PowerPlanParser.ParseActivePlanName(output));
    }

    [Fact]
    public void ParseActivePlanGuid_WithoutGuid_ReturnsNull()
    {
        Assert.Null(PowerPlanParser.ParseActivePlanGuid("no guid here"));
        Assert.Null(PowerPlanParser.ParseActivePlanGuid(null));
    }

    [Fact]
    public void TryParseGuid_AcceptsBraces()
    {
        Assert.True(PowerPlanParser.TryParseGuid("{381b4222-f694-41f0-9685-ff5bb260df2e}", out var guid));
        Assert.Equal(Guid.Parse("381b4222-f694-41f0-9685-ff5bb260df2e"), guid);
    }

    [Fact]
    public void TryParseGuid_WithInvalidValue_ReturnsFalse()
    {
        Assert.False(PowerPlanParser.TryParseGuid("not-a-guid", out _));
        Assert.False(PowerPlanParser.TryParseGuid(null, out _));
    }
}
