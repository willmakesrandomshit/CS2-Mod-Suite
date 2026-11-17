import React from 'react';
import type { ModRegistrar } from 'cs2/modding';
import { bindValue, trigger, useValue } from 'cs2/api';
import './style.scss';

const active$ = bindValue<boolean>('autoDirector', 'active', false);
const shot$ = bindValue<string>('autoDirector', 'shot', '');

const DirectorButton: React.FC = () => {
  const active = useValue(active$);
  const shot = useValue(shot$);
  return (
    <div className="ad-wrap">
      <button
        className={active ? 'ad-btn ad-on' : 'ad-btn'}
        onClick={() => trigger('autoDirector', 'toggle')}
        title="Auto Director: the camera follows moving cars with cinematic cuts. Click again to stop."
      >
        {active ? 'STOP DIRECTOR' : 'AUTO DIRECTOR'}
      </button>
      {active && shot ? <span className="ad-shot">Shot: {shot}</span> : null}
    </div>
  );
};

const register: ModRegistrar = (moduleRegistry) => {
  moduleRegistry.append('GameTopLeft', DirectorButton);
};

export default register;
