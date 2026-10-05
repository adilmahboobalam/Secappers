<script setup lang="ts">
import { ref } from 'vue';
import Button from '../common/Button.vue';
import { AlertCircle, ChevronDown, ChevronUp } from 'lucide-vue-next';

interface Props {
  isOpen: boolean;
  title?: string;
  message?: string;
  reason?: string;
  technicalDetails?: string;
}

withDefaults(defineProps<Props>(), {
  title: 'Unable to protect folder',
  message: 'SecApper could not apply the required Windows permissions.',
  reason: 'Administrator permission is required to modify NTFS security descriptors.',
});

const emit = defineEmits<{
  (e: 'close'): void;
  (e: 'retry'): void;
}>();

const showAdvanced = ref(false);
</script>

<template>
  <div
    v-if="isOpen"
    class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-xs select-none"
    @click="emit('close')"
  >
    <div
      class="bg-white dark:bg-[#0F1E30] border border-[#E4E7EC] dark:border-[#1E293B] rounded-xl shadow-modal max-w-md w-full overflow-hidden animate-scale-in"
      @click.stop
    >
      <div class="p-6">
        <div class="flex items-start gap-3.5">
          <div class="w-10 h-10 rounded-full bg-[#FEF3F2] dark:bg-[#7F1D1D]/30 border border-[#FECDCA] dark:border-[#DC2626]/40 flex items-center justify-center text-[#D92D20] shrink-0">
            <AlertCircle class="w-5 h-5 stroke-[2]" />
          </div>

          <div class="flex-1 min-w-0">
            <h3 class="text-base font-bold text-[#101828] dark:text-[#F8FAFC]">
              {{ title }}
            </h3>
            <p class="text-xs text-[#475467] dark:text-[#CBD5E1] mt-1 leading-relaxed">
              {{ message }}
            </p>

            <div v-if="reason" class="mt-3 p-3 rounded-lg bg-[#F8FAFC] dark:bg-[#06152A] border border-[#EAECF0] dark:border-[#1E293B] text-xs">
              <span class="font-semibold text-[#344054] dark:text-[#F8FAFC]">Possible reason:</span>
              <p class="text-[#667085] dark:text-[#94A3B8] mt-0.5">{{ reason }}</p>
            </div>

            <!-- Expandable Advanced Technical Details -->
            <div v-if="technicalDetails" class="mt-3">
              <button
                @click="showAdvanced = !showAdvanced"
                class="inline-flex items-center gap-1 text-[11px] font-semibold text-[#122D55] dark:text-[#93C5FD] hover:underline"
              >
                <span>Advanced Details</span>
                <ChevronUp v-if="showAdvanced" class="w-3.5 h-3.5" />
                <ChevronDown v-else class="w-3.5 h-3.5" />
              </button>

              <div
                v-if="showAdvanced"
                class="mt-2 p-2.5 rounded bg-[#091D38] text-[#93C5FD] font-mono text-[10px] overflow-x-auto max-h-36 leading-normal select-text"
              >
                {{ technicalDetails }}
              </div>
            </div>
          </div>
        </div>
      </div>

      <div class="px-6 py-3.5 bg-[#F8FAFC] dark:bg-[#091D38]/50 border-t border-[#E4E7EC] dark:border-[#1E293B] flex items-center justify-end gap-2.5">
        <Button variant="secondary" size="md" @click="emit('close')">
          Cancel
        </Button>
        <Button variant="primary" size="md" @click="emit('retry')">
          Try Again
        </Button>
      </div>
    </div>
  </div>
</template>
