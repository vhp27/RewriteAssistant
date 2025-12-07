/**
 * Entry point for the Rewrite Assistant Node.js backend
 */

import { ipcServer, PIPE_NAME } from './services/IPCServer';

console.log('Rewrite Assistant Backend starting...');

/**
 * Sets up orphan prevention by monitoring stdin.
 * When the frontend process terminates (crashes or exits), stdin will close,
 * allowing the backend to detect the disconnection and exit gracefully.
 * This prevents zombie backend processes from accumulating.
 */
function setupOrphanPrevention(): void {
  // Keep stdin open to receive close events
  process.stdin.resume();

  // Primary handler: stdin 'end' event fires when frontend disconnects
  process.stdin.on('end', async () => {
    console.log('Frontend disconnected (stdin end), exiting backend...');
    await ipcServer.stop();
    process.exit(0);
  });

  // Backup handler: stdin 'close' event as fallback
  process.stdin.on('close', async () => {
    console.log('Frontend disconnected (stdin close), exiting backend...');
    await ipcServer.stop();
    process.exit(0);
  });

  console.log('Orphan prevention enabled: monitoring stdin for frontend disconnect');
}

async function main(): Promise<void> {
  try {
    // Set up orphan prevention before starting the server
    setupOrphanPrevention();

    await ipcServer.start();
    console.log(`Backend ready, listening on ${PIPE_NAME}`);

    // Handle graceful shutdown
    process.on('SIGINT', async () => {
      console.log('Received SIGINT, shutting down...');
      await ipcServer.stop();
      process.exit(0);
    });

    process.on('SIGTERM', async () => {
      console.log('Received SIGTERM, shutting down...');
      await ipcServer.stop();
      process.exit(0);
    });
  } catch (error) {
    console.error('Failed to start backend:', error);
    process.exit(1);
  }
}

main();

export { ipcServer };
