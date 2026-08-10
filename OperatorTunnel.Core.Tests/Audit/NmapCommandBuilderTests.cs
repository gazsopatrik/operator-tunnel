using OperatorTunnel.Audit;

namespace OperatorTunnel.Core.Tests.Audit;

public sealed class NmapCommandBuilderTests
{
    [Fact]
    public void BuildsStructuredXmlCommandWithoutShellSyntax()
    {
        var command = NmapCommandBuilder.Build(["10.10.20.15", "10.10.20.0/24"]);

        Assert.Equal("nmap.exe", command.FileName);
        Assert.Equal(["-oX", "-", "--", "10.10.20.15", "10.10.20.0/24"], command.Arguments);
    }

    [Theory]
    [InlineData("-sV")]
    [InlineData("10.0.0.1&whoami")]
    [InlineData("10.0.0.1|whoami")]
    public void RejectsOptionsAndShellSyntax(string target)
    {
        Assert.Throws<ArgumentException>(() => NmapCommandBuilder.Build([target]));
    }
}
