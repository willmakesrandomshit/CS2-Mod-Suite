import { PortfolioHelp, useRememberedPreference } from "../portfolio-ux";
// React's KeyboardEvent is aliased — the bare name must keep referring to the
// DOM type (the document-level hotkey handler below is typed against it).
import { ChangeEvent, Component, ErrorInfo, KeyboardEvent as ReactKeyboardEvent, MouseEvent as ReactMouseEvent, ReactNode, useEffect, useState } from "react";
import { createPortal } from "react-dom";
import {
  useToolState,
  cmdToggleSegment,
  cmdSetLineStyle,
  cmdSetSegmentStyle,
  cmdDeleteLine,
  cmdSetHoveredLine,
  cmdSetHoveredSegment,
  cmdSetHoveredArea,
  cmdClearHoveredArea,
  cmdSetLineCurvature,
  cmdToggleVanillaMarkings,
  cmdActivateTool,
  cmdSetCurrentStyle,
  cmdSetCurrentAreaStyle,
  cmdToggleAreaMode,
  cmdToggleAreaPaintMode,
  cmdSetAreaStyle,
  cmdToggleAreaVisible,
  cmdDeleteArea,
  cmdResetNode,
  cmdAutoGenerateMarkings,
  cmdUndoMarking,
  cmdRedoMarking,
  cmdApplyTemplate,
  cmdSaveTemplate,
  cmdDeleteTemplate,
  cmdToggleTemplateFavorite,
  cmdGenerateJunctionMarkings,
  cmdCopySelection,
  cmdPasteSelection,
  cmdMirrorSelection,
  cmdInvertCurvature,
  cmdRepeatSelection,
  cmdSelectAll,
  cmdClearSelection,
  cmdCreateGroup,
  cmdAcceptRemap,
  cmdDeleteRemap,
  cmdSetCurrentDrawingMode,
  cmdSetLineDrawingMode,
  cmdSetLineLateralOffset,
  cmdSnapLineToRoad,
  cmdResetLineCurvature,
  DRAWING_MODE,
  TOOL_STATE,
  LineVM,
  SegmentVM,
  AreaVM,
  TemplateVM,
  RemapEntryVM,
} from "../hooks/useToolState";
import {
  usePinnedStyles,
  cmdTogglePinLineStyle,
  cmdTogglePinAreaStyle,
} from "../hooks/usePinnedStyles";
import { registerSegmentAnchor, setAnchorExpanded, segKey, areaKey } from "../hooks/positionRegistry";
import { ChevronRight, Cross, Eye, EyeOff, Trash, Cycle } from "../components/icons";
import { LineStylePreview, AreaStylePreview, isG87LineStyle } from "../components/stylePreviews";
import { Dropdown, DropdownOption } from "../components/Dropdown";
import { TooltipProvider, Tooltip } from "../components/Tooltip";
import { useT } from "../i18n";
import type { StringKey } from "../i18n";
import { tokens as T } from "../styles/tokens";
import {
  Panel,
  PanelStickyChrome,
  PanelTitle,
  PanelHeaderRow,
  CloseBtn,
  StatusRow,
  StatusDot,
  ToggleRow,
  IconToggleBtn,
  FoldoutHeader,
  NodeIdText,
  PanelHint,
  PanelList,
  ModeRow,
  ModeBtn,
  DraftBox,
  DraftHint,
  FieldRow,
  FieldLabel,
  SectionTitle,
  HintsBox,
  HintRow,
  HintKey,
  LineRowOuter,
  LineHeader,
  LineChevron,
  LineTitle,
  SwatchWrap,
  G87Mark,
  LineSegCount,
  LineBody,
  StyleRow,
  CurvRow,
  CurvLabel,
  CurvInput,
  CurvUnit,
  CurvResetBtn,
  CurvStepBtn,
  PopoverRoot,
  PopoverBtn,
  PopoverMarker,
  PopoverDropdownWrap,
  SegmentRow,
  SegmentInfo,
  SegmentLen,
  SegmentIndicator,
  Btn,
  ConfirmRow,
  TabBar,
  TabButton,
  PresetCard,
  ToolbarRow,
  Badge,
} from "./panel.styles";

// Containment boundary — a JS exception inside the panel must not propagate to
// the game's React root and tear the whole HUD down. Caught errors are logged
// and the panel renders a tiny "broken" placeholder until the underlying state
// changes (typically next C# tick).
class PanelErrorBoundary extends Component<{ children: ReactNode }, { error: Error | null }> {
  state = { error: null as Error | null };
  static getDerivedStateFromError(error: Error) { return { error }; }
  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error("MarkingStudio panel crashed:", error, info);
  }
  render() {
    if (this.state.error) {
      return <PanelErrorFallback error={this.state.error} onRetry={() => this.setState({ error: null })} />;
    }
    return this.props.children;
  }
}

const PanelErrorFallback = ({ error, onRetry }: { error: Error; onRetry: () => void }) => {
  const t = useT();
  return (
    <Panel style={{ borderColor: T.colorDanger }}>
      <PanelTitle>{t("panel.error.title")}</PanelTitle>
      <PanelHint>{error.message}</PanelHint>
      <Btn onClick={onRetry}>{t("panel.error.retry")}</Btn>
    </Panel>
  );
};

// MarkingStyle enum on the C# side — numeric values must stay in sync.
const STYLE_VALUES = [0, 1, 5, 8, 4, 10, 11, 13, 12, 2, 3, 6, 7, 14, 15, 16, 9] as const;
type StyleValue = typeof STYLE_VALUES[number];

// Lookup table: enum value → i18n string key. Keeps style label rendering
// alongside the enum mapping rather than scattered across components.
// STYLE_VALUES order groups related looks together in the dropdown (vanilla
// whites incl. double, vanilla yellows, G87 white + yellow, curb) — the
// numeric enum order is append-only history, not a presentation order.
const STYLE_KEYS: Record<number, StringKey> = {
  0: "style.solid",
  1: "style.dashed",
  2: "style.g87Solid",
  3: "style.g87Dashed",
  4: "style.doubleSolid",
  5: "style.dashedDense",
  6: "style.g87Yellow",
  7: "style.g87YellowDashed",
  8: "style.dashedLong",
  9: "style.curb",
  10: "style.yellowSolid",
  11: "style.yellowDashed",
  12: "style.yellowDoubleSolid",
  13: "style.yellowSolidDashed",
  14: "style.g87Chevron30",
  15: "style.g87Chevron60",
  16: "style.g87ChevronWide",
};

const styleLabel = (t: ReturnType<typeof useT>, style: number): string =>
  t(STYLE_KEYS[style] ?? "style.unknown");

// Area fill styles — ids match kStyleSurfaceNames in MarkingAreaEmissionSystem.
// Ids 7-13 are reserved dead slots (the vanilla grass/sand/tiles experiment —
// those surfaces can't be made to render on intersections, see the emission
// catalogue comment) and are hidden here. The numeric id order is append-only
// serialization history, not a presentation order.
// 15/17+ — vanilla surfaces revived via VanillaSurfaceLateClone (16 is a retired
// reserved slot, like 7-13).
const AREA_STYLE_VALUES = [0, 14, 1, 2, 3, 4, 5, 6, 15, 17, 18, 19, 20, 21, 22] as const;
const AREA_STYLE_ID_SET: ReadonlySet<number> = new Set(AREA_STYLE_VALUES);

const areaStyleLabel = (t: ReturnType<typeof useT>, styleId: number): string => {
  const key = `areaStyle.${styleId}` as StringKey;
  return AREA_STYLE_ID_SET.has(styleId) ? t(key) : t("style.unknown");
};

// Shared option lists for every style picker (panel rows + in-world popovers).
// `pinned` = the user's favourite ids (usePinnedStyles) — those options float to
// the top in their catalogue order; the rest keep the curated order below.
const sortPinnedFirst = <V,>(opts: DropdownOption<V>[]): DropdownOption<V>[] =>
  [...opts.filter((o) => o.pinned), ...opts.filter((o) => !o.pinned)];

