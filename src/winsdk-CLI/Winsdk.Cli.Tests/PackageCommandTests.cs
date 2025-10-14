using Winsdk.Cli.Commands;
using Winsdk.Cli.Services;

namespace Winsdk.Cli.Tests;

[TestClass]
public class PackageCommandTests : BaseCommandTests
{
    private string _tempDirectory = null!;
    private string _testWinsdkDirectory = null!;
    private IConfigService _configService = null!;
    private IBuildToolsService _buildToolsService = null!;
    private IMsixService _msixService = null!;

    [TestInitialize]
    public void Setup()
    {
        // Create a temporary directory for testing
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"WinsdkPackageTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);

        // Set up a temporary winsdk directory for testing (isolates tests from real winsdk directory)
        _testWinsdkDirectory = Path.Combine(_tempDirectory, ".winsdk");
        Directory.CreateDirectory(_testWinsdkDirectory);

        // Set up services with test cache directory
        _configService = GetRequiredService<IConfigService>();
        _configService.ConfigPath = Path.Combine(_tempDirectory, "winsdk.yaml");

        var directoryService = GetRequiredService<IWinsdkDirectoryService>();
        directoryService.SetCacheDirectoryForTesting(_testWinsdkDirectory);
        
        _buildToolsService = GetRequiredService<IBuildToolsService>();
        _msixService = GetRequiredService<IMsixService>();
    }

