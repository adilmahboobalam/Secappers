<script setup lang="ts">
import { ref } from 'vue';
import type { FolderRecord } from '../../types';
import FolderStatus from './FolderStatus.vue';
import Button from '../common/Button.vue';
import {
  Lock,
  Unlock,
  Folder,
  FolderOpen,
  MoreVertical,
  Shield,
  Eye,
  Trash2,
  ExternalLink,
  ShieldAlert,
  Info,
} from 'lucide-vue-next';

interface Props {
  folder: FolderRecord;
}

const props = defineProps<Props>();

const emit = defineEmits<{
  (e: 'unlock', folder: FolderRecord): void;
  (e: 'lock', folder: FolderRecord): void;
  (e: 'open', folder: FolderRecord): void;
  (e: 'details', folder: FolderRecord): void;
  (e: 'remove', folder: FolderRecord): void;
}>();

const showMenu = ref(false);

function toggleMenu(e: Event) {
  e.stopPropagation();
  showMenu.value = !showMenu.value;
}

function handleUnlock() {
  showMenu.value = false;
  emit('unlock', props.folder);
}

function handleLock() {
  showMenu.value = false;
  emit('lock', props.folder);
}

function handleOpen() {
  showMenu.value = false;
  emit('open', props.folder);
}

function handleDetails() {
  showMenu.value = false;
  emit('details', props.folder);
}

function handleRemove() {
  showMenu.value = false;
  emit('remove', props.folder);
}
</script>