const makeLineStyleOptions = (t: ReturnType<typeof useT>, pinned: number[]): DropdownOption<StyleValue>[] =>
  sortPinnedFirst(
    STYLE_VALUES.map((s) => ({
      value: s,
      label: styleLabel(t, s),
      preview: <LineStylePreview style={s} width={28} height={8} />,
      pinned: pinned.includes(s),
    })),
  );

const makeAreaStyleOptions = (t: ReturnType<typeof useT>, pinned: number[]): DropdownOption<number>[] =>
  sortPinnedFirst(
    AREA_STYLE_VALUES.map((s) => ({
      value: s,
      label: areaStyleLabel(t, s),
      preview: <AreaStylePreview styleId={s} size={12} />,
      pinned: pinned.includes(s),
    })),
  );

// Exported wrapper — boundary first, then real panel. moduleRegistry mounts
// this into GameTopRight, so the boundary protects the game UI from our bugs.
const portfolioBindings = {  };
export const MarkingStudioPanel = () => (
  <PanelErrorBoundary>
    <TooltipProvider>
      <MarkingStudioPanelInner />
    </TooltipProvider>
  </PanelErrorBoundary>
);

// Floating in-world popover anchored at a segment's midpoint. Collapsed it's
// a small state dot (white = visible, red = hidden); hovering expands it into
// the button row — visibility toggle, style cycle, delete-line (C6, two-press
// confirm). One dot per segment keeps a many-segment line from wallpapering
// the world with button rows.
//
// Positioning is IMPERATIVE (Stage 5e): the root registers itself with
// positionRegistry, and the per-frame GetScreenPoints binding writes
// style.left/top/transform directly — camera movement never re-renders React.
// The registry also hides the popover (display:none) while its segment is
// off-screen / behind the camera, and scales it down with camera distance
// (snapping back to full size while hover-expanded).
const POPOVER_DELETE_CONFIRM_MS = 2500;

const SegmentPopover = ({ seg }: { seg: SegmentVM }) => {
  const t = useT();
  const pinned = usePinnedStyles();
  const key = segKey(seg.lineIndex, seg.segmentIndex);
  const [expanded, setExpanded] = useState(false);
  // The style dropdown's menu is portalled to document.body — while it's open
  // the cursor legitimately lives outside PopoverRoot, so the popover must not
  // collapse (that would unmount the dropdown mid-pick).
  const [styleOpen, setStyleOpen] = useState(false);
  const showButtons = expanded || styleOpen;
  // Two-press delete state, local to each popover. First click flips on; second
  // click within the window dispatches. Resets on timeout or when the segment
  // changes (popovers re-render with new keys when the line is rebuilt).
  const [confirmingDelete, setConfirmingDelete] = useState(false);
  useEffect(() => {
    if (!confirmingDelete) return;
    const id = window.setTimeout(() => setConfirmingDelete(false), POPOVER_DELETE_CONFIRM_MS);
    return () => window.clearTimeout(id);
  }, [confirmingDelete]);

  // Mirror the hover-expansion into the registry so it can clamp the
  // camera-distance scale to ≥1 while the buttons are up. The unregister path
  // (ref callback with null) clears the flag on unmount.
  useEffect(() => {
    setAnchorExpanded(key, showButtons);
  }, [key, showButtons]);

  // Portal into document.body — our panel mounts inside GameTopRight which
  // likely has CSS transform/will-change set up by CS2, creating a containing
  // block that pins our `position: fixed` to the slot instead of the viewport.
  // A body portal gives us the true viewport-relative coordinates the
  // Camera.WorldToScreenPoint values were computed against.
  return createPortal(
    <PopoverRoot
      ref={(el: HTMLElement | null) => registerSegmentAnchor(key, el)}
      onMouseEnter={() => {
        setExpanded(true);
        // Per-segment hover (C3): light up THIS segment specifically, not the
        // whole line. The popover's anchored to one segment, so the UX should
        // narrow attention to that segment alone.
        cmdSetHoveredSegment(seg.lineIndex, seg.segmentIndex);
      }}
      onMouseLeave={() => {
        // Cancel pending delete if the user moves away — avoids the confirm
        // state lingering after the user gave up.
        setConfirmingDelete(false);
        // Cursor heading into the portalled style menu also "leaves" the root —
        // keep the row alive while the menu is open (it closes via onOpenChange).
        if (styleOpen) return;
        setExpanded(false);
        cmdSetHoveredSegment(-1, -1);
      }}
    >
      {!showButtons ? (
        <PopoverMarker $hidden={!seg.visible} />
      ) : (
        <>
          <Tooltip
            content={seg.visible ? t("segment.hide.tooltip") : t("segment.show.tooltip")}
          >
            <PopoverBtn
              // $active when the segment is hidden — telegraphs the toggle state at
              // a glance without forcing the user to interpret the icon.
              $active={!seg.visible}
              onClick={() => cmdToggleSegment(seg.lineIndex, seg.segmentIndex)}
            >
              {seg.visible ? <Eye size={14} /> : <EyeOff size={14} />}
            </PopoverBtn>
          </Tooltip>
          <PopoverDropdownWrap>
            <Dropdown
              value={seg.style as StyleValue}
              options={makeLineStyleOptions(t, pinned.lineStyles)}
              onChange={(s) => cmdSetSegmentStyle(seg.lineIndex, seg.segmentIndex, s)}
              onTogglePin={cmdTogglePinLineStyle}
              onOpenChange={(open) => {
                setStyleOpen(open);
                // Menu closed with the cursor possibly over the (portalled)
                // menu, i.e. outside the root — collapse explicitly; hovering
                // the popover again re-expands it.
                if (!open) {
                  setExpanded(false);
                  cmdSetHoveredSegment(-1, -1);
                }
              }}
            />
          </PopoverDropdownWrap>
          <Tooltip
            content={
              confirmingDelete ? t("line.delete.confirm.btn") : t("line.delete")
            }
          >
            <PopoverBtn
              // $active = red-tinted confirm state. Same visual language as the
              // panel's inline DeleteLineButton confirm row.
              $active={confirmingDelete}
              style={
                confirmingDelete
                  ? {
                      background: T.colorDangerSoft,
                      borderColor: T.colorDanger,
                      color: T.colorDanger,
                    }
                  : undefined
              }
              onClick={() => {
                if (confirmingDelete) {
                  cmdDeleteLine(seg.lineIndex);
                  setConfirmingDelete(false);
                } else {
                  setConfirmingDelete(true);
                }
              }}
            >
              <Trash size={14} color={confirmingDelete ? T.colorDanger : undefined} />
            </PopoverBtn>
          </Tooltip>
        </>
      )}
    </PopoverRoot>,
    document.body,
  );
};

