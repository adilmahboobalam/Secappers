<script setup lang="ts">
import { computed } from 'vue';
import type { FolderStatus } from '../../types';

interface Props {
  status: FolderStatus;
  size?: 'sm' | 'md';
}

const props = withDefaults(defineProps<Props>(), {
  size: 'md',
});

const config = computed(() => {
  switch (props.status) {
    case 'Locked':
      return {
        label: 'LOCKED',
        classes: 'bg-[#FEF3F2] text-[#B42318] border-[#FECDCA] dark:bg-[#7F1D1D]/30 dark:text-[#F87171] dark:border-[#DC2626]/40',
        dot: 'bg-[#C5202B]',
      };
    case 'Unlocked':
      return {
        label: 'UNLOCKED',
        classes: 'bg-[#ECFDF3] text-[#027A48] border-[#ABEFC6] dark:bg-[#064E3B]/30 dark:text-[#34D399] dark:border-[#059669]/40',
        dot: 'bg-[#12B76A]',
      };
    case 'Locking':
      return {
        label: 'SECURING...',
        classes: 'bg-[#EFF8FF] text-[#175CD3] border-[#B2DDFF] dark:bg-[#1E3A8A]/30 dark:text-[#60A5FA] dark:border-[#2563EB]/40',
        dot: 'bg-[#1570EF] animate-ping',
      };
    case 'Unlocking':
      return {
        label: 'RESTORING...',
        classes: 'bg-[#EFF8FF] text-[#175CD3] border-[#B2DDFF] dark:bg-[#1E3A8A]/30 dark:text-[#60A5FA] dark:border-[#2563EB]/40',
        dot: 'bg-[#1570EF] animate-ping',
      };
    case 'RecoveryRequired':
    case 'Inconsistent':
      return {
        label: 'RECOVERY NEEDED',
        classes: 'bg-[#FFFAEB] text-[#B54708] border-[#FEDF89] dark:bg-[#78350F]/30 dark:text-[#FBBF24] dark:border-[#D97706]/40',
        dot: 'bg-[#F79009] animate-pulse',
      };
    default:
      return {
        label: props.status.toUpperCase(),
        classes: 'bg-[#F2F4F7] text-[#344054] border-[#EAECF0] dark:bg-[#1E293B] dark:text-[#CBD5E1]',
        dot: 'bg-[#667085]',
      };
  }
});
</script>

<template>
  <span
    :class="[
      'inline-flex items-center font-bold tracking-wider rounded border select-none',
      size === 'sm' ? 'px-2 py-0.5 text-[10px] gap-1' : 'px-2.5 py-1 text-xs gap-1.5',
      config.classes
    ]"
  >
    <span :class="['w-1.5 h-1.5 rounded-full shrink-0', config.dot]"></span>
    {{ config.label }}
  </span>
</template>
