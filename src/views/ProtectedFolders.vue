<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import PageHeader from '../components/layout/PageHeader.vue';
import FolderCard from '../components/folders/FolderCard.vue';
import EmptyState from '../components/common/EmptyState.vue';
import Button from '../components/common/Button.vue';
import LockFolderDialog from '../components/dialogs/LockFolderDialog.vue';
import UnlockFolderDialog from '../components/dialogs/UnlockFolderDialog.vue';
import ConfirmationDialog from '../components/dialogs/ConfirmationDialog.vue';
import FolderDetails from '../components/folders/FolderDetails.vue';
import { useFolderStore } from '../stores/folders';
import { useToast } from '../composables/useToast';
import type { FolderRecord } from '../types';
import { Plus, Search, FolderLock, Filter } from 'lucide-vue-next';

const folderStore = useFolderStore();
const toast = useToast();

const searchQuery = ref('');
const activeFilter = ref<'All' | 'Locked' | 'Unlocked'>('All');

const showLockDialog = ref(false);
const showUnlockDialog = ref(false);
const showRemoveConfirmDialog = ref(false);
const showDetailsDialog = ref(false);
const targetFolder = ref<FolderRecord | null>(null);
const isBusy = ref(false);

onMounted(async () => {
  await folderStore.refreshFolders();
});

const filteredFolders = computed(() => {
  return folderStore.folders.filter((folder) => {
    const matchesFilter =
      activeFilter.value === 'All' ||
      (activeFilter.value === 'Locked' && folder.status === 'Locked') ||
      (activeFilter.value === 'Unlocked' && folder.status === 'Unlocked');

    const q = searchQuery.value.trim().toLowerCase();
    const matchesSearch =
      !q ||
      folder.folderName.toLowerCase().includes(q) ||
      folder.folderPath.toLowerCase().includes(q);

    return matchesFilter && matchesSearch;
  });
});

function handleOpenUnlock(folder: FolderRecord) {
  targetFolder.value = folder;
  showUnlockDialog.value = true;
}

function handleLock(folder: FolderRecord) {
  handleOpenUnlock(folder);
}

function handleOpenFolder(folder: FolderRecord) {
  folderStore.openFolder(folder.id);
}

function handleOpenDetails(folder: FolderRecord) {
  targetFolder.value = folder;
  showDetailsDialog.value = true;
}

function handleOpenRemove(folder: FolderRecord) {
  targetFolder.value = folder;
  showRemoveConfirmDialog.value = true;
}

async function handleConfirmRemove(password?: string) {
  if (!targetFolder.value) return;
  isBusy.value = true;
  try {
    await folderStore.removeProtection(targetFolder.value.id, password || '');
    toast.success('Protection Removed', `Permissions restored to "${targetFolder.value.folderName}".`);
    showRemoveConfirmDialog.value = false;
  } catch (err: any) {
    toast.error('Unable to Remove Protection', err.message || 'Incorrect password.');
  } finally {
    isBusy.value = false;
  }
}
</script>

<template>
  <div class="space-y-6">
    <!-- Header -->
    <PageHeader
      title="Protected Folders"
      subtitle="Manage folders currently secured by SecApper."
    >
      <template #actions>
        <Button variant="primary" size="md" @click="showLockDialog = true">
          <template #icon><Plus class="w-4 h-4" /></template>
          Protect Folder
        </Button>
      </template>
    </PageHeader>

    <!-- Filters and Search Bar -->
    <div class="flex flex-col sm:flex-row items-stretch sm:items-center justify-between gap-3">
      <!-- Status Filter Tabs -->
      <div class="flex items-center p-1 rounded-lg bg-[#E4E7EC]/60 dark:bg-[#06152A] text-xs font-semibold select-none">
        <button
          v-for="filter in (['All', 'Locked', 'Unlocked'] as const)"
          :key="filter"
          @click="activeFilter = filter"
          :class="[
            'px-3 py-1.5 rounded-md transition-all',
            activeFilter === filter
              ? 'bg-white dark:bg-[#122D55] text-[#101828] dark:text-[#F8FAFC] shadow-xs'
              : 'text-[#667085] dark:text-[#94A3B8] hover:text-[#101828] dark:hover:text-white'
          ]"
        >
          {{ filter }}
          <span class="ml-1 opacity-70 font-mono">
            ({{
              filter === 'All'
                ? folderStore.totalCount
                : filter === 'Locked'
                ? folderStore.lockedCount
                : folderStore.unlockedCount
            }})
          </span>
        </button>
      </div>

      <!-- Search Box -->
      <div class="relative w-full sm:w-72">
        <Search class="w-4 h-4 absolute left-3 top-2.5 text-[#98A2B3]" />
        <input
          type="text"
          v-model="searchQuery"
          placeholder="Search by folder name or path..."
          class="w-full pl-9 pr-3 py-1.5 text-xs bg-white dark:bg-[#0F1E30] border border-[#D0D5DD] dark:border-[#1E293B] rounded-lg text-[#101828] dark:text-[#F8FAFC] focus:outline-none focus:ring-2 focus:ring-[#122D55]/30"
        />
      </div>
    </div>

    <!-- Folder Cards List -->
    <div v-if="filteredFolders.length > 0" class="space-y-3">
      <FolderCard
        v-for="folder in filteredFolders"
        :key="folder.id"
        :folder="folder"
        @unlock="handleOpenUnlock"
        @lock="handleLock"
        @open="handleOpenFolder"
        @details="handleOpenDetails"
        @remove="handleOpenRemove"
      />
    </div>

    <!-- Empty State -->
    <EmptyState
      v-else
      :icon="FolderLock"
      :title="folderStore.totalCount === 0 ? 'No folders are currently protected.' : 'No matching folders found.'"
      :description="folderStore.totalCount === 0 ? 'Protect your confidential directories with Windows NTFS security.' : 'Try adjusting your search query or filter.'"
    >
      <Button v-if="folderStore.totalCount === 0" variant="primary" size="md" @click="showLockDialog = true">
        <template #icon><Plus class="w-4 h-4" /></template>
        Protect Folder
      </Button>
    </EmptyState>

    <!-- Dialogs -->
    <LockFolderDialog
      :is-open="showLockDialog"
      @close="showLockDialog = false"
      @locked="folderStore.refreshFolders"
    />

    <UnlockFolderDialog
      :is-open="showUnlockDialog"
      :folder="targetFolder"
      @close="showUnlockDialog = false"
      @unlocked="folderStore.refreshFolders"
    />

    <ConfirmationDialog
      :is-open="showRemoveConfirmDialog"
      title="Remove SecApper Protection?"
      :message="`Are you sure you want to remove protection for '${targetFolder?.folderName}'? This restores default filesystem permissions. The folder and its contents will NOT be deleted.`"
      confirm-label="Remove Protection"
      variant="danger"
      :require-password="true"
      :loading="isBusy"
      @close="showRemoveConfirmDialog = false"
      @confirm="handleConfirmRemove"
    />

    <FolderDetails
      :is-open="showDetailsDialog"
      :folder="targetFolder"
      @close="showDetailsDialog = false"
    />
  </div>
</template>
