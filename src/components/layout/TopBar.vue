<script setup lang="ts">
import { computed } from 'vue';
import { useSecurityStore } from '../../stores/security';
import { useUserStore } from '../../stores/user';
import { nativeBridge } from '../../services/nativeBridge';
import shieldUrl from '../../assets/branding/secapper-shield.png';
import { Minus, Square, X, WifiOff, Shield } from 'lucide-vue-next';

const securityStore = useSecurityStore();
const userStore = useUserStore();

const isProtected = computed(() => securityStore.isProtected);
const statusText = computed(() => (isProtected.value ? 'PROTECTED' : 'ATTENTION REQUIRED'));
const userName = computed(() => userStore.profile.userName || 'Security User');
const securityTier = computed(() => userStore.profile.securityTier || 'Standard');

function handleMinimize() {
  nativeBridge.window.minimize();
}

function handleMaximize() {
  nativeBridge.window.maximize();
}

function handleClose() {
  nativeBridge.window.close();
}
</script>

<template>
  <header
    class="h-10 bg-white dark:bg-[#091D38] border-b border-[#E4E7EC] dark:border-[#1E293B] flex items-center justify-between px-3 select-none app-drag-region shrink-0 z-30"
  >
    <!-- Left: Subtitle / Window Title -->
    <div class="flex items-center gap-2 app-no-drag">
      <img :src="shieldUrl" alt="SecApper Shield" class="w-4 h-4 object-contain" />
      <span class="text-xs font-semibold text-[#101828] dark:text-[#F8FAFC] tracking-wide">
        SecApper
      </span>
      <span class="text-[11px] text-[#667085] dark:text-[#94A3B8] hidden sm:inline">
        • Windows Security &amp; Ransomware Protection
      </span>
    </div>

    <!-- Center/Right: Status pill + Window Controls -->
    <div class="flex items-center gap-3 app-no-drag">
      <!-- Dynamic User Chip -->
      <button
        @click="userStore.relaunchSetup"
        class="hidden sm:flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-[11px] font-medium bg-[#F2F4F7] hover:bg-[#E4E7EC] dark:bg-[#1E293B] dark:hover:bg-[#2A3F60] text-[#344054] dark:text-[#E2E8F0] border border-[#E4E7EC] dark:border-[#334155] transition-all cursor-pointer"
        title="SecApper Dynamic Profile - Click to customize"
      >
        <div class="w-3.5 h-3.5 rounded-full bg-[#122D55] text-white flex items-center justify-center text-[9px] font-bold">
          {{ userName.charAt(0).toUpperCase() }}
        </div>
        <span class="max-w-[100px] truncate">{{ userName }}</span>
        <span class="text-[9px] font-mono px-1 rounded bg-[#122D55]/10 dark:bg-blue-500/20 text-blue-700 dark:text-blue-300 font-bold">
          {{ securityTier }}
        </span>
      </button>

      <!-- Live Security Badge -->
      <div
        :class="[
          'flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-[11px] font-semibold tracking-wider transition-colors',
          isProtected
            ? 'bg-[#ECFDF3] text-[#027A48] border border-[#ABEFC6] dark:bg-[#064E3B]/40 dark:text-[#34D399] dark:border-[#059669]/50'
            : 'bg-[#FEF3F2] text-[#B42318] border border-[#FECDCA] dark:bg-[#7F1D1D]/40 dark:text-[#F87171] dark:border-[#DC2626]/50'
        ]"
      >
        <span
          :class="[
            'w-1.5 h-1.5 rounded-full',
            isProtected ? 'bg-[#12B76A]' : 'bg-[#D92D20] animate-ping'
          ]"
        ></span>
        {{ statusText }}
      </div>

      <!-- Offline Mode indicator badge -->
      <div
        class="hidden md:flex items-center gap-1 px-2 py-0.5 rounded text-[11px] text-[#667085] dark:text-[#94A3B8] bg-[#F2F4F7] dark:bg-[#1E293B]"
        title="SecApper operates 100% offline. Protection continues without internet."
      >
        <Shield class="w-3 h-3 text-[#122D55] dark:text-[#93C5FD]" />
        <span>Offline-First</span>
      </div>

      <!-- Native Windows Window Controls -->
      <div class="flex items-center ml-1 border-l border-[#E4E7EC] dark:border-[#1E293B] pl-2">
        <button
          @click="handleMinimize"
          class="w-8 h-7 flex items-center justify-center text-[#667085] hover:text-[#101828] hover:bg-[#F2F4F7] dark:hover:bg-[#1E293B] dark:hover:text-[#F8FAFC] rounded transition-colors"
          title="Minimize"
        >
          <Minus class="w-3.5 h-3.5" />
        </button>
        <button
          @click="handleMaximize"
          class="w-8 h-7 flex items-center justify-center text-[#667085] hover:text-[#101828] hover:bg-[#F2F4F7] dark:hover:bg-[#1E293B] dark:hover:text-[#F8FAFC] rounded transition-colors"
          title="Maximize"
        >
          <Square class="w-3 h-3" />
        </button>
        <button
          @click="handleClose"
          class="w-8 h-7 flex items-center justify-center text-[#667085] hover:text-white hover:bg-[#C5202B] rounded transition-colors"
          title="Minimize to System Tray"
        >
          <X class="w-3.5 h-3.5" />
        </button>
      </div>
    </div>
  </header>
</template>
