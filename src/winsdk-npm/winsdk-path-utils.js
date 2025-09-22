/**
 * Get the path to a .winsdk directory (local or global)
 * @param {boolean} isGlobal - Whether to get the global path (true) or local path (false)
 * @returns {string} The full path to the .winsdk directory
 * @throws {Error} If the .winsdk directory is not found
 */
function getWinsdkPath(isGlobal = false) {
  const { execSync } = require('child_process');
  const { getWinsdkCliPath } = require('./winsdk-cli-utils');
  
  try {
    const winsdkCliPath = getWinsdkCliPath();
    const globalFlag = isGlobal ? ' --global' : '';
    const result = execSync(`"${winsdkCliPath}" get-winsdk-path${globalFlag}`, {
      encoding: 'utf8',
      stdio: ['pipe', 'pipe', 'pipe']
    });
    return result.trim();
  } catch (error) {
    const pathType = isGlobal ? 'Global' : 'Local';
    const setupCommand = isGlobal ? 'winsdk setup' : 'winsdk init';
    throw new Error(`${pathType} .winsdk directory not found. Make sure to run '${setupCommand}' first.`);
  }
}

/**
 * Get the path to the global .winsdk directory
 * @returns {string} The full path to the global .winsdk directory
 * @throws {Error} If the global .winsdk directory is not found
 */
function getGlobalWinsdkPath() {
  return getWinsdkPath(true);
}

/**
 * Get the path to the local .winsdk directory
 * @returns {string} The full path to the local .winsdk directory
 * @throws {Error} If the local .winsdk directory is not found
 */
function getLocalWinsdkPath() {
  return getWinsdkPath(false);
}

module.exports = {
  getGlobalWinsdkPath,
  getLocalWinsdkPath
};