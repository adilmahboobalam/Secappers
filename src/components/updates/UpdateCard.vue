<script setup lang="ts">
import { computed } from 'vue';
import type { UpdateInfo } from '../../types';
import Button from '../common/Button.vue';
import UpdateProgress from './UpdateProgress.vue';
import {
  CheckCircle2,
  RefreshCw,
  Download,
  ShieldCheck,
  Database,
  Lock,
  History,
  AlertCircle,
} from 'lucide-vue-next';

interface Props {
  updateInfo: UpdateInfo;
  isChecking: boolean;
  isDownloading: boolean;
  isInstalling: boolean;
}

const props = defineProps<Props>();

const emit = defineEmits<{
  (e: 'check'): void;
  (e: 'update'): void;
  (e: 'later'): void;
}>();

const isUpToDate = computed(() => !props.updateInfo.hasUpdate && props.updateInfo.status === 'Idle');
const isBusy = computed(() => props.isChecking || props.isDownloading || props.isInstalling);
</script>

<template>
  <div class="space-y-6">
    <!-- Main Update Status Card -->
    <div class="sec-card p-6 sm:p-8 bg-white dark:bg-[#0F1E30]">
      <div class="flex flex-col sm:flex-row sm:items-start justify-between gap-6">
        <div class="flex items-start gap-4">
          <div
            :class="[
              'w-12 h-12 rounded-xl flex items-center justify-center shrink-0 border shadow-xs',
              updateInfo.hasUpdate
                ? 'bg-[#EFF8FF] dark:bg-[#1E3A8A]/30 border-[#B2DDFF] text-[#175CD3] dark:text-[#60A5FA]'
                : 'bg-[#ECFDF3] dark:bg-[#064E3B]/30 border-[#ABEFC6] text-[#027A48] dark:text-[#34D399]'
            ]"
          >
            <Download v-if="updateInfo.hasUpdate" class="w-6 h-6 stroke-[2]" />
            <CheckCircle2 v-else class="w-6 h-6 stroke-[2]" />
          </div>

          <div>
            <div class="text-xs font-bold uppercase tracking-wider text-[#667085] dark:text-[#94A3B8] mb-1">
              SecApper Update Channel
            </div>

            <h3 class="text-xl font-bold text-[#101828] dark:text-[#F8FAFC]">
              <span v-if="updateInfo.hasUpdate">Update Available</span>
              <span v-else>You're up to date</span>
            </h3>

            <p class="text-xs text-[#667085] dark:text-[#94A3B8] mt-1">
              {{ updateInfo.statusMessage || "SecApper is running the verified release." }}
            </p>

            <!-- Version Comparison Pills -->
            <div class="flex items-center gap-3 mt-4 text-xs font-mono">
              <div class="px-2.5 py-1 rounded bg-[#F2F4F7] dark:bg-[#06152A] text-[#344054] dark:text-[#CBD5E1] border border-[#EAECF0] dark:border-[#1E293B]">
                Current: <strong>v{{ updateInfo.currentVersion }}</strong>
              </div>
              <div
                v-if="updateInfo.hasUpdate"
                class="px-2.5 py-1 rounded bg-[#EFF8FF] dark:bg-[#1E3A8A]/30 text-[#175CD3] dark:text-[#60A5FA] border border-[#B2DDFF]"
              >
                Latest: <strong>v{{ updateInfo.availableVersion }}</strong>
              </div>
            </div>
          </div>
        </div>

        <!-- Action Buttons -->
        <div class="flex items-center gap-3 shrink-0">
          <Button
            variant="secondary"
            size="md"
            :loading="isChecking"
            :disabled="isBusy"
            @click="emit('check')"
          >
            <template #icon><RefreshCw class="w-4 h-4" /></template>
            Check for Updates
          </Button>

          <Button
            v-if="updateInfo.hasUpdate"
            variant="primary"
            size="md"
            :loading="isDownloading || isInstalling"
            @click="emit('update')"
          >
            <template #icon><Download class="w-4 h-4" /></template>
            Update Now
          </Button>
        </div>
      </div>

      <!-- Live Progress View during Update -->
      <div v-if="isDownloading || isInstalling || updateInfo.status === 'Ready'" class="mt-8 pt-6 border-t border-[#F2F4F7] dark:border-[#1E293B]">
        <UpdateProgress :status="updateInfo.status as any" :progress="updateInfo.downloadProgress" />
      </div>

      <!-- Release Notes if available -->
      <div v-if="updateInfo.hasUpdate && updateInfo.releaseNotes" class="mt-6 pt-6 border-t border-[#F2F4F7] dark:border-[#1E293B]">
        <div class="text-xs font-bold text-[#101828] dark:text-[#F8FAFC] mb-2 uppercase tracking-wide">
          What's New in v{{ updateInfo.availableVersion }}
        </div>
        <div class="p-4 rounded-lg bg-[#F8FAFC] dark:bg-[#06152A] border border-[#EAECF0] dark:border-[#1E293B] text-xs text-[#475467] dark:text-[#CBD5E1] leading-relaxed">
          {{ updateInfo.releaseNotes }}
        </div>
      </div>
    </div>

    <!-- Data Preservation Guarantee Section (Section 19) -->
    <div class="sec-card p-6 bg-[#F8FAFC] dark:bg-[#091D38]/40 border border-[#E4E7EC] dark:border-[#14325C]/50">
      <div class="flex items-center gap-2 mb-3">
        <ShieldCheck class="w-4 h-4 text-[#12B76A]" />
        <h4 class="text-xs font-bold text-[#101828] dark:text-[#F8FAFC] uppercase tracking-wider">
          SecApper In-Place Data Protection Guarantee
        </h4>
      </div>
      <p class="text-xs text-[#667085] dark:text-[#94A3B8] mb-4">
        All updates are installed in place. The update process automatically preserves:
      </p>

      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3 text-xs">
        <div class="p-3 rounded-lg bg-white dark:bg-[#0F1E30] border border-[#EAECF0] dark:border-[#1E293B] flex items-center gap-2">
          <Database class="w-4 h-4 text-[#122D55] dark:text-[#93C5FD] shrink-0" />
          <span class="text-[#344054] dark:text-[#CBD5E1] font-medium">SQLite Database &amp; SDDL Backups</span>
        </div>
        <div class="p-3 rounded-lg bg-white dark:bg-[#0F1E30] border border-[#EAECF0] dark:border-[#1E293B] flex items-center gap-2">
          <Lock class="w-4 h-4 text-[#C5202B] shrink-0" />
          <span class="text-[#344054] dark:text-[#CBD5E1] font-medium">Locked Folders &amp; Passwords</span>
        </div>
        <div class="p-3 rounded-lg bg-white dark:bg-[#0F1E30] border border-[#EAECF0] dark:border-[#1E293B] flex items-center gap-2">
          <History class="w-4 h-4 text-[#F79009] shrink-0" />
          <span class="text-[#344054] dark:text-[#CBD5E1] font-medium">Audit Logs &amp; Security Events</span>
        </div>
        <div class="p-3 rounded-lg bg-white dark:bg-[#0F1E30] border border-[#EAECF0] dark:border-[#1E293B] flex items-center gap-2">
          <ShieldCheck class="w-4 h-4 text-[#12B76A] shrink-0" />
          <span class="text-[#344054] dark:text-[#CBD5E1] font-medium">Ransomware Heuristic Settings</span>
        </div>
      </div>
    </div>
  </div>
</template>
