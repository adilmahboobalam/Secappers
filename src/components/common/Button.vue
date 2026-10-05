<script setup lang="ts">
import { computed } from 'vue';
import { Loader2 } from 'lucide-vue-next';

interface Props {
  variant?: 'primary' | 'secondary' | 'outline' | 'ghost' | 'danger' | 'navy';
  size?: 'sm' | 'md' | 'lg';
  loading?: boolean;
  disabled?: boolean;
  type?: 'button' | 'submit' | 'reset';
}

const props = withDefaults(defineProps<Props>(), {
  variant: 'primary',
  size: 'md',
  loading: false,
  disabled: false,
  type: 'button',
});

const emit = defineEmits<{
  (e: 'click', event: MouseEvent): void;
}>();

const variantClasses = computed(() => {
  switch (props.variant) {
    case 'primary':
      return 'bg-[#C5202B] hover:bg-[#A81B24] active:bg-[#8F161E] text-white shadow-sm border border-transparent font-medium';
    case 'navy':
      return 'bg-[#122D55] hover:bg-[#0B1E38] active:bg-[#06152A] text-white shadow-sm border border-transparent font-medium';
    case 'secondary':
      return 'bg-white dark:bg-[#0F1E30] hover:bg-[#F8FAFC] dark:hover:bg-[#1E293B] text-[#101828] dark:text-[#F8FAFC] border border-[#E4E7EC] dark:border-[#1E293B] shadow-sm font-medium';
    case 'outline':
      return 'bg-transparent border border-[#122D55] text-[#122D55] hover:bg-[#122D55]/5 font-medium';
    case 'danger':
      return 'bg-[#D92D20] hover:bg-[#B42318] text-white shadow-sm border border-transparent font-medium';
    case 'ghost':
      return 'bg-transparent hover:bg-black/5 dark:hover:bg-white/5 text-[#667085] dark:text-[#94A3B8] hover:text-[#101828] dark:hover:text-[#F8FAFC] font-medium';
    default:
      return 'bg-[#C5202B] text-white';
  }
});

const sizeClasses = computed(() => {
  switch (props.size) {
    case 'sm':
      return 'text-xs px-2.5 py-1.5 rounded-md gap-1.5';
    case 'lg':
      return 'text-base px-5 py-2.5 rounded-md gap-2.5';
    case 'md':
    default:
      return 'text-sm px-3.5 py-2 rounded-md gap-2';
  }
});

const handleClick = (e: MouseEvent) => {
  if (!props.disabled && !props.loading) {
    emit('click', e);
  }
};
</script>

<template>
  <button
    :type="type"
    :disabled="disabled || loading"
    :class="[
      'inline-flex items-center justify-center transition-all duration-150 select-none outline-none focus-visible:ring-2 focus-visible:ring-[#122D55]/40 active:scale-[0.99]',
      variantClasses,
      sizeClasses,
      (disabled || loading) ? 'opacity-60 cursor-not-allowed pointer-events-none' : 'cursor-pointer'
    ]"
    @click="handleClick"
  >
    <Loader2 v-if="loading" class="w-4 h-4 animate-spin text-current" />
    <slot name="icon" v-if="!loading"></slot>
    <slot></slot>
  </button>
</template>
