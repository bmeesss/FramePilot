using System.Globalization;
using System.Text;
using FramePilot.Core.PresentMon;

namespace FramePilot.Core.Tests;

public class PresentMonCsvParserTests
{
    private const string V2Header =
        "Application,ProcessID,SwapChainAddress,PresentRuntime,SyncInterval,PresentFlags,AllowsTearing,PresentMode," +
        "CPUStartTime,FrameTime,CPUBusy,CPUWait,GPULatency,GPUTime,GPUBusy,GPUWait,DisplayLatency,DisplayedTime,AnimationError,AnimationTime";

    private const string V1Header =
        "Application,ProcessID,SwapChainAddress,Runtime,SyncInterval,PresentFlags,Dropped,TimeInSeconds," +
        "msInPresentAPI,msBetweenPresents,AllowsTearing,PresentMode,msUntilRenderComplete,msUntilDisplayed," +
        "msBetweenDisplayChange,msUntilRenderStart,msGPUActive,QPCTime";

    private const string LegacyHeader =
        "Application,ProcessID,SwapChainAddress,PresentRuntime,SyncInterval,PresentFlags,AllowsTearing,PresentMode," +
        "TimeInMs,MsBetweenSimulationStart,MsBetweenPresents,MsBetweenDisplayChange,MsInPresentAPI," +
        "MsRenderPresentLatency,MsUntilDisplayed,CPUStartTimeInMs,MsBetweenAppStart,MsCPUBusy,MsCPUWait," +
        "MsGPULatency,MsGPUTime,MsGPUBusy,MsGPUWait,MsAnimationError,AnimationTime,MsFlipDelay";

