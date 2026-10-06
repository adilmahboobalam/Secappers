<script setup lang="ts">
import { onMounted } from 'vue';
import PageHeader from '../components/layout/PageHeader.vue';
import UpdateCard from '../components/updates/UpdateCard.vue';
import { useUpdateStore } from '../stores/updates';
import { useToast } from '../composables/useToast';

const updateStore = useUpdateStore();
const toast = useToast();

onMounted(async () => {
  // Check if needed
});

async function handleCheck() {
  await updateStore.checkForUpdates();
  if (updateStore.updateInfo.hasUpdate) {
    toast.info('New Update Available', `SecApper v${updateStore.updateInfo.availableVersion} is ready to download.`);
  } else {
    toast.success('Up to Date', `SecApper v${updateStore.updateInfo.currentVersion} is the latest release.`);
  }
}

async function handleUpdate() {
  try {
    await updateStore.downloadUpdate();
    if (updateStore.updateInfo.status === 'Ready') {
      await updateStore.installUpdate();
    }
  } catch (err: any) {
    toast.error('Update Failed', err.message || 'Could not complete update process.');
  }
}
</script>

<template>
  <div class="space-y-6 max-w-4xl mx-auto">
    <!-- Header -->
    <PageHeader
      title="Software Updates"
      subtitle="Keep SecApper secure with verified in-place upgrades."
    />

    <!-- Main Update Experience -->
    <UpdateCard
      :update-info="updateStore.updateInfo"
      :is-checking="updateStore.isChecking"
      :is-downloading="updateStore.isDownloading"
      :is-installing="updateStore.isInstalling"
      @check="handleCheck"
      @update="handleUpdate"
    />
  </div>
</template>
