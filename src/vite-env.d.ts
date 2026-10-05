/// <reference types="vite/client" />

declare module '*.vue' {
  import type { DefineComponent } from 'vue';
  const component: DefineComponent<{}, {}, any>;
  export default component;
}

interface Window {
  chrome?: {
    webview?: {
      postMessage: (message: any) => void;
      addEventListener: (type: string, listener: (event: any) => void) => void;
      removeEventListener: (type: string, listener: (event: any) => void) => void;
    };
  };
  secapper?: import('./types/bridge').SecapperBridge;
}
