<script setup lang="ts">
import { computed } from 'vue';
import SecurityShield from './SecurityShield.vue';
import { useSecurityStore } from '../../stores/security';
import { useFolderStore } from '../../stores/folders';
import { useRansomwareStore } from '../../stores/ransomware';
import { CheckCircle2, AlertTriangle, ShieldAlert } from 'lucide-vue-next';

const securityStore = useSecurityStore();
const folderStore = useFolderStore();
const ransomwareStore = useRansomwareStore();

const isProtected = computed(() => securityStore.isProtected);
const threatLevel = computed(() => securityStore.threatLevel);
const protectedCount = computed(() => folderStore.totalCount);
const lockedCount = computed(() => folderStore.lockedCount);
const ransomwareActive = computed(() => ransomwareStore.status.active);
</script>

<template>
  <div
    class="sec-card p-8 text-center relative overflow-hidden bg-white dark:bg-[#0F1E30] border border-[#E4E7EC] dark:border-[#1E293B]"
  >
    <!-- Background subtle brand watermark -->
    <div
      class="absolute -right-12 -top-12 w-64 h-64 rounded-full bg-[#122D55]/[0.02] dark:bg-[#93C5FD]/[0.02] pointer-events-none"
    ></div>

    <!-- Centered Brand Shield -->
    <div class="mb-5 flex justify-center">
      <SecurityShield :is-protected="isProtected" size="lg" />
    </div>

    <!-- Main Authoritative Headline -->
    <div class="space-y-1 mb-2">
      <div class="text-xs font-bold tracking-[0.2em] text-[#667085] dark:text-[#94A3B8] uppercase">
        SecApper Security Engine
      </div>
      <h2
        :class="[
          'text-2xl sm:text-3xl font-extrabold tracking-tight',
          isProtected ? 'text-[#101828] dark:text-[#F8FAFC]' : 'text-[#C5202B]'
        ]"
      >
        <span v-if="isProtected">YOUR SYSTEM IS PROTECTED</span>
        <span v-else>ATTENTION REQUIRED</span>
      </h2>
    </div>

    <!-- Subtitle explanation -->
    <p class="text-sm text-[#667085] dark:text-[#94A3B8] max-w-md mx-auto mb-6">
      <span v-if="isProtected">
        SecApper is actively protecting your secured folders with Windows NTFS access control and real-time ransomware heuristics.
      </span>
      <span v-else>
        One or more security conditions require your review to ensure complete protection.
      </span>
    </p>

    <!-- Protection Active Status Pill -->
    <div class="inline-flex items-center gap-2 px-3 py-1 rounded-full text-xs font-semibold mb-8 border"
      :class="[
        isProtected
          ? 'bg-[#ECFDF3] text-[#027A48] border-[#ABEFC6] dark:bg-[#064E3B]/30 dark:text-[#34D399] dark:border-[#059669]/50'
          : 'bg-[#FEF3F2] text-[#B42318] border-[#FECDCA] dark:bg-[#7F1D1D]/30 dark:text-[#F87171] dark:border-[#DC2626]/50'
      ]"
    >
      <span
        :class="[
          'w-2 h-2 rounded-full',
          isProtected ? 'bg-[#12B76A]' : 'bg-[#D92D20] animate-ping'
        ]"
      ></span>
      <span>{{ isProtected ? 'Protection Active & Verified' : 'Protection Needs Review' }}</span>
    </div>

    <!-- Core Metrics Strip -->
    <div class="grid grid-cols-1 sm:grid-cols-3 gap-4 pt-6 border-t border-[#F2F4F7] dark:border-[#1E293B] max-w-2xl mx-auto">
      <!-- Metric: Protected Folders -->
      <div class="p-3 rounded-lg bg-[#F8FAFC] dark:bg-[#091D38]/50 border border-[#EAECF0] dark:border-[#14325C]/40">
        <div class="text-xs text-[#667085] dark:text-[#94A3B8] font-medium mb-1">
          Protected Folders
        </div>
        <div class="text-2xl font-bold text-[#101828] dark:text-[#F8FAFC]">
          {{ protectedCount }}
        </div>
        <div class="text-[11px] text-[#667085] dark:text-[#94A3B8] mt-0.5">
          {{ lockedCount }} currently locked
        </div>
      </div>

      <!-- Metric: Threat Level -->
      <div class="p-3 rounded-lg bg-[#F8FAFC] dark:bg-[#091D38]/50 border border-[#EAECF0] dark:border-[#14325C]/40">
        <div class="text-xs text-[#667085] dark:text-[#94A3B8] font-medium mb-1">
          Threat Level
        </div>
        <div
          :class="[
            'text-2xl font-bold tracking-wide',
            threatLevel === 'LOW' ? 'text-[#12B76A]' : threatLevel === 'MEDIUM' ? 'text-[#F79009]' : 'text-[#C5202B]'
          ]"
        >
          {{ threatLevel }}
        </div>
        <div class="text-[11px] text-[#667085] dark:text-[#94A3B8] mt-0.5">
          Heuristic scoring
        </div>
      </div>

      <!-- Metric: Ransomware Protection -->
      <div class="p-3 rounded-lg bg-[#F8FAFC] dark:bg-[#091D38]/50 border border-[#EAECF0] dark:border-[#14325C]/40">
        <div class="text-xs text-[#667085] dark:text-[#94A3B8] font-medium mb-1">
          Ransomware Shield
        </div>
        <div class="text-2xl font-bold flex items-center justify-center gap-1.5"
          :class="ransomwareActive ? 'text-[#122D55] dark:text-[#93C5FD]' : 'text-[#667085]'"
        >
          {{ ransomwareActive ? 'ACTIVE' : 'OFF' }}
        </div>
        <div class="text-[11px] text-[#667085] dark:text-[#94A3B8] mt-0.5">
          Real-time watcher
        </div>
      </div>
    </div>
  </div>
</template>
