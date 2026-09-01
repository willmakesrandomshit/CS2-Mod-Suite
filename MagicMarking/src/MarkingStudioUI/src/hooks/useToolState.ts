import { bindValue, useValue, trigger } from "cs2/api";

export interface SegmentVM {
  lineIndex: number;
  segmentIndex: number;
  tStart: number;
  tEnd: number;
  visible: boolean;
  style: number;
  lengthM: number;
}

export interface LineVM {
  lineIndex: number;
  style: number;
  curv: number;
  curvatureVal?: number;
  drawingMode?: number;
  lateralOffset?: number;
  segments: SegmentVM[];
}

export interface AreaVM {
  areaIndex: number;
  styleId: number;
  visible: boolean;
  vertexCount: number;
  pieceCount: number;
  visiblePieces: number;
}

export interface TemplateVM {
  id: string;
  name: string;
  category: string;
  description: string;
  isFavorite: boolean;
  isBuiltIn: boolean;
  lineCount: number;
  areaCount: number;
}

export interface RemapEntryVM {
  markingId: number;
  nodeIndex: number;
  status: string;
  description: string;
  deviationMeters: number;
}

export const TOOL_STATE = {
  Default: 0,
  NodeSelected: 1,
  SourceSelected: 2,
  AreaSelecting: 3,
  AreaPainting: 4,
} as const;

export const DRAWING_MODE = {
  FollowRoad: 0,
  Straight: 1,
  CustomCurve: 2,
} as const;

export interface ToolStateVM {
  isActive: boolean;
  toolState: number;
  areaVertexCount: number;
  currentAreaStyle: number;
  selectedNodeIndex: number;
  currentStyle: number;
  currentDrawingMode: number;
  snapTargetDescription: string;
  snapTargetKind: string;
  lastClickedLine: number;
  lastClickedTick: number;
  hoveredLineInGame: number;
  hoveredAreaInGame: number;
  vanillaHidden: boolean;
  undoCount: number;
  redoCount: number;
  selectedLineCount: number;
  reviewCount: number;
  lines: LineVM[];
  areas: AreaVM[];
  templates: TemplateVM[];
  remapReviews: RemapEntryVM[];
}

const EMPTY: ToolStateVM = {
  isActive: false,
  toolState: 0,
  areaVertexCount: 0,
  currentAreaStyle: 0,
  selectedNodeIndex: -1,
  currentStyle: 0,
  currentDrawingMode: 0,
  snapTargetDescription: "Free Position",
  snapTargetKind: "None",
  lastClickedLine: -1,
  lastClickedTick: 0,
  hoveredLineInGame: -1,
  hoveredAreaInGame: -1,
  vanillaHidden: false,
  undoCount: 0,
  redoCount: 0,
  selectedLineCount: 0,
  reviewCount: 0,
  lines: [],
  areas: [],
  templates: [],
  remapReviews: [],
};

const STATE_BINDING = bindValue<ToolStateVM>("MarkingStudio", "GetPanelState", EMPTY);

export const useToolState = (): ToolStateVM => {
  const state = useValue(STATE_BINDING);
  if (!state) return EMPTY;
  return {
    ...state,
    lines: Array.isArray(state.lines) ? state.lines : [],
    areas: Array.isArray(state.areas) ? state.areas : [],
    templates: Array.isArray(state.templates) ? state.templates : [],
    remapReviews: Array.isArray(state.remapReviews) ? state.remapReviews : [],
  };
};

// --- Commands ---

export const cmdToggleSegment = (lineIndex: number, segmentIndex: number) => {
  trigger("MarkingStudio", "ToggleSegment", lineIndex, segmentIndex);
};

export const cmdSetLineStyle = (lineIndex: number, style: number) => {
  trigger("MarkingStudio", "SetLineStyle", lineIndex, style);
};

export const cmdSetSegmentStyle = (lineIndex: number, segmentIndex: number, style: number) => {
  trigger("MarkingStudio", "SetSegmentStyle", lineIndex, segmentIndex, style);
};

export const cmdDeleteLine = (lineIndex: number) => {
  trigger("MarkingStudio", "DeleteLine", lineIndex);
};

export const cmdSetLineCurvature = (lineIndex: number, percent: number) => {
  trigger("MarkingStudio", "SetLineCurvature", lineIndex, percent);
};

export const cmdToggleVanillaMarkings = () => {
  trigger("MarkingStudio", "ToggleVanillaMarkings");
};

export const cmdActivateTool = () => {
  trigger("MarkingStudio", "ActivateTool");
};

export const cmdSetCurrentStyle = (style: number) => {
  trigger("MarkingStudio", "SetCurrentStyle", style);
};

