using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Winsdk.Cli.Tests;

/// <summary>
/// Utility class for testing certificate operations and signature verification
/// </summary>
internal static class TestCertificateUtils
{
    /// <summary>
    /// Verifies if a file has a digital signature (Windows only)
    /// </summary>
    /// <param name="filePath">Path to the file to check</param>
    /// <returns>True if the file appears to have a digital signature</returns>
    public static bool HasDigitalSignature(string filePath)
    {
        if (!File.Exists(filePath))
            return false;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return false; // Can only verify on Windows

        try
        {
            // Try to read the file as a PE file and check if it has signature data
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            using var reader = new BinaryReader(fileStream);

            // Check DOS header
            fileStream.Seek(0, SeekOrigin.Begin);
            var dosSignature = reader.ReadUInt16();
            if (dosSignature != 0x5A4D) // "MZ"
                return false;

            // Get PE header offset
            fileStream.Seek(60, SeekOrigin.Begin);
            var peHeaderOffset = reader.ReadUInt32();

            // Check PE signature
            fileStream.Seek(peHeaderOffset, SeekOrigin.Begin);
            var peSignature = reader.ReadUInt32();
            if (peSignature != 0x00004550) // "PE\0\0"
                return false;

            // Skip COFF header (20 bytes)
            fileStream.Seek(peHeaderOffset + 4 + 20, SeekOrigin.Begin);

            // Read optional header magic
            var optionalHeaderMagic = reader.ReadUInt16();
            var is64Bit = optionalHeaderMagic == 0x020B; // PE32+
            
            // Calculate offset to data directories
            var dataDirectoryOffset = peHeaderOffset + 4 + 20 + (is64Bit ? 240 : 224) - (15 * 8);
            
            // Skip to Certificate Table entry (index 4 in data directories)
            fileStream.Seek(dataDirectoryOffset + (4 * 8), SeekOrigin.Begin);
            
            // Read Certificate Table RVA and Size
            var certTableRva = reader.ReadUInt32();
            var certTableSize = reader.ReadUInt32();
            
            // If both RVA and Size are non-zero, the file likely has a signature
            return certTableRva != 0 && certTableSize != 0;
        }
        catch
        {
            // If we can't read the PE structure, assume no signature
            return false;
        }
    }

    /// <summary>
    /// Gets the size of a file in bytes
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <returns>File size in bytes, or -1 if file doesn't exist</returns>
    public static long GetFileSize(string filePath)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            return fileInfo.Exists ? fileInfo.Length : -1;
        }
        catch
        {
            return -1;
        }
    }

    /// <summary>
    /// Verifies that a certificate file exists and can be loaded
    /// </summary>
    /// <param name="certPath">Path to the certificate file</param>
    /// <param name="password">Certificate password</param>
    /// <returns>True if the certificate can be loaded</returns>
    public static bool CanLoadCertificate(string certPath, string password)
    {
        if (!File.Exists(certPath))
            return false;

        try
        {
            // Use the modern X509CertificateLoader API instead of the obsolete constructor
            using var cert = X509CertificateLoader.LoadPkcs12FromFile(certPath, password, X509KeyStorageFlags.Exportable);
            return cert.HasPrivateKey;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Creates a temporary self-signed certificate for testing
    /// This is a fallback method if the main certificate generation fails
    /// </summary>
    /// <param name="outputPath">Path where to save the certificate</param>
    /// <param name="password">Password for the certificate</param>
    /// <param name="subjectName">Subject name for the certificate</param>
    /// <returns>True if certificate was created successfully</returns>
    public static bool CreateTestCertificate(string outputPath, string password, string subjectName)
    {
        try
        {
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest(subjectName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            // Add basic constraints
            request.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(false, false, 0, false));

            // Add key usage for code signing
            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false));

            // Add enhanced key usage for code signing
            request.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(
                    new OidCollection { new("1.3.6.1.5.5.7.3.3") }, // Code Signing OID
                    false));

            // Create the certificate
            var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));

            // Export as PFX
            var pfxData = cert.Export(X509ContentType.Pfx, password);
            File.WriteAllBytes(outputPath, pfxData);

            return File.Exists(outputPath);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Runs signtool.exe verify command to check if a file is properly signed
    /// This requires signtool.exe to be available in the system PATH
    /// </summary>
    /// <param name="filePath">Path to the file to verify</param>
    /// <returns>True if the file is properly signed according to signtool</returns>
    public static async Task<bool> VerifySignatureWithSigntool(string filePath)
    {
        if (!File.Exists(filePath))
            return false;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "signtool.exe",
                Arguments = $"verify /pa \"{filePath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
                return false;

            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
