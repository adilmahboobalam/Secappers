<script setup lang="ts">
import { ref, onMounted } from 'vue';
import PageHeader from '../components/layout/PageHeader.vue';
import Button from '../components/common/Button.vue';
import EmptyState from '../components/common/EmptyState.vue';
import { nativeBridge } from '../services/nativeBridge';
import { useToast } from '../composables/useToast';
import type { RecoveryReport, RecoveryIssue } from '../types';
import {
  LifeBuoy,
  RefreshCw,
  Database,
  ShieldCheck,
  FolderCheck,
  CheckCircle2,
  AlertTriangle,
  Wrench,
  Unlock,
} from 'lucide-vue-next';

const toast = useToast();

const isChecking = ref(false);
const isRepairing = ref(false);
const activeRepairId = ref<string | null>(null);

const report = ref<RecoveryReport>({
  databaseHealthy: true,
  permissionBackupsAvailable: true,
  foldersConsistent: true,
  recoveryServiceReady: true,
  issues: [],
  totalFoldersChecked: 0,
  checkedAt: new Date().toISOString(),
});

onMounted(async () => {
  await runCheck();
});

async function runCheck() {
  isChecking.value = true;
  try {
    const data = await nativeBridge.recovery.check();
    report.value = data;
    if (data.issues.length === 0) {
      toast.success('Recovery Scan Passed', 'All filesystem descriptors and SQLite databases are completely healthy.');
    } else {
      toast.warning('Issues Detected', `${data.issues.length} security consistency issue(s) found.`);
    }
  } catch (err: any) {
    toast.error('Recovery Check Failed', err.message);
  } finally {
    isChecking.value = false;
  }
}

async function handleRepair(issue: RecoveryIssue) {
  isRepairing.value = true;
  activeRepairId.value = issue.id;
  try {
    const res = await nativeBridge.recovery.repair(issue.id);
    toast.success('Repaired Successfully', res.message || `Protection repaired for "${issue.folderName}".`);
    await runCheck();
  } catch (err: any) {
    toast.error('Repair Failed', err.message);
  } finally {
    isRepairing.value = false;
    activeRepairId.value = null;
  }
}

async function handleRestoreAccess(issue: RecoveryIssue) {
  if (!issue.folderId) return;
  isRepairing.value = true;
  activeRepairId.value = issue.id;
  try {
    const res = await nativeBridge.recovery.restoreAccess(issue.folderId);
    toast.success('Access Restored', res.message || `Full access restored for "${issue.folderName}".`);
    await runCheck();
  } catch (err: any) {
    toast.error('Restore Failed', err.message);
  } finally {
    isRepairing.value = false;
    activeRepairId.value = null;
  }
}
</script>

