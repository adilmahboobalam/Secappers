<script setup lang="ts">
import { computed } from 'vue';
import { Loader2, CheckCircle2, ShieldCheck, Download, HardDrive } from 'lucide-vue-next';

interface Props {
  status: 'Downloading' | 'Verifying' | 'Installing' | 'Ready' | 'Idle' | 'Checking' | 'Available' | 'Error';
  progress: number;
}

const props = withDefaults(defineProps<Props>(), {
  progress: 0,
});

const steps = [
  { id: 'Downloading', label: 'Downloading Payload', icon: Download },
  { id: 'Verifying', label: 'Verifying SHA-256', icon: ShieldCheck },
  { id: 'Installing', label: 'Installing In-Place', icon: HardDrive },
  { id: 'Ready', label: 'Ready to Restart', icon: CheckCircle2 },
];

const currentStepIndex = computed(() => {
  switch (props.status) {
    case 'Downloading':
      return 0;
    case 'Verifying':
      return 1;
    case 'Installing':
      return 2;
    case 'Ready':
      return 3;
    default:
      return 0;
  }
});
</script>

<template>
  <div class="space-y-4">
    <!-- Progress Bar -->
    <div class="space-y-1.5">
      <div class="flex justify-between items-center text-xs">
        <span class="font-semibold text-[#101828] dark:text-[#F8FAFC]">
          {{ steps[currentStepIndex]?.label || 'Processing Update' }}
        </span>
        <span class="font-mono text-[#667085] dark:text-[#94A3B8]">
          {{ progress }}%
        </span>
      </div>

      <div class="w-full h-2 rounded-full bg-[#E4E7EC] dark:bg-[#1E293B] overflow-hidden">
        <div
          class="h-full bg-[#122D55] dark:bg-[#3B82F6] transition-all duration-300 rounded-full"
          :style="{ width: `${Math.max(5, progress)}%` }"
        ></div>
      </div>
    </div>

    <!-- Step Indicators -->
    <div class="grid grid-cols-4 gap-2 pt-2">
      <div
        v-for="(step, index) in steps"
        :key="step.id"
        :class="[
          'p-2.5 rounded-lg border text-center transition-all',
          index < currentStepIndex
            ? 'bg-[#ECFDF3] dark:bg-[#064E3B]/20 border-[#ABEFC6] text-[#027A48] dark:text-[#34D399]'
            : index === currentStepIndex
            ? 'bg-[#EFF8FF] dark:bg-[#1E3A8A]/30 border-[#B2DDFF] text-[#175CD3] dark:text-[#60A5FA] font-semibold'
            : 'bg-[#F8FAFC] dark:bg-[#06152A] border-[#EAECF0] dark:border-[#1E293B] text-[#98A2B3]'
        ]"
      >
        <div class="flex justify-center mb-1">
          <Loader2 v-if="index === currentStepIndex && status !== 'Ready'" class="w-4 h-4 animate-spin" />
          <component v-else :is="step.icon" class="w-4 h-4" />
        </div>
        <div class="text-[10px] truncate">{{ step.label }}</div>
      </div>
    </div>
  </div>
</template>