// In-world popover for an area, anchored at its polygon centroid (C# sends it
// on the same GetScreenPoints channel under an `area:` key). Same collapsed/
// expanded scheme as SegmentPopover; the collapsed face is the area's fill
// swatch, so the dot itself says which area it is.
const AreaPopover = ({ area }: { area: AreaVM }) => {
  const t = useT();
  const pinned = usePinnedStyles();
  const key = areaKey(area.areaIndex);
  const [expanded, setExpanded] = useState(false);
  // Same contract as SegmentPopover: keep the row mounted while the portalled
  // style menu is open.
  const [styleOpen, setStyleOpen] = useState(false);
  const showButtons = expanded || styleOpen;
  const [confirmingDelete, setConfirmingDelete] = useState(false);
  useEffect(() => {
    if (!confirmingDelete) return;
    const id = window.setTimeout(() => setConfirmingDelete(false), POPOVER_DELETE_CONFIRM_MS);
    return () => window.clearTimeout(id);
  }, [confirmingDelete]);

  useEffect(() => {
    setAnchorExpanded(key, showButtons);
  }, [key, showButtons]);

  return createPortal(
    <PopoverRoot
      ref={(el: HTMLElement | null) => registerSegmentAnchor(key, el)}
      onMouseEnter={() => {
        setExpanded(true);
        // Same hover-bridge as the panel row: outline this area in the world.
        cmdSetHoveredArea(area.areaIndex);
      }}
      onMouseLeave={() => {
        setConfirmingDelete(false);
        if (styleOpen) return;
        setExpanded(false);
        cmdClearHoveredArea(area.areaIndex);
      }}
    >
      {!showButtons ? (
        area.visible ? (
          <AreaStylePreview styleId={area.styleId} size={12} />
        ) : (
          <PopoverMarker $hidden />
        )
      ) : (
        <>
          <Tooltip content={area.visible ? t("area.hide.tooltip") : t("area.show.tooltip")}>
            <PopoverBtn
              $active={!area.visible}
              onClick={() => cmdToggleAreaVisible(area.areaIndex)}
            >
              {area.visible ? <Eye size={14} /> : <EyeOff size={14} />}
            </PopoverBtn>
          </Tooltip>
          <PopoverDropdownWrap>
            <Dropdown
              value={area.styleId}
              options={makeAreaStyleOptions(t, pinned.areaStyles)}
              onChange={(s) => cmdSetAreaStyle(area.areaIndex, s)}
              onTogglePin={cmdTogglePinAreaStyle}
              onOpenChange={(open) => {
                setStyleOpen(open);
                if (!open) {
                  setExpanded(false);
                  cmdClearHoveredArea(area.areaIndex);
                }
              }}
            />
          </PopoverDropdownWrap>
          <Tooltip content={confirmingDelete ? t("line.delete.confirm.btn") : t("area.delete")}>
            <PopoverBtn
              $active={confirmingDelete}
              style={
                confirmingDelete
                  ? {
                      background: T.colorDangerSoft,
                      borderColor: T.colorDanger,
                      color: T.colorDanger,
                    }
                  : undefined
              }
              onClick={() => {
                if (confirmingDelete) {
                  cmdDeleteArea(area.areaIndex);
                  setConfirmingDelete(false);
                } else {
                  setConfirmingDelete(true);
                }
              }}
            >
              <Trash size={14} color={confirmingDelete ? T.colorDanger : undefined} />
            </PopoverBtn>
          </Tooltip>
        </>
      )}
    </PopoverRoot>,
    document.body,
  );
};

// Keyboard reference, collapsed into a one-line foldout by default — a static
// cheat-sheet must not compete with the working area for a third of the panel.
// It opens expanded on the "select a node" card (the panel is otherwise empty
// there, and that's the onboarding moment) and collapsed while editing.
const HotkeysFoldout = ({ defaultOpen = false }: { defaultOpen?: boolean }) => {
  const t = useT();
  const [open, setOpen] = useState(defaultOpen);
  return (
    <HintsBox>
      <FoldoutHeader onClick={() => setOpen(!open)}>
        <LineChevron $open={open}>
          <ChevronRight size={10} />
        </LineChevron>
        <span>{t("hotkeys.title")}</span>
      </FoldoutHeader>
      {open && (
        <>
          <HintRow><HintKey>Ctrl+M</HintKey><span>{t("hotkeys.toggle")}</span></HintRow>
          <HintRow><HintKey>Y</HintKey><span>{t("hotkeys.cycleLine")}</span></HintRow>
          <HintRow><HintKey>A</HintKey><span>{t("hotkeys.areaMode")}</span></HintRow>
          <HintRow><HintKey>U</HintKey><span>{t("hotkeys.cycleArea")}</span></HintRow>
          <HintRow><HintKey>{t("hotkeys.rmb")}</HintKey><span>{t("hotkeys.rmb.desc")}</span></HintRow>
          <HintRow><HintKey>{t("hotkeys.esc")}</HintKey><span>{t("hotkeys.esc.desc")}</span></HintRow>
        </>
      )}
    </HintsBox>
  );
};

// One instruction line tracking the tool's state machine — the panel's answer
// to "what do I do now". Data comes straight from the existing VM fields.
const toolStatus = (t: ReturnType<typeof useT>, state: { toolState: number; areaVertexCount: number }): string => {
  if (state.toolState === TOOL_STATE.AreaPainting) {
    return t("status.paint");
  }
  if (state.toolState === TOOL_STATE.AreaSelecting) {
    return t("status.area", { n: state.areaVertexCount });
  }
  if (state.toolState === TOOL_STATE.SourceSelected) {
    return t("status.line.second");
  }
  return t("status.line.first");
};

