import { Application, extensions } from 'pixi.js';
import { configureCubismSDK, Live2DModel, Live2DPlugin, MotionPriority } from 'untitled-pixi-live2d-engine/cubism';

import './style.css';

const MODEL_URL = './model/vip-nono.model3.json';
const MOTIONS = ['Happy', 'JoyJump', 'Dance', 'Charge', 'Sweat', 'Sleepy', 'Failed', 'Nope', 'Yawn'];
const AUTO_MOTION_CHECK_INTERVAL_MS = 15000;
const AUTO_IDLE_TRIGGER_MS = 180000;
const AUTO_IDLE_MOTIONS = ['Sleepy', 'Yawn', 'Happy'];
const BLINK_INTERVAL_SECONDS = 8;
const DEFAULT_MOTION_DURATION_MS = 4200;
const MOTION_DURATIONS_MS = {
  Happy: 3200,
  JoyJump: 3400,
  Dance: 7000,
  Charge: 4500,
  Sweat: 3000,
  Sleepy: 5100,
  Failed: 5100,
  Nope: 3500,
  Yawn: 3200
};
const VISIBLE_ZOOM = 1.68;
const MODEL_SAFE_PADDING = 36;
const MODEL_TARGET_WIDTH = 176;
const MODEL_TARGET_HEIGHT = 218;
const MODEL_POSITION_OFFSET_X = 0;
const MODEL_POSITION_OFFSET_Y = -6;
const MOTION_RESET_PARAMETERS = [
  ['Param', 0],
  ['Param2', 0],
  ['Param3', 0],
  ['Param4', 0],
  ['Param5', 0],
  ['Param6', 0],
  ['Param7', 0],
  ['Param8', 0],
  ['Param9', 0],
  ['Param10', 0],
  ['Param11', 0],
  ['Param12', 0],
  ['Param13', 0],
  ['Param17', 0],
  ['Param18', 0],
  ['Param19', 0],
  ['Param20', 0],
  ['Param21', 0],
  ['Param22', 0],
  ['Param23', 0],
  ['Param24', 0],
  ['Param25', 0],
  ['Param26', 0],
  ['Param27', 0],
  ['Param28', 0],
  ['Param29', 0],
  ['ParamEyeLOpen', 1],
  ['ParamEyeROpen', 1],
  ['ParamEyeBallX', 0],
  ['ParamEyeBallY', 0],
  ['ParamEyeLSmile', 0],
  ['ParamEyeRSmile', 0]
];
const IDLE_PARAMETERS = [
  ['Param4', 0],
  ['Param27', 1],
  ['Param19', 0],
  ['Param28', 0],
  ['Param29', 0],
  ['Param24', 0],
  ['Param22', 0],
  ['Param23', 0],
  ['ParamEyeLOpen', 1],
  ['ParamEyeROpen', 1],
  ['ParamEyeBallX', 0],
  ['ParamEyeBallY', 0],
  ['ParamEyeLSmile', 0],
  ['ParamEyeRSmile', 0]
];
const HIT_ZONE_MOTIONS = {
  head: ['Happy', 'JoyJump', 'Yawn'],
  ear: ['Nope', 'Sweat'],
  body: ['Dance', 'Happy'],
  wing: ['JoyJump', 'Nope'],
  ring: ['Charge', 'Sleepy'],
  empty: ['Sleepy', 'Happy', 'Failed']
};
const DOUBLE_CLICK_MOTIONS = {
  head: ['JoyJump', 'Happy'],
  ear: ['Nope', 'Sweat'],
  body: ['Dance', 'JoyJump'],
  wing: ['JoyJump', 'Dance'],
  ring: ['Charge'],
  empty: ['Happy', 'Dance']
};
const LONG_PRESS_MOTIONS = {
  head: ['Yawn', 'Sleepy'],
  ear: ['Nope'],
  body: ['Sleepy', 'Charge'],
  wing: ['Sweat', 'Nope'],
  ring: ['Charge'],
  empty: ['Sleepy', 'Yawn']
};
const DRAG_RELEASE_MOTIONS = {
  short: ['Happy'],
  medium: ['JoyJump', 'Sweat'],
  long: ['Sweat', 'Nope', 'Failed']
};
const ROAM_RESET_PARAMETERS = [
  ['Param19', 0],
  ['Param28', 0],
  ['Param29', 0],
  ['Param24', 0],
  ['Param22', 0],
  ['Param23', 0]
];
const DRAG_RESET_PARAMETERS = [
  ['Param7', 0],
  ['Param8', 0],
  ['Param9', 0],
  ['Param10', 0],
  ['Param11', 0],
  ['Param19', 0],
  ['Param28', 0],
  ['Param29', 0],
  ['Param24', 0],
  ['Param22', 0],
  ['Param23', 0]
];
const CODEX_STATUS_MOTION_COOLDOWN_MS = 9000;
const CODEX_CHARGING_STATES = new Set([
  'thinking',
  'planning',
  'working',
  'coding',
  'editing',
  'building',
  'testing',
  'reviewing'
]);
const CODEX_THINKING_MOTION = 'Charge';
const CODEX_CHARGE_INTRO_MS = 3200;
const SPEECH_BUBBLE_VISIBLE_MS = 5200;
const SPEECH_BUBBLE_MAX_LENGTH = 34;
const CODEX_TERMINAL_STATES = new Set(['success', 'warning', 'error', 'blocked']);
const CODEX_STATUS_LABELS = {
  success: '完成',
  warning: '注意',
  error: '失败',
  blocked: '需要处理'
};
const CODEX_STATUS_MOTIONS = {
  thinking: CODEX_THINKING_MOTION,
  planning: CODEX_THINKING_MOTION,
  working: 'Dance',
  coding: 'Dance',
  editing: 'Dance',
  building: 'JoyJump',
  testing: 'JoyJump',
  reviewing: 'Sleepy',
  success: 'Happy',
  warning: 'Sweat',
  error: 'Failed',
  blocked: 'Nope'
};
const MOUSE_FOLLOW_POSE_SOURCES = new Set(['mouse-follow', 'mouse-follow-throw']);
const THROW_POSE_SOURCE = 'mouse-follow-throw';

