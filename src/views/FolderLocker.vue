<script setup lang="ts">
import { ref } from 'vue';
import { useRouter } from 'vue-router';
import PageHeader from '../components/layout/PageHeader.vue';
import Button from '../components/common/Button.vue';
import LockFolderDialog from '../components/dialogs/LockFolderDialog.vue';
import { useFolderStore } from '../stores/folders';
import { FolderLock, Plus, ShieldCheck, KeyRound, CheckCircle2, Shield } from 'lucide-vue-next';

const router = useRouter();
const folderStore = useFolderStore();
const showLockDialog = ref(false);

const steps = [
  {
    number: '01',
    title: 'Select Folder',
    description: 'Pick any local NTFS directory on Windows 10 or 11 you wish to safeguard.',
    icon: FolderLock,
  },
  {
    number: '02',
    title: 'Set Password',
    description: 'Derive a cryptographic credential using 310,000 PBKDF2-SHA256 iterations.',
    icon: KeyRound,
  },
  {
    number: '03',
    title: 'Apply Windows Protection',
    description: 'Strip inherited access and inject explicit Deny ACEs directly into the filesystem ACL.',
    icon: Shield,
  },
  {
    number: '04',
    title: 'Verify Protection',
    description: 'Cryptographically verify SDDL descriptors and register directory for heuristic monitoring.',
    icon: CheckCircle2,
  },
];

function handleFolderLocked() {
  router.push('/protected-folders');
}
</script>

<template>
  <div class="space-y-8 max-w-4xl mx-auto">
    <!-- Header -->
    <PageHeader
      title="Folder Locker"
      subtitle="Protect your folders using Windows security permissions."
    />

    <!-- Main Action Card -->
    <div
      class="sec-card p-8 sm:p-12 text-center bg-white dark:bg-[#0F1E30] border border-[#E4E7EC] dark:border-[#1E293B]"
    >
      <div class="w-16 h-16 rounded-2xl bg-[#C5202B]/10 dark:bg-[#C5202B]/20 flex items-center justify-center text-[#C5202B] mx-auto mb-5 shadow-xs">
        <FolderLock class="w-8 h-8 stroke-[2]" />
      </div>

      <h2 class="text-2xl font-bold text-[#101828] dark:text-[#F8FAFC]">
        Protect a Folder
      </h2>

      <p class="text-sm text-[#667085] dark:text-[#94A3B8] max-w-md mx-auto mt-2 mb-8 leading-relaxed">
        Choose a folder to secure. SecApper applies native Windows NTFS access control rules so unauthorized local users and ransomware cannot open or modify your data.
      </p>

      <div class="flex justify-center">
        <Button variant="primary" size="lg" @click="showLockDialog = true">
          <template #icon><Plus class="w-5 h-5" /></template>
          Protect Folder
        </Button>
      </div>
    </div>

    <!-- How It Works Section (Section 14) -->
    <div>
      <div class="text-xs font-bold uppercase tracking-wider text-[#667085] dark:text-[#94A3B8] mb-4">
        How It Works
      </div>

      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div
          v-for="step in steps"
          :key="step.number"
          class="sec-card p-5 bg-white dark:bg-[#0F1E30] border border-[#E4E7EC] dark:border-[#1E293B]"
        >
          <div class="flex items-center justify-between mb-3">
            <span class="text-xs font-bold font-mono text-[#C5202B] dark:text-[#F87171] tracking-wider">
              {{ step.number }}
            </span>
            <component :is="step.icon" class="w-4 h-4 text-[#122D55] dark:text-[#93C5FD]" />
          </div>

          <h4 class="text-sm font-bold text-[#101828] dark:text-[#F8FAFC] mb-1">
            {{ step.title }}
          </h4>

          <p class="text-xs text-[#667085] dark:text-[#94A3B8] leading-relaxed">
            {{ step.description }}
          </p>
        </div>
      </div>
    </div>

    <!-- Dialog -->
    <LockFolderDialog
      :is-open="showLockDialog"
      @close="showLockDialog = false"
      @locked="handleFolderLocked"
    />
  </div>
</template>
