declare module 'cs2/modding' {
  import type { ComponentType } from 'react';
  export type ModRegistrar = (registry: { append(slot: string, component: ComponentType): void }) => void;
}

declare module 'cs2/api' {
  export interface BindingSubscription { dispose(): void; }
  export interface ValueBinding<T> {
    value: T;
    subscribe(callback: (value: T) => void): BindingSubscription;
  }
  export function bindValue<T>(group: string, name: string, fallback?: T): ValueBinding<T>;
  export function trigger(group: string, name: string, ...args: unknown[]): void;
}

declare module 'cs2/ui' {
  import type { ComponentType, PropsWithChildren } from 'react';
  export const FloatingButton: ComponentType<{ src: string; selected?: boolean; onSelect?: () => void }>;
  export const Tooltip: ComponentType<PropsWithChildren<{ tooltip: string }>>;
}

declare module '*.svg' {
  const source: string;
  export default source;
}