const stageElement = document.querySelector('#stage');
const statusElement = document.querySelector('#status');
const speechBubbleElement = document.querySelector('#speech-bubble');
let app;
let model;
let dragMode = true;
let movedDuringPointer = false;
let lastMotion = '';
let autoMotionsEnabled = true;
let gazeTrackingEnabled = true;
let lastInteractionAt = Date.now();
let idleTimeoutId = 0;
let motionRunId = 0;
let motionActive = false;
let roamPoseActive = false;
let roamPoseSource = 'roam';
let roamPoseDirection = { dx: 0, dy: 0, progress: 0, speed: 0, source: 'roam' };
let dragPoseActive = false;
let dragPose = { clientX: 0, clientY: 0, dx: 0, dy: 0, distance: 0, durationMs: 0 };
let codexStatusActive = false;
let codexStatusState = 'idle';
let codexStatusMessage = '';
let codexStatusStamp = '';
let codexStatusMotionAt = 0;
let codexThinkingInterrupted = false;
let codexThinkingMotionActive = false;
let codexThinkingChargeTimerId = 0;
let codexChargeLoopActive = false;
let codexChargeLoopStartedAt = 0;
let speechBubbleTimeoutId = 0;

function setStatus(text, failed = false) {
  statusElement.textContent = text;
  statusElement.classList.toggle('failed', failed);
}

function truncateText(text, maxLength) {
  const characters = Array.from(text);
  if (characters.length <= maxLength) {
    return text;
  }

  return `${characters.slice(0, maxLength - 1).join('')}...`;
}

function showSpeechBubble(text) {
  if (!speechBubbleElement) {
    return;
  }

  window.clearTimeout(speechBubbleTimeoutId);
  speechBubbleElement.textContent = text;
  speechBubbleElement.classList.add('visible');
  speechBubbleTimeoutId = window.setTimeout(hideSpeechBubble, SPEECH_BUBBLE_VISIBLE_MS);
}

function hideSpeechBubble() {
  if (!speechBubbleElement) {
    return;
  }

  speechBubbleElement.classList.remove('visible');
}

function postHostMessage(message) {
  window.chrome?.webview?.postMessage(message);
}

function randomItem(items) {
  return items[Math.floor(Math.random() * items.length)];
}

function randomDifferentMotion(items) {
  const candidates = items.filter((item) => item !== lastMotion);
  return randomItem(candidates.length > 0 ? candidates : items);
}

function clamp(value, min, max) {
  return Math.min(max, Math.max(min, value));
}

