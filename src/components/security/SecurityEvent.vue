<script setup lang="ts">
import { computed } from 'vue';
import type { SecurityEvent } from '../../types';
import { CheckCircle2, AlertTriangle, AlertOctagon, Info, Shield, HardDrive, Terminal } from 'lucide-vue-next';

interface Props {
  event: SecurityEvent;
}

const props = defineProps<Props>();

const formattedTime = computed(() => {
  try {
    const d = new Date(props.event.createdAt);
    return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  } catch {
    return '00:00';
  }
});

const formattedDate = computed(() => {
  try {
    const d = new Date(props.event.createdAt);
    return d.toLocaleDateString([], { month: 'short', day: 'numeric', year: 'numeric' });
  } catch {
    return '';
  }
});

const severityConfig = computed(() => {
  switch (props.event.severity) {
    case 'Critical':
      return {
        icon: AlertOctagon,
        iconClass: 'text-[#C5202B]',
        bgClass: 'bg-[#FEF3F2] dark:bg-[#7F1D1D]/30 border-[#C5202B]/30',
        badge: 'Critical',
      };
    case 'High':
      return {
        icon: AlertTriangle,
        iconClass: 'text-[#D92D20]',
        bgClass: 'bg-[#FEF3F2] dark:bg-[#7F1D1D]/20 border-[#FECDCA] dark:border-[#DC2626]/30',
        badge: 'High',
      };
    case 'Warning':
      return {
        icon: AlertTriangle,
        iconClass: 'text-[#F79009]',
        bgClass: 'bg-[#FFFAEB] dark:bg-[#78350F]/20 border-[#FEDF89] dark:border-[#D97706]/30',
        badge: 'Warning',
      };
    case 'Info':
    default:
      return {
        icon: CheckCircle2,
        iconClass: 'text-[#12B76A]',
        bgClass: 'bg-[#F8FAFC] dark:bg-[#0F1E30] border-[#E4E7EC] dark:border-[#1E293B]',
        badge: 'Verified',
      };
  }
});
</script>

<template>
  <div class="flex items-start gap-4 py-3.5 group">
    <!-- Timestamp column -->
    <div class="w-16 pt-0.5 text-right shrink-0">
      <div class="text-xs font-semibold text-[#101828] dark:text-[#F8FAFC] font-mono leading-tight">
        {{ formattedTime }}
      </div>
      <div class="text-[10px] text-[#667085] dark:text-[#94A3B8]">
        {{ formattedDate }}
      </div>
    </div>

    <!-- Timeline node / icon -->
    <div
      :class="[
        'w-8 h-8 rounded-full flex items-center justify-center shrink-0 border shadow-xs transition-transform group-hover:scale-105',
        severityConfig.bgClass
      ]"
    >
      <component :is="severityConfig.icon" :class="['w-4 h-4', severityConfig.iconClass]" />
    </div>

    <!-- Event details card -->
    <div
      class="flex-1 p-3.5 rounded-lg border bg-white dark:bg-[#0F1E30] border-[#E4E7EC] dark:border-[#1E293B] shadow-xs group-hover:border-[#CBD5E1] dark:group-hover:border-[#334155] transition-colors"
    >
      <div class="flex flex-wrap items-center justify-between gap-2 mb-1.5">
        <div class="flex items-center gap-2">
          <span class="text-xs font-bold text-[#101828] dark:text-[#F8FAFC]">
            {{ event.eventType }}
          </span>
          <span
            v-if="event.folderName"
            class="inline-flex items-center gap-1 text-[11px] font-medium text-[#122D55] dark:text-[#93C5FD] bg-[#122D55]/5 dark:bg-[#122D55]/20 px-2 py-0.5 rounded"
          >
            <HardDrive class="w-3 h-3" />
            {{ event.folderName }}
          </span>
        </div>

        <span
          :class="[
            'text-[10px] font-semibold px-2 py-0.5 rounded-full border',
            event.severity === 'Critical'
              ? 'bg-[#FEF3F2] text-[#B42318] border-[#FECDCA]'
              : event.severity === 'Warning'
              ? 'bg-[#FFFAEB] text-[#B54708] border-[#FEDF89]'
              : 'bg-[#ECFDF3] text-[#027A48] border-[#ABEFC6]'
          ]"
        >
          {{ event.actionTaken || severityConfig.badge }}
        </span>
      </div>

      <p class="text-xs text-[#475467] dark:text-[#94A3B8] leading-relaxed">
        {{ event.description }}
      </p>

      <div
        v-if="event.processName"
        class="mt-2 pt-2 border-t border-[#F2F4F7] dark:border-[#1E293B] flex items-center gap-2 text-[11px] text-[#667085] dark:text-[#94A3B8] font-mono"
      >
        <Terminal class="w-3 h-3 text-[#98A2B3]" />
        <span>Process: {{ event.processName }}</span>
        <span v-if="event.processId" class="text-[#98A2B3]">(PID: {{ event.processId }})</span>
      </div>
    </div>
  </div>
</template>
