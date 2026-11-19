/* Hallow Mic Chain - a VST2 effect for Equalizer APO: noise gate -> compressor -> lookahead limiter, per channel.
   Real-time safe: processReplacing never allocates, locks or does I/O; settings changed from another thread are
   picked up at the start of the next block. Any sample rate; denormals flushed (FTZ/DAZ) while processing.
   Settings: 16 VST parameters (0..1) and a chunk ("HMC1", version, count, 16 floats in real units). */
#include <math.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <xmmintrin.h>
#include "vst2abi.h"

#define NPARAMS 16
#define MAXCH 2
#define MAXLA 512           /* lookahead buffer (samples) - 1.5 ms is 288 samples even at 192 kHz */
#define CHUNK_MAGIC 0x31434D48 /* "HMC1" little-endian */
#define LOOKAHEAD_MS 1.5f

enum { P_GATE_ON, P_GATE_THR, P_GATE_ATK, P_GATE_HOLD, P_GATE_REL, P_GATE_RANGE,
       P_COMP_ON, P_COMP_THR, P_COMP_RATIO, P_COMP_ATK, P_COMP_REL, P_COMP_MAKEUP, P_COMP_KNEE,
       P_LIM_ON, P_LIM_CEIL, P_LIM_REL };

typedef struct { const char *name, *label; float lo, hi, def; int kind; } ParamInfo;   /* kind: 0 linear, 1 log, 2 switch */
static const ParamInfo PI[NPARAMS] = {
    { "GateOn",  "",   0,     1,     1,     2 },
    { "GateThr", "dB", -80,   -10,   -50,   0 },
    { "GateAtk", "ms", 0.1f,  50,    1,     1 },
    { "GateHld", "ms", 0,     500,   150,   0 },
    { "GateRel", "ms", 5,     2000,  200,   1 },
    { "GateRng", "dB", -90,   0,     -30,   0 },
    { "CompOn",  "",   0,     1,     1,     2 },
    { "CompThr", "dB", -60,   0,     -24,   0 },
    { "Ratio",   ":1", 1,     20,    3,     1 },
    { "CompAtk", "ms", 0.1f,  200,   10,    1 },
    { "CompRel", "ms", 5,     2000,  150,   1 },
    { "Makeup",  "dB", 0,     24,    6,     0 },
    { "Knee",    "dB", 0,     24,    6,     0 },
    { "LimOn",   "",   0,     1,     1,     2 },
    { "Ceiling", "dB", -24,   0,     -1,    0 },
    { "LimRel",  "ms", 5,     1000,  60,    1 },
};

typedef struct {
    float gDet, gGain; int gHold, gOpen;     /* gate */
    float cEnv;                              /* compressor gain reduction (dB, <= 0) */
    float lBuf[MAXLA], lReq[MAXLA]; float lGain; int lPos;   /* limiter */
} Chan;

typedef struct {
    AEffect fx;
    float v[NPARAMS];            /* real units */
    volatile long dirty;         /* settings changed: recompute coefficients at the next block */
    float sr; int la;            /* sample rate, lookahead samples */
    /* coefficients */
    float gThr, gThrClose, gRange, gAtkC, gRelC, gDetC; int gHoldN;
    float cThr, cRatio, cKnee, cAtkC, cRelC, cMakeup;
    float lCeil, lAtkC, lRelC;
    int gOn, cOn, lOn;
    Chan ch[MAXCH];
    unsigned char chunk[12 + NPARAMS * 4];
} Plug;

