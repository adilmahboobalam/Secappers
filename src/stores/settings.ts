import { defineStore } from 'pinia';
import { ref } from 'vue';
import { nativeBridge } from '../services/nativeBridge';
import type { AppSettings } from '../types';

export const useSettingsStore = defineStore('settings', () => {
  const settings = ref<AppSettings>({
    autoCheckUpdates: true,
    updateFrequency: 'Daily',
    ransomwareProtectionEnabled: true,
    massModificationThreshold: 30,
    explorerIntegrationEnabled: true,
    masterPinConfigured: false,
    darkMode: false,
    startWithWindows: true,
    minimizeToTray: true,
  });

  const loading = ref(false);
  const error = ref<string | null>(null);

  async function loadSettings() {
    loading.value = true;
    try {
      const data = await nativeBridge.settings.get();
      settings.value = data;
      applyTheme(data.darkMode);
    } catch (err: any) {
      error.value = err.message || 'Failed to load settings';
    } finally {
      loading.value = false;
    }
  }

  async function updateSetting<K extends keyof AppSettings>(key: K, value: AppSettings[K]) {
    try {
      settings.value[key] = value;
      if (key === 'darkMode') {
        applyTheme(Boolean(value));
      }
      await nativeBridge.settings.update({ [key]: value });
    } catch (err: any) {
      error.value = err.message || 'Failed to update setting';
    }
  }

  function applyTheme(isDark: boolean) {
    if (typeof document !== 'undefined') {
      if (isDark) {
        document.documentElement.classList.add('dark');
      } else {
        document.documentElement.classList.remove('dark');
      }
    }
  }

  return {
    settings,
    loading,
    error,
    loadSettings,
    updateSetting,
    applyTheme,
  };
});
