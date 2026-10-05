<script setup lang="ts">
import type { FolderRecord } from '../../types';
import FolderCard from './FolderCard.vue';
import EmptyState from '../common/EmptyState.vue';
import Button from '../common/Button.vue';
import { FolderLock, Plus } from 'lucide-vue-next';

interface Props {
  folders: FolderRecord[];
  emptyTitle?: string;
  emptyDescription?: string;
}

withDefaults(defineProps<Props>(), {
  emptyTitle: 'No folders are currently protected.',
  emptyDescription: 'Protect your sensitive files by locking them with Windows NTFS security permissions.',
});

defineEmits<{
  (e: 'unlock', folder: FolderRecord): void;
  (e: 'lock', folder: FolderRecord): void;
  (e: 'open', folder: FolderRecord): void;
  (e: 'details', folder: FolderRecord): void;
  (e: 'remove', folder: FolderRecord): void;
  (e: 'create'): void;
}>();
</script>

<template>
  <div>
    <div v-if="folders.length > 0" class="space-y-3">
      <FolderCard
        v-for="folder in folders"
        :key="folder.id"
        :folder="folder"
        @unlock="$emit('unlock', folder)"
        @lock="$emit('lock', folder)"
        @open="$emit('open', folder)"
        @details="$emit('details', folder)"
        @remove="$emit('remove', folder)"
      />
    </div>

    <EmptyState
      v-else
      :icon="FolderLock"
      :title="emptyTitle"
      :description="emptyDescription"
    >
      <Button variant="primary" size="md" @click="$emit('create')">
        <template #icon><Plus class="w-4 h-4" /></template>
        Protect Folder
      </Button>
    </EmptyState>
  </div>
</template>
