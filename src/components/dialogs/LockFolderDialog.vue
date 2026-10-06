<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { useFolderStore } from '../../stores/folders';
import { useToast } from '../../composables/useToast';
import { nativeBridge } from '../../services/nativeBridge';
import Button from '../common/Button.vue';
import { Shield, Folder, Eye, EyeOff, Lock, CheckCircle2, AlertCircle, KeyRound, Sparkles } from 'lucide-vue-next';

interface Props {
  isOpen: boolean;
  initialPath?: string;
}

const props = withDefaults(defineProps<Props>(), {
  initialPath: '',
});

const emit = defineEmits<{
  (e: 'close'): void;
  (e: 'locked', folder: any): void;
}>();

const folderStore = useFolderStore();
const toast = useToast();

const folderPath = ref(props.initialPath);
const useMasterPin = ref(true);
const password = ref('');
const confirmPassword = ref('');
const showPassword = ref(false);
const loading = ref(false);
const errorMessage = ref<string | null>(null);

const folderName = computed(() => {
  if (!folderPath.value) return '';
  const parts = folderPath.value.split(/[\/\\]/).filter(Boolean);
  return parts.length > 0 ? parts[parts.length - 1] : folderPath.value;
});

async function handleBrowse() {
  const selected = await folderStore.browseFolder();
  if (selected) {
    folderPath.value = selected;
  }
}

