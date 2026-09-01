import React, { useState, useEffect, useCallback } from 'react';
import { ModRegistrar } from 'cs2/modding';
import { bindValue, trigger } from 'cs2/api';
import { FloatingButton, Tooltip } from 'cs2/ui';
import iconSrc from './assets/event-engine-icon.svg';
import './style.scss';

interface VenueData {
  entityIndex: number;
  name: string;
  prefabName: string;
  typeDescription: string;
  capacity: number;
  currentOccupants: number;
  transitStopsNearby: number;
  parkingSpacesNearby: number;
  posX: number;
  posY: number;
  posZ: number;
}

interface EventData {
  id: number;
  title: string;
  category: number;
  categoryLabel: string;
  categoryIcon: string;
  venueEntityIndex: number;
  venueName: string;
  expectedAttendance: number;
  currentAttendance: number;
  travelingCount: number;
  carsCount: number;
  transitCount: number;
  walkingCount: number;
  taxiCount: number;
  startHour: number;
  startMinute: number;
  durationHours: number;
  minutesUntilEvent: number;
  phase: number;
  phaseLabel: string;
  trafficImpact: string;
  carUsagePercent: number;
  transitUsagePercent: number;
  taxiUsagePercent: number;
}

const isOpenBinding = bindValue<boolean>('EventEngine', 'isOpen', false);
const activeTabBinding = bindValue<string>('EventEngine', 'activeTab', 'overview');
const statusBadgeBinding = bindValue<string>('EventEngine', 'statusBadge', 'No Events');
const activeEventsCountBinding = bindValue<number>('EventEngine', 'activeEventsCount', 0);
const eventsJsonBinding = bindValue<string>('EventEngine', 'eventsJson', '[]');
const venuesJsonBinding = bindValue<string>('EventEngine', 'venuesJson', '[]');
const selectedVenueJsonBinding = bindValue<string>('EventEngine', 'selectedVenueJson', 'null');
const nextEventJsonBinding = bindValue<string>('EventEngine', 'nextEventJson', 'null');
const toastMessageBinding = bindValue<string>('EventEngine', 'toastMessage', '');
const isVenuePickingActiveBinding = bindValue<boolean>('EventEngine', 'isVenuePickingActive', false);
const launcherSelectedBinding = bindValue<boolean>('EventEngine', 'launcherSelected', false);
const scheduleStatusBinding = bindValue<string>('EventEngine', 'scheduleStatus', 'idle');

const parseArrayBinding = <T,>(raw: unknown): T[] => {
  if (Array.isArray(raw)) return raw as T[];
  if (typeof raw !== 'string' || raw.length === 0) return [];
  try {
    const parsed = JSON.parse(raw);
    return Array.isArray(parsed) ? parsed as T[] : [];
  } catch {
    return [];
  }
};

const parseObjectBinding = <T,>(raw: unknown): T | null => {
  if (raw !== null && typeof raw === 'object' && !Array.isArray(raw)) return raw as T;
  if (typeof raw !== 'string' || raw.length === 0) return null;
  try {
    const parsed = JSON.parse(raw);
    return parsed !== null && typeof parsed === 'object' && !Array.isArray(parsed) ? parsed as T : null;
  } catch {
    return null;
  }
};

const asText = (value: unknown, fallback = ''): string =>
  typeof value === 'string' ? value : fallback;

const asNumber = (value: unknown, fallback = 0): number =>
  typeof value === 'number' && Number.isFinite(value) ? value : fallback;

const normalizeVenue = (raw: unknown): VenueData => {
  const venue = raw !== null && typeof raw === 'object' ? raw as Record<string, unknown> : {};
  return {
    entityIndex: asNumber(venue.entityIndex),
    name: asText(venue.name, 'Unnamed Venue'),
    prefabName: asText(venue.prefabName, 'Unknown Prefab'),
    typeDescription: asText(venue.typeDescription, 'Venue'),
    capacity: asNumber(venue.capacity),
    currentOccupants: asNumber(venue.currentOccupants),
    transitStopsNearby: asNumber(venue.transitStopsNearby),
    parkingSpacesNearby: asNumber(venue.parkingSpacesNearby),
    posX: asNumber(venue.posX),
    posY: asNumber(venue.posY),
    posZ: asNumber(venue.posZ),
  };
};

