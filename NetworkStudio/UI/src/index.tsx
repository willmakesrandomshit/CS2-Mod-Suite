import { PortfolioHelp, useRememberedPreference } from "./portfolio-ux";
import React, { useEffect } from 'react';
import { FloatingButton, Tooltip } from 'cs2/ui';
import { useValue, trigger, bindValue } from 'cs2/bindings';
import iconSrc from './assets/network-studio-icon.svg';
import './style.scss';

const isOpen$ = bindValue<boolean>('NetworkStudio', 'isOpen', false);
const selectedEdgeId$ = bindValue<number>('NetworkStudio', 'selectedEdgeId', 0);
const furnitureCount$ = bindValue<number>('NetworkStudio', 'furnitureCount', 0);
const furnitureJson$ = bindValue<string>('NetworkStudio', 'furnitureJson', '[]');

const portfolioBindings = { "isOpen": isOpen$, "selectedEdgeId": selectedEdgeId$, "furnitureCount": furnitureCount$ };
export const NetworkStudioToolbarButton: React.FC = () => {
  const isOpen = useValue(isOpen$);

  const toggle = () => {
    trigger('NetworkStudio', 'setOpen', !isOpen);
  };

  return (
    <Tooltip tooltip="Network Studio — Experimental road-furniture inspector">
      <FloatingButton
        src={iconSrc}
        selected={isOpen}
        onSelect={toggle}
      />
    </Tooltip>
  );
};

interface FurnitureItem {
  index: number;
  name: string;
  category: string;
  offset: number;
  onAsphalt: boolean;
}

export const NetworkStudioPanel: React.FC = () => {
  const isOpen = useValue(isOpen$);
  const selectedEdgeId = useValue(selectedEdgeId$);
  const furnitureJson = useValue(furnitureJson$);

  let items: FurnitureItem[] = [];
  try {
    items = JSON.parse(furnitureJson || '[]');
  } catch (e) {
    items = [];
  }

  const close = () => {
    trigger('NetworkStudio', 'setOpen', false);
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
    <div className="ns-panel" data-portfolio-panel>
      <div className="ns-header">
        <h2>Network Studio (Prototype)</h2>
        <button className="ns-close-btn" onClick={close} aria-label="Close panel">×</button>
      </div>

      <PortfolioHelp runtimeGroup={"Portfolio.NetworkStudio"} name={"Network Studio"} version={"0.1.0"} steps={["Select a road segment.", "Inspect the detected furniture and distance from the road centre.", "Clear selection or close the panel when finished."]} note={"This candidate is inspection-only. Furniture relocation is withheld pending recovery and reload verification."} bindings={portfolioBindings} />
      <div className="ns-content">
        <div className="ns-empty-state">
          <p><strong>Inspection only in this local candidate.</strong></p>
          <p>Furniture relocation is withheld because reliable reset and world-reload behavior have not passed runtime verification.</p>
        </div>
        {selectedEdgeId === 0 ? (
          <div className="ns-empty-state">
            <p><strong>No road selected.</strong></p>
            <p>Click a road to inspect detected furniture. Press Esc or right-click to clear the selection.</p>
          </div>
        ) : items.length === 0 ? (
          <div className="ns-empty-state">
            <p>No attached props or signs found on this road segment.</p>
          </div>
        ) : (
          <div className="ns-furniture-list">
            <div style={{ fontSize: '12px', color: '#94a3b8', marginBottom: '4px' }}>
              Selected road ({items.length} detected items)
            </div>
            {items.map((item) => (
              <div
                key={item.index}
                className="ns-item-card"
              >
                <div className="ns-item-header">
                  <span className="ns-item-title">{item.name}</span>
                  <span className="ns-item-tag">{item.category}</span>
                </div>
                <div style={{ fontSize: '11px', color: '#cbd5e1' }}>
                  Distance from road centre: {item.offset.toFixed(1)} m
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
};

export default function registerMod(moduleRegistry: any) {
  moduleRegistry.append('GameTopLeft', NetworkStudioToolbarButton);
  moduleRegistry.append('GameTopRight', NetworkStudioPanel);
}
