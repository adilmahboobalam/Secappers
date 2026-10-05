<script setup lang="ts">
import type { Component } from 'vue';
import { ShieldAlert } from 'lucide-vue-next';

interface Props {
  icon?: Component;
  title: string;
  description: string;
}

withDefaults(defineProps<Props>(), {
  icon: ShieldAlert,
});
</script>

<template>
  <div class="flex flex-col items-center justify-center p-8 sm:p-12 text-center rounded-lg border border-dashed border-[#D0D5DD] dark:border-[#334155] bg-white/50 dark:bg-[#0F1E30]/40">
    <div class="w-12 h-12 rounded-full bg-[#122D55]/5 dark:bg-[#122D55]/30 flex items-center justify-center text-[#122D55] dark:text-[#93C5FD] mb-4">
      <component :is="icon" class="w-6 h-6 stroke-[1.75]" />
    </div>
    <h3 class="text-base font-semibold text-[#101828] dark:text-[#F8FAFC] mb-1.5">{{ title }}</h3>
    <p class="text-sm text-[#667085] dark:text-[#94A3B8] max-w-sm mb-6 leading-relaxed">{{ description }}</p>
    <div v-if="$slots.default" class="flex items-center gap-3">
      <slot></slot>
    </div>
  </div>
</template>