const MarkingStudioPanelInner = () => {
  const state = useToolState();
  const t = useT();
  const pinned = usePinnedStyles();
  const [activeTab, setActiveTab] = useState<"DRAW" | "TEMPLATES" | "GENERATOR" | "TRANSFORMS" | "REVIEW">("DRAW");
  const [expandedLine, setExpandedLine] = useState<number>(-1);
  const [expandedArea, setExpandedArea] = useState<number>(-1);
  const [pendingDelete, setPendingDelete] = useState<number>(-1);
  const [linesFolded, setLinesFolded] = useState(false);
  const [areasFolded, setAreasFolded] = useState(false);

  // Template creation local state
  const [newTemplateName, setNewTemplateName] = useState("");
  const [newTemplateCategory, setNewTemplateCategory] = useState("Intersections");
  const [templateSearch, setTemplateSearch] = useState("");

  // Generator local state
  const [genStopBars, setGenStopBars] = useState(true);
  const [genCrosswalks, setGenCrosswalks] = useState(true);
  const [genTurnGuides, setGenTurnGuides] = useState(true);
  const [genIslands, setGenIslands] = useState(true);

  // Transform local state
  const [repeatCount, setRepeatCount] = useState(3);
  const [repeatStep, setRepeatStep] = useState(2.0);

  useEffect(() => {
    if (state.lines.length === 0) {
      setExpandedLine(-1);
    } else if (state.lines.length === 1) {
      setExpandedLine(0);
    } else if (expandedLine >= state.lines.length) {
      setExpandedLine(-1);
    }
  }, [state.selectedNodeIndex, state.lines.length]);

  useEffect(() => {
    if (expandedArea >= state.areas.length) setExpandedArea(-1);
  }, [state.selectedNodeIndex, state.areas.length]);

  useEffect(() => {
    if (state.lastClickedLine >= 0 && state.lastClickedLine < state.lines.length) {
      setExpandedLine(state.lastClickedLine);
    } else if (state.lastClickedTick > 0 && state.lastClickedLine === -1) {
      setExpandedLine(-1);
    }
  }, [state.lastClickedTick]);

  useEffect(() => {
    if (!state.isActive || state.selectedNodeIndex < 0) return;

    const handler = (e: KeyboardEvent) => {
      if (e.ctrlKey && e.key.toLowerCase() === "z") {
        e.preventDefault();
        if (e.shiftKey) cmdRedoMarking();
        else cmdUndoMarking();
        return;
      }
      if (e.ctrlKey && e.key.toLowerCase() === "y") {
        e.preventDefault();
        cmdRedoMarking();
        return;
      }
      if (e.ctrlKey || e.metaKey || e.altKey) return;
      const tag = (e.target as HTMLElement | null)?.tagName?.toUpperCase();
      if (tag === "INPUT" || tag === "TEXTAREA" || tag === "SELECT") return;

      const lineCount = state.lines.length;

      if (e.key === "Tab" && lineCount > 0) {
        e.preventDefault();
        const cur = expandedLine < 0 ? -1 : expandedLine;
        const step = e.shiftKey ? -1 : 1;
        const next = ((cur + step) % lineCount + lineCount) % lineCount;
        setExpandedLine(next);
        setPendingDelete(-1);
        return;
      }

      if (e.key === "Escape") {
        if (pendingDelete >= 0) {
          setPendingDelete(-1);
        } else if (expandedLine >= 0) {
          setExpandedLine(-1);
        }
        return;
      }

      if (e.key === "Enter") {
        if (expandedLine < 0 && lineCount > 0) setExpandedLine(0);
        return;
      }

      if (e.key === "Delete" && expandedLine >= 0) {
        if (pendingDelete === expandedLine) {
          cmdDeleteLine(expandedLine);
          setPendingDelete(-1);
        } else {
          setPendingDelete(expandedLine);
        }
        return;
      }

      if (expandedLine >= 0 && /^[1-5]$/.test(e.key)) {
        const idx = parseInt(e.key, 10) - 1;
        if (idx >= 0 && idx < STYLE_VALUES.length) {
          cmdSetLineStyle(expandedLine, STYLE_VALUES[idx]);
        }
        return;
      }
    };

    document.addEventListener("keydown", handler);
    return () => document.removeEventListener("keydown", handler);
  }, [state.isActive, state.selectedNodeIndex, state.lines.length, expandedLine, pendingDelete]);

  useEffect(() => {
    if (pendingDelete < 0) return;
    const id = window.setTimeout(() => setPendingDelete(-1), 3000);
    return () => window.clearTimeout(id);
  }, [pendingDelete]);

  if (!state.isActive) return null;

  const inAreaMode = state.toolState === TOOL_STATE.AreaSelecting;
  const inPaintMode = state.toolState === TOOL_STATE.AreaPainting;
  const inAnyAreaMode = inAreaMode || inPaintMode;

  if (state.selectedNodeIndex < 0) {
    return (
      <Panel>
        <PanelHeaderRow>
          <PanelTitle>{t("panel.appTitle")}</PanelTitle>
          <Tooltip content={t("panel.close.tooltip")}>
            <CloseBtn onClick={() => cmdActivateTool()}>
              <Cross size={10} />
            </CloseBtn>
          </Tooltip>
        </PanelHeaderRow>
        <div style={{ textAlign: "center", padding: "28rem 16rem" }}>
          <div style={{ marginBottom: "8rem", fontSize: "11rem", fontWeight: 700, letterSpacing: "1rem", color: T.colorAccent }}>MARKING STUDIO</div>
          <h3 style={{ margin: "0 0 6rem 0", fontSize: "14rem", fontWeight: "bold", color: "#f1f5f9" }}>
            Select a Road or Junction
          </h3>
          <p style={{ margin: "0 0 16rem 0", fontSize: "12rem", color: "#94a3b8", lineHeight: 1.4 }}>
            Click anywhere on the road network to customize lane dividers, edge lines, painted islands, or generate realistic markings automatically.
          </p>
          <Btn $full style={{ background: T.colorAccent }} onClick={() => cmdActivateTool()}>
            <span>Select Junction</span>
          </Btn>
        </div>
        <HotkeysFoldout defaultOpen={false} />
      </Panel>
    );
  }

  const popoverLine =
    expandedLine >= 0 && expandedLine < state.lines.length ? state.lines[expandedLine] : null;
  const popoverArea = state.areas.find((a) => a.areaIndex === expandedArea) ?? null;

  const lineStyleOptions = makeLineStyleOptions(t, pinned.lineStyles);
  const areaStyleOptions = makeAreaStyleOptions(t, pinned.areaStyles);

  const filteredTemplates = state.templates.filter((tp) => {
    if (!templateSearch) return true;
    const q = templateSearch.toLowerCase();
    return tp.name.toLowerCase().includes(q) || tp.category.toLowerCase().includes(q);
  });

  return (
    <>
      <Panel>
        <PortfolioHelp runtimeGroup={"Portfolio.MarkingStudio"} name={"Magic Marking"} version={"2.4.1-beta.1"} steps={["Activate the marking tool and select a road or junction.", "Choose a line or area style and inspect the drawing preview.", "Use Undo/Redo to recover from a change."]} note={"Enable Magic Marking or Town Road Lane, not both. Road markings do not change traffic rules."} bindings={portfolioBindings} />
      <PanelStickyChrome>
          <PanelHeaderRow>
            <PanelTitle>{t("panel.appTitle")}</PanelTitle>
            <Tooltip content={t("panel.close.tooltip")}>
              <CloseBtn onClick={() => cmdActivateTool()}>
                <Cross size={10} />
              </CloseBtn>
            </Tooltip>
          </PanelHeaderRow>
          <StatusRow>
            <StatusDot />
            <span>{toolStatus(t, state)}</span>
          </StatusRow>

          {/* Quick Undo / Redo Toolbar */}
          <ToolbarRow>
            <div style={{ display: "flex" }}>
              <Tooltip content="Undo last change (Ctrl+Z)">
                <Btn
                  style={{ marginRight: T.space1 }}
                  onClick={cmdUndoMarking}
                  disabled={state.undoCount === 0}
                >
                  <Cycle size={12} />
                  <span>Undo ({state.undoCount})</span>
                </Btn>
              </Tooltip>
              <Tooltip content="Redo change (Ctrl+Y)">
                <Btn
                  onClick={cmdRedoMarking}
                  disabled={state.redoCount === 0}
                >
                  <Cycle size={12} />
                  <span>Redo ({state.redoCount})</span>
                </Btn>
              </Tooltip>
            </div>
            <NodeIdText>Node #{state.selectedNodeIndex}</NodeIdText>
          </ToolbarRow>

          {/* 5-Tab Bar */}
          <TabBar>
            <TabButton $active={activeTab === "DRAW"} onClick={() => setActiveTab("DRAW")}>
              Draw
            </TabButton>
            <TabButton $active={activeTab === "GENERATOR"} onClick={() => setActiveTab("GENERATOR")}>
              Auto Generate
            </TabButton>
            <TabButton $active={activeTab === "TEMPLATES"} onClick={() => setActiveTab("TEMPLATES")}>
              Presets ({state.templates.length})
            </TabButton>
            <TabButton $active={activeTab === "TRANSFORMS"} onClick={() => setActiveTab("TRANSFORMS")}>
              Tools
            </TabButton>
            <TabButton $active={activeTab === "REVIEW"} onClick={() => setActiveTab("REVIEW")}>
              Review {state.reviewCount > 0 && <Badge $danger>{state.reviewCount}</Badge>}
            </TabButton>
          </TabBar>

          {/* TAB 1: DRAW MODE */}
          {activeTab === "DRAW" && (
            <>
              <SectionTitle>{t("section.drawing")}</SectionTitle>
              <ModeRow>
                <Tooltip content={t("mode.lines.tooltip")}>
                  <ModeBtn
                    $active={!inAnyAreaMode}
                    onClick={() => {
                      if (inAreaMode) cmdToggleAreaMode();
                      if (inPaintMode) cmdToggleAreaPaintMode();
                    }}
                  >
                    {t("mode.lines")}
                  </ModeBtn>
                </Tooltip>
                <Tooltip content={t("mode.area.tooltip")}>
                  <ModeBtn
                    $active={inAreaMode}
                    onClick={() => {
                      if (inPaintMode) cmdToggleAreaPaintMode();
                      if (!inAreaMode) cmdToggleAreaMode();
                    }}
                  >
                    {t("mode.area")}
                  </ModeBtn>
                </Tooltip>
                <Tooltip content={t("mode.paint.tooltip")}>
                  <ModeBtn
                    $active={inPaintMode}
                    onClick={() => {
                      if (inAreaMode) cmdToggleAreaMode();
                      if (!inPaintMode) cmdToggleAreaPaintMode();
                    }}
                  >
                    {t("mode.paint")}
                  </ModeBtn>
                </Tooltip>
              </ModeRow>

              {inAnyAreaMode ? (
                <>
                  <FieldRow>
                    <Tooltip content={t("next.areaStyle.tooltip")}>
                      <FieldLabel>{t("next.areaStyle")}</FieldLabel>
                    </Tooltip>
                    <Dropdown
                      value={state.currentAreaStyle}
                      options={areaStyleOptions}
                      onChange={(s) => cmdSetCurrentAreaStyle(s)}
                      onTogglePin={cmdTogglePinAreaStyle}
                    />
                  </FieldRow>
                  <DraftBox>
                    {inPaintMode ? (
                      <>
                        <DraftHint>{t("paint.hint.hover")}</DraftHint>
                        <DraftHint>{t("paint.hint.click")}</DraftHint>
                        <Btn $full onClick={() => cmdToggleAreaPaintMode()}>
                          <span>{t("area.draft.cancel")}</span>
                        </Btn>
                      </>
                    ) : (
                      <>
                        <DraftHint>{t("area.draft.hint.add")}</DraftHint>
                        <DraftHint>{t("area.draft.hint.undo")}</DraftHint>
                        <DraftHint>{t("area.draft.hint.close")}</DraftHint>
                        <Btn $full onClick={() => cmdToggleAreaMode()}>
                          <span>{t("area.draft.cancel")}</span>
                        </Btn>
                      </>
                    )}
                  </DraftBox>
                </>
              ) : (
                <>
                  <FieldRow>
                    <Tooltip content={t("next.lineStyle.tooltip")}>
                      <FieldLabel>{t("next.lineStyle")}</FieldLabel>
                    </Tooltip>
                    <Dropdown
                      value={state.currentStyle as StyleValue}
                      options={lineStyleOptions}
                      onChange={(s) => cmdSetCurrentStyle(s)}
                      onTogglePin={cmdTogglePinLineStyle}
                    />
                  </FieldRow>
                  <FieldRow style={{ marginTop: "6rem" }}>
                    <FieldLabel>Alignment</FieldLabel>
                    <div style={{ display: "flex", gap: "4rem" }}>
                      <Tooltip content="Follow authoritative road and lane curve geometry (Default)">
                        <Btn
                          style={{
                            padding: "3rem 8rem",
                            fontSize: T.fontSizeXs,
                            background: state.currentDrawingMode === DRAWING_MODE.FollowRoad ? T.colorAccent : undefined,
                          }}
                          onClick={() => cmdSetCurrentDrawingMode(DRAWING_MODE.FollowRoad)}
                        >
                          Follow Road
                        </Btn>
                      </Tooltip>
                      <Tooltip content="Direct straight chord between endpoints">
                        <Btn
                          style={{
                            padding: "3rem 8rem",
                            fontSize: T.fontSizeXs,
                            background: state.currentDrawingMode === DRAWING_MODE.Straight ? T.colorAccent : undefined,
                          }}
                          onClick={() => cmdSetCurrentDrawingMode(DRAWING_MODE.Straight)}
                        >
                          Straight
                        </Btn>
                      </Tooltip>
                      <Tooltip content="Custom curvature with manual pull & bulge">
                        <Btn
                          style={{
                            padding: "3rem 8rem",
                            fontSize: T.fontSizeXs,
                            background: state.currentDrawingMode === DRAWING_MODE.CustomCurve ? T.colorAccent : undefined,
                          }}
                          onClick={() => cmdSetCurrentDrawingMode(DRAWING_MODE.CustomCurve)}
                        >
                          Custom
                        </Btn>
                      </Tooltip>
                    </div>
                  </FieldRow>
                  <div
                    style={{
                      display: "flex",
                      alignItems: "center",
                      justifyContent: "space-between",
                      padding: "4rem 8rem",
                      marginTop: "6rem",
                      background: "rgba(15, 23, 42, 0.65)",
                      border: "1rem solid rgba(255, 255, 255, 0.12)",
                      borderRadius: T.radiusSm,
                      fontSize: T.fontSizeXs,
                    }}
                  >
                    <span style={{ color: T.colorTextMuted }}>Snap Target:</span>
                    <span
                      style={{
                        fontWeight: T.fontWeightBold,
                        color:
                          state.snapTargetKind === "LaneBoundary"
                            ? "#38bdf8"
                            : state.snapTargetKind === "RoadEdge"
                            ? "#fb923c"
                            : state.snapTargetKind === "MedianEdge"
                            ? "#facc15"
                            : state.snapTargetKind === "MarkingEndpoint"
                            ? "#4ade80"
                            : state.snapTargetKind === "LineIntersection"
                            ? "#f472b6"
                            : "#94a3b8",
                      }}
                    >
                      {state.snapTargetDescription || "Free Position"}
                    </span>
                  </div>
                </>
              )}
            </>
          )}

          {/* TAB 2: TEMPLATES & PRESETS */}
          {activeTab === "TEMPLATES" && (
            <>
              <SectionTitle>Marking Templates</SectionTitle>
              <input
                type="text"
                placeholder="Search templates..."
                value={templateSearch}
                onChange={(e) => setTemplateSearch(e.target.value)}
                style={{
                  width: "100%",
                  padding: "4rem 8rem",
                  marginBottom: T.space2,
                  background: T.colorSurfaceSolid,
                  color: T.colorTextPrimary,
                  border: "1rem solid " + T.colorBorderSoft,
                  borderRadius: T.radiusSm,
                  boxSizing: "border-box",
                }}
              />
              <div style={{ maxHeight: "220rem", overflowY: "auto", marginBottom: T.space2 }}>
                {filteredTemplates.map((tp) => (
                  <PresetCard key={tp.id}>
                    <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "4rem" }}>
                      <span style={{ fontWeight: T.fontWeightBold, fontSize: T.fontSizeSm }}>{tp.name}</span>
                      <span style={{ fontSize: T.fontSizeXs, color: T.colorTextMuted }}>{tp.category}</span>
                    </div>
                    <div style={{ fontSize: T.fontSizeXs, color: T.colorTextMuted, marginBottom: "8rem" }}>
                      {tp.description}
                    </div>
                    <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                      <span style={{ fontSize: T.fontSizeXs, color: T.colorTextMuted }}>
                        {tp.lineCount} lines · {tp.areaCount} areas
                      </span>
                      <div style={{ display: "flex" }}>
                        <Btn
                          style={{ marginRight: "4rem", padding: "2rem 6rem" }}
                          onClick={() => cmdToggleTemplateFavorite(tp.id)}
                        >
                          <span>{tp.isFavorite ? "★" : "☆"}</span>
                        </Btn>
                        <Btn
                          style={{ background: T.colorAccent, padding: "2rem 10rem" }}
                          onClick={() => cmdApplyTemplate(tp.id)}
                        >
                          <span>Apply</span>
                        </Btn>
                      </div>
                    </div>
                  </PresetCard>
                ))}
              </div>
              <SectionTitle>Save Current as Template</SectionTitle>
              <FieldRow>
                <FieldLabel>Name</FieldLabel>
                <input
                  type="text"
                  placeholder="My Custom Preset"
                  value={newTemplateName}
                  onChange={(e) => setNewTemplateName(e.target.value)}
                  style={{
                    flex: 1,
                    padding: "3rem 6rem",
                    background: T.colorSurfaceSolid,
                    color: T.colorTextPrimary,
                    border: "1rem solid " + T.colorBorderSoft,
                    borderRadius: T.radiusSm,
                  }}
                />
              </FieldRow>
              <Btn
                $full
                onClick={() => {
                  if (newTemplateName.trim()) {
                    cmdSaveTemplate(newTemplateName.trim(), newTemplateCategory, "User created template.");
                    setNewTemplateName("");
                  }
                }}
              >
                <span>Save Template</span>
              </Btn>
            </>
          )}

          {/* TAB 3: PROCEDURAL GENERATOR */}
          {activeTab === "GENERATOR" && (
            <>
              <SectionTitle>Procedural Junction Markings</SectionTitle>
              <div style={{ fontSize: T.fontSizeXs, color: T.colorTextMuted, marginBottom: T.space2 }}>
                Analyzes intersection geometry and generates editable marking primitives.
              </div>
              <ToggleRow>
                <FieldLabel>Stop Bars / Yield Lines</FieldLabel>
                <input
                  type="checkbox"
                  checked={genStopBars}
                  onChange={(e) => setGenStopBars(e.target.checked)}
                />
              </ToggleRow>
              <ToggleRow>
                <FieldLabel>Crosswalk Boundaries</FieldLabel>
                <input
                  type="checkbox"
                  checked={genCrosswalks}
                  onChange={(e) => setGenCrosswalks(e.target.checked)}
                />
              </ToggleRow>
              <ToggleRow>
                <FieldLabel>Turn Guides (Dashed Curves)</FieldLabel>
                <input
                  type="checkbox"
                  checked={genTurnGuides}
                  onChange={(e) => setGenTurnGuides(e.target.checked)}
                />
              </ToggleRow>
              <ToggleRow>
                <FieldLabel>Median Island Fills</FieldLabel>
                <input
                  type="checkbox"
                  checked={genIslands}
                  onChange={(e) => setGenIslands(e.target.checked)}
                />
              </ToggleRow>
              <Btn
                $full
                style={{ background: T.colorAccent, marginTop: T.space2 }}
                onClick={() => cmdGenerateJunctionMarkings(genStopBars, genCrosswalks, genTurnGuides, genIslands)}
              >
                <span>Generate Junction Markings</span>
              </Btn>
            </>
          )}

          {/* TAB 4: TRANSFORMS & TOOLS */}
          {activeTab === "TRANSFORMS" && (
            <>
              <SectionTitle>Selection & Transforms</SectionTitle>
              <div style={{ display: "flex", justifyContent: "space-between", marginBottom: T.space2 }}>
                <Btn style={{ flex: 1, marginRight: T.space1 }} onClick={cmdSelectAll}>
                  <span>Select All</span>
                </Btn>
                <Btn style={{ flex: 1 }} onClick={cmdClearSelection}>
                  <span>Clear</span>
                </Btn>
              </div>
              <div style={{ display: "flex", justifyContent: "space-between", marginBottom: T.space2 }}>
                <Btn style={{ flex: 1, marginRight: T.space1 }} onClick={cmdCopySelection}>
                  <span>Copy</span>
                </Btn>
                <Btn style={{ flex: 1 }} onClick={cmdPasteSelection}>
                  <span>Paste</span>
                </Btn>
              </div>
              <div style={{ display: "flex", justifyContent: "space-between", marginBottom: T.space2 }}>
                <Btn style={{ flex: 1, marginRight: T.space1 }} onClick={cmdMirrorSelection}>
                  <span>Mirror H/V</span>
                </Btn>
                <Btn style={{ flex: 1 }} onClick={cmdInvertCurvature}>
                  <span>Invert Curve</span>
                </Btn>
              </div>
              <SectionTitle>Repeat Pattern</SectionTitle>
              <FieldRow>
                <FieldLabel>Count</FieldLabel>
                <input
                  type="number"
                  min="1"
                  max="10"
                  value={repeatCount}
                  onChange={(e) => setRepeatCount(Math.max(1, parseInt(e.target.value, 10) || 1))}
                  style={{ width: "50rem", padding: "3rem", background: T.colorSurfaceSolid, color: T.colorTextPrimary }}
                />
              </FieldRow>
              <Btn $full onClick={() => cmdRepeatSelection(repeatCount, repeatStep)}>
                <span>Repeat Placement ({repeatCount}x)</span>
              </Btn>
            </>
          )}

          {/* TAB 5: ROAD REBUILD REVIEW */}
          {activeTab === "REVIEW" && (
            <>
              <SectionTitle>Adaptive Road Remap Review</SectionTitle>
              {state.remapReviews.length === 0 ? (
                <div style={{ fontSize: T.fontSizeSm, color: T.colorTextMuted, padding: T.space2 }}>
                  All road markings are currently synchronized with road network geometry.
                </div>
              ) : (
                <div style={{ maxHeight: "240rem", overflowY: "auto" }}>
                  {state.remapReviews.map((rev) => (
                    <PresetCard key={`${rev.nodeIndex}-${rev.markingId}`}>
                      <div style={{ display: "flex", justifyContent: "space-between", marginBottom: "4rem" }}>
                        <span style={{ fontWeight: T.fontWeightBold, fontSize: T.fontSizeSm }}>
                          Marking #{rev.markingId} (Node #{rev.nodeIndex})
                        </span>
                        <Badge $danger={rev.status === "Ambiguous"}>{rev.status}</Badge>
                      </div>
                      <div style={{ fontSize: T.fontSizeXs, color: T.colorTextMuted, marginBottom: "8rem" }}>
                        {rev.description} (Deviation: {rev.deviationMeters.toFixed(2)}m)
                      </div>
                      <div style={{ display: "flex", justifyContent: "flex-end" }}>
                        <Btn
                          style={{ marginRight: "4rem" }}
                          onClick={() => cmdDeleteRemap(rev.markingId)}
                          $danger
                        >
                          <span>Delete</span>
                        </Btn>
                        <Btn
                          style={{ background: T.colorAccent }}
                          onClick={() => cmdAcceptRemap(rev.markingId)}
                        >
                          <span>Accept Remap</span>
                        </Btn>
                      </div>
                    </PresetCard>
                  ))}
                </div>
              )}
            </>
          )}

        </PanelStickyChrome>

        {activeTab === "DRAW" && state.lines.length > 0 && (
          <>
            <FoldoutHeader onClick={() => setLinesFolded(!linesFolded)}>
              <LineChevron $open={!linesFolded}>
                <ChevronRight size={10} />
              </LineChevron>
              <span>{`${t("section.lines")} · ${state.lines.length}`}</span>
            </FoldoutHeader>
            <PanelList>
              {state.lines.map((line) => {
                if (
                  linesFolded &&
                  expandedLine !== line.lineIndex &&
                  state.hoveredLineInGame !== line.lineIndex
                )
                  return null;
                return (
                  <LineRow
                    key={line.lineIndex}
                    line={line}
                    isExpanded={expandedLine === line.lineIndex}
                    isGameHovered={state.hoveredLineInGame === line.lineIndex}
                    isPendingDelete={pendingDelete === line.lineIndex}
                    onToggleExpand={() =>
                      setExpandedLine(expandedLine === line.lineIndex ? -1 : line.lineIndex)
                    }
                    onCancelPendingDelete={() => setPendingDelete(-1)}
                  />
                );
              })}
            </PanelList>
          </>
        )}

        {activeTab === "DRAW" && state.areas.length > 0 && (
          <>
            <FoldoutHeader onClick={() => setAreasFolded(!areasFolded)}>
              <LineChevron $open={!areasFolded}>
                <ChevronRight size={10} />
              </LineChevron>
              <span>{`${t("section.areas")} · ${state.areas.length}`}</span>
            </FoldoutHeader>
            <PanelList>
              {state.areas.map((area) => {
                if (
                  areasFolded &&
                  expandedArea !== area.areaIndex &&
                  state.hoveredAreaInGame !== area.areaIndex
                )
                  return null;
                return (
                  <AreaRow
                    key={area.areaIndex}
                    area={area}
                    isExpanded={expandedArea === area.areaIndex}
                    isGameHovered={state.hoveredAreaInGame === area.areaIndex}
                    areaStyleOptions={areaStyleOptions}
                    onToggleExpand={() =>
                      setExpandedArea(expandedArea === area.areaIndex ? -1 : area.areaIndex)
                    }
                  />
                );
              })}
            </PanelList>
          </>
        )}

        {activeTab === "DRAW" && (
          <>
            <SectionTitle>{t("section.node")}</SectionTitle>
            <ToggleRow>
              <FieldLabel>{t("node.vanilla.label")}</FieldLabel>
              <Tooltip content={t("vanilla.tooltip")}>
                <IconToggleBtn
                  $active={state.vanillaHidden}
                  onClick={cmdToggleVanillaMarkings}
                >
                  {state.vanillaHidden ? <EyeOff size={14} /> : <Eye size={14} />}
                </IconToggleBtn>
              </Tooltip>
            </ToggleRow>
            <ResetNodeButton />
          </>
        )}

        <Tooltip content={t("node.autoGenerate.tooltip")}>
          <Btn $full style={{ marginTop: T.space2 }} onClick={cmdAutoGenerateMarkings}>
            <span>{t("node.autoGenerate")}</span>
          </Btn>
        </Tooltip>
        
        <NodeIdText>{t("panel.title", { n: state.selectedNodeIndex })}</NodeIdText>

        <HotkeysFoldout />
      </Panel>
      {!inAnyAreaMode && popoverLine?.segments.map((seg) => (
        <SegmentPopover
          key={`pop-${seg.lineIndex}-${seg.segmentIndex}`}
          seg={seg}
        />
      ))}
      {!inAnyAreaMode && popoverArea && (
        <AreaPopover key={`pop-area-${popoverArea.areaIndex}`} area={popoverArea} />
      )}
    </>
  );
};

