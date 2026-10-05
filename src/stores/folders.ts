import { defineStore } from 'pinia';
import { ref, computed } from 'vue';
import { nativeBridge } from '../services/nativeBridge';
import type { FolderRecord } from '../types';
import { useSecurityStore } from './security';

export const useFolderStore = defineStore('folders', () => {
  const folders = ref<FolderRecord[]>([]);
  const selectedFolder = ref<FolderRecord | null>(null);
  const loading = ref(false);
  const actionLoading = ref(false);
  const actionMessage = ref<string>('');
  const error = ref<string | null>(null);

  const securityStore = useSecurityStore();

  const totalCount = computed(() => folders.value.length);
  const lockedCount = computed(() => folders.value.filter((f) => f.status === 'Locked').length);
  const unlockedCount = computed(() => folders.value.filter((f) => f.status === 'Unlocked').length);

  const lockedFolders = computed(() => folders.value.filter((f) => f.status === 'Locked'));
  const unlockedFolders = computed(() => folders.value.filter((f) => f.status === 'Unlocked'));
  const recentFolders = computed(() => {
    return [...folders.value]
      .sort((a, b) => new Date(b.updatedAt).getTime() - new Date(a.updatedAt).getTime())
      .slice(0, 5);
  });

  async function refreshFolders() {
    loading.value = true;
    error.value = null;
    try {
      const data = await nativeBridge.folders.list();
      folders.value = data || [];
      // Update security store count sync
      if (securityStore.status) {
        securityStore.status.protectedFoldersCount = folders.value.length;
        securityStore.status.lockedFoldersCount = lockedCount.value;
      }
    } catch (err: any) {
      error.value = err.message || 'Failed to fetch protected folders';
    } finally {
      loading.value = false;
    }
  }

  async function lockFolder(folderPath: string, password: string) {
    actionLoading.value = true;
    actionMessage.value = 'Applying Windows security permissions...';
    error.value = null;
    try {
      const res = await nativeBridge.folders.lock(folderPath, password);
      if (!res.success) {
        throw new Error(res.error || 'Failed to lock folder');
      }
      await refreshFolders();
      await securityStore.refreshStatus();
      return res.folder;
    } catch (err: any) {
      error.value = err.message || 'Error locking folder';
      throw err;
    } finally {
      actionLoading.value = false;
      actionMessage.value = '';
    }
  }

  async function unlockFolder(folderId: string, password: string) {
    actionLoading.value = true;
    actionMessage.value = 'Restoring filesystem permissions...';
    error.value = null;
    try {
      const res = await nativeBridge.folders.unlock(folderId, password);
      if (!res.success) {
        throw new Error(res.error || 'Failed to unlock folder');
      }
      await refreshFolders();
      await securityStore.refreshStatus();
      return res.folder;
    } catch (err: any) {
      error.value = err.message || 'Error unlocking folder';
      throw err;
    } finally {
      actionLoading.value = false;
      actionMessage.value = '';
    }
  }

  async function removeProtection(folderId: string, password: string) {
    actionLoading.value = true;
    actionMessage.value = 'Removing SecApper protection and restoring access...';
    error.value = null;
    try {
      const res = await nativeBridge.folders.removeProtection(folderId, password);
      if (!res.success) {
        throw new Error(res.error || 'Failed to remove protection');
      }
      await refreshFolders();
      await securityStore.refreshStatus();
      return true;
    } catch (err: any) {
      error.value = err.message || 'Error removing protection';
      throw err;
    } finally {
      actionLoading.value = false;
      actionMessage.value = '';
    }
  }

  async function browseFolder(): Promise<string | null> {
    try {
      return await nativeBridge.folders.browseFolder();
    } catch (err: any) {
      error.value = err.message || 'Failed to open folder picker';
      return null;
    }
  }

  async function openFolder(folderId: string): Promise<boolean> {
    try {
      return await nativeBridge.folders.openFolder(folderId);
    } catch (err: any) {
      error.value = err.message || 'Failed to open folder';
      return false;
    }
  }

  return {
    folders,
    selectedFolder,
    loading,
    actionLoading,
    actionMessage,
    error,
    totalCount,
    lockedCount,
    unlockedCount,
    lockedFolders,
    unlockedFolders,
    recentFolders,
    refreshFolders,
    lockFolder,
    unlockFolder,
    removeProtection,
    browseFolder,
    openFolder,
  };
});
