<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import PageHeader from '../components/layout/PageHeader.vue';
import ThreatLevelComponent from '../components/security/ThreatLevel.vue';
import SecurityEventComponent from '../components/security/SecurityEvent.vue';
import Button from '../components/common/Button.vue';
import EmptyState from '../components/common/EmptyState.vue';
import ConfirmationDialog from '../components/dialogs/ConfirmationDialog.vue';
import { useRansomwareStore } from '../stores/ransomware';
import { useFolderStore } from '../stores/folders';
import { useEventStore } from '../stores/events';
import { useSecurityStore } from '../stores/security';
import { useToast } from '../composables/useToast';
import {
  ShieldCheck,
  ShieldAlert,
  Activity,
  HardDrive,
  Eye,
  AlertTriangle,
  History,
  Power,
} from 'lucide-vue-next';

const ransomwareStore = useRansomwareStore();
const folderStore = useFolderStore();
const eventStore = useEventStore();
const securityStore = useSecurityStore();
const toast = useToast();

const isToggling = ref(false);
const showPanicConfirmDialog = ref(false);
const isPanicBusy = ref(false);

onMounted(async () => {
  await Promise.allSettled([
    ransomwareStore.refreshStatus(),
    folderStore.refreshFolders(),
    eventStore.refreshEvents(15),
  ]);
});

const isActive = computed(() => ransomwareStore.status.active);
const threatLevel = computed(() => ransomwareStore.status.threatLevel);
const monitoredFoldersCount = computed(() => folderStore.totalCount);
const suspiciousEventsToday = computed(() => ransomwareStore.status.suspiciousEventsToday);

async function toggleMonitoring() {
  isToggling.value = true;
  try {
    const newState = !isActive.value;
    await ransomwareStore.toggleProtection(newState);
    toast.info(
      newState ? 'Ransomware Monitoring Activated' : 'Ransomware Monitoring Paused',
      newState
        ? 'Protected folders are monitored for mass alterations in real time.'
        : 'Heuristic monitoring has been paused.'
    );
  } catch (err: any) {
    toast.error('Failed to change monitoring state', err.message);
  } finally {
    isToggling.value = false;
  }
}

async function handleConfirmPanicLock() {
  isPanicBusy.value = true;
  try {
    const res = await securityStore.panicLock();
    toast.success('Panic Lock Executed', `Secured ${res.lockedCount} folder(s) immediately.`);
    await folderStore.refreshFolders();
    showPanicConfirmDialog.value = false;
  } catch (err: any) {
    toast.error('Panic Lock Failed', err.message || 'Error executing emergency lock.');
  } finally {
    isPanicBusy.value = false;
  }
}
</script>

