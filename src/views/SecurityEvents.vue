<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import PageHeader from '../components/layout/PageHeader.vue';
import SecurityEventComponent from '../components/security/SecurityEvent.vue';
import Button from '../components/common/Button.vue';
import EmptyState from '../components/common/EmptyState.vue';
import ConfirmationDialog from '../components/dialogs/ConfirmationDialog.vue';
import { useEventStore } from '../stores/events';
import { useToast } from '../composables/useToast';
import { History, Download, Trash2, Search, Filter } from 'lucide-vue-next';

const eventStore = useEventStore();
const toast = useToast();

const showClearConfirmDialog = ref(false);
const isClearing = ref(false);

onMounted(async () => {
  await eventStore.refreshEvents();
});

// Group filtered events by Date heading (TODAY, YESTERDAY, EARLIER)
const groupedEvents = computed(() => {
  const groups: { [key: string]: typeof eventStore.filteredEvents } = {};
  const todayStr = new Date().toISOString().slice(0, 10);
  const yesterday = new Date(Date.now() - 86400000).toISOString().slice(0, 10);

  eventStore.filteredEvents.forEach((event) => {
    const eventDate = event.createdAt.slice(0, 10);
    let groupKey = 'EARLIER';
    if (eventDate === todayStr) {
      groupKey = 'TODAY';
    } else if (eventDate === yesterday) {
      groupKey = 'YESTERDAY';
    } else {
      try {
        groupKey = new Date(event.createdAt).toLocaleDateString([], {
          month: 'long',
          day: 'numeric',
          year: 'numeric',
        }).toUpperCase();
      } catch {
        groupKey = eventDate;
      }
    }

    if (!groups[groupKey]) {
      groups[groupKey] = [];
    }
    groups[groupKey].push(event);
  });

  return groups;
});

async function handleExportCsv() {
  await eventStore.exportCsv();
  toast.success('Export Complete', 'Security audit log exported to CSV successfully.');
}

async function handleConfirmClear() {
  isClearing.value = true;
  try {
    await eventStore.clearEvents();
    toast.success('Audit Log Cleared', 'Security events have been removed from local database.');
    showClearConfirmDialog.value = false;
  } catch (err: any) {
    toast.error('Clear Failed', err.message);
  } finally {
    isClearing.value = false;
  }
}
</script>

<template>
  <div class="space-y-6">
    <!-- Header -->
    <PageHeader
      title="Security Events"
      subtitle="Audit and review security activity detected by SecApper."
    >
      <template #actions>
        <Button variant="secondary" size="md" @click="handleExportCsv">
          <template #icon><Download class="w-4 h-4" /></template>
          Export CSV
        </Button>

        <Button
          v-if="eventStore.events.length > 0"
          variant="ghost"
          size="md"
          @click="showClearConfirmDialog = true"
        >
          <template #icon><Trash2 class="w-4 h-4" /></template>
          Clear History
        </Button>
      </template>
    </PageHeader>

    <!-- Filters & Search Toolbar -->
    <div class="flex flex-col sm:flex-row items-stretch sm:items-center justify-between gap-3">
      <!-- Severity Filter Buttons -->
      <div class="flex items-center p-1 rounded-lg bg-[#E4E7EC]/60 dark:bg-[#06152A] text-xs font-semibold select-none overflow-x-auto">
        <button
          v-for="sev in (['All', 'Info', 'Warning', 'High', 'Critical'] as const)"
          :key="sev"
          @click="eventStore.filterSeverity = sev"
          :class="[
            'px-3 py-1.5 rounded-md transition-all whitespace-nowrap',
            eventStore.filterSeverity === sev
              ? 'bg-white dark:bg-[#122D55] text-[#101828] dark:text-[#F8FAFC] shadow-xs'
              : 'text-[#667085] dark:text-[#94A3B8] hover:text-[#101828] dark:hover:text-white'
          ]"
        >
          {{ sev }}
        </button>
      </div>

      <!-- Search Input -->
      <div class="relative w-full sm:w-72">
        <Search class="w-4 h-4 absolute left-3 top-2.5 text-[#98A2B3]" />
        <input
          type="text"
          v-model="eventStore.searchQuery"
          placeholder="Filter events, folders, or PIDs..."
          class="w-full pl-9 pr-3 py-1.5 text-xs bg-white dark:bg-[#0F1E30] border border-[#D0D5DD] dark:border-[#1E293B] rounded-lg text-[#101828] dark:text-[#F8FAFC] focus:outline-none focus:ring-2 focus:ring-[#122D55]/30"
        />
      </div>
    </div>

    <!-- Timeline List by Date Groups -->
    <div v-if="eventStore.filteredEvents.length > 0" class="space-y-6">
      <div
        v-for="(eventsInGroup, dateLabel) in groupedEvents"
        :key="dateLabel"
        class="space-y-3"
      >
        <!-- Date Header Banner -->
        <div class="flex items-center gap-2">
          <span class="text-xs font-bold font-mono tracking-wider text-[#122D55] dark:text-[#93C5FD] uppercase">
            {{ dateLabel }}
          </span>
          <div class="flex-1 h-px bg-[#E4E7EC] dark:bg-[#1E293B]"></div>
          <span class="text-[11px] text-[#667085] dark:text-[#94A3B8] font-mono">
            {{ eventsInGroup.length }} event(s)
          </span>
        </div>

        <!-- Timeline Container -->
        <div class="sec-card p-4 sm:p-5 bg-white dark:bg-[#0F1E30] divide-y divide-[#F2F4F7] dark:divide-[#1E293B]">
          <SecurityEventComponent
            v-for="event in eventsInGroup"
            :key="event.id"
            :event="event"
          />
        </div>
      </div>
    </div>

    <!-- Empty State -->
    <EmptyState
      v-else
      :icon="History"
      title="No security events detected."
      description="SecApper logs all folder lock operations, permission verifications, and heuristic detections here."
    />

    <!-- Clear Confirmation Modal -->
    <ConfirmationDialog
      :is-open="showClearConfirmDialog"
      title="Clear Security Event History?"
      message="This will delete logged security operations and heuristic audit records from your local SQLite database. This action cannot be undone."
      confirm-label="Clear All Events"
      variant="danger"
      :loading="isClearing"
      @close="showClearConfirmDialog = false"
      @confirm="handleConfirmClear"
    />
  </div>
</template>