function markInteraction() {
  lastInteractionAt = Date.now();
  interruptCodexThinkingCharge();
}

function fitModel() {
  if (!app || !model) {
    return;
  }

  const availableWidth = Math.max(1, Math.min(app.screen.width - MODEL_SAFE_PADDING * 2, MODEL_TARGET_WIDTH));
  const availableHeight = Math.max(1, Math.min(app.screen.height - MODEL_SAFE_PADDING * 2, MODEL_TARGET_HEIGHT));
  const modelWidth = Math.max(1, model.width / model.scale.x);
  const modelHeight = Math.max(1, model.height / model.scale.y);
  const scale = Math.min(availableWidth / modelWidth, availableHeight / modelHeight) * VISIBLE_ZOOM;

  model.scale.set(scale);
  model.position.set(
    app.screen.width / 2 + MODEL_POSITION_OFFSET_X,
    app.screen.height / 2 + MODEL_POSITION_OFFSET_Y
  );
}

function setParameter(parameterId, value) {
  const coreModel = model?.internalModel?.coreModel;
  const parameters = coreModel?.getModel?.().parameters;
  const parameterIndex = parameters?.ids?.indexOf(parameterId) ?? -1;
  if (parameterIndex >= 0) {
    coreModel.setParameterValueByIndex(parameterIndex, value, 1);
  }
}

function applyIdleParameters() {
  for (const [parameterId, value] of IDLE_PARAMETERS) {
    setParameter(parameterId, value);
  }
}

function configureEyeBlink() {
  const eyeBlink = model?.internalModel?.eyeBlink;
  if (!eyeBlink) {
    return;
  }

  eyeBlink.setBlinkingInterval?.(BLINK_INTERVAL_SECONDS);

  if (typeof eyeBlink.determinNextBlinkingTiming === 'function') {
    eyeBlink.determinNextBlinkingTiming = function determinNextBlinkingTiming() {
      return this._userTimeSeconds + BLINK_INTERVAL_SECONDS;
    };
    eyeBlink._nextBlinkingTime = (eyeBlink._userTimeSeconds ?? 0) + BLINK_INTERVAL_SECONDS;
  }

  if ('blinkInterval' in eyeBlink) {
    eyeBlink.blinkInterval = BLINK_INTERVAL_SECONDS * 1000;
    eyeBlink.nextBlinkTimeLeft = eyeBlink.blinkInterval;
  }
}

function clearIdleTimeout() {
  if (idleTimeoutId) {
    window.clearTimeout(idleTimeoutId);
    idleTimeoutId = 0;
  }
}

function resetMotionParameters() {
  model.stopMotions();
  for (const [parameterId, value] of MOTION_RESET_PARAMETERS) {
    setParameter(parameterId, value);
  }
}

function enterIdle() {
  if (!model) {
    return;
  }

  clearIdleTimeout();
  motionActive = false;
  resetMotionParameters();
  applyIdleParameters();
}

function scheduleIdleAfterMotion(group, runId) {
  clearIdleTimeout();
  idleTimeoutId = window.setTimeout(() => {
    if (runId === motionRunId) {
      enterIdle();
    }
  }, MOTION_DURATIONS_MS[group] ?? DEFAULT_MOTION_DURATION_MS);
}

function playMotion(group, options = {}) {
  if (!model) {
    return;
  }

  const keepActive = Boolean(options.keepActive);

  if (dragPoseActive) {
    resetDragPose();
  }

  if (roamPoseActive) {
    resetRoamPose();
  }

  const runId = ++motionRunId;
  clearIdleTimeout();
  motionActive = true;
  lastMotion = group;
  resetMotionParameters();
  void model.motion(group, 0, MotionPriority.FORCE).then((started) => {
    if (runId !== motionRunId) {
      return;
    }

    if (!started) {
      console.warn(`Motion ${group} was not started`);
      enterIdle();
      return;
    }

    if (!keepActive) {
      scheduleIdleAfterMotion(group, runId);
    }
  }).catch((error) => {
    console.warn(`Could not play motion ${group}`, error);
    if (runId === motionRunId) {
      enterIdle();
    }
  });
}

function playMotionCommand(group) {
  markInteraction();
  playMotion(group);
}

function playMotionFromPool(motions) {
  if (!motions?.length) {
    return;
  }

  playMotion(randomDifferentMotion(motions));
}

function playRandomMotion() {
  markInteraction();
  playMotionFromPool(MOTIONS);
}

