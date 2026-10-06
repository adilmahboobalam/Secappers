<script setup lang="ts">
import { onMounted, ref } from 'vue';
import Sidebar from './Sidebar.vue';
import TopBar from './TopBar.vue';
import Toast from '../common/Toast.vue';
import UnlockFolderDialog from '../dialogs/UnlockFolderDialog.vue';
import DynamicSetupWizard from '../onboarding/DynamicSetupWizard.vue';
import { nativeBridge } from '../../services/nativeBridge';
import { useSecurityStore } from '../../stores/security';
import { useFolderStore } from '../../stores/folders';
import { useEventStore } from '../../stores/events';
import { useRansomwareStore } from '../../stores/ransomware';
import { useSettingsStore } from '../../stores/settings';
import { useUserStore } from '../../stores/user';
import type { FolderRecord } from '../../types';

const securityStore = useSecurityStore();
const folderStore = useFolderStore();
const eventStore = useEventStore();
const ransomwareStore = useRansomwareStore();
const settingsStore = useSettingsStore();
const userStore = useUserStore();

// Double-click intercept unlock popup
const interceptUnlockFolder = ref<FolderRecord | null>(null);
const showInterceptUnlockDialog = ref(false);

onMounted(async () => {
  // Check dynamic setup status on startup
  await userStore.checkSetupStatus();

  // Initial parallel sync with real backend
  await Promise.allSettled([
    securityStore.refreshStatus(),
    folderStore.refreshFolders(),
    eventStore.refreshEvents(),
    ransomwareStore.refreshStatus(),
    settingsStore.loadSettings(),
  ]);

  // Listen for native push events (e.g. explorer double-click on locked folder)
  nativeBridge.on('unlockRequested', (folder: FolderRecord) => {
    if (folder) {
      interceptUnlockFolder.value = folder;
      showInterceptUnlockDialog.value = true;
    }
  });

  nativeBridge.on('folderChanged', async () => {
    await folderStore.refreshFolders();
    await securityStore.refreshStatus();
  });
});
</script>

<template>
  <div class="flex h-screen w-screen overflow-hidden bg-[#F6F8FB] dark:bg-[#081525] text-[#101828] dark:text-[#F8FAFC]">
    <!-- Left Navigation Sidebar -->
    <Sidebar />

    <!-- Main Content Area -->
    <div class="flex-1 flex flex-col min-w-0 h-full overflow-hidden">
      <!-- Desktop Header & Window Controls -->
      <TopBar />

      <!-- Scrollable Main Viewport -->
      <main class="flex-1 overflow-y-auto px-6 py-6 lg:px-8">
        <div class="max-w-6xl mx-auto">
          <router-view v-slot="{ Component }">
            <transition name="fade" mode="out-in">
              <component :is="Component" />
            </transition>
          </router-view>
        </div>
      </main>
    </div>

    <!-- Global Toast Notifications -->
    <Toast />

    <!-- Active Windows Explorer Intercept Unlock Dialog -->
    <UnlockFolderDialog
      v-if="showInterceptUnlockDialog && interceptUnlockFolder"
      :folder="interceptUnlockFolder"
      :is-open="showInterceptUnlockDialog"
      @close="showInterceptUnlockDialog = false"
      @unlocked="showInterceptUnlockDialog = false"
    />

    <!-- Dynamic First-Run Setup & Personalization Wizard -->
    <transition name="fade">
      <DynamicSetupWizard v-if="userStore.showSetupWizard" />
    </transition>
  </div>
</template>
