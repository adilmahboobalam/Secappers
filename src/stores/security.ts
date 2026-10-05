import { defineStore } from 'pinia';
import { ref, computed } from 'vue';
import { nativeBridge } from '../services/nativeBridge';
import type { SystemSecurityStatus, ThreatLevel } from '../types';

export const useSecurityStore = defineStore('security', () => {
  const status = ref<SystemSecurityStatus>({
    status: 'PROTECTED',
    statusText: 'Your system is protected. SecApper is protecting your secured folders.',
    isProtected: true,
    protectedFoldersCount: 0,
    lockedFoldersCount: 0,
    threatLevel: 'LOW',
    ransomwareActive: true,
    databaseHealthy: true,
    backupsAvailable: true,
    hasRecoveryIssues: false,
    masterPinConfigured: false,
    explorerIntegrationActive: true,
    version: '1.1.0',
  });

  const loading = ref(false);
  const error = ref<string | null>(null);

  const isProtected = computed(() => status.value?.isProtected ?? false);
  const threatLevel = computed<ThreatLevel>(() => status.value?.threatLevel ?? 'LOW');
  const hasAttention = computed(() => {
    return status.value?.status !== 'PROTECTED' || status.value?.hasRecoveryIssues || threatLevel.value !== 'LOW';
  });

  async function refreshStatus() {
    loading.value = true;
    error.value = null;
    try {
      const data = await nativeBridge.security.getStatus();
      status.value = data;
    } catch (err: any) {
      error.value = err.message || 'Failed to refresh security status';
    } finally {
      loading.value = false;
    }
  }

  async function panicLock() {
    loading.value = true;
    try {
      const res = await nativeBridge.security.panicLock();
      await refreshStatus();
      return res;
    } catch (err: any) {
      error.value = err.message || 'Failed to execute panic lock';
      throw err;
    } finally {
      loading.value = false;
    }
  }

  return {
    status,
    loading,
    error,
    isProtected,
    threatLevel,
    hasAttention,
    refreshStatus,
    panicLock,
  };
});