const normalizeEvent = (raw: unknown): EventData => {
  const event = raw !== null && typeof raw === 'object' ? raw as Record<string, unknown> : {};
  return {
    id: asNumber(event.id),
    title: asText(event.title, 'Untitled Event'),
    category: asNumber(event.category),
    categoryLabel: asText(event.categoryLabel, 'Event'),
    categoryIcon: asText(event.categoryIcon, '🎟️'),
    venueEntityIndex: asNumber(event.venueEntityIndex),
    venueName: asText(event.venueName, 'Unknown Venue'),
    expectedAttendance: asNumber(event.expectedAttendance),
    currentAttendance: asNumber(event.currentAttendance),
    travelingCount: asNumber(event.travelingCount),
    carsCount: asNumber(event.carsCount),
    transitCount: asNumber(event.transitCount),
    walkingCount: asNumber(event.walkingCount),
    taxiCount: asNumber(event.taxiCount),
    startHour: asNumber(event.startHour),
    startMinute: asNumber(event.startMinute),
    durationHours: asNumber(event.durationHours, 1),
    minutesUntilEvent: asNumber(event.minutesUntilEvent),
    phase: asNumber(event.phase),
    phaseLabel: asText(event.phaseLabel, 'Scheduled'),
    trafficImpact: asText(event.trafficImpact, 'LOW'),
    carUsagePercent: asNumber(event.carUsagePercent),
    transitUsagePercent: asNumber(event.transitUsagePercent),
    taxiUsagePercent: asNumber(event.taxiUsagePercent),
  };
};

const validTabs = new Set(['overview', 'venues', 'schedule', 'traffic', 'settings']);
const normalizeTab = (value: unknown): string =>
  typeof value === 'string' && validTabs.has(value) ? value : 'overview';

const EVENT_CATEGORIES = [
  { id: 0, label: 'Football Match', icon: '⚽' },
  { id: 1, label: 'Sports Event', icon: '🏟️' },
  { id: 2, label: 'Rock Concert', icon: '🎵' },
  { id: 3, label: 'Festival & Fair', icon: '🎪' },
  { id: 4, label: 'Live Show', icon: '🎤' },
  { id: 5, label: 'Championship', icon: '🏆' },
  { id: 6, label: 'Convention & Expo', icon: '👥' },
  { id: 7, label: 'Custom Event', icon: '🎛️' },
];

export const EventEngineToolbarButton: React.FC = () => {
  const [selected, setSelected] = useState(launcherSelectedBinding.value);

  useEffect(() => {
    const sub = launcherSelectedBinding.subscribe(setSelected);
    return () => sub.dispose();
  }, []);

  const toggle = useCallback(() => {
    trigger('EventEngine', 'setOpen', !selected);
  }, [selected]);

  return (
    <Tooltip tooltip="Event Engine — Stadium & Venue Events">
      <FloatingButton
        src={iconSrc}
        selected={selected}
        onSelect={toggle}
      />
    </Tooltip>
  );
};

