import { PortfolioHelp, useRememberedPreference } from "./portfolio-ux";
import React, { useState, useEffect, useCallback } from 'react';
import type { ModRegistrar } from 'cs2/modding';
import { bindValue, trigger } from 'cs2/api';
import { FloatingButton, Tooltip } from 'cs2/ui';
import iconSrc from './assets/citypulse-icon.svg';
import './style.scss';

declare const engine: any;

interface MasterOverview {
  masterCityHealthScore: number;
  trafficHealthScore: number;
  transitHealthScore: number;
  parkingHealthScore: number;
  serviceHealthScore: number;
  buildingHealthScore: number;
  networkHealthScore: number;
  criticalAlertCount: number;
  warningAlertCount: number;
  advisoryAlertCount: number;
  legacyModDetected: boolean;
  legacyModWarningMessage: string;
}

interface CityAlert {
  id: number;
  sourceModule: string;
  severity: string;
  title: string;
  measuredEvidence: string;
  inferredCause: string;
  confidence: string;
  entityIndex: number;
  posX: number;
  posY: number;
  posZ: number;
  actionLabel: string;
}

const visibleBinding = bindValue<boolean>('cityPulse', 'visible', false);

const portfolioBindings = { "visible": visibleBinding };
export const CityPulseToolbarButton: React.FC = () => {
  const [isOpen, setIsOpen] = useState(visibleBinding.value);

  useEffect(() => {
    const sub = visibleBinding.subscribe(setIsOpen);
    return () => sub.dispose();
  }, []);

  const toggle = useCallback(() => trigger('cityPulse', 'toggleVisible'), []);

  return (
    <Tooltip tooltip="City Pulse — View city diagnostics">
      <FloatingButton
        src={iconSrc}
        selected={isOpen}
        onSelect={toggle}
      />
    </Tooltip>
  );

};

