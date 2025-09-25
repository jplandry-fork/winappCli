const fs = require('fs-extra');
const path = require('path');
const { addMsixIdentityToExe } = require('windows-sdk');

module.exports = async (context) => {
  // context.electronPlatformName: 'win32', 'darwin', 'linux'
  // context.target.name: 'nsis', 'portable', etc. (sometimes undefined if not installer build)

  if (context.electronPlatformName === 'win32') {
    const exePath = path.join(context.appOutDir, `${context.packager.appInfo.productName}.exe`);
    const appxManifestPath = path.join(__dirname, 'appxmanifest.xml');
    
    try {
      console.log(`Adding MSIX identity from: ${appxManifestPath}`);
      
      const result = await addMsixIdentityToExe(exePath, appxManifestPath, {
        verbose: true,
        location: context.appOutDir
      });
      
      console.log('Successfully added MSIX identity');
      
    } catch (error) {
      console.error('Error adding MSIX identity to executable:', error.message);
      throw error;
    }
  }
};