function playAutoMotion() {
  if (!autoMotionsEnabled || motionActive || roamPoseActive || dragPoseActive || codexStatusActive) {
    return;
  }

  const idleTime = Date.now() - lastInteractionAt;
  if (idleTime < AUTO_IDLE_TRIGGER_MS) {
    return;
  }

  lastInteractionAt = Date.now();
  playMotionFromPool(AUTO_IDLE_MOTIONS);
}

function classifyPointer(event) {
  const rect = app.canvas.getBoundingClientRect();
  const x = (event.clientX - rect.left) / Math.max(1, rect.width);
  const y = (event.clientY - rect.top) / Math.max(1, rect.height);
  const centeredX = Math.abs(x - 0.5);

  if (y >= 0.74 && x >= 0.22 && x <= 0.82) {
    return 'ring';
  }

  if (y >= 0.56 && y <= 0.78 && centeredX > 0.25 && centeredX < 0.46) {
    return 'wing';
  }

  if (y >= 0.20 && y <= 0.43 && centeredX > 0.26) {
    return 'ear';
  }

  if (y >= 0.31 && y <= 0.62 && centeredX <= 0.26) {
    return 'head';
  }

  if (y > 0.56 && y < 0.77 && centeredX <= 0.31) {
    return 'body';
  }

  return 'empty';
}

function playMotionForPointer(event) {
  handleInteraction('click', event.clientX, event.clientY);
}

function getDragReleaseMotions(distance) {
  if (distance >= 180) {
    return DRAG_RELEASE_MOTIONS.long;
  }

  if (distance >= 60) {
    return DRAG_RELEASE_MOTIONS.medium;
  }

  return DRAG_RELEASE_MOTIONS.short;
}

function getInteractionMotions(type, zone, distance) {
  if (type === 'doubleClick') {
    return DOUBLE_CLICK_MOTIONS[zone] ?? HIT_ZONE_MOTIONS[zone] ?? MOTIONS;
  }

  if (type === 'longPress') {
    return LONG_PRESS_MOTIONS[zone] ?? HIT_ZONE_MOTIONS[zone] ?? MOTIONS;
  }

  if (type === 'dragRelease') {
    return getDragReleaseMotions(distance);
  }

  return HIT_ZONE_MOTIONS[zone] ?? MOTIONS;
}

function handleInteraction(type, clientX, clientY, distance = 0) {
  markInteraction();
  const zone = classifyPointer({ clientX, clientY });
  playMotionFromPool(getInteractionMotions(type, zone, distance));
}

function focusPoint(clientX, clientY) {
  if (!model || !gazeTrackingEnabled || dragPoseActive || roamPoseActive) {
    return;
  }

  model.focus(clientX, clientY);
}

function focusCenter() {
  if (!model || !app) {
    return;
  }

  model.focus(app.screen.width / 2, app.screen.height / 2);
}

function isMouseFollowPoseActive() {
  return roamPoseActive && MOUSE_FOLLOW_POSE_SOURCES.has(roamPoseSource);
}

function setRoamPose(pose) {
  const active = Boolean(pose?.active);
  if (!active || !model || !app) {
    resetRoamPose();
    return;
  }

  const source = String(pose?.source ?? 'roam');
  if (MOUSE_FOLLOW_POSE_SOURCES.has(source)) {
    markInteraction();
  }

  if (!MOUSE_FOLLOW_POSE_SOURCES.has(source) && isCodexThinkingState() && !codexThinkingInterrupted) {
    resetRoamPose();
    return;
  }

  const dx = Number(pose.dx) || 0;
  const dy = Number(pose.dy) || 0;
  const length = Math.hypot(dx, dy);
  if (length <= 0.001) {
    resetRoamPose();
    return;
  }

  roamPoseActive = true;
  roamPoseSource = source;
  roamPoseDirection = {
    dx: dx / length,
    dy: dy / length,
    progress: clamp(Number(pose.progress) || 0, 0, 1),
    speed: length,
    source
  };
}

function resetRoamPose() {
  roamPoseActive = false;
  roamPoseSource = 'roam';
  roamPoseDirection = { dx: 0, dy: 0, progress: 0, speed: 0, source: 'roam' };
  resetRoamParameters();
  focusCenter();
}

function resetRoamParameters() {
  for (const [parameterId, value] of ROAM_RESET_PARAMETERS) {
    setParameter(parameterId, value);
  }
}

