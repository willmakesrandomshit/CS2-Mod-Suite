import { PortfolioHelp, useRememberedPreference } from "./portfolio-ux";
import React, { useEffect } from 'react';
import { FloatingButton, Tooltip } from 'cs2/ui';
import { useValue, trigger, bindValue } from 'cs2/bindings';
import iconSrc from './assets/parkify-icon.svg';
import './style.scss';

const isOpen$ = bindValue<boolean>('Parkify', 'isOpen', false);
const accessibleParks$ = bindValue<number>('Parkify', 'accessibleParks', 0);
const pedestrianPaths$ = bindValue<number>('Parkify', 'pedestrianPaths', 0);

const portfolioBindings = { "isOpen": isOpen$, "accessibleParks": accessibleParks$, "pedestrianPaths": pedestrianPaths$ };
export const ParkifyToolbarButton: React.FC = () => {
  const isOpen = useValue(isOpen$);

  const toggle = () => {
    trigger('Parkify', 'setOpen', !isOpen);
  };

  return (
    <Tooltip tooltip="Parkify — Development status and park/path counts">
      <FloatingButton
        src={iconSrc}
        selected={isOpen}
        onSelect={toggle}
      />
    </Tooltip>
  );
};

export const ParkifyPanel: React.FC = () => {
  const isOpen = useValue(isOpen$);
  const accessibleParks = useValue(accessibleParks$);
  const pedestrianPaths = useValue(pedestrianPaths$);

  const close = () => {
    trigger('Parkify', 'setOpen', false);
  };

  useEffect(() => {
    if (!isOpen) return;
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') close();
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [isOpen]);

  if (!isOpen) return null;

  return (
    <div className="pk-panel" data-portfolio-panel>
      <div className="pk-header">
        <h2>Parkify (Development Hold)</h2>
        <button className="pk-close-btn" onClick={close} aria-label="Close panel">×</button>
      </div>

      <PortfolioHelp runtimeGroup={"Portfolio.Parkify"} name={"Parkify"} version={"0.1.0"} steps={["Load a disposable city.", "Inspect the park and pedestrian-lane counts.", "Use the game to inspect actual access and service routing."]} note={"This candidate is read-only. It does not create access or suppress road-access warnings."} bindings={portfolioBindings} />
      <div className="pk-content">
        <div style={{ fontSize: '13px', color: '#cbd5e1', lineHeight: '1.4' }}>
          This local candidate is read-only. It does not create pedestrian access, suppress road-access warnings, or guarantee service routing. The previous global refresh mutation has been disabled pending a proper access model and runtime proof.
        </div>

        <div className="pk-metric-card">
          <span className="pk-label">Parks Found</span>
          <span className="pk-val">{accessibleParks}</span>
        </div>

        <div className="pk-metric-card">
          <span className="pk-label">Pedestrian Network Lanes</span>
          <span className="pk-val">{pedestrianPaths}</span>
        </div>

      </div>
    </div>
  );
};

export default function registerMod(moduleRegistry: any) {
  moduleRegistry.append('GameTopLeft', ParkifyToolbarButton);
  moduleRegistry.append('GameTopRight', ParkifyPanel);
}
