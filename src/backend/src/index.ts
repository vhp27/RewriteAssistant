/**
 * Entry point for the Rewrite Assistant Node.js backend
 */

import { ipcServer, PIPE_NAME } from './services/IPCServer';

console.log('Rewrite Assistant Backend starting...');

async function main(): Promise<void> {
  try {
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
