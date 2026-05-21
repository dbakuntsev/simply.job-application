namespace Simply.JobApplication.Tools.QnA.Harness;

// Minimal .env loader: KEY=VALUE per line, '#' and blank lines ignored,
// optional surrounding single or double quotes on the value are stripped.
// Loaded values are set into the current process's environment.
internal static class DotEnv
{
    public static int LoadIfPresent(string path)
    {
        if (!File.Exists(path)) return 0;

        var count = 0;
        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            // Allow `export KEY=VALUE` for shell-compatibility.
            if (line.StartsWith("export ", StringComparison.Ordinal))
                line = line["export ".Length..].TrimStart();

            var eq = line.IndexOf('=');
            if (eq <= 0) continue;

            var key   = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();

            if (value.Length >= 2 &&
                ((value[0] == '"'  && value[^1] == '"') ||
                 (value[0] == '\'' && value[^1] == '\'')))
            {
                value = value[1..^1];
            }

            Environment.SetEnvironmentVariable(key, value);
            count++;
        }
        return count;
    }
}
