<script setup lang="ts">
import { computed } from 'vue';
import type { ThreatLevel } from '../../types';
import { ShieldCheck, ShieldAlert, AlertTriangle, AlertOctagon } from 'lucide-vue-next';

interface Props {
  level: ThreatLevel;
  showDetails?: boolean;
}

const props = withDefaults(defineProps<Props>(), {
  level: 'LOW',
  showDetails: true,
});

const config = computed(() => {
  switch (props.level) {
    case 'LOW':
      return {
        label: 'LOW THREAT LEVEL',
        description: 'No suspicious process activity or unauthorized filesystem tampering detected.',
        colorClass: 'text-[#12B76A]',
        bgClass: 'bg-[#ECFDF3] dark:bg-[#064E3B]/20 border-[#ABEFC6] dark:border-[#059669]/30',
        badgeBg: 'bg-[#12B76A]',
        icon: ShieldCheck,
      };
    case 'MEDIUM':
      return {
        label: 'MEDIUM THREAT LEVEL',
        description: 'Unusual file operations observed. Threat heuristics actively evaluating process behaviors.',
        colorClass: 'text-[#F79009]',
        bgClass: 'bg-[#FFFAEB] dark:bg-[#78350F]/20 border-[#FEDF89] dark:border-[#D97706]/30',
        badgeBg: 'bg-[#F79009]',
        icon: AlertTriangle,
      };
    case 'HIGH':
      return {
        label: 'HIGH THREAT LEVEL',
        description: 'Rapid modifications or extension changes detected in protected directory.',
        colorClass: 'text-[#D92D20]',
        bgClass: 'bg-[#FEF3F2] dark:bg-[#7F1D1D]/20 border-[#FECDCA] dark:border-[#DC2626]/30',
        badgeBg: 'bg-[#D92D20]',
        icon: ShieldAlert,
      };
    case 'CRITICAL':
    default:
      return {
        label: 'CRITICAL THREAT LEVEL',
        description: 'Mass file alterations identified. Emergency panic lock and process isolation recommended.',
        colorClass: 'text-[#C5202B]',
        bgClass: 'bg-[#FEF3F2] dark:bg-[#7F1D1D]/30 border-[#C5202B]/40',
        badgeBg: 'bg-[#C5202B]',
        icon: AlertOctagon,
      };
  }
});
</script>

<template>
  <div :class="['p-5 rounded-lg border transition-all duration-200', config.bgClass]">
    <div class="flex items-start gap-4">
      <div
        :class="[
          'w-12 h-12 rounded-lg flex items-center justify-center shrink-0 border shadow-sm',
          config.bgClass,
          config.colorClass
        ]"
      >
        <component :is="config.icon" class="w-6 h-6 stroke-[2]" />
      </div>

      <div class="flex-1 min-w-0">
        <div class="flex items-center gap-2 mb-1">
          <span :class="['w-2 h-2 rounded-full', config.badgeBg]"></span>
          <span :class="['text-xs font-bold tracking-wider uppercase', config.colorClass]">
            {{ config.label }}
          </span>
        </div>

        <p v-if="showDetails" class="text-xs text-[#475467] dark:text-[#CBD5E1] leading-relaxed">
          {{ config.description }}
        </p>

        <!-- Threat Bar Indicator -->
        <div class="grid grid-cols-4 gap-1.5 mt-3 pt-3 border-t border-black/5 dark:border-white/5">
          <div
            class="h-1.5 rounded-full transition-all"
            :class="level === 'LOW' || level === 'MEDIUM' || level === 'HIGH' || level === 'CRITICAL' ? 'bg-[#12B76A]' : 'bg-[#E4E7EC] dark:bg-[#1E293B]'"
          ></div>
          <div
            class="h-1.5 rounded-full transition-all"
            :class="level === 'MEDIUM' || level === 'HIGH' || level === 'CRITICAL' ? 'bg-[#F79009]' : 'bg-[#E4E7EC] dark:bg-[#1E293B]'"
          ></div>
          <div
            class="h-1.5 rounded-full transition-all"
            :class="level === 'HIGH' || level === 'CRITICAL' ? 'bg-[#D92D20]' : 'bg-[#E4E7EC] dark:bg-[#1E293B]'"
          ></div>
          <div
            class="h-1.5 rounded-full transition-all"
            :class="level === 'CRITICAL' ? 'bg-[#C5202B] animate-pulse' : 'bg-[#E4E7EC] dark:bg-[#1E293B]'"
          ></div>
        </div>
      </div>
    </div>
  </div>
</template>