static float clampf(float x, float lo, float hi) { return x < lo ? lo : x > hi ? hi : x; }
static float toNorm(int i, float v)
{
    const ParamInfo *p = &PI[i]; v = clampf(v, p->lo, p->hi);
    if (p->kind == 2) return v >= 0.5f ? 1.0f : 0.0f;
    if (p->kind == 1) return logf(v / p->lo) / logf(p->hi / p->lo);
    return (v - p->lo) / (p->hi - p->lo);
}
static float fromNorm(int i, float n)
{
    const ParamInfo *p = &PI[i]; n = clampf(n, 0, 1);
    if (p->kind == 2) return n >= 0.5f ? 1.0f : 0.0f;
    if (p->kind == 1) return p->lo * powf(p->hi / p->lo, n);
    return p->lo + n * (p->hi - p->lo);
}
static float coefMs(float ms, float sr) { float n = ms * 0.001f * sr; return n < 1.0f ? 0.0f : expf(-1.0f / n); }
static float dbToLin(float db) { return powf(10.0f, db * 0.05f); }

static void resetState(Plug *p)
{
    for (int c = 0; c < MAXCH; c++) {
        Chan *h = &p->ch[c];
        memset(h, 0, sizeof(*h));
        h->gGain = p->gOn ? p->gRange : 1.0f; h->lGain = 1.0f;
        for (int i = 0; i < MAXLA; i++) h->lReq[i] = 1.0f;
    }
}

static void recompute(Plug *p)
{
    float sr = p->sr > 1000 ? p->sr : 48000;
    int la = (int)(sr * LOOKAHEAD_MS * 0.001f + 0.5f); if (la < 1) la = 1; if (la > MAXLA - 1) la = MAXLA - 1;
    if (la != p->la) { p->la = la; p->fx.initialDelay = la; resetState(p); }
    p->gOn = p->v[P_GATE_ON] >= 0.5f; p->cOn = p->v[P_COMP_ON] >= 0.5f; p->lOn = p->v[P_LIM_ON] >= 0.5f;
    p->gThr = dbToLin(p->v[P_GATE_THR]); p->gThrClose = p->gThr * 0.7079f;   /* 3 dB hysteresis */
    p->gRange = dbToLin(p->v[P_GATE_RANGE]);
    p->gAtkC = coefMs(p->v[P_GATE_ATK], sr); p->gRelC = coefMs(p->v[P_GATE_REL], sr);
    p->gDetC = coefMs(10.0f, sr);                                             /* level detector: instant up, 10 ms down */
    p->gHoldN = (int)(p->v[P_GATE_HOLD] * 0.001f * sr);
    p->cThr = p->v[P_COMP_THR]; p->cRatio = p->v[P_COMP_RATIO]; p->cKnee = p->v[P_COMP_KNEE]; p->cMakeup = p->v[P_COMP_MAKEUP];
    p->cAtkC = coefMs(p->v[P_COMP_ATK], sr); p->cRelC = coefMs(p->v[P_COMP_REL], sr);
    p->lCeil = dbToLin(p->v[P_LIM_CEIL]);
    p->lAtkC = expf(-5.0f / (float)la);                                       /* reaches the target within the lookahead */
    p->lRelC = coefMs(p->v[P_LIM_REL], sr);
}

/* compressor static curve with a soft knee: gain change in dB (<= 0) for an input level in dB */
static float compGain(const Plug *p, float x)
{
    float T = p->cThr, R = p->cRatio, W = p->cKnee, d = x - T;
    if (2.0f * d < -W) return 0.0f;
    if (W > 0.0f && 2.0f * fabsf(d) <= W) { float t = d + W * 0.5f; return (1.0f / R - 1.0f) * t * t / (2.0f * W); }
    return (1.0f / R - 1.0f) * d;
}

