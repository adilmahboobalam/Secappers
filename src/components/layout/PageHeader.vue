<script setup lang="ts">
interface Props {
  title: string;
  subtitle?: string;
}

defineProps<Props>();
</script>

<template>
  <div class="flex flex-col sm:flex-row sm:items-center justify-between pb-6 mb-6 border-b border-[#E4E7EC] dark:border-[#1E293B] gap-4">
    <div>
      <h1 class="text-2xl sm:text-[28px] font-bold tracking-tight text-[#101828] dark:text-[#F8FAFC]">
        {{ title }}
      </h1>
      <p v-if="subtitle" class="text-sm text-[#667085] dark:text-[#94A3B8] mt-1 leading-normal">
        {{ subtitle }}
      </p>
    </div>

    <div v-if="$slots.actions" class="flex items-center gap-2.5 shrink-0">
      <slot name="actions"></slot>
    </div>
  </div>
</template>