<template>
  <div class="space-y-6">
    <!-- Header -->
    <PageHeader
      title="Recovery Center"
      subtitle="Check and restore SecApper security states."
    >
      <template #actions>
        <Button
          variant="primary"
          size="md"
          :loading="isChecking"
          @click="runCheck"
        >
          <template #icon><RefreshCw class="w-4 h-4" /></template>
          Run Recovery Check
        </Button>
      </template>
    </PageHeader>

    <!-- Health Indicators Grid (Section 18) -->
    <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
      <!-- Database Health -->
      <div class="sec-card p-5 bg-white dark:bg-[#0F1E30]">
        <div class="flex items-center justify-between mb-3">
          <Database class="w-5 h-5 text-[#122D55] dark:text-[#93C5FD]" />
          <span
            :class="[
              'inline-flex items-center gap-1 text-[11px] font-bold px-2 py-0.5 rounded-full',
              report.databaseHealthy
                ? 'bg-[#ECFDF3] text-[#027A48] dark:bg-[#064E3B]/30 dark:text-[#34D399]'
                : 'bg-[#FEF3F2] text-[#B42318]'
            ]"
          >
            <CheckCircle2 v-if="report.databaseHealthy" class="w-3 h-3" />
            <AlertTriangle v-else class="w-3 h-3" />
            {{ report.databaseHealthy ? 'Healthy' : 'Attention' }}
          </span>
        </div>
        <div class="text-xs text-[#667085] dark:text-[#94A3B8] font-medium">Database</div>
        <div class="text-base font-bold text-[#101828] dark:text-[#F8FAFC] mt-0.5">
          SQLite Persistent Storage
        </div>
        <div class="text-[11px] text-[#667085] dark:text-[#94A3B8] mt-1">
          Schema v1.1 • WAL mode
        </div>
      </div>

      <!-- Permission Backups -->
      <div class="sec-card p-5 bg-white dark:bg-[#0F1E30]">
        <div class="flex items-center justify-between mb-3">
          <ShieldCheck class="w-5 h-5 text-[#122D55] dark:text-[#93C5FD]" />
          <span
            :class="[
              'inline-flex items-center gap-1 text-[11px] font-bold px-2 py-0.5 rounded-full',
              report.permissionBackupsAvailable
                ? 'bg-[#ECFDF3] text-[#027A48] dark:bg-[#064E3B]/30 dark:text-[#34D399]'
                : 'bg-[#FEF3F2] text-[#B42318]'
            ]"
          >
            <CheckCircle2 v-if="report.permissionBackupsAvailable" class="w-3 h-3" />
            <AlertTriangle v-else class="w-3 h-3" />
            {{ report.permissionBackupsAvailable ? 'Available' : 'Missing' }}
          </span>
        </div>
        <div class="text-xs text-[#667085] dark:text-[#94A3B8] font-medium">Permission Backups</div>
        <div class="text-base font-bold text-[#101828] dark:text-[#F8FAFC] mt-0.5">
          SDDL Descriptors
        </div>
        <div class="text-[11px] text-[#667085] dark:text-[#94A3B8] mt-1">
          SHA-256 hash verified
        </div>
      </div>

      <!-- Protected Folders Consistency -->
      <div class="sec-card p-5 bg-white dark:bg-[#0F1E30]">
        <div class="flex items-center justify-between mb-3">
          <FolderCheck class="w-5 h-5 text-[#122D55] dark:text-[#93C5FD]" />
          <span
            :class="[
              'inline-flex items-center gap-1 text-[11px] font-bold px-2 py-0.5 rounded-full',
              report.foldersConsistent
                ? 'bg-[#ECFDF3] text-[#027A48] dark:bg-[#064E3B]/30 dark:text-[#34D399]'
                : 'bg-[#FEF3F2] text-[#B42318]'
            ]"
          >
            <CheckCircle2 v-if="report.foldersConsistent" class="w-3 h-3" />
            <AlertTriangle v-else class="w-3 h-3" />
            {{ report.foldersConsistent ? 'Consistent' : 'Mismatch' }}
          </span>
        </div>
        <div class="text-xs text-[#667085] dark:text-[#94A3B8] font-medium">Protected Folders</div>
        <div class="text-base font-bold text-[#101828] dark:text-[#F8FAFC] mt-0.5">
          Filesystem vs DB State
        </div>
        <div class="text-[11px] text-[#667085] dark:text-[#94A3B8] mt-1">
          {{ report.totalFoldersChecked }} folder(s) validated
        </div>
      </div>

      <!-- Recovery Service -->
      <div class="sec-card p-5 bg-white dark:bg-[#0F1E30]">
        <div class="flex items-center justify-between mb-3">
          <LifeBuoy class="w-5 h-5 text-[#122D55] dark:text-[#93C5FD]" />
          <span
            :class="[
              'inline-flex items-center gap-1 text-[11px] font-bold px-2 py-0.5 rounded-full',
              report.recoveryServiceReady
                ? 'bg-[#ECFDF3] text-[#027A48] dark:bg-[#064E3B]/30 dark:text-[#34D399]'
                : 'bg-[#FEF3F2] text-[#B42318]'
            ]"
          >
            <CheckCircle2 v-if="report.recoveryServiceReady" class="w-3 h-3" />
            <AlertTriangle v-else class="w-3 h-3" />
            {{ report.recoveryServiceReady ? 'Ready' : 'Unavailable' }}
          </span>
        </div>
        <div class="text-xs text-[#667085] dark:text-[#94A3B8] font-medium">Recovery Service</div>
        <div class="text-base font-bold text-[#101828] dark:text-[#F8FAFC] mt-0.5">
          Journaling &amp; Repair
        </div>
        <div class="text-[11px] text-[#667085] dark:text-[#94A3B8] mt-1">
          Automated rollback engine
        </div>
      </div>
    </div>

    <!-- Active Issues or Healthy State -->
    <div v-if="report.issues.length > 0" class="space-y-4">
      <div class="text-xs font-bold uppercase tracking-wider text-[#C5202B] flex items-center gap-1.5">
        <AlertTriangle class="w-4 h-4" />
        <span>Inconsistencies Requiring Attention ({{ report.issues.length }})</span>
      </div>

      <div class="space-y-3">
        <div
          v-for="issue in report.issues"
          :key="issue.id"
          class="sec-card p-5 bg-white dark:bg-[#0F1E30] border border-[#FECDCA] dark:border-[#DC2626]/40 flex flex-col sm:flex-row sm:items-center justify-between gap-4"
        >
          <div class="space-y-1">
            <div class="flex items-center gap-2">
              <span class="text-xs font-bold text-[#B42318] dark:text-[#F87171] uppercase">
                {{ issue.issueType }}
              </span>
              <span class="text-sm font-bold text-[#101828] dark:text-[#F8FAFC]">
                {{ issue.folderName }}
              </span>
            </div>
            <p class="text-xs font-mono text-[#667085] dark:text-[#94A3B8]">
              {{ issue.folderPath }}
            </p>
            <p class="text-xs text-[#475467] dark:text-[#CBD5E1]">
              {{ issue.description }}
            </p>
          </div>

          <div class="flex items-center gap-2 shrink-0">
            <Button
              v-if="issue.canAutoRepair"
              variant="navy"
              size="sm"
              :loading="isRepairing && activeRepairId === issue.id"
              @click="handleRepair(issue)"
            >
              <template #icon><Wrench class="w-3.5 h-3.5" /></template>
              Repair Protection
            </Button>

            <Button
              variant="secondary"
              size="sm"
              :loading="isRepairing && activeRepairId === issue.id"
              @click="handleRestoreAccess(issue)"
            >
              <template #icon><Unlock class="w-3.5 h-3.5" /></template>
              Restore Access
            </Button>
          </div>
        </div>
      </div>
    </div>

    <!-- Clean State Banner -->
    <div
      v-else
      class="sec-card p-8 text-center bg-white dark:bg-[#0F1E30] border border-[#E4E7EC] dark:border-[#1E293B]"
    >
      <div class="w-12 h-12 rounded-full bg-[#ECFDF3] dark:bg-[#064E3B]/30 border border-[#ABEFC6] text-[#027A48] dark:text-[#34D399] flex items-center justify-center mx-auto mb-3">
        <CheckCircle2 class="w-6 h-6 stroke-[2]" />
      </div>

      <h3 class="text-base font-bold text-[#101828] dark:text-[#F8FAFC]">
        Security State Consistent &amp; Verified
      </h3>

      <p class="text-xs text-[#667085] dark:text-[#94A3B8] max-w-md mx-auto mt-1 leading-relaxed">
        All registered folders match filesystem NTFS descriptors. Cryptographic hashes and rollback journals are synchronized.
      </p>
    </div>
  </div>
</template>