async function handleProtect() {
  errorMessage.value = null;

  if (!folderPath.value.trim()) {
    errorMessage.value = 'Please select a folder to protect.';
    return;
  }

  if (useMasterPin.value) {
    loading.value = true;
    try {
      // Empty password signals the backend to use the configured Master PIN credentials
      const locked = await folderStore.lockFolder(folderPath.value.trim(), '');
      toast.success('Folder Protected', `"${folderName.value}" is now secured with your Master PIN.`);
      emit('locked', locked);
      emit('close');
    } catch (err: any) {
      errorMessage.value = err.message || 'Unable to apply Windows security permissions to this folder.';
    } finally {
      loading.value = false;
    }
    return;
  }

  // Custom password flow
  if (!password.value) {
    errorMessage.value = 'Please enter a password to secure this folder.';
    return;
  }

  if (password.value.length < 4) {
    errorMessage.value = 'Password must be at least 4 characters long.';
    return;
  }

  if (password.value !== confirmPassword.value) {
    errorMessage.value = 'Passwords do not match.';
    return;
  }

  loading.value = true;
  try {
    const locked = await folderStore.lockFolder(folderPath.value.trim(), password.value);
    toast.success('Folder Protected', `"${folderName.value}" is now secured with your custom password.`);
    emit('locked', locked);
    emit('close');
  } catch (err: any) {
    errorMessage.value = err.message || 'Unable to apply Windows security permissions to this folder.';
  } finally {
    loading.value = false;
  }
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
      <!-- Header with Shield -->
      <div class="p-6 pb-4 border-b border-[#E4E7EC] dark:border-[#1E293B] bg-[#F8FAFC] dark:bg-[#091D38]/50">
        <div class="w-12 h-12 rounded-xl bg-[#C5202B]/10 dark:bg-[#C5202B]/20 flex items-center justify-center text-[#C5202B] mb-3">
          <Shield class="w-6 h-6 stroke-[2]" />
        </div>
        <h3 class="text-lg font-bold text-[#101828] dark:text-[#F8FAFC]">
          Protect Folder
        </h3>
        <p class="text-xs text-[#667085] dark:text-[#94A3B8] mt-1 leading-normal">
          SecApper will strip inheritance and apply native Windows NTFS Deny permissions.
        </p>
      </div>

      <!-- Form Body -->
      <div class="p-6 space-y-4">
        <!-- Target Folder Selection -->
        <div>
          <label class="block text-xs font-semibold text-[#344054] dark:text-[#CBD5E1] mb-1.5">
            Folder Path
          </label>
          <div class="flex gap-2">
            <input
              type="text"
              v-model="folderPath"
              placeholder="C:\Users\...\Documents"
              class="flex-1 px-3 py-2 text-xs font-mono bg-white dark:bg-[#06152A] border border-[#D0D5DD] dark:border-[#1E293B] rounded-lg text-[#101828] dark:text-[#F8FAFC] focus:outline-none focus:ring-2 focus:ring-[#122D55]/30"
            />
            <Button variant="secondary" size="md" @click="handleBrowse">
              Browse
            </Button>
          </div>
          <div v-if="folderName" class="mt-1 text-[11px] text-[#667085] dark:text-[#94A3B8] font-medium">
            Folder: <strong class="text-[#101828] dark:text-[#F8FAFC]">{{ folderName }}</strong>
          </div>
        </div>

        <!-- Master PIN Option (Default) -->
        <div v-if="useMasterPin" class="p-3.5 rounded-xl bg-[#F0FDF4] dark:bg-[#064E3B]/20 border border-[#BBF7D0] dark:border-[#059669]/30">
          <div class="flex items-start gap-3">
            <div class="w-8 h-8 rounded-lg bg-[#12B76A]/10 text-[#12B76A] flex items-center justify-center shrink-0 mt-0.5">
              <KeyRound class="w-4 h-4" />
            </div>
            <div class="flex-1 min-w-0">
              <div class="flex items-center gap-2">
                <h4 class="text-xs font-bold text-[#065F46] dark:text-[#34D399]">
                  Secured with Master PIN
                </h4>
                <span class="inline-flex items-center gap-0.5 text-[10px] font-semibold bg-[#12B76A]/15 text-[#047857] dark:text-[#A7F3D0] px-1.5 py-0.5 rounded-full">
                  <Sparkles class="w-2.5 h-2.5" /> Recommended
                </span>
              </div>
              <p class="text-[11px] text-[#047857] dark:text-[#A7F3D0] mt-1 leading-relaxed">
                This folder will be locked directly using your Master PIN. You won't need to create or remember a separate password.
              </p>
              <button
                type="button"
                @click="useMasterPin = false"
                class="mt-2 text-[11px] font-semibold text-[#122D55] dark:text-[#93C5FD] hover:underline cursor-pointer"
              >
                Use a custom password instead →
              </button>
            </div>
          </div>
        </div>

        <!-- Custom Password Inputs (Optional) -->
        <div v-else class="space-y-3 p-3.5 rounded-xl bg-[#F8FAFC] dark:bg-[#06152A] border border-[#E2E8F0] dark:border-[#1E293B]">
          <div class="flex items-center justify-between">
            <label class="block text-xs font-semibold text-[#344054] dark:text-[#CBD5E1]">
              Custom Password
            </label>
            <button
              type="button"
              @click="useMasterPin = true"
              class="text-[11px] font-semibold text-[#12B76A] hover:underline cursor-pointer"
            >
              ← Use Master PIN instead
            </button>
          </div>

          <div class="relative">
            <input
              :type="showPassword ? 'text' : 'password'"
              v-model="password"
              placeholder="Enter custom password"
              class="w-full px-3 py-2 pr-10 text-xs bg-white dark:bg-[#0F1E30] border border-[#D0D5DD] dark:border-[#1E293B] rounded-lg text-[#101828] dark:text-[#F8FAFC] focus:outline-none focus:ring-2 focus:ring-[#122D55]/30"
            />
            <button
              type="button"
              @click="showPassword = !showPassword"
              class="absolute right-3 top-2.5 text-[#98A2B3] hover:text-[#344054] dark:hover:text-[#F8FAFC]"
            >
              <EyeOff v-if="showPassword" class="w-4 h-4" />
              <Eye v-else class="w-4 h-4" />
            </button>
          </div>

          <!-- Confirm Password Field -->
          <div>
            <label class="block text-xs font-semibold text-[#344054] dark:text-[#CBD5E1] mb-1.5">
              Confirm Custom Password
            </label>
            <input
              :type="showPassword ? 'text' : 'password'"
              v-model="confirmPassword"
              placeholder="Confirm custom password"
              class="w-full px-3 py-2 text-xs bg-white dark:bg-[#0F1E30] border border-[#D0D5DD] dark:border-[#1E293B] rounded-lg text-[#101828] dark:text-[#F8FAFC] focus:outline-none focus:ring-2 focus:ring-[#122D55]/30"
              @keyup.enter="handleProtect"
            />
          </div>
        </div>

        <!-- Error Message -->
        <div
          v-if="errorMessage"
          class="p-3 rounded-lg bg-[#FEF3F2] dark:bg-[#7F1D1D]/30 border border-[#FECDCA] dark:border-[#DC2626]/40 flex items-start gap-2 text-xs text-[#B42318] dark:text-[#F87171]"
        >
          <AlertCircle class="w-4 h-4 shrink-0 mt-0.5" />
          <span>{{ errorMessage }}</span>
        </div>
      </div>

      <!-- Action Footer -->
      <div class="px-6 py-4 border-t border-[#E4E7EC] dark:border-[#1E293B] bg-[#F8FAFC] dark:bg-[#091D38]/50 flex items-center justify-end gap-3">
        <Button variant="secondary" size="md" :disabled="loading" @click="emit('close')">
          Cancel
        </Button>
        <Button variant="primary" size="md" :loading="loading" @click="handleProtect">
          <template #icon><Lock class="w-4 h-4" /></template>
          Protect Folder
        </Button>
      </div>
    </div>
  </div>
</template>
