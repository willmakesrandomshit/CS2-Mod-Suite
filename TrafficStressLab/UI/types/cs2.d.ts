declare module "cs2/modding" {
  import type { ComponentType } from "react";
  export type ModRegistrar = (registry: { append(slot: string, component: ComponentType): void }) => void;
}
declare module "cs2/api" {
  export interface ValueSubscription<T> { value: T; dispose(): void; }
  export interface ValueBinding<T> { value: T; subscribe(cb: (value: T) => void): ValueSubscription<T>; }
  export function bindValue<T>(group: string, name: string, initial?: T): ValueBinding<T>;
  export function trigger(group: string, name: string, ...args: unknown[]): void;
}