    [Fact]
    public void Parse_V2Metrics_UsesFrameTimeColumn()
    {
        var csv = BuildV2Csv(new[] { 16.0, 16.0, 33.0 });

        var result = new PresentMonCsvParser().Parse(csv);

        Assert.Equal(PresentMonSchemaKind.PresentMonV2, result.Schema);
        Assert.Equal("FrameTime", result.FrameTimeColumn);
        Assert.Equal(3, result.Frames.Count);
        Assert.Equal(16.0, result.Frames[0].FrameTimeMs, 6);
        Assert.Equal(33.0, result.Frames[2].FrameTimeMs, 6);
        Assert.Equal("game.exe", result.Frames[0].Application);
        Assert.Equal(1234, result.Frames[0].ProcessId);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Parse_V1Metrics_UsesMsBetweenPresentsColumn()
    {
        var csv = BuildV1Csv(new[] { 20.0, 20.0 }, dropped: new[] { false, true });

        var result = new PresentMonCsvParser().Parse(csv);

        Assert.Equal(PresentMonSchemaKind.PresentMonV1, result.Schema);
        Assert.Equal("msBetweenPresents", result.FrameTimeColumn);
        Assert.Equal(2, result.Frames.Count);
        Assert.False(result.Frames[0].IsDropped);
        Assert.True(result.Frames[1].IsDropped);
        Assert.False(result.Frames[1].WasDisplayed);
    }

    [Fact]
    public void Parse_LegacyMetrics_UsesMsBetweenPresentsColumn()
    {
        var csv = BuildLegacyCsv(new[] { 12.5, 12.5 });

        var result = new PresentMonCsvParser().Parse(csv);

        Assert.Equal(PresentMonSchemaKind.PresentMonLegacy, result.Schema);
        Assert.Equal("MsBetweenPresents", result.FrameTimeColumn);
        Assert.Equal(12.5, result.Frames[0].FrameTimeMs, 6);
    }

    [Fact]
    public void Parse_EmptyFile_Throws()
    {
        var exception = Assert.Throws<PresentMonParseException>(() => new PresentMonCsvParser().Parse("   "));

        Assert.Equal(PresentMonParseError.EmptyFile, exception.Error);
    }

    [Fact]
    public void Parse_UnknownHeader_ThrowsUnsupportedSchema()
    {
        var csv = "Name,Value\r\na,1\r\n";

        var exception = Assert.Throws<PresentMonParseException>(() => new PresentMonCsvParser().Parse(csv));

        Assert.Equal(PresentMonParseError.UnsupportedSchema, exception.Error);
        Assert.Contains("PresentMon", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_WithoutUsableRows_ThrowsNoFrameData()
    {
        var csv = V2Header + Environment.NewLine + "game.exe,1234,0x0,DXGI,0,0,1,Legacy Flip,0.0,NA,0,0,0,0,0,0,0,0,0,0";

        var exception = Assert.Throws<PresentMonParseException>(() => new PresentMonCsvParser().Parse(csv));

        Assert.Equal(PresentMonParseError.NoFrameData, exception.Error);
    }

    [Fact]
    public void Parse_SkipsInvalidRowsAndReportsThem()
    {
        var sb = new StringBuilder();
        sb.AppendLine(V2Header);
        sb.AppendLine(V2Row(16.0));
        sb.AppendLine("game.exe,not-a-number,0x0,DXGI,0,0,1,Legacy Flip,0.0,16.0,0,0,0,0,0,0,0,0,0,0");
        sb.AppendLine("game.exe,1234,0x0,DXGI,0,0,1,Legacy Flip,0.0,0,0,0,0,0,0,0,0,0,0,0");
        sb.AppendLine(V2Row(20.0));

        var result = new PresentMonCsvParser().Parse(sb.ToString());

        Assert.Equal(2, result.Frames.Count);
        Assert.Equal(3, result.TotalDataRows);
        Assert.Equal(2, result.SkippedRows);
        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public void Parse_TruncatedLastRow_IsSkippedWithWarning()
    {
        var sb = new StringBuilder();
        sb.AppendLine(V2Header);
        sb.AppendLine(V2Row(16.0));
        sb.Append("game.exe,1234,0x0,DXGI,0,0,1,Legacy Flip,0.0,16.0");

        var result = new PresentMonCsvParser().Parse(sb.ToString());

        Assert.Single(result.Frames);
        Assert.Equal(1, result.SkippedRows);
        Assert.Contains(result.Warnings, w => w.Contains("truncated", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Parse_SupportsQuotedFieldsWithCommas()
    {
        var sb = new StringBuilder();
        sb.AppendLine(V2Header);
        sb.AppendLine("\"My, Game.exe\",1234,0x0,DXGI,0,0,1,Legacy Flip,0.0,16.0,0,0,0,0,0,0,0,0,0,0");

        var result = new PresentMonCsvParser().Parse(sb.ToString());

        Assert.Single(result.Frames);
        Assert.Equal("My, Game.exe", result.Frames[0].Application);
    }

    [Fact]
    public void Parse_GroupsFramesByProcess()
    {
        var sb = new StringBuilder();
        sb.AppendLine(V2Header);
        sb.AppendLine(V2Row(16.0));
        sb.AppendLine(V2Row(16.0).Replace("game.exe,1234", "other.exe,4321"));

        var result = new PresentMonCsvParser().Parse(sb.ToString());

        Assert.Equal(2, result.ProcessGroups.Count);
    }

    [Fact]
    public void ParseFile_MissingFile_Throws()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".csv");

        var exception = Assert.Throws<PresentMonParseException>(() => new PresentMonCsvParser().ParseFile(path));

        Assert.Equal(PresentMonParseError.EmptyFile, exception.Error);
    }

    [Fact]
    public void SplitCsvLine_HandlesQuotedValues()
    {
        var fields = PresentMonCsvParser.SplitCsvLine("a,\"b,c\",\"d\"\"e\"");

        Assert.Equal(3, fields.Count);
        Assert.Equal("a", fields[0]);
        Assert.Equal("b,c", fields[1]);
        Assert.Equal("d\"e", fields[2]);
    }

    private static string BuildV2Csv(IEnumerable<double> frameTimes)
    {
        var sb = new StringBuilder();
        sb.AppendLine(V2Header);

        var time = 0.0;
        foreach (var frameTime in frameTimes)
        {
            sb.AppendLine(V2Row(frameTime, time));
            time += frameTime / 1000.0;
        }

        return sb.ToString();
    }

    private static string V2Row(double frameTime, double? time = null)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "game.exe,1234,0x0000000000000000,DXGI,0,0,1,Hardware: Legacy Flip,{0:0.0000},{1:0.0000},{2:0.0000},{3:0.0000},0.5,{1:0.0000},0.4,0.1,10.0,16.0,0.0,0.0",
            time ?? 0.0,
            frameTime,
            frameTime * 0.6,
            frameTime * 0.4);
    }

    private static string BuildV1Csv(IEnumerable<double> frameTimes, bool[] dropped)
    {
        var sb = new StringBuilder();
        sb.AppendLine(V1Header);

        var index = 0;
        var time = 0.0;
        foreach (var frameTime in frameTimes)
        {
            var isDropped = dropped is not null && index < dropped.Length && dropped[index];
            sb.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "game.exe,1234,0x0000000000000000,DXGI,0,0,{0},{1:0.0000},0.2,{2:0.0000},1,Hardware: Legacy Flip,0.1,{3},{4:0.0000},0.2,1.0,0",
                isDropped ? 1 : 0,
                time,
                frameTime,
                isDropped ? "NA" : "5.0",
                time));
            time += frameTime / 1000.0;
            index++;
        }

        return sb.ToString();
    }

    private static string BuildLegacyCsv(IEnumerable<double> frameTimes)
    {
        var sb = new StringBuilder();
        sb.AppendLine(LegacyHeader);

        var time = 0.0;
        foreach (var frameTime in frameTimes)
        {
            sb.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "game.exe,1234,0x0000000000000000,DXGI,0,0,1,Hardware: Legacy Flip,{0:0.0000},{0:0.0000},{1:0.0000},{0:0.0000},0.2,1.0,5.0,{0:0.0000},{1:0.0000},{2:0.0000},{3:0.0000},0.5,{1:0.0000},0.4,0.1,0.0,0.0,0.0",
                time,
                frameTime,
                frameTime * 0.6,
                frameTime * 0.4));
            time += frameTime / 1000.0;
        }

        return sb.ToString();
    }
}
