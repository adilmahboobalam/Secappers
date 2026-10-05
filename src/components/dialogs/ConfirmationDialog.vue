<script setup lang="ts">
import { ref } from 'vue';
import Button from '../common/Button.vue';
import { AlertTriangle, Lock, Eye, EyeOff } from 'lucide-vue-next';

interface Props {
  isOpen: boolean;
  title: string;
  message: string;
  confirmLabel?: string;
  cancelLabel?: string;
  variant?: 'danger' | 'primary' | 'navy';
  requirePassword?: boolean;
  loading?: boolean;
}

const props = withDefaults(defineProps<Props>(), {
  confirmLabel: 'Confirm',
  cancelLabel: 'Cancel',
  variant: 'danger',
  requirePassword: false,
  loading: false,
});

const emit = defineEmits<{
  (e: 'close'): void;
  (e: 'confirm', password?: string): void;
}>();

const password = ref('');
const showPassword = ref(false);

function handleConfirm() {
  emit('confirm', props.requirePassword ? password.value : undefined);
}
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
        <div class="flex items-start gap-4">
          <div
            :class="[
              'w-10 h-10 rounded-full flex items-center justify-center shrink-0 border',
              variant === 'danger'
                ? 'bg-[#FEF3F2] dark:bg-[#7F1D1D]/30 border-[#FECDCA] dark:border-[#DC2626]/40 text-[#D92D20]'
                : 'bg-[#122D55]/10 dark:bg-[#122D55]/30 border-[#122D55]/20 text-[#122D55] dark:text-[#93C5FD]'
            ]"
          >
            <AlertTriangle class="w-5 h-5 stroke-[2]" />
          </div>

          <div class="flex-1 min-w-0">
            <h3 class="text-base font-bold text-[#101828] dark:text-[#F8FAFC]">
              {{ title }}
            </h3>
            <p class="text-xs text-[#667085] dark:text-[#94A3B8] mt-1.5 leading-relaxed">
              {{ message }}
            </p>

            <div v-if="requirePassword" class="mt-4">
              <label class="block text-xs font-semibold text-[#344054] dark:text-[#CBD5E1] mb-1">
                Enter password to authorize
              </label>
              <div class="relative">
                <input
                  :type="showPassword ? 'text' : 'password'"
                  v-model="password"
                  placeholder="Enter folder or Master PIN"
                  class="w-full px-3 py-2 pr-10 text-xs bg-white dark:bg-[#06152A] border border-[#D0D5DD] dark:border-[#1E293B] rounded-lg text-[#101828] dark:text-[#F8FAFC] focus:outline-none focus:ring-2 focus:ring-[#122D55]/30"
                  @keyup.enter="handleConfirm"
                />
                <button
                  type="button"
                  @click="showPassword = !showPassword"
                  class="absolute right-3 top-2.5 text-[#98A2B3] hover:text-[#344054] dark:hover:text-[#F8FAFC]"
                >
                  <EyeOff v-if="showPassword" class="w-3.5 h-3.5" />
                  <Eye v-else class="w-3.5 h-3.5" />
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>

      <div class="px-6 py-3.5 bg-[#F8FAFC] dark:bg-[#091D38]/50 border-t border-[#E4E7EC] dark:border-[#1E293B] flex items-center justify-end gap-2.5">
        <Button variant="secondary" size="md" :disabled="loading" @click="emit('close')">
          {{ cancelLabel }}
        </Button>
        <Button :variant="variant" size="md" :loading="loading" @click="handleConfirm">
          {{ confirmLabel }}
        </Button>
      </div>
    </div>
  </div>
</template>
