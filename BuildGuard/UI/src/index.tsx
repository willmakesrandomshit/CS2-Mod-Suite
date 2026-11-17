import React from 'react';
import type { ModRegistrar } from 'cs2/modding';
import { bindValue, useValue } from 'cs2/api';
import './style.scss';

const active$ = bindValue<boolean>('buildGuard', 'active', false);
const attention$ = bindValue<boolean>('buildGuard', 'attention', false);
const text$ = bindValue<string>('buildGuard', 'text', '');

// Shown only while the game's own edit tool is previewing a change. Read-only:
// it does not block, confirm or alter the edit.
const ImpactCard: React.FC = () => {
  const active = useValue(active$);
  const attention = useValue(attention$);
  const text = useValue(text$);
  if (!active) return null;
  return (
    <div className={attention ? 'bg-card bg-warn' : 'bg-card'}>
      <div className="bg-title">{attention ? 'This edit affects buildings' : 'Edit preview'}</div>
      <p className="bg-body">{text}</p>
      <div className="bg-foot">Summary of the game's own preview. Zoning, pedestrian paths and other mods' data are not examined yet.</div>
    </div>
  );
};

const register: ModRegistrar = (moduleRegistry) => {
  moduleRegistry.append('GameTopRight', ImpactCard);
};

export default register;
