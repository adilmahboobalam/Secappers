<script setup lang="ts">
import { Loader2 } from 'lucide-vue-next';

interface Props {
  message?: string;
  subtext?: string;
}

withDefaults(defineProps<Props>(), {
  message: 'Loading...',
  subtext: 'Verifying security descriptors',
});
</script>

<template>
  <div class="flex flex-col items-center justify-center p-10 text-center">
    <div class="relative w-10 h-10 mb-4 flex items-center justify-center">
      <Loader2 class="w-8 h-8 text-[#122D55] dark:text-[#93C5FD] animate-spin stroke-[2]" />
    </div>
    <div class="text-sm font-medium text-[#101828] dark:text-[#F8FAFC]">{{ message }}</div>
    <div v-if="subtext" class="text-xs text-[#667085] dark:text-[#94A3B8] mt-1">{{ subtext }}</div>
  </div>
</template>
