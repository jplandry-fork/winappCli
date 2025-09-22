// Main entry point for the Windows SDK BuildTools package
const buildtoolsUtils = require('./buildtools-utils');
const msixUtils = require('./msix-utils');
const winsdkPathUtils = require('./winsdk-path-utils');

module.exports = {
  // BuildTools utilities
  execWithBuildTools: buildtoolsUtils.execSyncWithBuildTools,

  // MSIX manifest utilities
  addMsixIdentityToExe: msixUtils.addMsixIdentityToExe,
  addElectronDebugIdentity: msixUtils.addElectronDebugIdentity,

  // Winsdk directory utilities
  getGlobalWinsdkPath: winsdkPathUtils.getGlobalWinsdkPath,
  getLocalWinsdkPath: winsdkPathUtils.getLocalWinsdkPath
};