const LineRow = ({
  line,
  isExpanded,
  isGameHovered,
  isPendingDelete,
  onToggleExpand,
  onCancelPendingDelete,
}: {
  line: LineVM;
  isExpanded: boolean;
  isGameHovered: boolean;
  isPendingDelete: boolean;
  onToggleExpand: () => void;
  onCancelPendingDelete: () => void;
}) => {
  const t = useT();
  const visibleCount = line.segments.filter((s) => s.visible).length;
  return (
    <LineRowOuter
      $expanded={isExpanded}
      $gameHovered={isGameHovered}
      onMouseEnter={() => cmdSetHoveredLine(line.lineIndex)}
      onMouseLeave={() => cmdSetHoveredLine(-1)}
    >
      <LineHeader onClick={onToggleExpand}>
        <LineChevron $open={isExpanded}>
          <ChevronRight size={10} />
        </LineChevron>
        {/* 1-based for humans; commands keep the raw index. */}
        <LineTitle>{t("line.title", { n: line.lineIndex + 1 })}</LineTitle>
        <Tooltip content={styleLabel(t, line.style)}>
          <SwatchWrap>
            <LineStylePreview style={line.style} width={28} height={8} />
            {isG87LineStyle(line.style) && <G87Mark>G87</G87Mark>}
          </SwatchWrap>
        </Tooltip>
        <LineSegCount>
          {t("line.segCount", { visible: visibleCount, total: line.segments.length })}
        </LineSegCount>
      </LineHeader>
      <LineBody $open={isExpanded}>
        <StyleSelector line={line} />
        <CurvatureInput line={line} />
        {line.segments.map((seg) => (
          <SegmentRowComponent key={`${seg.lineIndex}-${seg.segmentIndex}`} seg={seg} />
        ))}
        <DeleteLineButton
          lineIndex={line.lineIndex}
          keyboardConfirming={isPendingDelete}
          onKeyboardCancel={onCancelPendingDelete}
        />
      </LineBody>
    </LineRowOuter>
  );
};

