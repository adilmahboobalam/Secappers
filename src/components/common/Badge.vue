<script setup lang="ts">
import { computed } from 'vue';

interface Props {
  variant?: 'success' | 'warning' | 'danger' | 'navy' | 'neutral' | 'info';
  size?: 'sm' | 'md';
  dot?: boolean;
}

const props = withDefaults(defineProps<Props>(), {
  variant: 'neutral',
  size: 'md',
  dot: false,
});

const variantClasses = computed(() => {
  switch (props.variant) {
    case 'success':
      return 'bg-[#ECFDF3] text-[#027A48] border-[#ABEFC6] dark:bg-[#064E3B]/30 dark:text-[#34D399] dark:border-[#059669]/40';
    case 'warning':
      return 'bg-[#FFFAEB] text-[#B54708] border-[#FEDF89] dark:bg-[#78350F]/30 dark:text-[#FBBF24] dark:border-[#D97706]/40';
    case 'danger':
      return 'bg-[#FEF3F2] text-[#B42318] border-[#FECDCA] dark:bg-[#7F1D1D]/30 dark:text-[#F87171] dark:border-[#DC2626]/40';
    case 'navy':
      return 'bg-[#122D55]/10 text-[#122D55] border-[#122D55]/20 dark:bg-[#122D55]/30 dark:text-[#93C5FD] dark:border-[#1E3A8A]/50';
    case 'info':
      return 'bg-[#EFF8FF] text-[#175CD3] border-[#B2DDFF] dark:bg-[#1E3A8A]/30 dark:text-[#60A5FA] dark:border-[#2563EB]/40';
    case 'neutral':
    default:
      return 'bg-[#F2F4F7] text-[#344054] border-[#EAECF0] dark:bg-[#1E293B] dark:text-[#CBD5E1] dark:border-[#334155]';
  }
});

const dotColor = computed(() => {
  switch (props.variant) {
    case 'success':
      return 'bg-[#12B76A]';
    case 'warning':
      return 'bg-[#F79009]';
    case 'danger':
      return 'bg-[#D92D20]';
    case 'navy':
      return 'bg-[#122D55]';
    case 'info':
      return 'bg-[#1570EF]';
    case 'neutral':
    default:
      return 'bg-[#667085]';
  }
});
</script>

<template>
  <span
    :class="[
      'inline-flex items-center font-medium rounded-full border',
      size === 'sm' ? 'px-2 py-0.5 text-[11px] gap-1' : 'px-2.5 py-0.5 text-xs gap-1.5',
      variantClasses
    ]"
  >
    <span v-if="dot" :class="['w-1.5 h-1.5 rounded-full shrink-0', dotColor]"></span>
    <slot></slot>
  </span>
</template>