function applyMovementAppendagePose({
  directionX,
  directionY,
  speedPressure,
  activity,
  phase,
  now,
  dragPullX = 0,
  dragPullY = 0,
  source = 'roam'
}) {
  const fast = clamp(speedPressure, 0, 1);
  const load = clamp(activity, 0, 1.35);
  const lateral = clamp(directionX + dragPullX * 0.24, -1, 1);
  const vertical = clamp(directionY + dragPullY * 0.18, -1, 1);
  const throwBoost = source === THROW_POSE_SOURCE ? 1.22 : 1;
  const flapRate = 2.35 + fast * 5.2 + (source === THROW_POSE_SOURCE ? 2.2 : 0);
  const flap = Math.sin(now * flapRate + phase * Math.PI * 2);
  const counterFlap = Math.sin(now * (flapRate * 0.72) + phase * Math.PI * 2 + 1.7);
  const tremble = Math.sin(now * (8.5 + fast * 13.5) + phase * 5.1) * fast;
  const lift = -vertical;
  const windLoad = load * throwBoost;
  const earTuck = (-4.5 - fast * 8.5 + Math.max(0, vertical) * 3.5) * windLoad;
  const earSpread = lateral * (7 + fast * 6) * windLoad;
  const earFlutter = (flap * (2.3 + fast * 4.4) + tremble * 2.1) * windLoad;
  const earVertical = (lift * (8.5 + fast * 5.5) + counterFlap * (2 + fast * 3.2)) * windLoad;

  setParameter('Param19', clamp(earTuck - earSpread + earFlutter, -30, 18));
  setParameter('Param28', clamp(earTuck + earSpread - earFlutter * 0.92, -30, 18));
  setParameter('Param29', clamp(earVertical, -24, 24));

  const wingBase = (5.5 + fast * 6.5 - vertical * 4.2) * windLoad;
  const wingFlap = (flap * (5.5 + fast * 9.5) + tremble * 3.6) * windLoad;
  const wingBank = lateral * (8 + fast * 9) * windLoad;
  const wingLift = (lift * (9.5 + fast * 8.5) + counterFlap * (4 + fast * 5.5)) * windLoad;
  const trailingKick = Math.max(0, fast - 0.34) * 11 * windLoad;

  setParameter('Param24', clamp(wingBase + wingLift + wingFlap * 0.36, -30, 30));
  setParameter('Param22', clamp(-10 - wingBank - wingFlap - trailingKick, -30, 18));
  setParameter('Param23', clamp(-10 + wingBank + wingFlap * 0.88 - trailingKick * 0.72, -30, 18));
}

function updateRoamPose() {
  if (!model || !app || !roamPoseActive || motionActive || dragPoseActive) {
    return;
  }

  const { dx, dy, progress, speed, source } = roamPoseDirection;
  const now = performance.now() / 1000;
  const sourceSpeed = source === 'roam'
    ? clamp(speed / 110, 0, 1)
    : clamp(speed / (source === THROW_POSE_SOURCE ? 1700 : 1050), 0, 1);
  const phaseActivity = source === 'roam'
    ? Math.sin(Math.PI * progress) * 0.45
    : progress * 0.72;
  const activity = clamp(0.22 + phaseActivity + sourceSpeed * 0.62, 0.18, 1.28);

  model.focus(
    app.screen.width * (0.5 + dx * (0.22 + sourceSpeed * 0.14)),
    app.screen.height * (0.5 + dy * (0.18 + sourceSpeed * 0.12))
  );

  applyMovementAppendagePose({
    directionX: dx,
    directionY: dy,
    speedPressure: sourceSpeed,
    activity,
    phase: progress,
    now,
    source
  });
}

function setDragPose(pose) {
  const active = Boolean(pose?.active);
  if (active) {
    markInteraction();
  }

  if (!active || !model || !app) {
    resetDragPose();
    return;
  }

  if (!dragPoseActive) {
    motionRunId += 1;
    clearIdleTimeout();
    motionActive = false;
    resetMotionParameters();
    applyIdleParameters();
  }

  roamPoseActive = false;
  resetRoamParameters();
  dragPoseActive = true;
  dragPose = {
    clientX: Number(pose.clientX) || app.screen.width / 2,
    clientY: Number(pose.clientY) || app.screen.height / 2,
    dx: Number(pose.dx) || 0,
    dy: Number(pose.dy) || 0,
    distance: Number(pose.distance) || 0,
    durationMs: Number(pose.durationMs) || 0
  };
}