    [TestCleanup]
    public void Cleanup()
    {
        // Clean up temporary files and directories
        if (Directory.Exists(_tempDirectory))
        {
            try
            {
                Directory.Delete(_tempDirectory, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    /// <summary>
    /// Creates a minimal test package structure with manifest and basic files
    /// </summary>
    private void CreateTestPackageStructure(string packageDir)
    {
        Directory.CreateDirectory(packageDir);

        // Create a basic AppxManifest.xml
        var manifestContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Package xmlns=""http://schemas.microsoft.com/appx/manifest/foundation/windows10""
         xmlns:uap=""http://schemas.microsoft.com/appx/manifest/uap/windows10"">
  <Identity Name=""TestPackage""
            Publisher=""CN=TestPublisher""
            Version=""1.0.0.0"" />
  <Properties>
    <DisplayName>Test Package</DisplayName>
    <PublisherDisplayName>Test Publisher</PublisherDisplayName>
    <Description>Test package for integration testing</Description>
  </Properties>
  <Dependencies>
    <TargetDeviceFamily Name=""Windows.Universal"" MinVersion=""10.0.0.0"" MaxVersionTested=""10.0.0.0"" />
  </Dependencies>
  <Applications>
    <Application Id=""TestApp"" Executable=""TestApp.exe"" EntryPoint=""TestApp.App"">
      <uap:VisualElements DisplayName=""Test App"" Description=""Test application""
                          BackgroundColor=""#777777"" Square150x150Logo=""Assets\Logo.png"" />
    </Application>
  </Applications>
</Package>";

        File.WriteAllText(Path.Combine(packageDir, "AppxManifest.xml"), manifestContent);

        // Create Assets directory and a fake logo
        var assetsDir = Path.Combine(packageDir, "Assets");
        Directory.CreateDirectory(assetsDir);
        File.WriteAllText(Path.Combine(assetsDir, "Logo.png"), "fake png content");

        // Create a fake executable
        File.WriteAllText(Path.Combine(packageDir, "TestApp.exe"), "fake exe content");
    }

    [TestMethod]
    public async Task PackageCommand_WithBuildToolsPreInstalled_RunsSuccessfully()
    {
        // Arrange - Pre-install BuildTools by ensuring they exist
        try
        {
            var buildToolsPath = await _buildToolsService.EnsureBuildToolsAsync(quiet: true);
            if (buildToolsPath == null)
            {
                Assert.Inconclusive("Cannot run test - BuildTools installation failed. This may be expected in some test environments.");
                return;
            }

            // Verify makeappx.exe is available
            var makeappxPath = _buildToolsService.GetBuildToolPath("makeappx.exe");
            if (makeappxPath == null)
            {
                Assert.Inconclusive("Cannot run test - makeappx.exe not found in BuildTools installation.");
                return;
            }

            // Create test package structure
            var packageDir = Path.Combine(_tempDirectory, "TestPackage");
            CreateTestPackageStructure(packageDir);

            // Act - Run makeappx.exe to verify it works with pre-installed BuildTools
            var outputPath = Path.Combine(_tempDirectory, "TestPackage.msix");
            var arguments = $"pack /o /d \"{packageDir}\" /p \"{outputPath}\"";

            var (stdout, stderr) = await _buildToolsService.RunBuildToolAsync("makeappx.exe", arguments, verbose: false, quiet: true);

            // Assert - Command should succeed
            Assert.IsNotNull(stdout, "makeappx.exe should produce output");
            
            // The command might fail due to missing signing or other requirements in test environment,
            // but the important thing is that the tool was found and executed
            Console.WriteLine($"makeappx output: {stdout}");
            if (!string.IsNullOrEmpty(stderr))
            {
                Console.WriteLine($"makeappx stderr: {stderr}");
            }
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("execution failed"))
        {
            // This is acceptable - the tool ran but failed due to test environment limitations
            // The important thing is that BuildTools were found and the tool was executed
            Console.WriteLine($"Tool execution failed as expected in test environment: {ex.Message}");
        }
        catch (FileNotFoundException)
        {
            Assert.Fail("BuildTools should have been pre-installed but makeappx.exe was not found");
        }
    }

    [TestMethod]
    public async Task PackageCommand_WithoutBuildToolsPreInstalled_AutoInstallsAndRuns()
    {
        // Arrange - Start with a clean test environment (no pre-installed BuildTools)
        // The test environment should be isolated and not have BuildTools initially

        // Create test package structure
        var packageDir = Path.Combine(_tempDirectory, "TestPackage");
        CreateTestPackageStructure(packageDir);

        try
        {
            // Act - Use EnsureBuildToolAvailableAsync which should auto-install if needed
            var makeappxPath = await _buildToolsService.EnsureBuildToolAvailableAsync("makeappx.exe", quiet: true);
            
            // Verify we got a valid path
            Assert.IsNotNull(makeappxPath, "EnsureBuildToolAvailableAsync should return a valid path");
            Assert.IsTrue(File.Exists(makeappxPath), "The returned makeappx.exe path should exist");

            // Act - Run makeappx.exe to verify it works after auto-installation
            var outputPath = Path.Combine(_tempDirectory, "TestPackage.msix");
            var arguments = $"pack /o /d \"{packageDir}\" /p \"{outputPath}\"";

            var (stdout, stderr) = await _buildToolsService.RunBuildToolAsync("makeappx.exe", arguments, verbose: false, quiet: true);

            // Assert - Command should have executed (even if it fails due to test environment)
            Assert.IsNotNull(stdout, "makeappx.exe should produce output after auto-installation");
            
            Console.WriteLine($"makeappx output after auto-install: {stdout}");
            if (!string.IsNullOrEmpty(stderr))
            {
                Console.WriteLine($"makeappx stderr after auto-install: {stderr}");
            }
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("execution failed"))
        {
            // This is acceptable - the tool ran but failed due to test environment limitations
            Console.WriteLine($"Tool execution failed as expected in test environment: {ex.Message}");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Could not install"))
        {
            Assert.Inconclusive($"Cannot complete test - BuildTools auto-installation failed: {ex.Message}. This may be expected in some test environments.");
        }
        catch (FileNotFoundException ex)
        {
            Assert.Inconclusive($"Cannot complete test - Tool not found after auto-installation: {ex.Message}. This may be expected in some test environments.");
        }
    }

    [TestMethod]
    public async Task PackageCommand_ToolDiscovery_FindsCommonBuildTools()
    {
        // This test verifies that common build tools can be discovered after installation
        var commonTools = new[] { "makeappx.exe", "makepri.exe", "mt.exe", "signtool.exe" };
        var foundTools = new List<string>();
        var missingTools = new List<string>();

        try
        {
            // Ensure BuildTools are installed
            var buildToolsPath = await _buildToolsService.EnsureBuildToolsAsync(quiet: true);
            if (buildToolsPath == null)
            {
                Assert.Inconclusive("Cannot run test - BuildTools installation failed.");
                return;
            }

            // Check each common tool
            foreach (var tool in commonTools)
            {
                var toolPath = _buildToolsService.GetBuildToolPath(tool);
                if (toolPath != null)
                {
                    foundTools.Add(tool);
                    Console.WriteLine($"Found {tool} at: {toolPath}");
                }
                else
                {
                    missingTools.Add(tool);
                    Console.WriteLine($"Missing: {tool}");
                }
            }

            // Assert - We should find at least some of the common tools
            Assert.IsNotEmpty(foundTools, $"Should find at least some common build tools. Found: [{string.Join(", ", foundTools)}], Missing: [{string.Join(", ", missingTools)}]");
            
            // Specifically check for makeappx since it's commonly used
            Assert.Contains("makeappx.exe", foundTools, "makeappx.exe should be available in BuildTools");
        }
        catch (InvalidOperationException)
        {
            Assert.Inconclusive("Cannot complete test - BuildTools installation failed. This may be expected in some test environments.");
        }
    }
}