<template>
  <div class="space-y-6">
    <!-- Header -->
    <PageHeader
      title="Ransomware Protection"
      subtitle="Monitor protected folders for suspicious file activity."
    >
      <template #actions>
        <Button
          :variant="isActive ? 'secondary' : 'primary'"
          size="md"
          :loading="isToggling"
          @click="toggleMonitoring"
        >
          <template #icon><Power class="w-4 h-4" /></template>
          {{ isActive ? 'Pause Monitoring' : 'Enable Monitoring' }}
        </Button>

        <Button
          variant="danger"
          size="md"
          @click="showPanicConfirmDialog = true"
        >
          <template #icon><ShieldAlert class="w-4 h-4" /></template>
          Emergency Panic Lock
        </Button>
      </template>
    </PageHeader>

    <!-- Main Status Banner -->
    <div
      class="sec-card p-6 sm:p-8 bg-white dark:bg-[#0F1E30] flex flex-col md:flex-row md:items-center justify-between gap-6"
    >
      <div class="flex items-start gap-4">
        <div
          :class="[
            'w-12 h-12 rounded-xl flex items-center justify-center shrink-0 border shadow-xs',
            isActive
              ? 'bg-[#ECFDF3] dark:bg-[#064E3B]/30 border-[#ABEFC6] text-[#12B76A]'
              : 'bg-[#F2F4F7] dark:bg-[#1E293B] border-[#EAECF0] text-[#667085]'
          ]"
        >
          <ShieldCheck v-if="isActive" class="w-7 h-7 stroke-[2]" />
          <ShieldAlert v-else class="w-7 h-7 stroke-[2]" />
        </div>

        <div>
          <div class="flex items-center gap-2 mb-1">
            <span
              :class="[
                'w-2 h-2 rounded-full',
                isActive ? 'bg-[#12B76A]' : 'bg-[#667085]'
              ]"
            ></span>
            <span class="text-xs font-bold uppercase tracking-wider text-[#667085] dark:text-[#94A3B8]">
              {{ isActive ? 'ACTIVE' : 'MONITORING PAUSED' }}
            </span>
          </div>

          <h2 class="text-xl font-bold text-[#101828] dark:text-[#F8FAFC]">
            SecApper is monitoring protected folders
          </h2>

          <p class="text-xs text-[#667085] dark:text-[#94A3B8] mt-1 max-w-xl leading-relaxed">
            Multi-factor heuristic algorithms inspect filesystem events for rapid encryption, extension modification, and unauthorized mass deletion.
          </p>
        </div>
      </div>

      <div class="p-4 rounded-lg bg-[#F8FAFC] dark:bg-[#06152A] border border-[#EAECF0] dark:border-[#1E293B] text-xs space-y-1">
        <div class="text-[#667085] dark:text-[#94A3B8] font-medium">Detection Engine</div>
        <div class="font-bold text-[#101828] dark:text-[#F8FAFC]">FileSystemWatcher + Heuristics</div>
        <div class="text-[11px] text-[#12B76A] flex items-center gap-1">
          <Activity class="w-3 h-3" />
          Real-time Process Inspection
        </div>
      </div>
    </div>

    <!-- Threat Level Visual (Section 16) -->
    <ThreatLevelComponent :level="threatLevel" />

    <!-- 4 Stats Cards Grid -->
    <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
      <div class="sec-card p-4 bg-white dark:bg-[#0F1E30]">
        <div class="flex items-center justify-between text-[#667085] dark:text-[#94A3B8] mb-2">
          <span class="text-xs font-medium">Monitored Folders</span>
          <HardDrive class="w-4 h-4 text-[#122D55] dark:text-[#93C5FD]" />
        </div>
        <div class="text-2xl font-bold text-[#101828] dark:text-[#F8FAFC]">
          {{ monitoredFoldersCount }}
        </div>
        <div class="text-[11px] text-[#667085] dark:text-[#94A3B8] mt-1">
          Direct filesystem watchers
        </div>
      </div>

      <div class="sec-card p-4 bg-white dark:bg-[#0F1E30]">
        <div class="flex items-center justify-between text-[#667085] dark:text-[#94A3B8] mb-2">
          <span class="text-xs font-medium">Suspicious Activity</span>
          <AlertTriangle class="w-4 h-4 text-[#F79009]" />
        </div>
        <div class="text-2xl font-bold text-[#101828] dark:text-[#F8FAFC]">
          {{ suspiciousEventsToday }}
        </div>
        <div class="text-[11px] text-[#667085] dark:text-[#94A3B8] mt-1">
          Flags raised today
        </div>
      </div>

      <div class="sec-card p-4 bg-white dark:bg-[#0F1E30]">
        <div class="flex items-center justify-between text-[#667085] dark:text-[#94A3B8] mb-2">
          <span class="text-xs font-medium">Events Today</span>
          <History class="w-4 h-4 text-[#1570EF]" />
        </div>
        <div class="text-2xl font-bold text-[#101828] dark:text-[#F8FAFC]">
          {{ eventStore.todayEventsCount }}
        </div>
        <div class="text-[11px] text-[#667085] dark:text-[#94A3B8] mt-1">
          Audit operations logged
        </div>
      </div>

      <div class="sec-card p-4 bg-white dark:bg-[#0F1E30]">
        <div class="flex items-center justify-between text-[#667085] dark:text-[#94A3B8] mb-2">
          <span class="text-xs font-medium">Current Threat Level</span>
          <ShieldCheck class="w-4 h-4 text-[#12B76A]" />
        </div>
        <div
          class="text-2xl font-bold font-mono"
          :class="threatLevel === 'LOW' ? 'text-[#12B76A]' : threatLevel === 'MEDIUM' ? 'text-[#F79009]' : 'text-[#C5202B]'"
        >
          {{ threatLevel }}
        </div>
        <div class="text-[11px] text-[#667085] dark:text-[#94A3B8] mt-1">
          Heuristic score: 0/100
        </div>
      </div>
    </div>

    <!-- Security Guidance Disclaimer -->
    <div class="p-4 rounded-lg bg-[#F8FAFC] dark:bg-[#06152A] border border-[#EAECF0] dark:border-[#1E293B] text-xs text-[#667085] dark:text-[#94A3B8] flex items-center gap-3">
      <ShieldCheck class="w-5 h-5 text-[#122D55] dark:text-[#93C5FD] shrink-0" />
      <span>
        <strong>SecApper Defense Architecture:</strong> Combining Windows NTFS Deny access controls with real-time heuristic monitoring provides layered filesystem protection. Regular backups and verified folder states are maintained continuously.
      </span>
    </div>

    <!-- Modals -->
    <ConfirmationDialog
      :is-open="showPanicConfirmDialog"
      title="Execute Emergency Panic Lock?"
      message="SecApper will immediately apply Deny permissions to all registered folders to prevent unauthorized access or malware modification."
      confirm-label="Lock All Folders"
      variant="danger"
      :loading="isPanicBusy"
      @close="showPanicConfirmDialog = false"
      @confirm="handleConfirmPanicLock"
    />
  </div>
</template>