export const CityPulsePanel: React.FC = () => {
  const [visible, setVisible] = useState<boolean>(visibleBinding.value);
  const [activeTab, setActiveTab] = useState<'overview' | 'traffic' | 'transit' | 'parking' | 'services' | 'buildings' | 'network'>('overview');
  const [selectedAlert, setSelectedAlert] = useState<CityAlert | null>(null);
  const [showAdvanced, setShowAdvanced] = useRememberedPreference("CityPulse.showAdvanced", false);
  const [toastMessage, setToastMessage] = useState<string | null>(null);

  const [overview, setOverview] = useState<MasterOverview>({
    masterCityHealthScore: 0,
    trafficHealthScore: 0,
    transitHealthScore: 0,
    parkingHealthScore: 0,
    serviceHealthScore: 0,
    buildingHealthScore: 0,
    networkHealthScore: 0,
    criticalAlertCount: 0,
    warningAlertCount: 0,
    advisoryAlertCount: 0,
    legacyModDetected: false,
    legacyModWarningMessage: ''
  });

  const [alerts, setAlerts] = useState<CityAlert[]>([]);
  const [trafficSummary, setTrafficSummary] = useState<any>({ cityHealthScore: 0, activeVehicles: 0, averageCitySpeedKph: 0, congestionIndex: 0, bottleneckCount: 0 });
  const [trafficBottlenecks, setTrafficBottlenecks] = useState<any[]>([]);
  const [transitSummary, setTransitSummary] = useState<any>({ totalLines: 0, activeVehicles: 0, totalPassengers: 0, totalWaiting: 0, avgUtilization: 0, overcrowdedCount: 0, bunchingCount: 0, healthScore: 0 });
  const [transitLines, setTransitLines] = useState<any[]>([]);
  const [transitStops, setTransitStops] = useState<any[]>([]);
  const [parkingSummary, setParkingSummary] = useState<any>({ totalOffStreetSpaces: 0, totalParkedCars: 0, networkUtilizationPercent: 0, fullFacilitiesCount: 0, underutilizedCount: 0, totalFacilities: 0, healthScore: 0 });
  const [parkingFacilities, setParkingFacilities] = useState<any[]>([]);
  const [serviceSummary, setServiceSummary] = useState<any>({ totalFacilities: 0, criticalCount: 0, warningCount: 0, optimalCount: 0, averageEfficiency: 0, healthScore: 0 });
  const [serviceFacilities, setServiceFacilities] = useState<any[]>([]);
  const [buildingSummary, setBuildingSummary] = useState<any>({ totalBuildingsScanned: 0, troubledCount: 0, workerShortageCount: 0, lowEfficiencyCount: 0, abandonedCount: 0, healthScore: 0 });
  const [buildingIssues, setBuildingIssues] = useState<any[]>([]);
  const [selectedBuilding, setSelectedBuilding] = useState<any>(null);
  const [networkSummary, setNetworkSummary] = useState<any>({ totalSegments: 0, scannedSegments: 0, criticalCount: 0, errorCount: 0, warningCount: 0, infoCount: 0, healthScore: 0, lastStatus: 'Waiting for first scan', undoCount: 0 });
  const [networkDefects, setNetworkDefects] = useState<any[]>([]);

  const showToast = (msg: string) => {
    setToastMessage(msg);
    setTimeout(() => setToastMessage(null), 3500);
  };

  useEffect(() => {
    const subVisible = visibleBinding.subscribe(setVisible);

    if (typeof engine !== 'undefined') {
      engine.on('cityPulse.overview', (val: MasterOverview) => { if (val) setOverview(val); });
      engine.on('cityPulse.alerts', (val: CityAlert[]) => setAlerts(val || []));
      engine.on('cityPulse.traffic', (val: any) => { if (val) setTrafficSummary(val); });
      engine.on('cityPulse.trafficBottlenecks', (val: any[]) => setTrafficBottlenecks(val || []));
      engine.on('cityPulse.transit', (val: any) => { if (val) setTransitSummary(val); });
      engine.on('cityPulse.transitLines', (val: any[]) => setTransitLines(val || []));
      engine.on('cityPulse.transitStops', (val: any[]) => setTransitStops(val || []));
      engine.on('cityPulse.parking', (val: any) => { if (val) setParkingSummary(val); });
      engine.on('cityPulse.parkingFacilities', (val: any[]) => setParkingFacilities(val || []));
      engine.on('cityPulse.services', (val: any) => { if (val) setServiceSummary(val); });
      engine.on('cityPulse.serviceFacilities', (val: any[]) => setServiceFacilities(val || []));
      engine.on('cityPulse.buildings', (val: any) => { if (val) setBuildingSummary(val); });
      engine.on('cityPulse.buildingIssues', (val: any[]) => setBuildingIssues(val || []));
      engine.on('cityPulse.selectedBuilding', (val: any) => setSelectedBuilding(val && val.entityIndex > 0 ? val : null));
      engine.on('cityPulse.network', (val: any) => { if (val) setNetworkSummary(val); });
      engine.on('cityPulse.networkDefects', (val: any[]) => setNetworkDefects(val || []));
    }

    return () => {
      subVisible.dispose();
    };
  }, []);

  const close = useCallback(() => {
    trigger('cityPulse', 'closeVisible');
  }, []);

  useEffect(() => {
    if (!visible) return;
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        close();
      }
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [visible, close]);

  const jumpToPos = (x: number, y: number, z: number, label?: string) => {
    trigger('cityPulse', 'jumpToPosition', x, y, z);
    if (label) showToast(`Jumped to ${label}`);
  };

  if (!visible) return null;

  const overviewReady = overview.masterCityHealthScore > 0;

  const getStatusText = (score: number) => {
    if (!overviewReady) return 'SCANNING';
    if (score >= 80) return 'GOOD';
    if (score >= 60) return 'WATCH';
    return 'PROBLEM';
  };

  const getStatusClass = (score: number) => {
    if (!overviewReady) return 'status-waiting';
    if (score >= 80) return 'status-good';
    if (score >= 60) return 'status-watch';
    return 'status-problem';
  };

  const masterStatus = getStatusText(overview.masterCityHealthScore);

  return (
    <div className="suite-panel city-pulse-panel" data-portfolio-panel role="dialog" aria-label="City Pulse Diagnostic Suite">
      {/* PANEL HEADER */}
      <div className="suite-header">
        <div className="header-left">
          <img src={iconSrc} alt="City Pulse" className="header-icon" />
          <div>
            <h2 className="header-title">City Pulse</h2>
            <span className="header-subtitle">Measured City Signals</span>
          </div>
        </div>
        <div className="header-right">
          <span className={`suite-badge ${getStatusClass(overview.masterCityHealthScore)}`}>
            {overviewReady ? `${overview.masterCityHealthScore}% / ${masterStatus}` : 'SCANNING'}
          </span>
          <button className="suite-close-btn" onClick={close} title="Close Panel" aria-label="Close panel">×</button>
        </div>
      </div>

      {toastMessage && (
        <div className="suite-toast">
          <span>{toastMessage}</span>
        </div>
      )}

      {!overviewReady && (
        <div className="suite-banner scanning">
          City scan starting. Initial values are withheld until the first sample is ready.
        </div>
      )}

      {overview.legacyModDetected && (
        <div className="suite-banner warning">
          <span>Legacy mod detected: {overview.legacyModWarningMessage}</span>
        </div>
      )}

      {/* TOP NAVIGATION TABS */}
      <nav className="suite-tabs">
        <button
          className={activeTab === 'overview' ? 'tab-item active' : 'tab-item'}
          onClick={() => setActiveTab('overview')}
          title="Overview of citywide health and priority alerts"
        >
          Overview {alerts.length > 0 && <span className="tab-pill">{alerts.length}</span>}
        </button>
        <button
          className={activeTab === 'traffic' ? 'tab-item active' : 'tab-item'}
          onClick={() => setActiveTab('traffic')}
          title="Inspect vehicle speeds, congestion, and bottlenecks"
        >
          Traffic ({overview.trafficHealthScore}%)
        </button>
        <button
          className={activeTab === 'transit' ? 'tab-item active' : 'tab-item'}
          onClick={() => setActiveTab('transit')}
          title="Inspect bus, train, and subway lines"
        >
          Transit ({overview.transitHealthScore}%)
        </button>
        <button
          className={activeTab === 'parking' ? 'tab-item active' : 'tab-item'}
          onClick={() => setActiveTab('parking')}
          title="Inspect parking capacity and utilization"
        >
          Parking ({overview.parkingHealthScore}%)
        </button>
        <button
          className={activeTab === 'services' ? 'tab-item active' : 'tab-item'}
          onClick={() => setActiveTab('services')}
          title="Inspect city service building efficiency"
        >
          Services ({overview.serviceHealthScore}%)
        </button>
        <button
          className={activeTab === 'buildings' ? 'tab-item active' : 'tab-item'}
          onClick={() => setActiveTab('buildings')}
          title="Inspect building abandoned & worker shortage issues"
        >
          Buildings ({overview.buildingHealthScore}%)
        </button>
        <button
          className={activeTab === 'network' ? 'tab-item active' : 'tab-item'}
          onClick={() => setActiveTab('network')}
          title="Inspect broken road geometry and network defects"
        >
          Roads ({overview.networkHealthScore}%)
        </button>
      </nav>

      {/* BODY CONTENT AREA */}
      <PortfolioHelp runtimeGroup={"Portfolio.CityPulse"} name={"City Pulse"} version={"3.0.4-beta.1"} steps={["Wait for a city scan, then choose a diagnostic category.", "Select an alert and compare its measured evidence with the inferred cause.", "Use the location action to inspect the affected area."]} note={"Scores and inferred causes are diagnostic clues, not proof."} bindings={portfolioBindings} />
      <div className="suite-body">
        {/* OVERVIEW TAB */}
        {activeTab === 'overview' && (
          <div className="overview-view">
            {/* HERO HEALTH CARD */}
            <div className="hero-health-card">
              <div className="health-left">
                <span className="health-label">HEURISTIC CITY SUMMARY</span>
                <h1 className={`health-status-title ${getStatusClass(overview.masterCityHealthScore)}`}>
                  {!overviewReady ? 'Scanning city data' : masterStatus === 'GOOD' ? 'Few sampled warning signs' : masterStatus === 'WATCH' ? 'Review sampled warnings' : 'Review priority observations'}
                </h1>
                <p className="health-desc">
                  {!overviewReady
                    ? 'The first scan is starting. Health scores and alerts will appear after City Pulse has sampled the city.'
                    : 'Scores summarize limited samples using heuristics; they are not a complete city diagnosis or proof of a cause. Building scans finish in batches. Inspect the measured evidence below.'}
                </p>
              </div>
              <div className="health-right">
                <span className="section-subtitle">Updates automatically</span>
              </div>
            </div>

            {/* DOMAIN HEALTH CARDS */}
            <div className="domain-grid">
              <div className="domain-card" onClick={() => setActiveTab('traffic')} title="Click to view Traffic details">
                <div className="domain-header">
                  <span className="domain-name">Traffic</span>
                  <span className={`domain-badge ${getStatusClass(overview.trafficHealthScore)}`}>{getStatusText(overview.trafficHealthScore)}</span>
                </div>
                <div className="domain-metric">{overview.trafficHealthScore}%</div>
                <div className="domain-subtext">{trafficSummary.bottleneckCount} bottlenecks detected</div>
              </div>

              <div className="domain-card" onClick={() => setActiveTab('transit')} title="Click to view Transit details">
                <div className="domain-header">
                  <span className="domain-name">Transit</span>
                  <span className={`domain-badge ${getStatusClass(overview.transitHealthScore)}`}>{getStatusText(overview.transitHealthScore)}</span>
                </div>
                <div className="domain-metric">{overview.transitHealthScore}%</div>
                <div className="domain-subtext">{transitSummary.overcrowdedCount} overcrowded stops</div>
              </div>

              <div className="domain-card" onClick={() => setActiveTab('parking')} title="Click to view Parking details">
                <div className="domain-header">
                  <span className="domain-name">Parking</span>
                  <span className={`domain-badge ${getStatusClass(overview.parkingHealthScore)}`}>{getStatusText(overview.parkingHealthScore)}</span>
                </div>
                <div className="domain-metric">{overview.parkingHealthScore}%</div>
                <div className="domain-subtext">{parkingSummary.networkUtilizationPercent}% utilization</div>
              </div>

              <div className="domain-card" onClick={() => setActiveTab('services')} title="Click to view Services details">
                <div className="domain-header">
                  <span className="domain-name">Services</span>
                  <span className={`domain-badge ${getStatusClass(overview.serviceHealthScore)}`}>{getStatusText(overview.serviceHealthScore)}</span>
                </div>
                <div className="domain-metric">{overview.serviceHealthScore}%</div>
                <div className="domain-subtext">{serviceSummary.criticalCount} critical facilities</div>
              </div>

              <div className="domain-card" onClick={() => setActiveTab('buildings')} title="Click to view Buildings details">
                <div className="domain-header">
                  <span className="domain-name">Buildings</span>
                  <span className={`domain-badge ${getStatusClass(overview.buildingHealthScore)}`}>{getStatusText(overview.buildingHealthScore)}</span>
                </div>
                <div className="domain-metric">{overview.buildingHealthScore}%</div>
                <div className="domain-subtext">{buildingSummary.troubledCount} troubled structures</div>
              </div>

              <div className="domain-card" onClick={() => setActiveTab('network')} title="Click to view Road Network details">
                <div className="domain-header">
                  <span className="domain-name">Roads</span>
                  <span className={`domain-badge ${getStatusClass(overview.networkHealthScore)}`}>{getStatusText(overview.networkHealthScore)}</span>
                </div>
                <div className="domain-metric">{overview.networkHealthScore}%</div>
                <div className="domain-subtext">{networkSummary.criticalCount} geometry defects</div>
              </div>
            </div>

            {/* PRIORITY ALERTS / PROBLEM DRILLDOWN */}
            <div className="section-header-bar">
              <h3 className="section-title">Priority Alerts & Recommended Actions</h3>
              <span className="section-count">{alerts.length} Total</span>
            </div>

            {!overviewReady ? (
              <div className="suite-empty-state">
                <h3>Waiting for the first scan</h3>
                <p>Priority alerts will be reported here after City Pulse samples the city.</p>
              </div>
            ) : alerts.length === 0 ? (
              <div className="suite-empty-state">
                <h3>No Current Priority Alerts</h3>
                <p>No supported critical traffic, service, or network issue patterns are currently reported.</p>
              </div>
            ) : (
              <div className="alerts-feed">
                {alerts.map((al) => (
                  <div className={`problem-card ${al.severity.toLowerCase()}`} key={al.id}>
                    <div className="problem-header">
                      <div className="problem-title-group">
                        <span className="problem-badge">{al.sourceModule}</span>
                        <h4 className="problem-title">{al.title}</h4>
                      </div>
                      <span className="confidence-tag">{al.confidence} confidence</span>
                    </div>

                    <div className="problem-content">
                      <div className="problem-row">
                        <span className="row-label">What's happening:</span>
                        <span className="row-val">{al.measuredEvidence}</span>
                      </div>
                      <div className="problem-row">
                        <span className="row-label">Why it matters:</span>
                        <span className="row-val">{al.inferredCause}</span>
                      </div>
                      <div className="problem-row">
                        <span className="row-label">What you can do:</span>
                        <span className="row-val">{al.actionLabel || 'Inspect the affected location and adjust road capacity or service coverage.'}</span>
                      </div>
                    </div>

                    <div className="problem-actions">
                      {al.posX !== 0 || al.posY !== 0 || al.posZ !== 0 ? (
                        <button
                          className="suite-primary-btn compact"
                          onClick={() => jumpToPos(al.posX, al.posY, al.posZ, al.title)}
                          title="Move camera to the incident location"
                        >
                          Show Location
                        </button>
                      ) : null}
                      {al.entityIndex > 0 ? (
                        <button
                          className="suite-secondary-btn compact"
                          onClick={() => jumpToPos(al.posX, al.posY, al.posZ, al.title)}
                          title="Show the reported location"
                        >
                          Show Location
                        </button>
                      ) : null}
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        )}

        {/* TRAFFIC TAB */}
        {activeTab === 'traffic' && (
          <div className="tab-pane">
            <div className="kpi-summary-row">
              <div className="kpi-box">
                <span className="kpi-label">Sampled Vehicle Speed</span>
                <span className="kpi-value">{trafficSummary.averageCitySpeedKph} km/h</span>
              </div>
              <div className="kpi-box">
                <span className="kpi-label">Active Vehicles</span>
                <span className="kpi-value">{trafficSummary.activeVehicles}</span>
              </div>
              <div className="kpi-box">
                <span className="kpi-label">Stopped in Sample</span>
                <span className="kpi-value">{trafficSummary.congestionIndex}%</span>
              </div>
            </div>

            <div className="section-header-bar">
              <h3 className="section-title">Traffic Bottlenecks</h3>
              <span className="section-count">{trafficBottlenecks.length}</span>
            </div>

            {trafficBottlenecks.length === 0 ? (
              <div className="suite-empty-state">
                <h3>No Vanilla Bottleneck Markers Found</h3>
                <p>No active CS2 bottleneck markers were present when City Pulse sampled the city.</p>
              </div>
            ) : (
              <div className="items-list">
                {trafficBottlenecks.map((b: any, idx: number) => (
                  <div className="item-row" key={idx}>
                    <div className="item-info">
                      <div className="item-name">{b.locationName || `Lane #${b.entityIndex}`}</div>
                      <div className="item-desc">{b.severity} | Marker persistence: {b.queuePressurePercent}%</div>
                    </div>
                    <button
                      className="suite-primary-btn compact"
                      onClick={() => jumpToPos(b.posX, b.posY, b.posZ, b.locationName)}
                    >
                      Show Location
                    </button>
                  </div>
                ))}
              </div>
            )}
          </div>
        )}

        {/* TRANSIT TAB */}
        {activeTab === 'transit' && (
          <div className="tab-pane">
            <div className="kpi-summary-row">
              <div className="kpi-box">
                <span className="kpi-label">Total Lines</span>
                <span className="kpi-value">{transitSummary.totalLines}</span>
              </div>
              <div className="kpi-box">
                <span className="kpi-label">Total Passengers</span>
                <span className="kpi-value">{transitSummary.totalPassengers}</span>
              </div>
              <div className="kpi-box">
                <span className="kpi-label">Avg Utilization</span>
                <span className="kpi-value">{transitSummary.avgUtilization}%</span>
              </div>
            </div>

            <div className="section-header-bar">
              <h3 className="section-title">Transit Lines</h3>
              <span className="section-count">{transitLines.length}</span>
            </div>

            {transitLines.length === 0 ? (
              <div className="suite-empty-state">
                <h3>No Transit Lines Active</h3>
                <p>Create bus, tram, subway, or train lines to view transit performance.</p>
              </div>
            ) : (
              <div className="items-list">
                {transitLines.map((l: any, idx: number) => (
                  <div className="item-row" key={idx}>
                    <div className="item-info">
                      <div className="item-name">{l.name || `Line #${l.entityIndex}`}</div>
                      <div className="item-desc">{l.vehicleCount} vehicles | {l.passengers} passengers | {l.utilizationPercent}% measured load</div>
                    </div>
                    <button
                      className="suite-secondary-btn compact"
                      onClick={() => jumpToPos(l.worstStopPosX, l.worstStopPosY, l.worstStopPosZ, l.worstStopName || l.name)}
                    >
                      Show Busiest Stop
                    </button>
                  </div>
                ))}
              </div>
            )}
          </div>
        )}

        {/* PARKING TAB */}
        {activeTab === 'parking' && (
          <div className="tab-pane">
            <div className="kpi-summary-row">
              <div className="kpi-box">
                <span className="kpi-label">Total Spaces</span>
                <span className="kpi-value">{parkingSummary.totalOffStreetSpaces}</span>
              </div>
              <div className="kpi-box">
                <span className="kpi-label">Parked Vehicles</span>
                <span className="kpi-value">{parkingSummary.totalParkedCars}</span>
              </div>
              <div className="kpi-box">
                <span className="kpi-label">Full Facilities</span>
                <span className="kpi-value">{parkingSummary.fullFacilitiesCount}</span>
              </div>
            </div>

            <div className="section-header-bar">
              <h3 className="section-title">Parking Facilities</h3>
              <span className="section-count">{parkingFacilities.length}</span>
            </div>

            {parkingFacilities.length === 0 ? (
              <div className="suite-empty-state">
                <h3>No Parking Facilities Found</h3>
                <p>Build parking lots or garages to inspect off-street capacity.</p>
              </div>
            ) : (
              <div className="items-list">
                {parkingFacilities.map((f: any, idx: number) => (
                  <div className="item-row" key={idx}>
                    <div className="item-info">
                      <div className="item-name">{f.name || `Parking Facility #${f.entityIndex}`}</div>
                      <div className="item-desc">{f.parkedCars} / {f.capacity} spaces ({f.utilizationPercent}% full)</div>
                    </div>
                    <button
                      className="suite-primary-btn compact"
                      onClick={() => jumpToPos(f.posX, f.posY, f.posZ, f.name)}
                    >
                      Show Location
                    </button>
                  </div>
                ))}
              </div>
            )}
          </div>
        )}

        {/* SERVICES TAB */}
        {activeTab === 'services' && (
          <div className="tab-pane">
            <div className="kpi-summary-row">
              <div className="kpi-box">
                <span className="kpi-label">Total Facilities</span>
                <span className="kpi-value">{serviceSummary.totalFacilities}</span>
              </div>
              <div className="kpi-box">
                <span className="kpi-label">Optimal</span>
                <span className="kpi-value status-good">{serviceSummary.optimalCount}</span>
              </div>
              <div className="kpi-box">
                <span className="kpi-label">Critical</span>
                <span className="kpi-value status-problem">{serviceSummary.criticalCount}</span>
              </div>
            </div>

            <div className="section-header-bar">
              <h3 className="section-title">City Service Facilities</h3>
              <span className="section-count">{serviceFacilities.length}</span>
            </div>

            {serviceFacilities.length === 0 ? (
              <div className="suite-empty-state">
                <h3>No Service Facilities Found</h3>
                <p>Build health, police, fire, education, or garbage services to view diagnostics.</p>
              </div>
            ) : (
              <div className="items-list">
                {serviceFacilities.map((s: any, idx: number) => (
                  <div className="item-row" key={idx}>
                    <div className="item-info">
                      <div className="item-name">{s.name || `Service Facility #${s.entityIndex}`}</div>
                      <div className="item-desc">Efficiency: {s.efficiencyPercent}% | Status: {s.status}</div>
                    </div>
                    <button
                      className="suite-primary-btn compact"
                      onClick={() => jumpToPos(s.posX, s.posY, s.posZ, s.name)}
                    >
                      Show Location
                    </button>
                  </div>
                ))}
              </div>
            )}
          </div>
        )}

        {/* BUILDINGS TAB */}
        {activeTab === 'buildings' && (
          <div className="tab-pane">
            <div className="kpi-summary-row">
              <div className="kpi-box">
                <span className="kpi-label">Scanned Buildings</span>
                <span className="kpi-value">{buildingSummary.totalBuildingsScanned}</span>
              </div>
              <div className="kpi-box">
                <span className="kpi-label">Worker Shortages</span>
                <span className="kpi-value">{buildingSummary.workerShortageCount}</span>
              </div>
              <div className="kpi-box">
                <span className="kpi-label">Abandoned</span>
                <span className="kpi-value status-problem">{buildingSummary.abandonedCount}</span>
              </div>
            </div>

            <div className="section-header-bar">
              <h3 className="section-title">Building Issues</h3>
              <span className="section-count">{buildingIssues.length}</span>
            </div>

            {buildingIssues.length === 0 ? (
              <div className="suite-empty-state">
                <h3>No Tracked Building Issues</h3>
                <p>No abandonment, major vacancy, or low-efficiency signals were present in this scan.</p>
              </div>
            ) : (
              <div className="items-list">
                {buildingIssues.map((b: any, idx: number) => (
                  <div className="item-row" key={idx}>
                    <div className="item-info">
                      <div className="item-name">{b.buildingName || `Building #${b.entityIndex}`}</div>
                      <div className="item-desc">{b.primaryIssue} | {b.measuredEvidence}</div>
                    </div>
                    <button
                      className="suite-primary-btn compact"
                      onClick={() => jumpToPos(b.posX, b.posY, b.posZ, b.buildingName)}
                    >
                      Show Location
                    </button>
                  </div>
                ))}
              </div>
            )}
          </div>
        )}

        {/* ROADS / NETWORK TAB */}
        {activeTab === 'network' && (
          <div className="tab-pane">
            <div className="kpi-summary-row">
              <div className="kpi-box">
                <span className="kpi-label">Lane Entities</span>
                <span className="kpi-value">{networkSummary.totalSegments}</span>
              </div>
              <div className="kpi-box">
                <span className="kpi-label">Invalid Curves</span>
                <span className="kpi-value status-problem">{networkSummary.criticalCount}</span>
              </div>
              <div className="kpi-box">
                <span className="kpi-label">Status</span>
                <span className="kpi-value">{networkSummary.lastStatus}</span>
              </div>
            </div>

            <div className="section-header-bar">
              <h3 className="section-title">Read-only Lane Curve Checks</h3>
            </div>

            {networkDefects.length === 0 ? (
              <div className="suite-empty-state">
                <h3>No Invalid Curves in the Sample</h3>
                <p>This check only detects non-finite or zero-length lane curves; it is not a full network integrity guarantee.</p>
              </div>
            ) : (
              <div className="items-list">
                {networkDefects.map((d: any) => (
                  <div className="item-row" key={d.id}>
                    <div className="item-info">
                      <div className="item-name">{d.title}</div>
                      <div className="item-desc">{d.whatWeFound} | {d.evidence}</div>
                    </div>
                    <div className="item-actions">
                      <button
                        className="suite-secondary-btn compact"
                        onClick={() => jumpToPos(d.posX, d.posY, d.posZ, d.title)}
                        title="Show location in city"
                      >
                        Show
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        )}
      </div>

      {/* FOOTER */}
      <div className="suite-footer">
        <span className="footer-hint">City Pulse v3.0 | Continuous Diagnostics</span>
        <button
          className="advanced-toggle-btn"
          onClick={() => setShowAdvanced(!showAdvanced)}
          title="Toggle diagnostic telemetry details"
        >
          {showAdvanced ? 'Hide Advanced' : 'Advanced'}
        </button>
      </div>

      {showAdvanced && (
        <div className="advanced-drawer">
          <div className="drawer-row">
            <span>Critical Alerts: {overview.criticalAlertCount}</span>
            <span>Warnings: {overview.warningAlertCount}</span>
            <span>Advisories: {overview.advisoryAlertCount}</span>
          </div>
          <div className="drawer-row">
            <span>Sampled Lane Entities: {networkSummary.scannedSegments}</span>
            <span>Active Vehicle Agents: {trafficSummary.activeVehicles}</span>
          </div>
        </div>
      )}
    </div>
  );
};

const register: ModRegistrar = (moduleRegistry) => {
  moduleRegistry.append('GameTopLeft', CityPulseToolbarButton);
  moduleRegistry.append('GameTopRight', CityPulsePanel);
};

export default register;