function resetDragPose() {
  if (!dragPoseActive) {
    return;
  }

  dragPoseActive = false;
  dragPose = { clientX: 0, clientY: 0, dx: 0, dy: 0, distance: 0, durationMs: 0 };
  for (const [parameterId, value] of DRAG_RESET_PARAMETERS) {
    setParameter(parameterId, value);
  }
  enterIdle();
}

function updateDragPose() {
  if (!model || !app || !dragPoseActive) {
    return;
  }

  const now = performance.now() / 1000;
  const speedLength = Math.hypot(dragPose.dx, dragPose.dy);
  const directionX = speedLength > 0.001 ? dragPose.dx / speedLength : 0;
  const directionY = speedLength > 0.001 ? dragPose.dy / speedLength : -0.3;
  const pullX = clamp((dragPose.clientX / Math.max(1, app.screen.width) - 0.5) * 2, -1, 1);
  const pullY = clamp((dragPose.clientY / Math.max(1, app.screen.height) - 0.5) * 2, -1, 1);
  const speedPressure = clamp(speedLength / 22 + dragPose.distance / 340, 0, 1);
  const activity = clamp(0.36 + dragPose.distance / 150 + speedPressure * 0.58, 0.36, 1.28);
  const phase = clamp(dragPose.durationMs / 520, 0, 1);

  model.focus(
    app.screen.width * (0.5 + clamp(pullX * 0.26 + directionX * 0.12, -0.38, 0.38)),
    app.screen.height * (0.5 + clamp(pullY * 0.22 + directionY * 0.10, -0.34, 0.34))
  );

  setParameter('Param7', 1);
  setParameter('Param8', 0);
  setParameter('Param9', 0);
  setParameter('Param10', 0);
  setParameter('Param11', 1);
  applyMovementAppendagePose({
    directionX,
    directionY,
    speedPressure,
    activity,
    phase,
    now,
    dragPullX: pullX,
    dragPullY: pullY,
    source: 'drag'
  });
}

function normalizeCodexState(state) {
  const normalized = String(state ?? 'idle').trim().toLowerCase();
  if (!normalized || normalized === 'idle' || normalized === 'none') {
    return 'idle';
  }

  if (['plan', 'planning'].includes(normalized)) {
    return 'planning';
  }

  if (['think', 'thinking', 'analyzing', 'analysis'].includes(normalized)) {
    return 'thinking';
  }

  if (['work', 'working', 'running'].includes(normalized)) {
    return 'working';
  }

  if (['code', 'coding', 'edit', 'editing', 'patching'].includes(normalized)) {
    return normalized.startsWith('edit') ? 'editing' : 'coding';
  }

  if (['build', 'building', 'compile', 'compiling'].includes(normalized)) {
    return 'building';
  }

  if (['test', 'testing', 'verify', 'verifying'].includes(normalized)) {
    return 'testing';
  }

  if (['review', 'reviewing'].includes(normalized)) {
    return 'reviewing';
  }

  if (['ok', 'done', 'complete', 'completed', 'success', 'passed'].includes(normalized)) {
    return 'success';
  }

  if (['warn', 'warning'].includes(normalized)) {
    return 'warning';
  }

  if (['fail', 'failed', 'error'].includes(normalized)) {
    return 'error';
  }

  if (['block', 'blocked', 'waiting'].includes(normalized)) {
    return 'blocked';
  }

  return 'working';
}

function getCodexConclusionMessage(status = {}, state = 'success') {
  const explicitConclusion = typeof status.conclusion === 'string' ? status.conclusion.trim() : '';
  const message = explicitConclusion || (typeof status.message === 'string' ? status.message.trim() : '');
  const label = CODEX_STATUS_LABELS[state] ?? '结论';
  if (!message) {
    if (state === 'warning') {
      return '注意：Codex 已完成但有提醒';
    }

    if (state === 'error') {
      return '失败：Codex 执行遇到错误';
    }

    if (state === 'blocked') {
      return '需要处理：Codex 等待输入';
    }

    return '完成：Codex 执行完成';
  }

  return `${label}：${truncateText(message, SPEECH_BUBBLE_MAX_LENGTH)}`;
}

