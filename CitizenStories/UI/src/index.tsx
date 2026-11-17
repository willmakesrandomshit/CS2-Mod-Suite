import React, { useState, useEffect } from 'react';
import type { ModRegistrar } from 'cs2/modding';
import { bindValue, useValue } from 'cs2/api';
import './style.scss';

const hasStory$ = bindValue<boolean>('citizenStories', 'hasStory', false);
const headline$ = bindValue<string>('citizenStories', 'headline', '');
const story$ = bindValue<string>('citizenStories', 'story', '');

// Shown while a citizen is selected. Closing hides the card until a different citizen
// is selected (the headline changes). Read-only: nothing here changes the city.
const StoryCard: React.FC = () => {
  const hasStory = useValue(hasStory$);
  const headline = useValue(headline$);
  const story = useValue(story$);
  const [dismissed, setDismissed] = useState<string>('');

  useEffect(() => {
    if (!hasStory) setDismissed('');
  }, [hasStory]);

  if (!hasStory || dismissed === headline) return null;
  return (
    <div className="cs-card">
      <div className="cs-head">
        <span className="cs-title">{headline}</span>
        <button className="cs-close" onClick={() => setDismissed(headline)} aria-label="Close story">×</button>
      </div>
      <p className="cs-body">{story}</p>
      <div className="cs-foot">Built from the game's data about this citizen. Names are generated.</div>
    </div>
  );
};

const register: ModRegistrar = (moduleRegistry) => {
  moduleRegistry.append('GameTopRight', StoryCard);
};

export default register;
