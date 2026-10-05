import { defineStore } from 'pinia';
import { ref, computed } from 'vue';
import { nativeBridge } from '../services/nativeBridge';
import type { SecurityEvent, EventSeverity } from '../types';

export const useEventStore = defineStore('events', () => {
  const events = ref<SecurityEvent[]>([]);
  const loading = ref(false);
  const error = ref<string | null>(null);
  const filterSeverity = ref<string>('All');
  const searchQuery = ref<string>('');

  const todayEventsCount = computed(() => {
    const today = new Date().toISOString().slice(0, 10);
    return events.value.filter((e) => e.createdAt.startsWith(today)).length;
  });

  const recentEvents = computed(() => {
    return [...events.value].slice(0, 5);
  });

  const filteredEvents = computed(() => {
    return events.value.filter((event) => {
      const matchesSeverity = filterSeverity.value === 'All' || event.severity === filterSeverity.value;
      const q = searchQuery.value.trim().toLowerCase();
      const matchesSearch =
        !q ||
        event.description.toLowerCase().includes(q) ||
        event.eventType.toLowerCase().includes(q) ||
        (event.folderName && event.folderName.toLowerCase().includes(q)) ||
        (event.processName && event.processName.toLowerCase().includes(q));

      return matchesSeverity && matchesSearch;
    });
  });

  async function refreshEvents(limit: number = 200) {
    loading.value = true;
    error.value = null;
    try {
      const data = await nativeBridge.events.list(limit);
      events.value = data || [];
    } catch (err: any) {
      error.value = err.message || 'Failed to fetch security events';
    } finally {
      loading.value = false;
    }
  }

  async function clearEvents() {
    loading.value = true;
    try {
      await nativeBridge.events.clear();
      events.value = [];
    } catch (err: any) {
      error.value = err.message || 'Failed to clear events';
    } finally {
      loading.value = false;
    }
  }

  async function exportCsv() {
    try {
      const csv = await nativeBridge.events.exportCsv();
      const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.setAttribute('href', url);
      link.setAttribute('download', `SecApper-Security-Events-${new Date().toISOString().slice(0, 10)}.csv`);
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      URL.revokeObjectURL(url);
    } catch (err: any) {
      error.value = err.message || 'Failed to export CSV';
    }
  }

  return {
    events,
    loading,
    error,
    filterSeverity,
    searchQuery,
    todayEventsCount,
    recentEvents,
    filteredEvents,
    refreshEvents,
    clearEvents,
    exportCsv,
  };
});
