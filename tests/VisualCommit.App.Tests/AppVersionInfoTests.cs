using System.Reflection;
using VisualCommit.App.Services;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The app's version as About shows it (<see cref="AppVersionInfo"/>), read from an informational
/// version, and the identity an assembly's attributes give (<see cref="AppIdentity.FromAssembly"/>)
/// beside the harnesses' pinned one. Pure.
/// </summary>
public class AppVersionInfoTests
{
    [Theory]
    [InlineData("0.1.0+3dc088707b6e934a3f8fd391e48019cf3d76e500", "0.1.0", "3dc088707b6e934a3f8fd391e48019cf3d76e500", "0.1.0 (3dc0887)")]
    [InlineData("0.1.0+3dc0887", "0.1.0", "3dc0887", "0.1.0 (3dc0887)")]
    [InlineData("0.1.0", "0.1.0", null, "0.1.0")]
    [InlineData("0.1.0+local", "0.1.0", null, "0.1.0")]
    [InlineData("0.1.0+3dc088", "0.1.0", null, "0.1.0")]
    [InlineData("0.1.0+3dc088707b6e934a3f8fd391e48019cf3d76e5001", "0.1.0", null, "0.1.0")]
    [InlineData("0.1.0+3dc0887.dirty", "0.1.0", null, "0.1.0")]
    [InlineData(null, "0.0.0", null, "0.0.0")]
    [InlineData("", "0.0.0", null, "0.0.0")]
    public void Parse_takes_the_version_and_a_commit_of_7_to_40_hexadecimal_digits(string? informational, string version, string? commit, string display)
    {
        var parsed = AppVersionInfo.Parse(informational);

        Assert.Equal(new AppVersionInfo(version, commit), parsed);
        Assert.Equal(display, parsed.Display);
        Assert.Equal(commit?[..7], parsed.ShortCommit);
    }

    [Fact]
    public void The_pinned_identity_shows_0_1_0_and_seven_characters_of_its_commit()
    {
        Assert.Equal(
            new AppIdentity("VisualCommit", "Rahim Khajei", new AppVersionInfo("0.1.0", "1a2b3c4d5e6f708192a3b4c5d6e7f8091a2b3c4d"), "Copyright (c) 2026 Rahim Khajei"),
            TestIdentity.Pinned);
        Assert.Equal("0.1.0 (1a2b3c4)", TestIdentity.Pinned.Version.Display);
        Assert.Equal("Rahim Khajei", AppIdentity.CreatorName);
    }

    [Fact]
    public void The_app_assemblys_identity_is_its_product_creator_version_and_copyright()
    {
        var assembly = typeof(AppIdentity).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;

        var identity = AppIdentity.FromAssembly(assembly);

        Assert.Equal("VisualCommit", identity.Name);
        Assert.Equal(AppIdentity.CreatorName, identity.Creator);
        Assert.Equal(assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()!.Copyright, identity.Copyright);
        Assert.Equal("Copyright (c) 2026 Rahim Khajei", identity.Copyright);
        Assert.Equal(AppVersionInfo.Parse(informational), identity.Version);
        Assert.Equal(AppSession.AppVersion, identity.Version.Version);
    }
}
