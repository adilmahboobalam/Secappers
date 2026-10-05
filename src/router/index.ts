import { createRouter, createWebHashHistory } from 'vue-router';

const routes = [
  {
    path: '/',
    redirect: '/dashboard',
  },
  {
    path: '/dashboard',
    name: 'Dashboard',
    component: () => import('../views/Dashboard.vue'),
    meta: { title: 'Dashboard', subtitle: 'Monitor and manage your device security.' },
  },
  {
    path: '/folder-locker',
    name: 'FolderLocker',
    component: () => import('../views/FolderLocker.vue'),
    meta: { title: 'Folder Locker', subtitle: 'Protect your folders using Windows security permissions.' },
  },
  {
    path: '/protected-folders',
    name: 'ProtectedFolders',
    component: () => import('../views/ProtectedFolders.vue'),
    meta: { title: 'Protected Folders', subtitle: 'Manage folders currently secured by SecApper.' },
  },
  {
    path: '/ransomware',
    name: 'RansomwareProtection',
    component: () => import('../views/RansomwareProtection.vue'),
    meta: { title: 'Ransomware Protection', subtitle: 'Monitor protected folders for suspicious file activity.' },
  },
  {
    path: '/security-events',
    name: 'SecurityEvents',
    component: () => import('../views/SecurityEvents.vue'),
    meta: { title: 'Security Events', subtitle: 'Review security activity detected by SecApper.' },
  },
  {
    path: '/recovery',
    name: 'Recovery',
    component: () => import('../views/Recovery.vue'),
    meta: { title: 'Recovery Center', subtitle: 'Check and restore SecApper security states.' },
  },
  {
    path: '/updates',
    name: 'Updates',
    component: () => import('../views/Updates.vue'),
    meta: { title: 'Software Updates', subtitle: 'Keep SecApper secure with the latest updates.' },
  },
  {
    path: '/settings',
    name: 'Settings',
    component: () => import('../views/Settings.vue'),
    meta: { title: 'Settings', subtitle: 'Configure application security, privacy, and system preferences.' },
  },
  {
    path: '/about',
    name: 'About',
    component: () => import('../views/About.vue'),
    meta: { title: 'About SecApper', subtitle: 'Commercial-grade Windows security engine.' },
  },
];

const router = createRouter({
  history: createWebHashHistory(),
  routes,
});

export default router;
