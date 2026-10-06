<script setup lang="ts">
import { ref } from 'vue';
import type { FolderRecord } from '../../types';
import { useFolderStore } from '../../stores/folders';
import { useToast } from '../../composables/useToast';
import Button from '../common/Button.vue';
import logoUrl from '../../assets/branding/secapper-logo.png';
import { Unlock, Eye, EyeOff, AlertCircle } from 'lucide-vue-next';

interface Props {
  folder: FolderRecord | null;
  isOpen: boolean;
}

const props = defineProps<Props>();

const emit = defineEmits<{
  (e: 'close'): void;
  (e: 'unlocked', folder: FolderRecord): void;
}>();

const folderStore = useFolderStore();
const toast = useToast();

const password = ref('');
const showPassword = ref(false);
const loading = ref(false);
const errorMessage = ref<string | null>(null);

async function handleUnlock() {
  if (!props.folder) return;

  if (!password.value) {
    errorMessage.value = 'Please enter your Master PIN or password.';
    return;
  }

  loading.value = true;
  errorMessage.value = null;

  try {
    const unlocked = await folderStore.unlockFolder(props.folder.id, password.value);
    toast.success('Folder Unlocked', `Access restored to "${props.folder.folderName}".`);
    password.value = '';
    emit('unlocked', unlocked || props.folder);
    emit('close');
  } catch (err: any) {
    // Exact instruction: Never reveal sensitive security info
    errorMessage.value = 'Incorrect Master PIN or password. Please try again.';
  } finally {
    loading.value = false;
  }
}
</script>

<template>
  <div
    v-if="isOpen && folder"
    class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-xs select-none"
    @click="emit('close')"
  >
    <div
      class="bg-white dark:bg-[#0F1E30] border border-[#E4E7EC] dark:border-[#1E293B] rounded-xl shadow-modal max-w-sm w-full overflow-hidden text-center animate-scale-in"
      @click.stop
    >
      <!-- Premium Brand Header with Official Logo -->
      <div class="pt-7 pb-3 px-6">
        <div class="flex items-center justify-center mb-4">
          <div class="px-5 py-2.5 rounded-xl bg-white border border-slate-200 shadow-sm flex items-center justify-center">
            <img :src="logoUrl" alt="SecApper — Secure Your World" class="h-8 w-auto max-w-[210px] object-contain" />
          </div>
        </div>

        <h3 class="text-base font-bold text-[#101828] dark:text-[#F8FAFC]">
          Unlock Protected Folder
        </h3>
        <p class="text-sm font-semibold text-[#122D55] dark:text-[#93C5FD] mt-1 truncate">
          {{ folder.folderName }}
        </p>
        <p class="text-[11px] text-[#667085] dark:text-[#94A3B8] font-mono truncate mt-0.5" :title="folder.folderPath">
          {{ folder.folderPath }}
        </p>
      </div>

      <!-- Password Input Form -->
      <div class="px-6 py-4 space-y-3">
        <div class="text-left">
          <label class="block text-xs font-semibold text-[#344054] dark:text-[#CBD5E1] mb-1.5">
            Enter Master PIN or Password
          </label>
          <div class="relative">
            <input
              :type="showPassword ? 'text' : 'password'"
              v-model="password"
              placeholder="Enter Master PIN or password"
              autofocus
              class="w-full px-3 py-2.5 pr-10 text-sm bg-white dark:bg-[#06152A] border border-[#D0D5DD] dark:border-[#1E293B] rounded-lg text-[#101828] dark:text-[#F8FAFC] focus:outline-none focus:ring-2 focus:ring-[#122D55]/30 tracking-widest"
              @keyup.enter="handleUnlock"
            />
            <button
              type="button"
              @click="showPassword = !showPassword"
              class="absolute right-3 top-3 text-[#98A2B3] hover:text-[#344054] dark:hover:text-[#F8FAFC]"
            >
              <EyeOff v-if="showPassword" class="w-4 h-4" />
              <Eye v-else class="w-4 h-4" />
            </button>
          </div>
        </div>

        <!-- Error Feedback -->
        <div
          v-if="errorMessage"
          class="p-2.5 rounded-lg bg-[#FEF3F2] dark:bg-[#7F1D1D]/30 border border-[#FECDCA] dark:border-[#DC2626]/40 flex items-center justify-center gap-1.5 text-xs text-[#B42318] dark:text-[#F87171] font-medium"
        >
          <AlertCircle class="w-3.5 h-3.5 shrink-0" />
          <span>{{ errorMessage }}</span>
        </div>
      </div>

      <!-- Dialog Buttons -->
      <div class="px-6 py-4 bg-[#F8FAFC] dark:bg-[#091D38]/50 border-t border-[#E4E7EC] dark:border-[#1E293B] flex items-center justify-end gap-2.5">
        <Button variant="secondary" size="md" :disabled="loading" @click="emit('close')">
          Cancel
        </Button>
        <Button variant="navy" size="md" :loading="loading" @click="handleUnlock">
          <template #icon><Unlock class="w-4 h-4" /></template>
          Unlock
        </Button>
      </div>
    </div>
  </div>
</template>