static void processReplacing(AEffect *fx, float **in, float **out, int32_t frames)
{
    Plug *p = (Plug *)fx->object;
    unsigned int csr = _mm_getcsr(); _mm_setcsr(csr | 0x8040);   /* flush-to-zero + denormals-are-zero */
    if (p->dirty) { p->dirty = 0; recompute(p); }
    const int N = p->la + 1;
    for (int c = 0; c < MAXCH; c++) {
        Chan *h = &p->ch[c]; const float *x = in[c]; float *y = out[c];
        if (!x || !y) continue;
        for (int i = 0; i < frames; i++) {
            float s = x[i];
            if (!(s == s) || s > 1e6f || s < -1e6f) s = 0.0f;        /* NaN / runaway input */
            float a = fabsf(s);
            /* noise gate */
            if (p->gOn) {
                h->gDet = a > h->gDet ? a : h->gDet * p->gDetC;
                if (h->gDet >= p->gThr) { h->gOpen = 1; h->gHold = p->gHoldN; }
                else if (h->gDet < p->gThrClose) { if (h->gHold > 0) h->gHold--; else h->gOpen = 0; }
                float t = h->gOpen ? 1.0f : p->gRange;
                float k = t > h->gGain ? p->gAtkC : p->gRelC;
                h->gGain = t + (h->gGain - t) * k;
                s *= h->gGain; a = fabsf(s);
            }
            /* compressor */
            if (p->cOn) {
                float xdb = a > 1e-9f ? 20.0f * log10f(a) : -180.0f;
                float gr = compGain(p, xdb);
                float k = gr < h->cEnv ? p->cAtkC : p->cRelC;
                h->cEnv = gr + (h->cEnv - gr) * k;
                s *= dbToLin(h->cEnv + p->cMakeup);
            }
            /* lookahead limiter (the delay is always there so the latency never changes) */
            int pos = h->lPos;
            h->lBuf[pos] = s;
            a = fabsf(s);
            h->lReq[pos] = (p->lOn && a > p->lCeil) ? p->lCeil / a : 1.0f;
            float m = 1.0f; for (int j = 0; j < N; j++) if (h->lReq[j] < m) m = h->lReq[j];
            h->lGain = m + (h->lGain - m) * (m < h->lGain ? p->lAtkC : p->lRelC);
            int old = pos + 1; if (old >= N) old = 0;
            float o = h->lBuf[old];
            if (p->lOn) { o *= h->lGain; if (o > p->lCeil) o = p->lCeil; else if (o < -p->lCeil) o = -p->lCeil; }
            h->lPos = old;
            y[i] = o;
        }
        if (fabsf(h->cEnv) < 1e-12f) h->cEnv = 0.0f;
        if (h->gDet < 1e-20f) h->gDet = 0.0f;
    }
    _mm_setcsr(csr);
}

static void processDouble(AEffect *fx, double **in, double **out, int32_t frames) { (void)fx; for (int c = 0; c < MAXCH; c++) if (in[c] && out[c] && in[c] != out[c]) memcpy(out[c], in[c], sizeof(double) * frames); }

static void setParameter(AEffect *fx, int32_t i, float n) { Plug *p = (Plug *)fx->object; if (i < 0 || i >= NPARAMS) return; p->v[i] = fromNorm(i, n); p->dirty = 1; }
static float getParameter(AEffect *fx, int32_t i) { Plug *p = (Plug *)fx->object; return i < 0 || i >= NPARAMS ? 0.0f : toNorm(i, p->v[i]); }

static void copyStr(void *dst, const char *src, size_t max) { if (!dst) return; strncpy((char *)dst, src, max - 1); ((char *)dst)[max - 1] = 0; }

static int32_t makeChunk(Plug *p)
{
    uint32_t hdr[3] = { CHUNK_MAGIC, 1, NPARAMS };
    memcpy(p->chunk, hdr, 12); memcpy(p->chunk + 12, p->v, NPARAMS * 4);
    return (int32_t)sizeof(p->chunk);
}
static int readChunk(Plug *p, const unsigned char *d, intptr_t n)
{
    uint32_t hdr[3];
    if (!d || n < 12) return 0;
    memcpy(hdr, d, 12);
    if (hdr[0] != CHUNK_MAGIC || hdr[1] != 1 || hdr[2] == 0 || hdr[2] > 64 || n < 12 + (intptr_t)hdr[2] * 4) return 0;
    for (uint32_t i = 0; i < hdr[2] && i < NPARAMS; i++) {
        float f; memcpy(&f, d + 12 + i * 4, 4);
        if (!(f == f)) f = PI[i].def;
        p->v[i] = clampf(f, PI[i].lo, PI[i].hi);
        if (PI[i].kind == 2) p->v[i] = p->v[i] >= 0.5f ? 1.0f : 0.0f;
    }
    p->dirty = 1;
    return 1;
}

