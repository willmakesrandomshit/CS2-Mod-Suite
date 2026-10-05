declare module "cs2/modding" { import type {ComponentType} from "react"; export type ModRegistrar=(r:{append(s:string,c:ComponentType):void})=>void; }
declare module "cs2/api" { export interface S<T>{value:T;dispose():void} export interface B<T>{value:T;subscribe(c:(v:T)=>void):S<T>} export function bindValue<T>(g:string,n:string,f?:T):B<T>; export function trigger(g:string,n:string,...a:unknown[]):void; }
// Narrow declarations for the existing game controls used by this panel.
declare module "cs2/ui" {
  import type { ComponentType, ReactNode } from "react";
  export const FloatingButton: ComponentType<{ src: string; selected?: boolean; onSelect: () => void }>;
  export const Tooltip: ComponentType<{ tooltip: ReactNode; children: ReactNode }>;
}
declare module "*.svg" { const url: string; export default url; }
