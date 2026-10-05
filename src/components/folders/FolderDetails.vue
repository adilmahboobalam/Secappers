<script setup lang="ts">
import type { FolderRecord } from '../../types';
import FolderStatus from './FolderStatus.vue';
import Button from '../common/Button.vue';
import { X, Shield, Lock, Calendar, FileCode, CheckCircle2, HardDrive } from 'lucide-vue-next';

interface Props {
  folder: FolderRecord | null;
  isOpen: boolean;
}

defineProps<Props>();

const emit = defineEmits<{
  (e: 'close'): void;
}>();

function formatDate(iso?: string) {
  if (!iso) return 'Not recorded';
  try {
    return new Date(iso).toLocaleString();
  } catch {
    return iso;
  }
}
</script>

<template>
  <div
    v-if="isOpen && folder"
    class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-xs select-none"
    @click="emit('close')"
  >
    <div
      class="bg-white dark:bg-[#0F1E30] border border-[#E4E7EC] dark:border-[#1E293B] rounded-xl shadow-modal max-w-lg w-full overflow-hidden animate-scale-in"
      @click.stop
    >
      <!-- Dialog Header -->
      <div class="px-6 py-4 border-b border-[#E4E7EC] dark:border-[#1E293B] flex items-center justify-between bg-[#F8FAFC] dark:bg-[#091D38]/50">
        <div class="flex items-center gap-2.5">
          <div class="w-8 h-8 rounded-lg bg-[#122D55]/10 dark:bg-[#122D55]/30 flex items-center justify-center text-[#122D55] dark:text-[#93C5FD]">
            <Shield class="w-4 h-4" />
          </div>
          <div>
            <h3 class="text-sm font-bold text-[#101828] dark:text-[#F8FAFC]">Security Specifications</h3>
            <p class="text-[11px] text-[#667085] dark:text-[#94A3B8]">Windows NTFS filesystem descriptor details</p>
          </div>
        </div>

        <button
          @click="emit('close')"
          class="w-7 h-7 flex items-center justify-center text-[#98A2B3] hover:text-[#101828] dark:hover:text-[#F8FAFC] rounded transition-colors"
        >
          <X class="w-4 h-4" />
        </button>
      </div>

      <!-- Content Details -->
      <div class="p-6 space-y-4 text-xs">
        <div>
          <div class="text-[11px] font-semibold text-[#667085] dark:text-[#94A3B8] uppercase tracking-wider mb-1">
            Folder Information
          </div>
          <div class="p-3 rounded-lg bg-[#F8FAFC] dark:bg-[#06152A] border border-[#EAECF0] dark:border-[#1E293B] space-y-2">
            <div class="flex justify-between items-center">
              <span class="text-[#667085] dark:text-[#94A3B8]">Folder Name</span>
              <span class="font-bold text-[#101828] dark:text-[#F8FAFC]">{{ folder.folderName }}</span>
            </div>
            <div class="flex flex-col gap-0.5 pt-1 border-t border-[#F2F4F7] dark:border-[#14325C]/40">
              <span class="text-[#667085] dark:text-[#94A3B8]">Full Windows Path</span>
              <span class="font-mono text-[#101828] dark:text-[#CBD5E1] break-all">{{ folder.folderPath }}</span>
            </div>
            <div class="flex justify-between items-center pt-1 border-t border-[#F2F4F7] dark:border-[#14325C]/40">
              <span class="text-[#667085] dark:text-[#94A3B8]">Current Status</span>
              <FolderStatus :status="folder.status" size="sm" />
            </div>
          </div>
        </div>

        <div>
          <div class="text-[11px] font-semibold text-[#667085] dark:text-[#94A3B8] uppercase tracking-wider mb-1">
            Security Enforcement &amp; Cryptography
          </div>
          <div class="p-3 rounded-lg bg-[#F8FAFC] dark:bg-[#06152A] border border-[#EAECF0] dark:border-[#1E293B] space-y-2">
            <div class="flex justify-between items-center">
              <span class="text-[#667085] dark:text-[#94A3B8]">Access Control Mechanism</span>
              <span class="font-semibold text-[#12B76A] flex items-center gap-1">
                <CheckCircle2 class="w-3.5 h-3.5" />
                NTFS Deny ACE Active
              </span>
            </div>
            <div class="flex justify-between items-center">
              <span class="text-[#667085] dark:text-[#94A3B8]">Inheritance Rule</span>
              <span class="font-mono text-[#101828] dark:text-[#CBD5E1]">Stripped (Explicit Only)</span>
            </div>
            <div class="flex justify-between items-center">
              <span class="text-[#667085] dark:text-[#94A3B8]">Password KDF Algorithm</span>
              <span class="font-mono text-[#101828] dark:text-[#CBD5E1]">PBKDF2-SHA256 (310k iter)</span>
            </div>
            <div class="flex justify-between items-center">
              <span class="text-[#667085] dark:text-[#94A3B8]">SDDL Backup Integrity</span>
              <span class="font-mono text-[#122D55] dark:text-[#93C5FD]">SHA-256 Checksum Verified</span>
            </div>
          </div>
        </div>

        <div>
          <div class="text-[11px] font-semibold text-[#667085] dark:text-[#94A3B8] uppercase tracking-wider mb-1">
            Timestamps
          </div>
          <div class="p-3 rounded-lg bg-[#F8FAFC] dark:bg-[#06152A] border border-[#EAECF0] dark:border-[#1E293B] space-y-1.5">
            <div class="flex justify-between items-center">
              <span class="text-[#667085] dark:text-[#94A3B8]">Registration Date</span>
              <span class="text-[#101828] dark:text-[#CBD5E1]">{{ formatDate(folder.createdAt) }}</span>
            </div>
            <div class="flex justify-between items-center">
              <span class="text-[#667085] dark:text-[#94A3B8]">Last Locked</span>
              <span class="text-[#101828] dark:text-[#CBD5E1]">{{ formatDate(folder.lastLockedAt) }}</span>
            </div>
            <div class="flex justify-between items-center">
              <span class="text-[#667085] dark:text-[#94A3B8]">Last Unlocked</span>
              <span class="text-[#101828] dark:text-[#CBD5E1]">{{ formatDate(folder.lastUnlockedAt) }}</span>
            </div>
          </div>
        </div>
      </div>

      <!-- Dialog Footer -->
      <div class="px-6 py-3.5 border-t border-[#E4E7EC] dark:border-[#1E293B] flex justify-end bg-[#F8FAFC] dark:bg-[#091D38]/50">
        <Button variant="secondary" size="md" @click="emit('close')">
          Close Details
        </Button>
      </div>
    </div>
  </div>
</template>