function isCodexThinkingState(state = codexStatusState) {
  return CODEX_CHARGING_STATES.has(state);
}

function clearCodexThinkingChargeTimer() {
  if (codexThinkingChargeTimerId) {
    window.clearTimeout(codexThinkingChargeTimerId);
    codexThinkingChargeTimerId = 0;
  }
}

function shouldRunCodexThinkingCharge() {
  return Boolean(
    model
    && isCodexThinkingState()
    && !codexThinkingInterrupted
    && !dragPoseActive
    && !isMouseFollowPoseActive()
  );
}

function stopCodexThinkingCharge(resetCurrentMotion = false) {
  clearCodexThinkingChargeTimer();
  codexChargeLoopActive = false;

  if (
    resetCurrentMotion
    && model
    && codexThinkingMotionActive
    && motionActive
    && lastMotion === CODEX_THINKING_MOTION
  ) {
    motionRunId += 1;
    clearIdleTimeout();
    motionActive = false;
    resetMotionParameters();
    applyIdleParameters();
  }

  codexThinkingMotionActive = false;
}

function startCodexThinkingCharge() {
  if (codexThinkingMotionActive && (codexThinkingChargeTimerId || codexChargeLoopActive)) {
    return;
  }

  clearCodexThinkingChargeTimer();
  if (!shouldRunCodexThinkingCharge()) {
    codexThinkingMotionActive = false;
    codexChargeLoopActive = false;
    return;
  }

  codexThinkingMotionActive = true;
  codexChargeLoopActive = false;
  playMotion(CODEX_THINKING_MOTION, { keepActive: true });
  codexThinkingChargeTimerId = window.setTimeout(() => {
    codexThinkingChargeTimerId = 0;
    if (!shouldRunCodexThinkingCharge()) {
      codexThinkingMotionActive = false;
      codexChargeLoopActive = false;
      return;
    }

    enterCodexChargeLoopPose();
  }, CODEX_CHARGE_INTRO_MS);
}

function enterCodexChargeLoopPose() {
  if (!model || !shouldRunCodexThinkingCharge()) {
    codexThinkingMotionActive = false;
    codexChargeLoopActive = false;
    return;
  }

  motionRunId += 1;
  clearIdleTimeout();
  model.stopMotions();
  motionActive = true;
  lastMotion = CODEX_THINKING_MOTION;
  codexThinkingMotionActive = true;
  codexChargeLoopActive = true;
  codexChargeLoopStartedAt = performance.now();
  applyCodexChargeLoopPose(0);
}

function applyCodexChargeLoopPose(timeSeconds) {
  const pulse = (Math.sin(timeSeconds * Math.PI * 2 * 0.55) + 1) * 0.5;
  const softPulse = (Math.sin(timeSeconds * Math.PI * 2 * 0.28 + 0.7) + 1) * 0.5;
  const heartPhase = (timeSeconds * 1.15 + 0.74) % 1;
  const haloPhase = (timeSeconds * 0.58 + 0.84) % 1;

  setParameter('Param', 0.82 + softPulse * 0.18);
  setParameter('Param5', 1);
  setParameter('Param6', heartPhase * 3);
  setParameter('Param2', 0.78 + pulse * 0.22);
  setParameter('Param3', haloPhase * 4);
  setParameter('Param4', 1);
  setParameter('Param11', 0);
}

function updateCodexChargeLoopPose() {
  if (!codexChargeLoopActive || !model) {
    return;
  }

  if (!shouldRunCodexThinkingCharge()) {
    codexChargeLoopActive = false;
    return;
  }

  applyCodexChargeLoopPose((performance.now() - codexChargeLoopStartedAt) / 1000);
}

function interruptCodexThinkingCharge() {
  if (!isCodexThinkingState() || codexThinkingInterrupted) {
    return;
  }

  codexThinkingInterrupted = true;
  stopCodexThinkingCharge(true);
}

