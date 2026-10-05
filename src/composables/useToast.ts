import { ref } from 'vue';

export interface ToastItem {
  id: string;
  type: 'success' | 'warning' | 'error' | 'info';
  title: string;
  message?: string;
  duration?: number;
}

const toasts = ref<ToastItem[]>([]);

export function useToast() {
  function show(toast: Omit<ToastItem, 'id'>) {
    const id = 'toast_' + Math.random().toString(36).substring(2, 9);
    const item: ToastItem = {
      id,
      duration: 3500,
      ...toast,
    };
    toasts.value.push(item);

    if (item.duration && item.duration > 0) {
      setTimeout(() => {
        remove(id);
      }, item.duration);
    }
    return id;
  }

  function success(title: string, message?: string) {
    return show({ type: 'success', title, message });
  }

  function error(title: string, message?: string) {
    return show({ type: 'error', title, message, duration: 5000 });
  }

  function warning(title: string, message?: string) {
    return show({ type: 'warning', title, message });
  }

  function info(title: string, message?: string) {
    return show({ type: 'info', title, message });
  }

  function remove(id: string) {
    toasts.value = toasts.value.filter((t) => t.id !== id);
  }

  return {
    toasts,
    show,
    success,
    error,
    warning,
    info,
    remove,
  };
}