// Area accordion row — mirrors LineRow's layout so the two lists read as one
// visual system. Body controls: fill-style dropdown, visibility toggle, and a
// two-stage delete (same confirm pattern as lines).
const AreaRow = ({
  area,
  isExpanded,
  isGameHovered,
  areaStyleOptions,
  onToggleExpand,
}: {
  area: AreaVM;
  isExpanded: boolean;
  isGameHovered: boolean;
  areaStyleOptions: DropdownOption<number>[];
  onToggleExpand: () => void;
}) => {
  const t = useT();
  const [confirmingDelete, setConfirmingDelete] = useState(false);
  useEffect(() => {
    if (!confirmingDelete) return;
    const id = window.setTimeout(() => setConfirmingDelete(false), 3000);
    return () => window.clearTimeout(id);
  }, [confirmingDelete]);

  return (
    <LineRowOuter
      $expanded={isExpanded}
      $gameHovered={isGameHovered}
      onMouseEnter={() => cmdSetHoveredArea(area.areaIndex)}
      onMouseLeave={() => cmdClearHoveredArea(area.areaIndex)}
    >
      <LineHeader onClick={onToggleExpand}>
        <LineChevron $open={isExpanded}>
          <ChevronRight size={10} />
        </LineChevron>
        {/* 1-based for humans; commands keep the raw index. */}
        <LineTitle>{t("area.title", { n: area.areaIndex + 1 })}</LineTitle>
        <Tooltip content={areaStyleLabel(t, area.styleId)}>
          <SwatchWrap>
            <AreaStylePreview styleId={area.styleId} size={12} />
          </SwatchWrap>
        </Tooltip>
        <LineSegCount>
          {area.pieceCount > 1
            ? t("area.pieces", { visible: area.visiblePieces, total: area.pieceCount })
            : t("area.meta.vertices", { n: area.vertexCount })}
        </LineSegCount>
      </LineHeader>
      <LineBody $open={isExpanded}>
        <FieldRow style={{ marginTop: 0 }}>
          <FieldLabel>{t("area.style")}</FieldLabel>
          <Dropdown
            value={area.styleId}
            options={areaStyleOptions}
            onChange={(s) => cmdSetAreaStyle(area.areaIndex, s)}
            onTogglePin={cmdTogglePinAreaStyle}
          />
        </FieldRow>
        <Tooltip content={area.visible ? t("area.hide.tooltip") : t("area.show.tooltip")}>
          <Btn $full onClick={() => cmdToggleAreaVisible(area.areaIndex)}>
            {area.visible ? <Eye size={12} /> : <EyeOff size={12} />}
            <span>{area.visible ? t("area.hide.tooltip") : t("area.show.tooltip")}</span>
          </Btn>
        </Tooltip>
        {!confirmingDelete ? (
          <Btn $danger $full onClick={() => setConfirmingDelete(true)}>
            <Trash size={12} color={T.colorDanger} />
            <span>{t("area.delete")}</span>
          </Btn>
        ) : (
          <ConfirmRow>
            <Btn onClick={() => setConfirmingDelete(false)}>
              <span>{t("line.delete.cancel")}</span>
            </Btn>
            <Btn $danger onClick={() => { cmdDeleteArea(area.areaIndex); setConfirmingDelete(false); }}>
              <Trash size={12} color={T.colorDanger} />
              <span>{t("line.delete.confirm.btn")}</span>
            </Btn>
          </ConfirmRow>
        )}
      </LineBody>
    </LineRowOuter>
  );
};

