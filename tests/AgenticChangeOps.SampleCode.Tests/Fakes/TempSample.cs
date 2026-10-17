using System.Security.Cryptography;

namespace AgenticChangeOps.SampleCode.Tests.Fakes;

/// <summary>
/// A throwaway sample folder with the same shape as the real one but tiny invented files, plus a
/// sibling folder outside the sample for confinement tests. Removed on dispose.
/// </summary>
public sealed class TempSample : IDisposable
{
    public const string SecretOutsideText = "OUTSIDE-SENTINEL-0001";

    private readonly string _parent;

    public TempSample()
    {
        _parent = Path.Combine(Path.GetTempPath(), "acops-sample-" + Guid.NewGuid().ToString("N"));
        Root = Path.Combine(_parent, "sample");
        Outside = Path.Combine(_parent, "outside");
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Outside);

        File.WriteAllText(Path.Combine(Outside, "secret.txt"), SecretOutsideText);

        Write("orders-service/config/orders.settings", "orders.page.size = 25\n");
        Write("orders-service/src/OrderIntake", "function acceptOrder(order):\n    require order.lines\n");
        Write("orders-service/notes/design.md", "# Orders design\nSee billing-portal/notes/design.md\n");
        Write("billing-portal/config/portal.settings", "portal.invoice.page.size = 20\n");
        Write("billing-portal/src/InvoiceList", "function listInvoices(customerRef):\n    return rows\n");
        Write("billing-portal/notes/design.md", "# Billing design\nOrders feed invoices.\n");
    }

    /// <summary>The sample root (what the tools may see).</summary>
    public string Root { get; }

    /// <summary>A folder next to the sample that the tools must never reach.</summary>
    public string Outside { get; }

    public string Write(string relativePath, string text)
    {
        var full = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
        return full;
    }

    public string WriteBytes(string relativePath, byte[] bytes)
    {
        var full = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, bytes);
        return full;
    }

    /// <summary>
    /// Creates a directory link inside the sample that points at <paramref name="targetDirectory"/>:
    /// a symbolic link, or on Windows without the privilege a junction. Returns false only when
    /// neither can be made, and the tests that need a link then fail loudly rather than pass.
    /// </summary>
    public bool TryCreateDirectoryLink(string relativeLinkPath, string targetDirectory)
    {
        var link = Path.Combine(Root, relativeLinkPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(link)!);
        try
        {
            Directory.CreateSymbolicLink(link, targetDirectory);
            return Directory.Exists(link);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
        }

        if (!OperatingSystem.IsWindows())
            return false;

        var start = new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{targetDirectory}\"")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using var process = System.Diagnostics.Process.Start(start)!;
        process.WaitForExit();
        return process.ExitCode == 0 && Directory.Exists(link);
    }

    /// <summary>A hash of every file name and byte under the root, to prove nothing changed.</summary>
    public string Fingerprint()
    {
        using var sha = SHA256.Create();
        foreach (var file in Directory.EnumerateFiles(_parent, "*", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
        {
            var name = System.Text.Encoding.UTF8.GetBytes(Path.GetRelativePath(_parent, file));
            sha.TransformBlock(name, 0, name.Length, null, 0);
            var bytes = File.ReadAllBytes(file);
            sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
        }

        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Convert.ToHexString(sha.Hash!);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_parent, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
