import { defineStore } from 'pinia';
import { ref } from 'vue';
import { nativeBridge } from '../services/nativeBridge';
import type { UserProfile, SetupInitialData, CompleteSetupPayload } from '../types';
import { useSettingsStore } from './settings';
import { useSecurityStore } from './security';

export const useUserStore = defineStore('user', () => {
  const profile = ref<UserProfile>({
    userName: 'Security User',
    userRole: 'Security Administrator',
    avatar: 'shield-cyan',
    securityTier: 'Standard',
    installationId: 'SEC-INITIALIZING',
    setupDate: undefined,
    isCompleted: true,
  });

  const initialData = ref<SetupInitialData | null>(null);
  const isSetupCompleted = ref<boolean>(true);
  const showSetupWizard = ref<boolean>(false);
  const loading = ref<boolean>(false);
  const error = ref<string | null>(null);

  async function checkSetupStatus() {
    loading.value = true;
    try {
      // 1. Strictly one-time check: If already completed locally, never show setup wizard again
      const localCompleted = typeof localStorage !== 'undefined' && localStorage.getItem('secapper_setup_completed') === 'true';
      if (localCompleted) {
        isSetupCompleted.value = true;
        showSetupWizard.value = false;
        profile.value.isCompleted = true;
        await loadProfile();
        return;
      }

      // 2. Check native backend persistence
      const completed = await nativeBridge.setup.isCompleted();
      isSetupCompleted.value = completed;
      profile.value.isCompleted = completed;
      if (!completed) {
        showSetupWizard.value = true;
        await fetchInitialData();
      } else {
        showSetupWizard.value = false;
        if (typeof localStorage !== 'undefined') {
          localStorage.setItem('secapper_setup_completed', 'true');
        }
        await loadProfile();
      }
    } catch (err: any) {
      console.warn('Could not check setup status:', err);
      showSetupWizard.value = false;
    } finally {
      loading.value = false;
    }
  }

  async function fetchInitialData() {
    try {
      const data = await nativeBridge.setup.getInitialData();
      initialData.value = data;
      if (!profile.value.userName || profile.value.userName === 'Security User') {
        profile.value.userName = data.suggestedUsername;
      }
      profile.value.installationId = data.installationId;
      return data;
    } catch (err: any) {
      console.error('Failed to get setup initial data:', err);
      return null;
    }
  }

  async function loadProfile() {
    try {
      const data = await nativeBridge.profile.get();
      profile.value = data;
      isSetupCompleted.value = data.isCompleted;
    } catch (err: any) {
      console.error('Failed to load user profile:', err);
    }
  }

  async function completeSetup(payload: CompleteSetupPayload) {
    loading.value = true;
    error.value = null;
    try {
      const result = await nativeBridge.setup.complete(payload);
      if (!result.success) {
        throw new Error(result.error || 'Failed to complete setup configuration.');
      }

      if (typeof localStorage !== 'undefined') {
        localStorage.setItem('secapper_setup_completed', 'true');
      }

      await loadProfile();
      isSetupCompleted.value = true;
      showSetupWizard.value = false;

      // Sync settings & security status
      const settingsStore = useSettingsStore();
      const securityStore = useSecurityStore();
      await Promise.allSettled([
        settingsStore.loadSettings(),
        securityStore.refreshStatus(),
      ]);

      return true;
    } catch (err: any) {
      error.value = err.message || 'Error completing setup.';
      throw err;
    } finally {
      loading.value = false;
    }
  }

  async function skipSetup() {
    if (typeof localStorage !== 'undefined') {
      localStorage.setItem('secapper_setup_completed', 'true');
    }
    isSetupCompleted.value = true;
    showSetupWizard.value = false;

    try {
      await nativeBridge.setup.complete({
        userName: profile.value.userName || 'Security User',
        userRole: profile.value.userRole || 'Security Administrator',
        avatar: profile.value.avatar || 'sentinel',
        securityTier: 'Standard',
        recoveryCode: '',
        darkMode: true,
        ransomwareThreshold: 30,
        autoLockOnWindowClose: true,
      });
    } catch (err) {
      console.warn('Skip setup save notice:', err);
    }
  }

  async function updateProfile(data: Partial<UserProfile>) {
    try {
      await nativeBridge.profile.update(data);
      profile.value = { ...profile.value, ...data };
      return true;
    } catch (err: any) {
      error.value = err.message || 'Error updating profile.';
      return false;
    }
  }

  async function relaunchSetup() {
    await fetchInitialData();
    showSetupWizard.value = true;
  }

  return {
    profile,
    initialData,
    isSetupCompleted,
    showSetupWizard,
    loading,
    error,
    checkSetupStatus,
    fetchInitialData,
    loadProfile,
    completeSetup,
    skipSetup,
    updateProfile,
    relaunchSetup,
  };
});