// Full node reset — wipes every line, area and the vanilla-hide override on
// the selected node, restoring stock markings. Destructive and node-wide, so
// it gets the same two-stage confirm as deletes and hides at the very bottom
// of the panel (rendered only when there is actually something to reset).
const ResetNodeButton = () => {
  const t = useT();
  const [confirming, setConfirming] = useState(false);
  useEffect(() => {
    if (!confirming) return;
    const id = window.setTimeout(() => setConfirming(false), 3000);
    return () => window.clearTimeout(id);
  }, [confirming]);

  if (!confirming) {
    return (
      <Tooltip content={t("node.reset.tooltip")}>
        <Btn $danger $full onClick={() => setConfirming(true)}>
          <Trash size={12} color={T.colorDanger} />
          <span>{t("node.reset")}</span>
        </Btn>
      </Tooltip>
    );
  }
  return (
    <ConfirmRow>
      <Btn onClick={() => setConfirming(false)}>
        <span>{t("line.delete.cancel")}</span>
      </Btn>
      <Btn $danger onClick={() => { cmdResetNode(); setConfirming(false); }}>
        <Trash size={12} color={T.colorDanger} />
        <span>{t("line.delete.confirm.btn")}</span>
      </Btn>
    </ConfirmRow>
  );
};

// Two-stage delete button (B1): first click flips to a confirm row, second
// click on Delete actually deletes. Cancel aborts. Auto-resets to idle after
// 3 seconds of inactivity so a half-pressed confirm doesn't sit forever.
//
// Why inline (not a modal): cohtml's overlay positioning is fragile, and a
// modal blocks the rest of the panel for a destructive action that's already
// rare. Inline keeps the user's context (they can still see the line they're
// about to delete in the segment list above).
const DELETE_CONFIRM_TIMEOUT_MS = 3000;