export const EventEnginePanel: React.FC = () => {
  const [isOpen, setIsOpen] = useState(isOpenBinding.value);
  const [activeTab, setActiveTab] = useState(normalizeTab(activeTabBinding.value));
  const [statusBadge, setStatusBadge] = useState(statusBadgeBinding.value || 'No Events');
  const [activeEventsCount, setActiveEventsCount] = useState(activeEventsCountBinding.value || 0);
  const [eventsJson, setEventsJson] = useState(eventsJsonBinding.value || '[]');
  const [venuesJson, setVenuesJson] = useState(venuesJsonBinding.value || '[]');
  const [selectedVenueJson, setSelectedVenueJson] = useState(selectedVenueJsonBinding.value || 'null');
  const [nextEventJson, setNextEventJson] = useState(nextEventJsonBinding.value || 'null');
  const [toastMessage, setToastMessage] = useState(toastMessageBinding.value || '');
  const [isVenuePicking, setIsVenuePicking] = useState(isVenuePickingActiveBinding.value || false);
  const [scheduleStatus, setScheduleStatus] = useState(scheduleStatusBinding.value || 'idle');

  // Form State
  const [eventTitle, setEventTitle] = useState('City Championship Match');
  const [selectedCategory, setSelectedCategory] = useState(0);
  const [selectedVenueIndex, setSelectedVenueIndex] = useState<number>(0);
  const [attendance, setAttendance] = useState(15000);
  const [startHour, setStartHour] = useState(19);
  const [durationHours, setDurationHours] = useState(3);
  const [arrivalIntensity, setArrivalIntensity] = useState(1.0);
  const [departureIntensity, setDepartureIntensity] = useState(1.0);
  const [carPercent, setCarPercent] = useState(45);
  const [transitPercent, setTransitPercent] = useState(40);
  const [taxiPercent, setTaxiPercent] = useState(15);

  const [uiToast, setUiToast] = useState<string | null>(null);

  const showToast = (msg: string) => {
    setUiToast(msg);
    setTimeout(() => setUiToast(null), 3000);
  };

  useEffect(() => {
    const s1 = isOpenBinding.subscribe(setIsOpen);
    const s2 = activeTabBinding.subscribe((value) => setActiveTab(normalizeTab(value)));
    const s3 = statusBadgeBinding.subscribe((value) => setStatusBadge(asText(value, 'No Events')));
    const s4 = activeEventsCountBinding.subscribe((value) => setActiveEventsCount(asNumber(value)));
    const s5 = eventsJsonBinding.subscribe((value) => setEventsJson(value ?? '[]'));
    const s6 = venuesJsonBinding.subscribe((value) => setVenuesJson(value ?? '[]'));
    const s7 = selectedVenueJsonBinding.subscribe((value) => setSelectedVenueJson(value ?? 'null'));
    const s8 = nextEventJsonBinding.subscribe((value) => setNextEventJson(value ?? 'null'));
    const s9 = toastMessageBinding.subscribe((value) => setToastMessage(asText(value)));
    const s10 = isVenuePickingActiveBinding.subscribe((value) => setIsVenuePicking(value === true));
    const s11 = scheduleStatusBinding.subscribe((value) => setScheduleStatus(asText(value, 'idle')));

    return () => {
      s1.dispose(); s2.dispose(); s3.dispose(); s4.dispose(); s5.dispose();
      s6.dispose(); s7.dispose(); s8.dispose(); s9.dispose(); s10.dispose(); s11.dispose();
    };
  }, []);

  const close = useCallback(() => {
    trigger('EventEngine', 'setOpen', false);
  }, []);

  useEffect(() => {
    if (!isOpen) return;
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        if (isVenuePicking) trigger('EventEngine', 'cancelVenuePicker');
        else close();
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [isOpen, isVenuePicking, close]);

  // Parse Data
  const events = parseArrayBinding<unknown>(eventsJson).map(normalizeEvent);
  const venues = parseArrayBinding<unknown>(venuesJson).map(normalizeVenue);
  const selectedVenueRaw = parseObjectBinding<unknown>(selectedVenueJson);
  const nextEventRaw = parseObjectBinding<unknown>(nextEventJson);
  const selectedVenue = selectedVenueRaw ? normalizeVenue(selectedVenueRaw) : null;
  const nextEvent = nextEventRaw ? normalizeEvent(nextEventRaw) : null;
  const effectiveVenueIndex = selectedVenueIndex || selectedVenue?.entityIndex || venues[0]?.entityIndex || 0;
  const scheduleVenue = venues.find((venue) => venue.entityIndex === effectiveVenueIndex) || venues[0] || null;

  const cycleScheduleVenue = (direction: number) => {
    if (venues.length < 2) return;
    const current = Math.max(0, venues.findIndex((venue) => venue.entityIndex === effectiveVenueIndex));
    const next = (current + direction + venues.length) % venues.length;
    setSelectedVenueIndex(venues[next].entityIndex);
  };

  const switchTab = (tab: string) => {
    trigger('EventEngine', 'setActiveTab', tab);
  };

  const handleSelectVenue = (idx: number) => {
    trigger('EventEngine', 'selectVenue', idx);
    setSelectedVenueIndex(idx);
    showToast('Selected venue updated');
  };

  const handleScheduleEventHere = (venue: VenueData) => {
    setSelectedVenueIndex(venue.entityIndex);
    setAttendance(Math.round(venue.capacity * 0.85));
    switchTab('schedule');
  };

  const handleScheduleSubmit = () => {
    const payload = {
      title: eventTitle,
      category: selectedCategory,
      venueIdx: selectedVenueIndex || (selectedVenue?.entityIndex ?? 0),
      attendance: attendance,
      startHour: startHour,
      startMinute: startHour === 21 ? 0 : 30,
      duration: durationHours,
      arrivalInt: arrivalIntensity,
      depInt: departureIntensity,
      carPct: carPercent,
      transitPct: transitPercent,
      taxiPct: taxiPercent,
    };
    trigger('EventEngine', 'scheduleEventJson', JSON.stringify(payload));
  };

  const handleCancelEvent = (id: number) => {
    trigger('EventEngine', 'cancelEvent', id);
    showToast('Event cancelled');
  };

  const handlePanToVenue = (idx: number) => {
    trigger('EventEngine', 'panToVenue', idx);
  };

  const handleStartNow = (id: number) => {
    trigger('EventEngine', 'startEventNow', id);
    showToast('Event started LIVE');
  };

  const handleSimulateAttendance = (id: number, amount: number) => {
    trigger('EventEngine', 'simulateAttendance', id, amount);
    showToast(`Simulating ${amount.toLocaleString()} spectator trips`);
  };

  const handleEndEvent = (id: number) => {
    trigger('EventEngine', 'endEvent', id);
    showToast('Event ended — post-event departures dispatched');
  };

  const handleResetAll = () => {
    trigger('EventEngine', 'resetAllEvents');
    showToast('Reset all events');
  };

  if (!isOpen) return null;

  const isLive = nextEvent && nextEvent.phase === 2;
  const isArrivals = nextEvent && nextEvent.phase === 1;

  return (
    <div className="suite-panel event-engine-panel" role="dialog" aria-label="Event Engine Panel">
      {/* HEADER */}
      <div className="suite-header">
        <div className="header-left">
          <img src={iconSrc} alt="Event Engine" className="header-icon" />
          <div>
            <h2 className="header-title">Event Engine</h2>
            <span className="header-subtitle">Stadium &amp; Venue Event Scheduler</span>
          </div>
        </div>
        <div className="header-right">
          <span className={`suite-badge ${isLive ? 'status-live' : isArrivals ? 'status-warning' : 'status-good'}`}>
            {statusBadge}
          </span>
          <button className="suite-close-btn" onClick={close} title="Close Panel">✕</button>
        </div>
      </div>

      {/* TOAST ALERTS */}
      {(uiToast || toastMessage) && (
        <div className="suite-toast">
          <span>✓ {uiToast || toastMessage}</span>
        </div>
      )}

      {/* TAB NAVIGATION */}
      <div className="suite-tabs">
        <button
          className={`tab-btn ${activeTab === 'overview' ? 'active' : ''}`}
          onClick={() => switchTab('overview')}
        >
          Overview {events.length > 0 && <span className="tab-pill">{events.length}</span>}
        </button>
        <button
          className={`tab-btn ${activeTab === 'venues' ? 'active' : ''}`}
          onClick={() => switchTab('venues')}
        >
          Venues {venues.length > 0 && <span className="tab-pill">{venues.length}</span>}
        </button>
        <button
          className={`tab-btn ${activeTab === 'schedule' ? 'active' : ''}`}
          onClick={() => switchTab('schedule')}
        >
          Schedule
        </button>
        <button
          className={`tab-btn ${activeTab === 'traffic' ? 'active' : ''}`}
          onClick={() => switchTab('traffic')}
        >
          Traffic
        </button>
        <button
          className={`tab-btn ${activeTab === 'settings' ? 'active' : ''}`}
          onClick={() => switchTab('settings')}
        >
          Quick Test
        </button>
      </div>

      {/* BODY CONTENT */}
      <div className="suite-body">
        {/* ============================================================ */}
        {/* TAB 1: OVERVIEW */}
        {/* ============================================================ */}
        {activeTab === 'overview' && (
          <div className="tab-pane overview-pane">
            {events.length === 0 ? (
              /* EMPTY STATE */
              <div className="suite-empty-state">
                <div className="empty-icon">🏟️</div>
                <h3 className="empty-title">No Events Scheduled</h3>
                <p className="empty-desc">
                  Create a stadium match, concert, or festival and watch real citizens travel across your city to attend!
                </p>
                <div className="empty-actions-row">
                  <button className="suite-primary-btn" onClick={() => switchTab('venues')}>
                    Select a Venue
                  </button>
                  <button className="suite-secondary-btn" onClick={() => switchTab('schedule')}>
                    Schedule an Event
                  </button>
                </div>
                <div className="how-it-works-card">
                  <div className="card-title">How It Works</div>
                  <div className="steps-list">
                    <div className="step-item"><span className="step-num">1</span> Pick any stadium, arena or concert venue</div>
                    <div className="step-item"><span className="step-num">2</span> Schedule match, concert or championship</div>
                    <div className="step-item"><span className="step-num">3</span> Citizens start travelling 2h before the event</div>
                    <div className="step-item"><span className="step-num">4</span> Watch realistic road traffic &amp; transit surges</div>
                  </div>
                </div>
              </div>
            ) : isLive && nextEvent ? (
              /* LIVE EVENT HERO CARD */
              <div className="live-event-hero">
                <div className="live-badge-row">
                  <span className="live-pulse-dot" />
                  <span className="live-tag">EVENT LIVE</span>
                  <span className="live-category">{nextEvent.categoryIcon} {nextEvent.categoryLabel}</span>
                </div>
                <h3 className="live-title">{nextEvent.title}</h3>
                <div className="live-venue-tag">📍 {nextEvent.venueName}</div>

                <div className="live-stats-grid">
                  <div className="live-stat-card">
                    <span className="stat-label">Measured Attendance</span>
                    <span className="stat-value highlight">{nextEvent.currentAttendance.toLocaleString()}</span>
                    <span className="stat-sub">Target: {nextEvent.expectedAttendance.toLocaleString()}</span>
                  </div>
                  <div className="live-stat-card">
                    <span className="stat-label">Target Progress</span>
                    <span className="stat-value">{Math.round((nextEvent.currentAttendance / nextEvent.expectedAttendance) * 100)}%</span>
                    <span className="stat-sub">Dispatched visitors at venue</span>
                  </div>
                  <div className="live-stat-card">
                    <span className="stat-label">Arrival Requests Outstanding</span>
                    <span className="stat-value">{nextEvent.travelingCount.toLocaleString()}</span>
                    <span className="stat-sub">Requested minus measured arrivals</span>
                  </div>
                  <div className="live-stat-card">
                    <span className="stat-label">Demand Estimate</span>
                    <span className={`stat-value impact-${nextEvent.trafficImpact.toLowerCase()}`}>{nextEvent.trafficImpact}</span>
                    <span className="stat-sub">Based on requested attendance</span>
                  </div>
                </div>

                <div className="live-actions-row">
                  <button className="suite-secondary-btn" onClick={() => handlePanToVenue(nextEvent.venueEntityIndex)}>
                    Locate Venue
                  </button>
                  <button className="suite-secondary-btn" onClick={() => switchTab('traffic')}>
                    Traffic View
                  </button>
                  <button className="suite-danger-btn" onClick={() => handleEndEvent(nextEvent.id)}>
                    End Event &amp; Evacuate
                  </button>
                </div>
              </div>
            ) : nextEvent ? (
              /* NEXT SCHEDULED EVENT HERO CARD */
              <div className="next-event-hero">
                <div className="next-tag-row">
                  <span className="next-tag">NEXT EVENT</span>
                  <span className="next-time">
                    {nextEvent.minutesUntilEvent > 0 ? `Starts in ${nextEvent.minutesUntilEvent}m` : 'Starting Now'}
                  </span>
                </div>
                <h3 className="next-title">{nextEvent.categoryIcon} {nextEvent.title}</h3>
                <div className="next-venue">📍 {nextEvent.venueName} • Starts at {nextEvent.startHour}:{String(nextEvent.startMinute).padStart(2, '0')} ({nextEvent.durationHours}h)</div>

                <div className="next-metrics-row">
                  <div className="metric-pill">
                    <span>Expected Attendance:</span> <strong>{nextEvent.expectedAttendance.toLocaleString()}</strong>
                  </div>
                  <div className="metric-pill">
                    <span>Traffic Impact:</span> <strong className={`impact-${nextEvent.trafficImpact.toLowerCase()}`}>{nextEvent.trafficImpact}</strong>
                  </div>
                  <div className="metric-pill">
                    <span>Outstanding Requests:</span> <strong>{nextEvent.travelingCount.toLocaleString()}</strong>
                  </div>
                </div>

                <div className="next-actions-row">
                  <button className="suite-secondary-btn compact" onClick={() => handlePanToVenue(nextEvent.venueEntityIndex)}>
                    View Venue
                  </button>
                  <button className="suite-primary-btn compact" onClick={() => handleStartNow(nextEvent.id)}>
                    Start Event Now
                  </button>
                  <button className="suite-secondary-btn compact" onClick={() => handleCancelEvent(nextEvent.id)}>
                    Cancel Event
                  </button>
                </div>
              </div>
            ) : null}

            {/* UPCOMING EVENTS LIST */}
            {events.length > 0 && (
              <div className="upcoming-events-section">
                <div className="section-title">All Scheduled Events ({events.length})</div>
                <div className="events-list">
                  {events.map((ev) => (
                    <div key={ev.id} className="event-list-item">
                      <div className="item-icon">{ev.categoryIcon}</div>
                      <div className="item-info">
                        <div className="item-title">{ev.title}</div>
                        <div className="item-details">📍 {ev.venueName} • {ev.startHour}:{String(ev.startMinute).padStart(2, '0')} • {ev.expectedAttendance.toLocaleString()} Target</div>
                      </div>
                      <div className="item-status">
                        <span className={`phase-badge phase-${ev.phaseLabel.toLowerCase()}`}>{ev.phaseLabel}</span>
                        <button className="item-del-btn" onClick={() => handleCancelEvent(ev.id)} title="Cancel Event">✕</button>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </div>
        )}

        {/* ============================================================ */}
        {/* TAB 2: VENUES */}
        {/* ============================================================ */}
        {activeTab === 'venues' && (
          <div className="tab-pane venues-pane">
            <div className="venues-header-row">
              <div>
                <span className="section-title">Compatible Stadiums &amp; Venues</span>
                <span className="section-subtitle">{venues.length} venues found across city</span>
              </div>
              <button
                className={`suite-secondary-btn compact ${isVenuePicking ? 'active-pulse' : ''}`}
                onClick={() => trigger('EventEngine', 'toggleVenuePicker')}
              >
                {isVenuePicking ? 'Click Building on Map...' : 'Pick on Map'}
              </button>
            </div>

            {venues.length === 0 ? (
              <div className="suite-empty-state">
                <div className="empty-icon">🏟️</div>
                <h4 className="empty-title">No Venues Found</h4>
                <p className="empty-desc">
                  Build a stadium, sports ground, arena, concert hall, monument or large entertainment venue in your city!
                </p>
              </div>
            ) : (
              <div className="venues-grid">
                {venues.map((v) => {
                  const isSel = selectedVenue?.entityIndex === v.entityIndex;
                  return (
                    <div key={v.entityIndex} className={`venue-card ${isSel ? 'selected' : ''}`}>
                      <div className="venue-card-header">
                        <div>
                          <div className="venue-name">{v.name}</div>
                          <div className="venue-type">{v.typeDescription}</div>
                        </div>
                        <span className="venue-cap-badge">{v.capacity.toLocaleString()} suggested target</span>
                      </div>

                      <div className="venue-specs-row">
                        <div className="spec-item">
                          <span>Occupants:</span> <strong>{v.currentOccupants}</strong>
                        </div>
                        <div className="spec-item">
                          <span>Transit Stops:</span> <strong>{v.transitStopsNearby}</strong>
                        </div>
                        <div className="spec-item">
                          <span>Estimated Parking:</span> <strong>{v.parkingSpacesNearby}</strong>
                        </div>
                      </div>

                      <div className="venue-card-actions">
                        <button className="suite-secondary-btn compact" onClick={() => handlePanToVenue(v.entityIndex)}>
                          View
                        </button>
                        <button
                          className={`suite-primary-btn compact ${isSel ? 'disabled' : ''}`}
                          onClick={() => handleSelectVenue(v.entityIndex)}
                        >
                          {isSel ? 'Selected' : 'Select'}
                        </button>
                        <button className="suite-primary-btn compact highlight" onClick={() => handleScheduleEventHere(v)}>
                          Schedule Event
                        </button>
                      </div>
                    </div>
                  );
                })}
              </div>
            )}
          </div>
        )}

        {/* ============================================================ */}
        {/* TAB 3: SCHEDULE EVENT FORM */}
        {/* ============================================================ */}
        {activeTab === 'schedule' && (
          <div className="tab-pane schedule-pane">
            <div className="form-section">
              <label className="form-label">Event Title</label>
              <input
                type="text"
                className="suite-input"
                value={eventTitle}
                onChange={(e) => setEventTitle(e.target.value)}
                placeholder="e.g. City Championship Derby"
              />
            </div>

            <div className="form-section">
              <label className="form-label">Event Category</label>
              <div className="category-chips-grid">
                {EVENT_CATEGORIES.map((cat) => (
                  <button
                    key={cat.id}
                    className={`cat-chip ${selectedCategory === cat.id ? 'active' : ''}`}
                    onClick={() => setSelectedCategory(cat.id)}
                  >
                    <span className="cat-icon">{cat.icon}</span>
                    <span className="cat-text">{cat.label}</span>
                  </button>
                ))}
              </div>
            </div>

            <div className="form-row-2">
              <div className="form-section">
                <label className="form-label">Target Venue</label>
                {venues.length === 0 ? (
                  <div className="suite-select read-only">No venue discovered — select or build a venue first</div>
                ) : (
                  <div className="venue-picker-compact">
                    <div className="suite-select read-only" title="Selected event venue">
                      {scheduleVenue?.name || 'Unnamed Venue'} — {(scheduleVenue?.capacity || 0).toLocaleString()} suggested target (estimate)
                    </div>
                    <div className="presets-row">
                      <button className="preset-btn" onClick={() => cycleScheduleVenue(-1)}>Previous Venue</button>
                      <button className="preset-btn" onClick={() => cycleScheduleVenue(1)}>Next Venue</button>
                      <button className="preset-btn" onClick={() => switchTab('venues')}>Browse Venues</button>
                    </div>
                  </div>
                )}
              </div>

              <div className="form-section">
                <label className="form-label">Start Time</label>
                <div className="presets-row">
                  {[
                    { hour: 13, label: '13:30' },
                    { hour: 16, label: '16:30' },
                    { hour: 19, label: '19:30' },
                    { hour: 21, label: '21:00' },
                  ].map((time) => (
                    <button
                      key={time.hour}
                      className={`preset-btn ${startHour === time.hour ? 'active' : ''}`}
                      onClick={() => setStartHour(time.hour)}
                    >
                      {time.label}
                    </button>
                  ))}
                </div>
              </div>
            </div>

            <div className="form-section">
              <div className="label-with-val">
                <label className="form-label">Expected Attendance</label>
                <span className="val-badge">{attendance.toLocaleString()} Spectators</span>
              </div>
              <input
                type="range"
                className="suite-slider"
                min={2000}
                max={50000}
                step={500}
                value={attendance}
                onChange={(e) => setAttendance(Number(e.target.value))}
              />
              <div className="presets-row">
                <button className="preset-btn" onClick={() => setAttendance(5000)}>Small (5k)</button>
                <button className="preset-btn" onClick={() => setAttendance(12000)}>Medium (12k)</button>
                <button className="preset-btn" onClick={() => setAttendance(25000)}>Large (25k)</button>
                <button className="preset-btn" onClick={() => setAttendance(45000)}>Very Large (45k)</button>
              </div>
            </div>

            <div className="form-section">
              <div className="label-with-val">
                <label className="form-label">Transportation Planning Split</label>
                <span className="val-badge">{carPercent}% Cars • {transitPercent}% Transit • {taxiPercent}% Taxi</span>
              </div>
              <div className="modal-split-controls">
                <div className="split-item">
                  <span>🚗 Private Cars ({carPercent}%)</span>
                  <input
                    type="range"
                    className="suite-slider"
                    min={10}
                    max={80}
                    value={carPercent}
                    onChange={(e) => {
                      const v = Number(e.target.value);
                      setCarPercent(v);
                      setTransitPercent(Math.max(10, 100 - v - taxiPercent));
                    }}
                  />
                </div>
                <div className="split-item">
                  <span>🚆 Transit ({transitPercent}%)</span>
                  <input
                    type="range"
                    className="suite-slider"
                    min={10}
                    max={80}
                    value={transitPercent}
                    onChange={(e) => {
                      const v = Number(e.target.value);
                      setTransitPercent(v);
                      setCarPercent(Math.max(10, 100 - v - taxiPercent));
                    }}
                  />
                </div>
              </div>
              <div className="section-subtitle">Display estimate only—vanilla CS2 chooses the actual mode and route for each generated visit.</div>
            </div>

            <button className="suite-primary-btn large submit-btn" onClick={handleScheduleSubmit}>
              {scheduleStatus === 'error' ? 'Try Scheduling Again' : 'Schedule Event'}
            </button>
          </div>
        )}

        {/* ============================================================ */}
        {/* TAB 4: TRAFFIC & TRANSPORTATION */}
        {/* ============================================================ */}
        {activeTab === 'traffic' && (
          <div className="tab-pane traffic-pane">
            <div className="section-title">Event Transportation Planning Estimate</div>
            <div className="section-subtitle">Planning split applied to outstanding request counts. Vanilla CS2 still chooses every actual travel mode and route.</div>

            {nextEvent ? (
              <div className="traffic-stats-grid">
                <div className="traffic-stat-card">
                  <span className="card-icon">🚗</span>
                  <span className="card-title">Projected Private Cars</span>
                  <span className="card-val">{nextEvent.carsCount.toLocaleString()}</span>
                  <span className="card-sub">{nextEvent.carUsagePercent}% Planning Share</span>
                </div>
                <div className="traffic-stat-card">
                  <span className="card-icon">🚆</span>
                  <span className="card-title">Projected Public Transit</span>
                  <span className="card-val">{nextEvent.transitCount.toLocaleString()}</span>
                  <span className="card-sub">{nextEvent.transitUsagePercent}% Planning Share</span>
                </div>
                <div className="traffic-stat-card">
                  <span className="card-icon">🚶</span>
                  <span className="card-title">Projected Walking</span>
                  <span className="card-val">{nextEvent.walkingCount.toLocaleString()}</span>
                  <span className="card-sub">Walking &amp; Transfers</span>
                </div>
                <div className="traffic-stat-card">
                  <span className="card-icon">🚕</span>
                  <span className="card-title">Projected Taxis &amp; Drop-offs</span>
                  <span className="card-val">{nextEvent.taxiCount.toLocaleString()}</span>
                  <span className="card-sub">{nextEvent.taxiUsagePercent}% Planning Share</span>
                </div>
              </div>
            ) : (
              <div className="suite-empty-state">
                <div className="empty-icon">🚦</div>
                <div className="empty-title">No Active Event Traffic</div>
                <div className="empty-desc">Schedule an event to observe live spectator traffic patterns.</div>
              </div>
            )}

            <div className="traffic-impact-card">
              <div className="impact-header">
                <span>Requested-Demand Level</span>
                <span className={`impact-badge impact-${(nextEvent?.trafficImpact || 'LOW').toLowerCase()}`}>
                  {nextEvent?.trafficImpact || 'NONE'}
                </span>
              </div>
              <p className="impact-desc">
                Arrival requests build up during the 2 game-hours before the next occurrence of the selected time. The target is not guaranteed attendance. Vanilla chooses travel modes and routes. Events are runtime-only and clear when leaving the city.
              </p>
            </div>
          </div>
        )}

        {/* ============================================================ */}
        {/* TAB 5: QUICK TEST / DEVELOPER CONTROLS */}
        {/* ============================================================ */}
        {activeTab === 'settings' && (
          <div className="tab-pane settings-pane">
            <div className="section-title">Developer &amp; In-Game Simulation Testing</div>
            <div className="section-subtitle">Exercise native CS2 spectator trip generation and departures on demand</div>

            <div className="test-buttons-grid">
              <button className="test-btn" onClick={() => handleStartNow(nextEvent?.id ?? 0)}>
                <span className="btn-icon">⚡</span>
                <div>
                  <div className="btn-title">Start Event Now</div>
                  <div className="btn-desc">Force event to immediately go LIVE</div>
                </div>
              </button>

              <button className="test-btn" onClick={() => handleSimulateAttendance(nextEvent?.id ?? 0, 5000)}>
                <span className="btn-icon">👥</span>
                <div>
                  <div className="btn-title">5,000 Target Test</div>
                  <div className="btn-desc">Request a bounded batch of up to 3,500 trips</div>
                </div>
              </button>

              <button className="test-btn" onClick={() => handleSimulateAttendance(nextEvent?.id ?? 0, 10000)}>
                <span className="btn-icon">🏟️</span>
                <div>
                  <div className="btn-title">10,000 Target Test</div>
                  <div className="btn-desc">Request a bounded batch of up to 3,500 trips</div>
                </div>
              </button>

              <button className="test-btn" onClick={() => handleSimulateAttendance(nextEvent?.id ?? 0, 25000)}>
                <span className="btn-icon">🔥</span>
                <div>
                  <div className="btn-title">25,000 Target Test</div>
                  <div className="btn-desc">Request a bounded batch of up to 3,500 trips</div>
                </div>
              </button>

              <button className="test-btn danger" onClick={() => handleEndEvent(nextEvent?.id ?? 0)}>
                <span className="btn-icon">🏁</span>
                <div>
                  <div className="btn-title">End Current Event</div>
                  <div className="btn-desc">Trigger post-event departures</div>
                </div>
              </button>

              <button className="test-btn danger" onClick={handleResetAll}>
                <span className="btn-icon">🗑️</span>
                <div>
                  <div className="btn-title">Reset Event Engine State</div>
                  <div className="btn-desc">Wipe all active and scheduled events</div>
                </div>
              </button>
            </div>
          </div>
        )}
      </div>

      {/* FOOTER */}
      <div className="suite-footer">
        <span className="footer-hint">
          {events.length} Events Scheduled • {venues.length} Venues Active
        </span>
        <span className="footer-version">Event Engine v0.2.0-BETA</span>
      </div>
    </div>
  );
};

class EventEnginePanelBoundary extends React.Component<React.PropsWithChildren<{}>, { failed: boolean }> {
  state = { failed: false };

  static getDerivedStateFromError() {
    return { failed: true };
  }

  componentDidCatch(error: unknown) {
    console.error('[EventEngine] Panel render recovered from invalid UI data.', error);
  }

  private recover = () => {
    this.setState({ failed: false });
    trigger('EventEngine', 'setOpen', true);
    trigger('EventEngine', 'setActiveTab', 'overview');
  };

  render() {
    if (!this.state.failed) return this.props.children;
    return (
      <div className="suite-panel event-engine-panel event-engine-recovery" role="alert">
        <div className="suite-header">
          <div className="header-left"><h2 className="header-title">Event Engine</h2></div>
        </div>
        <div className="suite-body">
          <div className="suite-empty-state">
            <h3 className="empty-title">The panel recovered from invalid UI data</h3>
            <p className="empty-desc">Your event simulation is still running. Refresh the panel to continue.</p>
            <button className="suite-primary-btn" onClick={this.recover}>Refresh Event Engine</button>
          </div>
        </div>
      </div>
    );
  }
}

const EventEnginePanelWithBoundary: React.FC = () => (
  <EventEnginePanelBoundary><EventEnginePanel /></EventEnginePanelBoundary>
);

const register: ModRegistrar = (moduleRegistry) => {
  moduleRegistry.append('GameTopLeft', EventEngineToolbarButton);
  moduleRegistry.append('GameTopRight', EventEnginePanelWithBoundary);
};

export default register;
