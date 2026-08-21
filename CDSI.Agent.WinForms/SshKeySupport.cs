using System.Diagnostics;
using CDSI.Agent.Core.Git;

namespace CDSI.Agent.WinForms;

internal static class SshKeySupport
{
    private static readonly string[] PreferredKeyNames =
    [
        "id_ed25519",
        "id_ecdsa",
        "id_rsa"
    ];

    internal static string GetDefaultSshDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".ssh");
    }

    internal static SshKeyPairPaths? FindDefaultKeyPair(string sshDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sshDirectory);
        var directory = Path.GetFullPath(sshDirectory);
        foreach (var keyName in PreferredKeyNames)
        {
            var privateKeyPath = Path.Combine(directory, keyName);
            var publicKeyPath = privateKeyPath + ".pub";
            if (File.Exists(privateKeyPath) && File.Exists(publicKeyPath))
            {
                return new SshKeyPairPaths(publicKeyPath, privateKeyPath);
            }
        }

        return null;
    }

    internal static ProcessStartInfo CreateOpenWebsiteStartInfo(
        GitHostingProvider provider)
    {
        return new ProcessStartInfo
        {
            FileName = provider switch
            {
                GitHostingProvider.GitHub => "https://github.com/",
                GitHostingProvider.Gitee => "https://gitee.com/",
                _ => throw new ArgumentOutOfRangeException(nameof(provider))
            },
            UseShellExecute = true
        };
    }

    internal static ProcessStartInfo CreateSshKeyGenerationStartInfo(
        string comment)
    {
        var normalizedComment = string.IsNullOrWhiteSpace(comment)
            ? $"{Environment.UserName}@{Environment.MachineName}"
            : comment.Trim();
        var startInfo = new ProcessStartInfo
        {
            FileName = "ssh-keygen.exe",
            UseShellExecute = true,
            WorkingDirectory = Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile)
        };
        startInfo.ArgumentList.Add("-t");
        startInfo.ArgumentList.Add("ed25519");
        startInfo.ArgumentList.Add("-C");
        startInfo.ArgumentList.Add(normalizedComment);
        return startInfo;
    }
}

internal sealed record SshKeyPairPaths(
    string PublicKeyPath,
    string PrivateKeyPath);
