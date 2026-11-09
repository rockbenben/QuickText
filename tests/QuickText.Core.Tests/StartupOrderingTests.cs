namespace QuickText.Core.Tests;

public class StartupOrderingTests
{
    [Fact]
    public void Normal_second_launch_hands_off_before_loading_or_migrating_user_data()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "QuickText.sln"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var source = File.ReadAllText(Path.Combine(directory.FullName, "src", "QuickText.App", "App.xaml.cs"));
        int startup = source.IndexOf("protected override void OnStartup", StringComparison.Ordinal);
        int mutex = source.IndexOf("new System.Threading.Mutex", startup, StringComparison.Ordinal);
        int handoff = source.IndexOf("HandOffToRunningInstance();", mutex, StringComparison.Ordinal);
        int window = source.IndexOf("CreateMessageWindow();", handoff, StringComparison.Ordinal);
        int seed = source.IndexOf("Core.AppPaths.SeedPortableMachineState();", window, StringComparison.Ordinal);
        int settings = source.IndexOf("state.SettingsStore.Load()", seed, StringComparison.Ordinal);
        Assert.True(startup < mutex && mutex < handoff && handoff < window && window < seed && seed < settings);
        Assert.Contains("if (!IsAutostartLaunch(e)) HandOffToRunningInstance();", source);
        Assert.Contains("bool toolMode = e.Args.Contains(\"--smoke\") || e.Args.Contains(\"--shots\");", source);
    }
}
