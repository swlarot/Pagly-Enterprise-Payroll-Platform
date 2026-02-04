/// <reference types="vite/client" />

declare module '*.jsx' {
  import type { ComponentType } from 'react';
  const component: ComponentType;
  export default component;
}