function setCodexStatus(status = {}) {
  const progress = Number(status?.progress);
  const rawState = normalizeCodexState(status?.state ?? status?.status);
  const state = Number.isFinite(progress)
    && progress >= 100
    && !['error', 'warning', 'blocked'].includes(rawState)
    ? 'success'
    : rawState;
  const stateChanged = state !== codexStatusState;
  const conclusionMessage = getCodexConclusionMessage(status, state);
  const messageChanged = conclusionMessage !== codexStatusMessage;
  const statusStamp = typeof status?.updatedAt === 'string' ? status.updatedAt : '';
  const stampChanged = Boolean(statusStamp) && statusStamp !== codexStatusStamp;
  const statusChanged = stateChanged || messageChanged || stampChanged;

  if (statusChanged) {
    codexThinkingInterrupted = false;
  }

  codexStatusState = state;
  codexStatusMessage = conclusionMessage;
  codexStatusStamp = statusStamp;
  codexStatusActive = state !== 'idle';

  if (state === 'idle') {
    stopCodexThinkingCharge(true);
    hideSpeechBubble();
    return;
  }

  if (isCodexThinkingState(state)) {
    if (statusChanged || !codexThinkingMotionActive || !codexThinkingChargeTimerId) {
      startCodexThinkingCharge();
    }
    return;
  }

  if (stateChanged) {
    stopCodexThinkingCharge(true);
  }

  if (CODEX_TERMINAL_STATES.has(state) && (stateChanged || messageChanged)) {
    showSpeechBubble(conclusionMessage);
  }

  if (stateChanged) {
    playCodexStatusMotion(state);
  }
}

function playCodexStatusMotion(state) {
  if (motionActive || dragPoseActive || roamPoseActive) {
    return;
  }

  const motion = CODEX_STATUS_MOTIONS[state];
  const now = Date.now();
  if (!motion || now - codexStatusMotionAt < CODEX_STATUS_MOTION_COOLDOWN_MS) {
    return;
  }

  codexStatusMotionAt = now;
  playMotion(motion);
}

function installPointerBridge() {
  window.addEventListener('pointerdown', (event) => {
    markInteraction();
    movedDuringPointer = false;
    postHostMessage({
      type: 'drag-start',
      screenX: event.screenX,
      screenY: event.screenY
    });
  });

  window.addEventListener('pointermove', (event) => {
    if (!dragMode || event.buttons !== 1) {
      return;
    }

    movedDuringPointer = true;
    postHostMessage({
      type: 'drag-move',
      screenX: event.screenX,
      screenY: event.screenY
    });
  });

  window.addEventListener('pointerup', (event) => {
    postHostMessage({ type: 'drag-end' });
    if (!movedDuringPointer) {
      playMotionForPointer(event);
    }
  });
}

async function start() {
  extensions.add(Live2DPlugin);
  configureCubismSDK({ memorySizeMB: 32 });

  app = new Application();
  await app.init({
    resizeTo: window,
    preference: 'webgl',
    backgroundAlpha: 0,
    antialias: true,
    autoDensity: true,
    resolution: Math.min(window.devicePixelRatio || 1, 2)
  });

  app.canvas.id = 'live2d-canvas';
  stageElement.appendChild(app.canvas);

  model = await Live2DModel.from(MODEL_URL, {
    textureOptions: { lod: 'single-auto' }
  });
  configureEyeBlink();
  model.anchor.set(0.5);
  app.stage.addChild(model);
  fitModel();
  app.ticker.add(updateDragPose);
  app.ticker.add(updateRoamPose);
  app.ticker.add(updateCodexChargeLoopPose);

  window.addEventListener('resize', fitModel);
  window.desktopPet = {
    setDragMode(enabled) {
      dragMode = Boolean(enabled);
      document.body.classList.toggle('drag-mode', dragMode);
    },
    setAutoMotions(enabled) {
      autoMotionsEnabled = Boolean(enabled);
    },
    setGazeTracking(enabled) {
      gazeTrackingEnabled = Boolean(enabled);
      if (!gazeTrackingEnabled) {
        focusCenter();
      }
    },
    playMotion: playMotionCommand,
    playRandomMotion,
    playMotionForPoint(clientX, clientY) {
      handleInteraction('click', clientX, clientY);
    },
    handleInteraction,
    setRoamPose,
    setDragPose,
    setCodexStatus,
    focusPoint,
    focusCenter,
    classifyPoint(clientX, clientY) {
      return classifyPointer({ clientX, clientY });
    }
  };

  document.body.classList.toggle('drag-mode', dragMode);
  installPointerBridge();
  setStatus('');
  enterIdle();
  postHostMessage({ type: 'player-ready' });
  window.setInterval(playAutoMotion, AUTO_MOTION_CHECK_INTERVAL_MS);
}

start().catch((error) => {
  console.error(error);
  setStatus(`Live2D load failed: ${error?.message ?? error}`, true);
});