const DeleteLineButton = ({
  lineIndex,
  keyboardConfirming,
  onKeyboardCancel,
}: {
  lineIndex: number;
  keyboardConfirming: boolean;
  onKeyboardCancel: () => void;
}) => {
  const t = useT();
  // Local confirming state for mouse interactions; the keyboard-driven flow
  // (B2) flips through the parent via keyboardConfirming. Either source puts
  // the button into the confirm row.
  const [mouseConfirming, setMouseConfirming] = useState(false);
  const confirming = mouseConfirming || keyboardConfirming;

  // Mouse-confirming auto-resets after the timeout. Keyboard-confirming is
  // owned by the parent and reset by its own timeout / Esc handler, so we
  // don't touch it here — just clear our local copy.
  useEffect(() => {
    if (!mouseConfirming) return;
    const id = window.setTimeout(() => setMouseConfirming(false), DELETE_CONFIRM_TIMEOUT_MS);
    return () => window.clearTimeout(id);
  }, [mouseConfirming]);

  const cancel = () => {
    setMouseConfirming(false);
    if (keyboardConfirming) onKeyboardCancel();
  };

  if (!confirming) {
    return (
      <Btn $danger $full onClick={() => setMouseConfirming(true)}>
        <Trash size={12} color={T.colorDanger} />
        <span>{t("line.delete")}</span>
      </Btn>
    );
  }

  return (
    <ConfirmRow>
      <Btn onClick={cancel}>
        <span>{t("line.delete.cancel")}</span>
      </Btn>
      <Btn $danger onClick={() => cmdDeleteLine(lineIndex)}>
        <Trash size={12} color={T.colorDanger} />
        <span>{t("line.delete.confirm.btn")}</span>
      </Btn>
    </ConfirmRow>
  );
};

// Curvature input — exact percent over the C#-side pull factor [0, 0.8].
// 0% = straight chord, 50% = default arc (0.4), 100% = maximum roundness.
// A plain text field (range sliders don't function in CS2's cohtml): digits
// only, commit on Enter or blur, clamped to [0, 100]. While the user types,
// the draft string owns the field; otherwise it mirrors the C# value. The
// reset button resets to default arc.
const CurvatureInput = ({ line }: { line: LineVM }) => {
  const t = useT();
  const [draft, setDraft] = useState<string | null>(null);

  const commitCurv = () => {
    if (draft === null) return;
    const v = parseInt(draft, 10);
    if (!isNaN(v)) {
      cmdSetLineCurvature(line.lineIndex, Math.max(0, Math.min(100, v)));
    }
    setDraft(null);
  };

  const stepCurv = (dir: 1 | -1, e: ReactMouseEvent<HTMLButtonElement>) => {
    const mag = e.shiftKey ? 10 : e.ctrlKey ? 5 : 1;
    const parsed = draft !== null ? parseInt(draft, 10) : NaN;
    const base = !isNaN(parsed) ? parsed : line.curv;
    setDraft(null);
    cmdSetLineCurvature(line.lineIndex, Math.max(0, Math.min(100, base + dir * mag)));
  };

  const stepOffset = (dir: 1 | -1, e: ReactMouseEvent<HTMLButtonElement>) => {
    const mag = e.shiftKey ? 0.5 : 0.1;
    const current = typeof line.lateralOffset === "number" ? line.lateralOffset : 0;
    const next = Math.round((current + dir * mag) * 100) / 100;
    cmdSetLineLateralOffset(line.lineIndex, Math.max(-5.0, Math.min(5.0, next)));
  };

  const mode = line.drawingMode ?? 0;

  return (
    <div style={{ marginBottom: T.space2 }}>
      <FieldRow style={{ marginBottom: "6rem" }}>
        <FieldLabel>Alignment</FieldLabel>
        <div style={{ display: "flex", gap: "4rem" }}>
          <Btn
            style={{
              padding: "2rem 6rem",
              fontSize: T.fontSizeXs,
              background: mode === DRAWING_MODE.FollowRoad ? T.colorAccent : undefined,
            }}
            onClick={() => cmdSetLineDrawingMode(line.lineIndex, DRAWING_MODE.FollowRoad)}
          >
            Follow Road
          </Btn>
          <Btn
            style={{
              padding: "2rem 6rem",
              fontSize: T.fontSizeXs,
              background: mode === DRAWING_MODE.Straight ? T.colorAccent : undefined,
            }}
            onClick={() => cmdSetLineDrawingMode(line.lineIndex, DRAWING_MODE.Straight)}
          >
            Straight
          </Btn>
          <Btn
            style={{
              padding: "2rem 6rem",
              fontSize: T.fontSizeXs,
              background: mode === DRAWING_MODE.CustomCurve ? T.colorAccent : undefined,
            }}
            onClick={() => cmdSetLineDrawingMode(line.lineIndex, DRAWING_MODE.CustomCurve)}
          >
            Custom
          </Btn>
        </div>
      </FieldRow>

      <CurvRow>
        <Tooltip content={t("line.curvature.tooltip")}>
          <CurvLabel>{t("line.curvature")}</CurvLabel>
        </Tooltip>
        <Tooltip content={t("line.curvature.step")}>
          <CurvStepBtn onClick={(e: ReactMouseEvent<HTMLButtonElement>) => stepCurv(-1, e)}>−</CurvStepBtn>
        </Tooltip>
        <CurvInput
          type="text"
          value={draft ?? String(line.curv)}
          onChange={(e: ChangeEvent<HTMLInputElement>) =>
            setDraft(e.target.value.replace(/[^0-9]/g, "").slice(0, 3))
          }
          onBlur={commitCurv}
          onKeyDown={(e: ReactKeyboardEvent<HTMLInputElement>) => {
            if (e.key === "Enter") commitCurv();
          }}
        />
        <Tooltip content={t("line.curvature.step")}>
          <CurvStepBtn onClick={(e: ReactMouseEvent<HTMLButtonElement>) => stepCurv(1, e)}>+</CurvStepBtn>
        </Tooltip>
        <CurvUnit>%</CurvUnit>
        <Tooltip content="Snap Back to Road Curvature">
          <Btn
            style={{ padding: "2rem 6rem", marginLeft: "4rem", fontSize: T.fontSizeXs }}
            onClick={() => cmdSnapLineToRoad(line.lineIndex)}
          >
            <span>Snap to Road</span>
          </Btn>
        </Tooltip>
      </CurvRow>

      <CurvRow style={{ marginTop: "4rem" }}>
        <CurvLabel>Offset</CurvLabel>
        <CurvStepBtn onClick={(e: ReactMouseEvent<HTMLButtonElement>) => stepOffset(-1, e)}>−</CurvStepBtn>
        <CurvInput
          type="text"
          readOnly
          value={(line.lateralOffset ?? 0).toFixed(2)}
        />
        <CurvStepBtn onClick={(e: ReactMouseEvent<HTMLButtonElement>) => stepOffset(1, e)}>+</CurvStepBtn>
        <CurvUnit>m</CurvUnit>
        {line.lateralOffset !== 0 && (
          <Tooltip content="Reset Lateral Offset">
            <CurvResetBtn onClick={() => cmdSetLineLateralOffset(line.lineIndex, 0)}>
              <Cycle size={12} />
            </CurvResetBtn>
          </Tooltip>
        )}
      </CurvRow>
    </div>
  );
};

// Custom cohtml-safe Dropdown (see components/Dropdown.tsx). Options re-build
// on each render so they pick up locale changes (cheap — 5 entries).
const StyleSelector = ({ line }: { line: LineVM }) => {
  const t = useT();
  const pinned = usePinnedStyles();
  return (
    <StyleRow>
      <Dropdown
        value={line.style as StyleValue}
        options={makeLineStyleOptions(t, pinned.lineStyles)}
        onChange={(s) => cmdSetLineStyle(line.lineIndex, s)}
        onTogglePin={cmdTogglePinLineStyle}
      />
    </StyleRow>
  );
};

const SegmentRowComponent = ({ seg }: { seg: SegmentVM }) => {
  const t = useT();
  return (
    <SegmentRow
      $hidden={!seg.visible}
      onClick={() => cmdToggleSegment(seg.lineIndex, seg.segmentIndex)}
    >
      {/* Name left, length right — the old "seg 0 · 1.5m" single run read as
          an unparseable jumble. 1-based for humans. */}
      <SegmentInfo>{t("segment.label", { n: seg.segmentIndex + 1 })}</SegmentInfo>
      <SegmentLen>{t("segment.length", { m: seg.lengthM.toFixed(1) })}</SegmentLen>
      <SegmentIndicator>
        {seg.visible ? <Eye size={12} /> : <EyeOff size={12} />}
      </SegmentIndicator>
    </SegmentRow>
  );
};
