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

        if (!Directory.Exists(directory))
        {
            return null;
        }

        foreach (var publicKeyPath in Directory.EnumerateFiles(directory, "*.pub")
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var privateKeyPath = publicKeyPath[..^4];
            if (File.Exists(privateKeyPath))
            {
                return new SshKeyPairPaths(publicKeyPath, privateKeyPath);
            }
        }

        return null;
    }

    internal static SshKeyPairPaths CreateUnusedKeyPairPaths(string sshDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sshDirectory);
        var directory = Path.GetFullPath(sshDirectory);
        const string baseName = "id_ed25519_atlas";
        for (var suffix = 1; ; suffix++)
        {
            var keyName = suffix == 1 ? baseName : $"{baseName}_{suffix}";
            var privateKeyPath = Path.Combine(directory, keyName);
            var publicKeyPath = privateKeyPath + ".pub";
            if (!File.Exists(privateKeyPath) && !File.Exists(publicKeyPath))
            {
                return new SshKeyPairPaths(publicKeyPath, privateKeyPath);
            }
        }
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
        string comment,
        string privateKeyPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(privateKeyPath);
        var normalizedPrivateKeyPath = Path.GetFullPath(privateKeyPath);
        if (File.Exists(normalizedPrivateKeyPath) ||
            File.Exists(normalizedPrivateKeyPath + ".pub"))
        {
            throw new IOException("SSH 密钥目标文件已存在，不能覆盖。请重新生成路径。");
        }

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
        startInfo.ArgumentList.Add("-f");
        startInfo.ArgumentList.Add(normalizedPrivateKeyPath);
        return startInfo;
    }
}

internal sealed record SshKeyPairPaths(
    string PublicKeyPath,
    string PrivateKeyPath);
