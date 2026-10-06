<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import PageHeader from '../components/layout/PageHeader.vue';
import SecurityStatus from '../components/security/SecurityStatus.vue';
import FolderCard from '../components/folders/FolderCard.vue';
import SecurityEventComponent from '../components/security/SecurityEvent.vue';
import LockFolderDialog from '../components/dialogs/LockFolderDialog.vue';
import UnlockFolderDialog from '../components/dialogs/UnlockFolderDialog.vue';
import ConfirmationDialog from '../components/dialogs/ConfirmationDialog.vue';
import FolderDetails from '../components/folders/FolderDetails.vue';
import Button from '../components/common/Button.vue';
import EmptyState from '../components/common/EmptyState.vue';
import { useSecurityStore } from '../stores/security';
import { useFolderStore } from '../stores/folders';
import { useEventStore } from '../stores/events';
import { useToast } from '../composables/useToast';
import type { FolderRecord } from '../types';
import {
  Plus,
  Lock,
  Unlock,
  History,
  ShieldAlert,
  ArrowRight,
  FolderLock,
  LifeBuoy,
} from 'lucide-vue-next';

const router = useRouter();
const securityStore = useSecurityStore();
const folderStore = useFolderStore();
const eventStore = useEventStore();
const toast = useToast();

const showLockDialog = ref(false);
const showUnlockDialog = ref(false);
const showRemoveConfirmDialog = ref(false);
const showDetailsDialog = ref(false);
const showPanicConfirmDialog = ref(false);
const targetFolder = ref<FolderRecord | null>(null);
const isBusy = ref(false);

onMounted(async () => {
  await Promise.allSettled([
    securityStore.refreshStatus(),
    folderStore.refreshFolders(),
    eventStore.refreshEvents(10),
  ]);
});

function handleOpenUnlock(folder: FolderRecord) {
  targetFolder.value = folder;
  showUnlockDialog.value = true;
}

async function handleLock(folder: FolderRecord) {
  isBusy.value = true;
  try {
    await folderStore.lockFolder(folder.folderPath, '');
    toast.success('Folder Locked', `"${folder.folderName}" is now locked.`);
  } catch (err: any) {
    toast.error('Unable to Lock Folder', err.message || 'Failed to lock folder.');
  } finally {
    isBusy.value = false;
  }
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
    toast.success('Protection Removed', `Permissions restored to "${targetFolder.value.folderName}". Folder was NOT deleted.`);
    showRemoveConfirmDialog.value = false;
  } catch (err: any) {
    toast.error('Unable to Remove Protection', err.message || 'Incorrect password.');
  } finally {
    isBusy.value = false;
  }
}

async function handleConfirmPanicLock() {
  isBusy.value = true;
  try {
    const res = await securityStore.panicLock();
    toast.success('Panic Lock Executed', `Secured ${res.lockedCount} folder(s) immediately.`);
    await folderStore.refreshFolders();
    showPanicConfirmDialog.value = false;
  } catch (err: any) {
    toast.error('Panic Lock Failed', err.message || 'Error executing emergency lock.');
  } finally {
    isBusy.value = false;
  }
}
</script>

