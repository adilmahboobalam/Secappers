import { defineStore } from 'pinia';
import { ref } from 'vue';
import { nativeBridge } from '../services/nativeBridge';
import type { RansomwareStatus, ThreatLevel, SecurityEvent } from '../types';

export const useRansomwareStore = defineStore('ransomware', () => {
  const status = ref<RansomwareStatus>({
    active: true,
    threatLevel: 'LOW',
    monitoredFoldersCount: 0,
    suspiciousEventsToday: 0,
    blockedProcessesCount: 0,
    monitoringEngine: 'FileSystemWatcher + Heuristic Threat Scorer',
    recentAlerts: [],
  });

  const loading = ref(false);
  const error = ref<string | null>(null);

  async function refreshStatus() {
    loading.value = true;
    error.value = null;
    try {
      const data = await nativeBridge.ransomware.getStatus();
      status.value = data;
    } catch (err: any) {
      error.value = err.message || 'Failed to fetch ransomware protection status';
    } finally {
      loading.value = false;
    }
  }

  async function toggleProtection(enabled: boolean) {
    loading.value = true;
    try {
      await nativeBridge.ransomware.toggle(enabled);
      status.value.active = enabled;
    } catch (err: any) {
      error.value = err.message || 'Failed to toggle ransomware protection';
      throw err;
    } finally {
      loading.value = false;
    }
  }

  async function dismissAlert(alertId: string) {
    try {
      await nativeBridge.ransomware.dismissAlert(alertId);
      status.value.recentAlerts = status.value.recentAlerts.filter((a) => a.id !== alertId);
    } catch (err: any) {
      error.value = err.message || 'Failed to dismiss alert';
    }
  }

  return {
    status,
    loading,
    error,
    refreshStatus,
    toggleProtection,
    dismissAlert,
  };
});
