import { defineStore } from 'pinia';
import { ref } from 'vue';
import { nativeBridge } from '../services/nativeBridge';
import type { UpdateInfo } from '../types';

export const useUpdateStore = defineStore('updates', () => {
  const updateInfo = ref<UpdateInfo>({
    currentVersion: '1.1.0',
    availableVersion: '1.1.0',
    hasUpdate: false,
    downloadProgress: 0,
    status: 'Idle',
    statusMessage: "You're up to date.",
    lastChecked: undefined,
  });

  const isChecking = ref(false);
  const isDownloading = ref(false);
  const isInstalling = ref(false);
  const error = ref<string | null>(null);

  async function checkForUpdates() {
    isChecking.value = true;
    error.value = null;
    updateInfo.value.status = 'Checking';
    updateInfo.value.statusMessage = 'Checking for updates...';
    try {
      const info = await nativeBridge.updates.check();
      updateInfo.value = info;
    } catch (err: any) {
      error.value = err.message || 'Unable to check for updates (Offline or network unreachable)';
      updateInfo.value.status = 'Error';
      updateInfo.value.statusMessage = 'Offline: Protection continues normally without updates.';
    } finally {
      isChecking.value = false;
    }
  }

  async function downloadUpdate() {
    isDownloading.value = true;
    error.value = null;
    updateInfo.value.status = 'Downloading';
    updateInfo.value.downloadProgress = 10;
    updateInfo.value.statusMessage = 'Downloading update package...';
    try {
      await nativeBridge.updates.download();
      updateInfo.value.status = 'Ready';
      updateInfo.value.downloadProgress = 100;
      updateInfo.value.statusMessage = 'Update downloaded and verified. Ready to install.';
    } catch (err: any) {
      error.value = err.message || 'Failed to download update';
      updateInfo.value.status = 'Error';
      updateInfo.value.statusMessage = err.message || 'Failed to download update';
      throw err;
    } finally {
      isDownloading.value = false;
    }
  }

  async function installUpdate() {
    isInstalling.value = true;
    error.value = null;
    updateInfo.value.status = 'Installing';
    updateInfo.value.statusMessage = 'Applying update and restarting SecApper...';
    try {
      await nativeBridge.updates.install();
    } catch (err: any) {
      error.value = err.message || 'Failed to install update';
      updateInfo.value.status = 'Error';
      updateInfo.value.statusMessage = err.message || 'Failed to install update';
      throw err;
    } finally {
      isInstalling.value = false;
    }
  }

  return {
    updateInfo,
    isChecking,
    isDownloading,
    isInstalling,
    error,
    checkForUpdates,
    downloadUpdate,
    installUpdate,
  };
});