<template>
  <div class="space-y-8">
    <!-- Header -->
    <PageHeader
      title="Dashboard"
      subtitle="Commercial-grade Windows NTFS security &amp; ransomware monitoring."
    >
      <template #actions>
        <Button variant="primary" size="md" @click="showLockDialog = true">
          <template #icon><Plus class="w-4 h-4" /></template>
          Protect Folder
        </Button>
      </template>
    </PageHeader>

    <!-- Main Authoritative Security Overview -->
    <SecurityStatus />

    <!-- Quick Action Bar -->
    <div class="sec-card p-4 bg-white dark:bg-[#0F1E30] flex flex-wrap items-center justify-between gap-3">
      <div class="flex items-center gap-2 text-xs font-semibold text-[#101828] dark:text-[#F8FAFC]">
        <span class="text-[#667085] dark:text-[#94A3B8]">Quick Security Actions:</span>
      </div>

      <div class="flex flex-wrap items-center gap-2.5">
        <Button variant="primary" size="sm" @click="showLockDialog = true">
          <template #icon><Plus class="w-3.5 h-3.5" /></template>
          Protect Folder
        </Button>

        <Button variant="secondary" size="sm" @click="router.push('/protected-folders')">
          <template #icon><FolderLock class="w-3.5 h-3.5" /></template>
          Manage Folders
        </Button>

        <Button variant="secondary" size="sm" @click="router.push('/security-events')">
          <template #icon><History class="w-3.5 h-3.5" /></template>
          Security Audit
        </Button>

        <Button variant="secondary" size="sm" @click="router.push('/recovery')">
          <template #icon><LifeBuoy class="w-3.5 h-3.5" /></template>
          Recovery Center
        </Button>

        <Button
          v-if="folderStore.unlockedCount > 0"
          variant="danger"
          size="sm"
          @click="showPanicConfirmDialog = true"
        >
          <template #icon><ShieldAlert class="w-3.5 h-3.5" /></template>
          Emergency Lock All
        </Button>
      </div>
    </div>

    <!-- Dual Column: Protected Folders & Security Activity -->
    <div class="grid grid-cols-1 lg:grid-cols-2 gap-8">
      <!-- Left Column: Protected Folders Overview -->
      <div class="space-y-4">
        <div class="flex items-center justify-between">
          <div class="flex items-center gap-2">
            <h3 class="text-base font-bold text-[#101828] dark:text-[#F8FAFC]">
              Protected Folders
            </h3>
            <span class="text-xs font-mono px-2 py-0.5 rounded-full bg-[#122D55]/10 text-[#122D55] dark:bg-[#122D55]/30 dark:text-[#93C5FD]">
              {{ folderStore.totalCount }}
            </span>
          </div>

          <button
            @click="router.push('/protected-folders')"
            class="text-xs font-semibold text-[#122D55] dark:text-[#93C5FD] hover:underline flex items-center gap-1"
          >
            <span>View All</span>
            <ArrowRight class="w-3.5 h-3.5" />
          </button>
        </div>

        <div v-if="folderStore.recentFolders.length > 0" class="space-y-3">
          <FolderCard
            v-for="folder in folderStore.recentFolders"
            :key="folder.id"
            :folder="folder"
            @unlock="handleOpenUnlock"
            @lock="handleLock"
            @open="handleOpenFolder"
            @details="handleOpenDetails"
            @remove="handleOpenRemove"
          />
        </div>

        <EmptyState
          v-else
          :icon="FolderLock"
          title="No folders are currently protected"
          description="Click '+ Protect Folder' to secure your confidential files with Windows NTFS permissions."
        >
          <Button variant="primary" size="sm" @click="showLockDialog = true">
            <template #icon><Plus class="w-4 h-4" /></template>
            Protect Folder
          </Button>
        </EmptyState>
      </div>

      <!-- Right Column: Security Activity Timeline -->
      <div class="space-y-4">
        <div class="flex items-center justify-between">
          <div class="flex items-center gap-2">
            <h3 class="text-base font-bold text-[#101828] dark:text-[#F8FAFC]">
              Security Activity
            </h3>
            <span class="text-xs font-mono px-2 py-0.5 rounded-full bg-[#12B76A]/10 text-[#027A48] dark:bg-[#064E3B]/40 dark:text-[#34D399]">
              {{ eventStore.todayEventsCount }} today
            </span>
          </div>

          <button
            @click="router.push('/security-events')"
            class="text-xs font-semibold text-[#122D55] dark:text-[#93C5FD] hover:underline flex items-center gap-1"
          >
            <span>Full Audit Log</span>
            <ArrowRight class="w-3.5 h-3.5" />
          </button>
        </div>

        <div v-if="eventStore.recentEvents.length > 0" class="sec-card p-4 sm:p-5 bg-white dark:bg-[#0F1E30] divide-y divide-[#F2F4F7] dark:divide-[#1E293B]">
          <SecurityEventComponent
            v-for="evt in eventStore.recentEvents"
            :key="evt.id"
            :event="evt"
          />
        </div>

        <EmptyState
          v-else
          :icon="History"
          title="No security events detected"
          description="Security operations and ransomware monitoring logs will appear here in real time."
        />
      </div>
    </div>

    <!-- Modals -->
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

    <ConfirmationDialog
      :is-open="showPanicConfirmDialog"
      title="Execute Emergency Lock?"
      message="SecApper will immediately apply Deny permissions to all registered folders to prevent unauthorized access or malware modification."
      confirm-label="Lock All Folders"
      variant="danger"
      :loading="isBusy"
      @close="showPanicConfirmDialog = false"
      @confirm="handleConfirmPanicLock"
    />

    <FolderDetails
      :is-open="showDetailsDialog"
      :folder="targetFolder"
      @close="showDetailsDialog = false"
    />
  </div>
</template>