<template>
  <div
    class="sec-card p-4 sm:p-5 relative transition-all duration-200 hover:border-[#CBD5E1] dark:hover:border-[#334155] hover:shadow-card bg-white dark:bg-[#0F1E30]"
  >
    <div class="flex items-start justify-between gap-3">
      <!-- Icon + Folder Name + Path -->
      <div class="flex items-start gap-3.5 min-w-0">
        <!-- Status Icon Avatar -->
        <div
          :class="[
            'w-10 h-10 rounded-lg flex items-center justify-center shrink-0 border shadow-xs',
            folder.status === 'Locked'
              ? 'bg-[#FEF3F2] dark:bg-[#7F1D1D]/30 border-[#FECDCA] dark:border-[#DC2626]/40 text-[#C5202B]'
              : 'bg-[#ECFDF3] dark:bg-[#064E3B]/30 border-[#ABEFC6] dark:border-[#059669]/40 text-[#12B76A]'
          ]"
        >
          <Lock v-if="folder.status === 'Locked'" class="w-5 h-5 stroke-[2]" />
          <FolderOpen v-else class="w-5 h-5 stroke-[2]" />
        </div>

        <!-- Title & Path -->
        <div class="min-w-0">
          <div class="flex items-center gap-2">
            <h3 class="text-sm sm:text-base font-bold text-[#101828] dark:text-[#F8FAFC] truncate">
              {{ folder.folderName }}
            </h3>
            <span
              v-if="folder.protectionMode === 'LockedAndProtected'"
              class="hidden sm:inline-flex items-center gap-1 text-[10px] font-semibold text-[#122D55] dark:text-[#93C5FD] bg-[#122D55]/5 dark:bg-[#122D55]/25 px-1.5 py-0.5 rounded border border-[#122D55]/10"
            >
              <Shield class="w-2.5 h-2.5" />
              NTFS + Heuristics
            </span>
          </div>

          <p class="text-xs text-[#667085] dark:text-[#94A3B8] font-mono truncate mt-0.5" :title="folder.folderPath">
            {{ folder.folderPath }}
          </p>
        </div>
      </div>

      <!-- Right: Status Badge & Overflow Menu -->
      <div class="flex items-center gap-2 shrink-0">
        <FolderStatus :status="folder.status" size="sm" />

        <!-- Quick Context Menu -->
        <div class="relative">
          <button
            @click="toggleMenu"
            class="w-8 h-8 flex items-center justify-center text-[#667085] hover:text-[#101828] dark:hover:text-white hover:bg-[#F2F4F7] dark:hover:bg-[#1E293B] rounded-md transition-colors"
            title="Options"
          >
            <MoreVertical class="w-4 h-4" />
          </button>

          <!-- Dropdown menu -->
          <div
            v-if="showMenu"
            @click.stop
            class="absolute right-0 top-9 w-48 bg-white dark:bg-[#091D38] border border-[#E4E7EC] dark:border-[#1E293B] rounded-lg shadow-modal py-1 z-30 animate-scale-in text-xs font-medium"
          >
            <button
              v-if="folder.status === 'Locked'"
              @click="handleUnlock"
              class="w-full flex items-center gap-2 px-3 py-2 text-[#101828] dark:text-[#F8FAFC] hover:bg-[#F2F4F7] dark:hover:bg-[#122D55] text-left"
            >
              <Unlock class="w-3.5 h-3.5 text-[#12B76A]" />
              Unlock Folder
            </button>

            <button
              v-else
              @click="handleLock"
              class="w-full flex items-center gap-2 px-3 py-2 text-[#101828] dark:text-[#F8FAFC] hover:bg-[#F2F4F7] dark:hover:bg-[#122D55] text-left"
            >
              <Lock class="w-3.5 h-3.5 text-[#C5202B]" />
              Lock Folder
            </button>

            <button
              @click="handleOpen"
              class="w-full flex items-center gap-2 px-3 py-2 text-[#101828] dark:text-[#F8FAFC] hover:bg-[#F2F4F7] dark:hover:bg-[#122D55] text-left"
            >
              <ExternalLink class="w-3.5 h-3.5 text-[#1570EF]" />
              Open in Explorer
            </button>

            <button
              @click="handleDetails"
              class="w-full flex items-center gap-2 px-3 py-2 text-[#101828] dark:text-[#F8FAFC] hover:bg-[#F2F4F7] dark:hover:bg-[#122D55] text-left"
            >
              <Info class="w-3.5 h-3.5 text-[#667085]" />
              Security Details
            </button>

            <div class="my-1 border-t border-[#F2F4F7] dark:border-[#1E293B]"></div>

            <button
              @click="handleRemove"
              class="w-full flex items-center gap-2 px-3 py-2 text-[#D92D20] hover:bg-[#FEF3F2] dark:hover:bg-[#7F1D1D]/30 text-left"
            >
              <Trash2 class="w-3.5 h-3.5" />
              Remove Protection
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- Bottom Metadata Row & Direct Action Buttons -->
    <div
      class="mt-4 pt-3 border-t border-[#F2F4F7] dark:border-[#1E293B] flex flex-wrap items-center justify-between gap-3 text-xs"
    >
      <div class="flex items-center gap-2 text-[#667085] dark:text-[#94A3B8]">
        <Shield class="w-3.5 h-3.5 text-[#122D55] dark:text-[#93C5FD]" />
        <span>Windows NTFS Protection Active</span>
        <span>•</span>
        <span>Monitoring Active</span>
      </div>

      <!-- Quick Action Buttons -->
      <div class="flex items-center gap-2">
        <Button
          v-if="folder.status === 'Locked'"
          variant="primary"
          size="sm"
          @click="handleUnlock"
        >
          <template #icon><Unlock class="w-3.5 h-3.5" /></template>
          Unlock
        </Button>

        <Button
          v-else
          variant="navy"
          size="sm"
          @click="handleLock"
        >
          <template #icon><Lock class="w-3.5 h-3.5" /></template>
          Lock
        </Button>

        <Button
          variant="secondary"
          size="sm"
          @click="handleOpen"
        >
          <template #icon><ExternalLink class="w-3.5 h-3.5" /></template>
          Open
        </Button>
      </div>
    </div>
  </div>
</template>
