<script setup lang="ts">
import { useToast } from '../../composables/useToast';
import { CheckCircle2, AlertTriangle, XCircle, Info, X } from 'lucide-vue-next';

const { toasts, remove } = useToast();
</script>

<template>
  <div class="fixed bottom-5 right-5 z-50 flex flex-col gap-2 max-w-sm w-full pointer-events-none">
    <TransitionGroup
      enter-active-class="transition duration-200 ease-out"
      enter-from-class="transform translate-y-3 opacity-0 scale-95"
      enter-to-class="transform translate-y-0 opacity-100 scale-100"
      leave-active-class="transition duration-150 ease-in"
      leave-from-class="transform opacity-100 scale-100"
      leave-to-class="transform opacity-0 scale-95"
    >
      <div
        v-for="toast in toasts"
        :key="toast.id"
        class="pointer-events-auto flex items-start gap-3 p-3.5 rounded-lg border shadow-elevated bg-white dark:bg-[#0F1E30] text-[#101828] dark:text-[#F8FAFC]"
        :class="{
          'border-[#ABEFC6] dark:border-[#059669]/40': toast.type === 'success',
          'border-[#FEDF89] dark:border-[#D97706]/40': toast.type === 'warning',
          'border-[#FECDCA] dark:border-[#DC2626]/40': toast.type === 'error',
          'border-[#B2DDFF] dark:border-[#2563EB]/40': toast.type === 'info',
        }"
      >
        <div class="shrink-0 mt-0.5">
          <CheckCircle2 v-if="toast.type === 'success'" class="w-4 h-4 text-[#12B76A]" />
          <AlertTriangle v-else-if="toast.type === 'warning'" class="w-4 h-4 text-[#F79009]" />
          <XCircle v-else-if="toast.type === 'error'" class="w-4 h-4 text-[#D92D20]" />
          <Info v-else class="w-4 h-4 text-[#1570EF]" />
        </div>

        <div class="flex-1 min-w-0">
          <div class="text-xs font-semibold leading-tight">{{ toast.title }}</div>
          <div v-if="toast.message" class="text-xs text-[#667085] dark:text-[#94A3B8] mt-0.5 leading-normal">
            {{ toast.message }}
          </div>
        </div>

        <button
          @click="remove(toast.id)"
          class="shrink-0 text-[#98A2B3] hover:text-[#344054] dark:hover:text-[#F8FAFC] transition-colors p-0.5 rounded"
        >
          <X class="w-3.5 h-3.5" />
        </button>
      </div>
    </TransitionGroup>
  </div>
</template>