export const cmdSetCurrentAreaStyle = (styleId: number) => {
  trigger("MarkingStudio", "SetCurrentAreaStyle", styleId);
};

export const cmdToggleAreaMode = () => {
  trigger("MarkingStudio", "ToggleAreaMode");
};

export const cmdToggleAreaPaintMode = () => {
  trigger("MarkingStudio", "ToggleAreaPaintMode");
};

export const cmdSetAreaStyle = (areaIndex: number, styleId: number) => {
  trigger("MarkingStudio", "SetAreaStyle", areaIndex, styleId);
};

export const cmdToggleAreaVisible = (areaIndex: number) => {
  trigger("MarkingStudio", "ToggleAreaVisible", areaIndex);
};

export const cmdDeleteArea = (areaIndex: number) => {
  trigger("MarkingStudio", "DeleteArea", areaIndex);
};

export const cmdResetNode = () => {
  trigger("MarkingStudio", "ResetNode");
};

export const cmdAutoGenerateMarkings = () => {
  trigger("MarkingStudio", "AutoGenerateMarkings");
};

export const cmdSetHoveredLine = (lineIndex: number) => {
  trigger("MarkingStudio", "SetHoveredLine", lineIndex);
};

export const cmdSetHoveredSegment = (lineIndex: number, segmentIndex: number) => {
  trigger("MarkingStudio", "SetHoveredSegment", lineIndex, segmentIndex);
};

export const cmdSetHoveredArea = (areaIndex: number) => {
  trigger("MarkingStudio", "SetHoveredArea", areaIndex);
};

export const cmdClearHoveredArea = (areaIndex: number) => {
  trigger("MarkingStudio", "ClearHoveredArea", areaIndex);
};

// 2.2 Drawing Modes & Precision Snapping Commands
export const cmdSetCurrentDrawingMode = (mode: number) => {
  trigger("MarkingStudio", "SetCurrentDrawingMode", mode);
};

export const cmdSetLineDrawingMode = (lineIndex: number, mode: number) => {
  trigger("MarkingStudio", "SetLineDrawingMode", lineIndex, mode);
};

export const cmdSetLineLateralOffset = (lineIndex: number, offset: number) => {
  trigger("MarkingStudio", "SetLineLateralOffset", lineIndex, offset);
};

export const cmdSnapLineToRoad = (lineIndex: number) => {
  trigger("MarkingStudio", "SnapLineToRoad", lineIndex);
};

export const cmdResetLineCurvature = (lineIndex: number) => {
  trigger("MarkingStudio", "ResetLineCurvature", lineIndex);
};

// 2.1 Extended Commands
export const cmdUndoMarking = () => {
  trigger("MarkingStudio", "UndoMarking");
};

export const cmdRedoMarking = () => {
  trigger("MarkingStudio", "RedoMarking");
};

export const cmdApplyTemplate = (templateId: string) => {
  trigger("MarkingStudio", "ApplyTemplate", templateId);
};

export const cmdSaveTemplate = (name: string, category: string, desc: string) => {
  trigger("MarkingStudio", "SaveTemplate", name, category, desc);
};

export const cmdDeleteTemplate = (templateId: string) => {
  trigger("MarkingStudio", "DeleteTemplate", templateId);
};

export const cmdToggleTemplateFavorite = (templateId: string) => {
  trigger("MarkingStudio", "ToggleTemplateFavorite", templateId);
};

export const cmdGenerateJunctionMarkings = (stopBars: boolean, crosswalks: boolean, turnGuides: boolean, islands: boolean) => {
  trigger("MarkingStudio", "GenerateJunctionMarkings", stopBars, crosswalks, turnGuides, islands);
};

export const cmdCopySelection = () => {
  trigger("MarkingStudio", "CopySelection");
};

export const cmdPasteSelection = () => {
  trigger("MarkingStudio", "PasteSelection");
};

export const cmdMirrorSelection = () => {
  trigger("MarkingStudio", "MirrorSelection");
};

export const cmdInvertCurvature = () => {
  trigger("MarkingStudio", "InvertCurvature");
};

export const cmdRepeatSelection = (count: number, step: number) => {
  trigger("MarkingStudio", "RepeatSelection", count, step);
};

export const cmdSelectAll = () => {
  trigger("MarkingStudio", "SelectAll");
};

export const cmdClearSelection = () => {
  trigger("MarkingStudio", "ClearSelection");
};

export const cmdCreateGroup = (name: string) => {
  trigger("MarkingStudio", "CreateGroup", name);
};

export const cmdAcceptRemap = (markingId: number) => {
  trigger("MarkingStudio", "AcceptRemap", markingId);
};

export const cmdDeleteRemap = (markingId: number) => {
  trigger("MarkingStudio", "DeleteRemap", markingId);
};
