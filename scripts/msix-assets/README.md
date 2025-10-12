# MSIX Bundle Installation

This package contains a pre-release development build of the Windows Development CLI MSIX bundle.

## Quick Installation

Simply run the installer script:

**Option A - Automatic (Recommended):**
1. Right-click on `install.ps1`
2. Select "Run with PowerShell"
3. When prompted, allow elevation to Administrator
4. The script will install the certificate and bundle automatically

**Option B - From PowerShell:**
1. Open PowerShell
2. Navigate to this folder
3. Run: `.\install.ps1`

That's it! The script will handle both certificate and application installation.

## What's Included

- **winsdk_[version].msixbundle** - The MSIX bundle with x64 and ARM64 packages
- **install.ps1** - Automated installer script (handles certificate and bundle installation)

## Version Information

- Version: [version]
- Architectures: x64, ARM64

## Troubleshooting

### "Windows cannot install this package"
- Make sure you ran `install.ps1` with administrator privileges
- The certificate must be in the Trusted People store

### "This app package is not signed with a trusted certificate"
- Run `install.ps1` with administrator privileges
- Verify the certificate was installed to LocalMachine\TrustedPeople

## Support

For more information, visit: https://github.com/microsoft/winsdk
