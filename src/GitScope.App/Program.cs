using GitScope.Core;

namespace GitScope.App;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        try
        {
            string? Argument(string name)
            {
                int i = Array.IndexOf(args, name);
                if (i < 0) return null;
                if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                    throw new ArgumentException(name + " 缺少路径参数。");
                return args[i + 1];
            }
            var state = Path.GetFullPath(Argument("--state") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GitScope"));
            var runner = new GitProcessRunner();
            using var form = new MainForm(new RepositoryService(runner), new HistoryService(runner), new TargetService(state, runner), new AppSettingsStore(state), Argument("--repo"));
            Application.Run(form);
            return 0;
        }
        catch (Exception e)
        {
            MessageBox.Show("GitScope 未能启动。\n\n" + e.Message, "GitScope", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }
}