static intptr_t dispatcher(AEffect *fx, int32_t op, int32_t idx, intptr_t val, void *ptr, float opt)
{
    Plug *p = (Plug *)fx->object;
    switch (op) {
    case VST2_OPEN: return 0;
    case VST2_CLOSE: free(p); return 1;
    case VST2_GET_PROGRAM: return 0;
    case VST2_GET_PROGRAM_NAME: copyStr(ptr, "Default", 24); return 1;
    case VST2_GET_PARAM_LABEL: if (idx >= 0 && idx < NPARAMS) copyStr(ptr, PI[idx].label, 8); return 0;
    case VST2_GET_PARAM_NAME: if (idx >= 0 && idx < NPARAMS) copyStr(ptr, PI[idx].name, 8); return 0;
    case VST2_GET_PARAM_DISPLAY:
        if (idx >= 0 && idx < NPARAMS && ptr) {
            char b[16];
            if (PI[idx].kind == 2) snprintf(b, sizeof b, "%s", p->v[idx] >= 0.5f ? "On" : "Off");
            else snprintf(b, sizeof b, "%.1f", p->v[idx]);
            copyStr(ptr, b, 8);
        }
        return 0;
    case VST2_SET_SAMPLE_RATE: if (opt > 1000.0f && opt < 1000000.0f) { p->sr = opt; recompute(p); } return 0;
    case VST2_SET_BLOCK_SIZE: return 0;
    case VST2_MAINS_CHANGED: if (val) { recompute(p); resetState(p); } return 0;
    case VST2_GET_CHUNK: if (!ptr) return 0; { int32_t n = makeChunk(p); *(void **)ptr = p->chunk; return n; }
    case VST2_SET_CHUNK: return readChunk(p, (const unsigned char *)ptr, val);
    case VST2_CAN_BE_AUTOMATED: return 1;
    case VST2_GET_PLUG_CATEGORY: return 1;   /* effect */
    case VST2_GET_EFFECT_NAME: copyStr(ptr, "Hallow Mic Chain", 32); return 1;
    case VST2_GET_VENDOR_STRING: copyStr(ptr, "Hallow", 64); return 1;
    case VST2_GET_PRODUCT_STRING: copyStr(ptr, "Hallow Mic Chain", 64); return 1;
    case VST2_GET_VENDOR_VERSION: return 1000;
    case VST2_CAN_DO: return 0;
    case VST2_GET_TAIL_SIZE: return p->la;
    case VST2_GET_VST_VERSION: return 2400;
    default: return 0;
    }
}

__declspec(dllexport) AEffect *VSTPluginMain(Vst2HostCallback host)
{
    (void)host;
    Plug *p = (Plug *)calloc(1, sizeof(Plug));
    if (!p) return 0;
    p->fx.magic = VST2_MAGIC;
    p->fx.dispatcher = dispatcher;
    p->fx.setParameter = setParameter;
    p->fx.getParameter = getParameter;
    p->fx.processReplacing = processReplacing;
    p->fx.processDoubleReplacing = processDouble;
    p->fx.numPrograms = 1; p->fx.numParams = NPARAMS; p->fx.numInputs = MAXCH; p->fx.numOutputs = MAXCH;
    p->fx.flags = VST2_FLAG_CAN_REPLACING | VST2_FLAG_PROGRAM_CHUNKS;
    p->fx.ioRatio_deprecated = 1.0f;
    p->fx.object = p;
    p->fx.uniqueID = 0x484D4368;   /* 'HMCh' */
    p->fx.version = 1000;
    for (int i = 0; i < NPARAMS; i++) p->v[i] = PI[i].def;
    p->sr = 48000; p->la = 0;
    recompute(p); resetState(p);
    return &p->fx;
}
