/* Offline test host for HallowMicChain.dll: loads it the way Equalizer APO does (VSTPluginMain, open, sample rate,
   block size, mains on, chunk, processReplacing) and checks the gate, compressor, limiter, chunk and robustness.
   chaintest.exe <path to HallowMicChain.dll>   -> exit code = number of failed checks */
#include <windows.h>
#include <math.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "vst2abi.h"

typedef AEffect *(*MainProc)(Vst2HostCallback);
static MainProc vmain;
static int fails;
static void check(int ok, const char *what, double got) { printf("%s %-58s (%.3f)\n", ok ? "PASS" : "FAIL", what, got); if (!ok) fails++; }
static intptr_t host(AEffect *fx, int32_t op, int32_t i, intptr_t v, void *p, float o) { return op == VST2_HOST_VERSION ? 2400 : 0; }

static float settings[16];
static void defaults(void)
{   /* gate on/thr/atk/hold/rel/range, comp on/thr/ratio/atk/rel/makeup/knee, lim on/ceil/rel */
    float d[16] = { 0, -50, 1, 50, 50, -40,  0, -30, 4, 5, 100, 0, 0,  0, -3, 60 };
    memcpy(settings, d, sizeof d);
}
static AEffect *make(float sr)
{
    AEffect *fx = vmain(host);
    fx->dispatcher(fx, VST2_OPEN, 0, 0, 0, 0);
    fx->dispatcher(fx, VST2_SET_SAMPLE_RATE, 0, 0, 0, sr);
    fx->dispatcher(fx, VST2_SET_BLOCK_SIZE, 0, 480, 0, 0);
    unsigned char ch[12 + 64]; unsigned int h[3] = { 0x31434D48, 1, 16 };
    memcpy(ch, h, 12); memcpy(ch + 12, settings, 64);
    if (!fx->dispatcher(fx, VST2_SET_CHUNK, 0, sizeof ch, ch, 0)) { printf("FAIL set chunk\n"); fails++; }
    fx->dispatcher(fx, VST2_MAINS_CHANGED, 0, 1, 0, 0);
    return fx;
}
/* runs n frames of mono signal sig (copied to both channels) in blocks of 480; out gets channel 0 */
static void run(AEffect *fx, const float *sig, float *out, int n, int inplace)
{
    static float a[480], b[480], c[480], d[480];
    for (int i = 0; i < n; i += 480) {
        int k = n - i < 480 ? n - i : 480;
        memcpy(a, sig + i, k * 4); memcpy(b, sig + i, k * 4);
        float *in[2] = { a, b }; float *o2[2] = { c, d };
        fx->processReplacing(fx, in, inplace ? in : o2, k);
        memcpy(out + i, inplace ? a : c, k * 4);
    }
}
static double peak(const float *x, int from, int to) { double m = 0; for (int i = from; i < to; i++) if (fabs(x[i]) > m) m = fabs(x[i]); return m; }
static double rms(const float *x, int from, int to) { double s = 0; for (int i = from; i < to; i++) s += (double)x[i] * x[i]; return sqrt(s / (to - from)); }
static double db(double x) { return 20 * log10(x > 1e-12 ? x : 1e-12); }
static void sine(float *x, int from, int to, double amp, double f, double sr) { for (int i = from; i < to; i++) x[i] = (float)(amp * sin(2 * 3.14159265358979 * f * i / sr)); }

int main(int argc, char **argv)
{
    HMODULE m = LoadLibraryA(argc > 1 ? argv[1] : "HallowMicChain.dll");
    if (!m) { printf("FAIL cannot load the plugin\n"); return 99; }
    vmain = (MainProc)GetProcAddress(m, "VSTPluginMain");
    if (!vmain) { printf("FAIL no VSTPluginMain\n"); return 99; }
    defaults();
    { AEffect *fx = make(48000);
      check(fx->magic == VST2_MAGIC && fx->numParams == 16 && fx->numInputs == 2 && fx->numOutputs == 2 && (fx->flags & VST2_FLAG_CAN_REPLACING) && (fx->flags & VST2_FLAG_PROGRAM_CHUNKS), "AEffect looks right", fx->numParams);
      check(fx->initialDelay == 72, "lookahead 1.5 ms at 48 kHz reported as latency", fx->initialDelay);
      char name[64] = { 0 }; fx->dispatcher(fx, VST2_GET_PARAM_NAME, 1, 0, name, 0); check(strcmp(name, "GateThr") == 0, "parameter names", 0);
      fx->dispatcher(fx, VST2_CLOSE, 0, 0, 0, 0); }

    const float rates[3] = { 44100, 48000, 96000 };
    for (int r = 0; r < 3; r++) {
        float sr = rates[r]; int n = (int)sr * 3; char what[96];
        float *x = calloc(n, 4), *y = calloc(n, 4);
        printf("-- %.0f Hz\n", sr);
        /* gate: loud passes, quiet is pulled down by the range (-40 dB) */
        defaults(); settings[0] = 1;
        AEffect *fx = make(sr);
        int h = n / 3;
        sine(x, 0, h, 0.1, 440, sr); sine(x, h, n, 0.0003, 440, sr);   /* -20 dBFS then about -70 dBFS */
        run(fx, x, y, n, 0);
        int L = fx->initialDelay;
        double open = db(rms(y, h / 2 + L, h)) - db(rms(x, h / 2, h - L));
        sprintf(what, "gate open: loud speech passes (gain dB)"); check(fabs(open) < 0.2, what, open);
        double closed = db(rms(y, n - (int)(sr * 0.2), n)) - db(rms(x, n - (int)(sr * 0.2) - L, n - L));
        sprintf(what, "gate closed below threshold: about -40 dB"); check(closed < -38 && closed > -42, what, closed);
        fx->dispatcher(fx, VST2_CLOSE, 0, 0, 0, 0);

        /* compressor ratio: threshold -30, ratio 4, -10 dBFS sine -> -25 dBFS */
        defaults(); settings[6] = 1;
        fx = make(sr); sine(x, 0, n, 0.3162, 1000, sr); run(fx, x, y, n, 0);
        double o = db(peak(y, n - (int)(sr * 0.3), n));
        check(fabs(o - -25.0) < 1.5, "compressor 4:1 above -30 dB: -10 dBFS in -> ~-25 out", o);
        fx->dispatcher(fx, VST2_CLOSE, 0, 0, 0, 0);
        settings[8] = 2; fx = make(sr); sine(x, 0, n, 0.1, 1000, sr); run(fx, x, y, n, 0);
        o = db(peak(y, n - (int)(sr * 0.3), n));
        check(fabs(o - -25.0) < 1.5, "compressor 2:1: -20 dBFS in -> ~-25 out", o);
        fx->dispatcher(fx, VST2_CLOSE, 0, 0, 0, 0);
        /* compressor timing: a step from -40 to -10 dB (steady level); attack 5 ms -> 90% of the gain change in ~11.5 ms */
        settings[8] = 4; fx = make(sr);
        for (int i = 0; i < n; i++) x[i] = i < h ? 0.01f : 0.3162f;
        run(fx, x, y, n, 0); L = fx->initialDelay;
        double finalOut = y[n - 1], t90 = -1, first = y[h + L];
        for (int i = h + L; i < n; i++) if (y[i] <= first - 0.9 * (first - finalOut)) { t90 = (i - h - L) * 1000.0 / sr; break; }
        check(t90 > 6 && t90 < 18, "compressor attack 5 ms: 90% of the gain change (ms)", t90);
        check(fabs(db(finalOut) - -25.0) < 0.3, "compressor settles at -25 dB on a steady level", db(finalOut));
        for (int i = 0; i < n; i++) x[i] = i < h ? 0.3162f : 0.01f;
        fx->dispatcher(fx, VST2_MAINS_CHANGED, 0, 1, 0, 0);
        run(fx, x, y, n, 0);
        double hi = db(y[h + L - 1] / 0.3162), lo = db(y[n - 1] / 0.01), t63 = -1;   /* gain in dB before / after */
        for (int i = h + L; i < n; i++) { double g = db(y[i] / 0.01); if (g >= hi + 0.632 * (lo - hi) && lo > hi) { t63 = (i - h - L) * 1000.0 / sr; break; } }
        check(t63 > 85 && t63 < 115, "compressor release 100 ms: 63% of the way back in dB (ms)", t63);
        fx->dispatcher(fx, VST2_CLOSE, 0, 0, 0, 0);

        /* limiter: ceiling -3 dB, never exceeded, even with +12 dB peaks and clicks */
        defaults(); settings[13] = 1;
        fx = make(sr); srand(7);
        for (int i = 0; i < n; i++) { double v = ((rand() / (double)RAND_MAX) * 2 - 1) * (i % 4800 < 600 ? 4.0 : 0.5); x[i] = (float)v; }
        for (int i = 1000; i < n; i += 9973) x[i] = 8.0f;
        run(fx, x, y, n, 0);
        double pk = peak(y, 0, n);
        check(pk <= 0.70795 + 1e-6, "limiter: output never above the -3 dB ceiling", db(pk));
        double clipped = 0; int cnt = 0; for (int i = 0; i < n; i++) if (fabs(y[i]) >= 0.70794) cnt++; clipped = 100.0 * cnt / n;
        check(clipped < 1.0, "limiter: lookahead does the work, the safety clip rarely (%)", clipped);
        fx->dispatcher(fx, VST2_CLOSE, 0, 0, 0, 0);

        /* everything on, hostile input: NaN, Inf, huge, denormals, silence; in-place buffers; must stay finite and fast */
        defaults(); settings[0] = settings[6] = settings[13] = 1; settings[11] = 12;
        fx = make(sr);
        for (int i = 0; i < n; i++) {
            int seg = (i / 4800) % 6;
            x[i] = seg == 0 ? NAN : seg == 1 ? INFINITY : seg == 2 ? 1e30f : seg == 3 ? 1e-40f : seg == 4 ? 0.0f : (float)(0.5 * sin(i * 0.05));
        }
        LARGE_INTEGER f0, t0, t1; QueryPerformanceFrequency(&f0); QueryPerformanceCounter(&t0);
        for (int rep = 0; rep < 3; rep++) run(fx, x, y, n, 1);
        QueryPerformanceCounter(&t1);
        int bad = 0; for (int i = 0; i < n; i++) if (!isfinite(y[i]) || fabs(y[i]) > 0.9) bad++;
        check(bad == 0, "all stages on: no NaN/Inf, nothing above the ceiling", bad);
        double secs = (double)(t1.QuadPart - t0.QuadPart) / f0.QuadPart, rt = secs / 9.0;   /* 9 s of stereo audio */
        sprintf(what, "speed: fraction of real time used (stereo)"); check(rt < 0.05, what, rt);
        /* denormal-heavy tail: 1e-40 input for a long stretch must not slow down */
        for (int i = 0; i < n; i++) x[i] = 1e-40f;
        QueryPerformanceCounter(&t0); run(fx, x, y, n, 0); QueryPerformanceCounter(&t1);
        double rt2 = (double)(t1.QuadPart - t0.QuadPart) / f0.QuadPart / 3.0;
        check(rt2 < 0.05, "denormal input: fraction of real time used", rt2);
        fx->dispatcher(fx, VST2_CLOSE, 0, 0, 0, 0);
        free(x); free(y);
    }

    /* chunk round trip + parameters */
    {
        defaults(); AEffect *a = make(48000), *b = make(48000);
        float want[16]; srand(3);
        for (int i = 0; i < 16; i++) { want[i] = rand() / (float)RAND_MAX; a->setParameter(a, i, want[i]); }
        void *data = 0; intptr_t sz = a->dispatcher(a, VST2_GET_CHUNK, 0, 0, &data, 0);
        check(sz == 76 && data != 0, "chunk is 76 bytes", (double)sz);
        int ok = (int)b->dispatcher(b, VST2_SET_CHUNK, 0, sz, data, 0);
        double maxd = 0; for (int i = 0; i < 16; i++) { double d = fabs(a->getParameter(a, i) - b->getParameter(b, i)); if (d > maxd) maxd = d; }
        check(ok && maxd < 1e-5, "chunk round-trips every parameter", maxd);
        double nd = 0; for (int i = 0; i < 16; i++) { if (i == 0 || i == 6 || i == 13) continue; double d = fabs(a->getParameter(a, i) - want[i]); if (d > nd) nd = d; }
        check(nd < 1e-4, "parameter 0..1 mapping round-trips", nd);
        unsigned char junk[76]; memset(junk, 0x41, sizeof junk);
        check(b->dispatcher(b, VST2_SET_CHUNK, 0, sizeof junk, junk, 0) == 0, "a wrong chunk is refused", 0);
        check(b->dispatcher(b, VST2_SET_CHUNK, 0, 8, data, 0) == 0, "a short chunk is refused", 0);
        a->dispatcher(a, VST2_CLOSE, 0, 0, 0, 0); b->dispatcher(b, VST2_CLOSE, 0, 0, 0, 0);
    }
    /* optional: a chunk written by Hallow Mixer (base64-decoded ChunkData of the Streamer preset) */
    if (argc > 2) {
        FILE *f = fopen(argv[2], "rb"); unsigned char buf[256]; size_t n = f ? fread(buf, 1, sizeof buf, f) : 0; if (f) fclose(f);
        AEffect *fx = make(48000);
        int ok = (int)fx->dispatcher(fx, VST2_SET_CHUNK, 0, (intptr_t)n, buf, 0);
        char thr[16] = { 0 }, ratio[16] = { 0 }, ceil_[16] = { 0 };
        fx->dispatcher(fx, VST2_GET_PARAM_DISPLAY, 7, 0, thr, 0); fx->dispatcher(fx, VST2_GET_PARAM_DISPLAY, 8, 0, ratio, 0); fx->dispatcher(fx, VST2_GET_PARAM_DISPLAY, 14, 0, ceil_, 0);
        printf("   Mixer chunk: comp threshold %s, ratio %s, ceiling %s\n", thr, ratio, ceil_);
        check(ok && strcmp(thr, "-28.0") == 0 && strcmp(ratio, "4.0") == 0 && strcmp(ceil_, "-1.0") == 0, "Hallow Mixer's Streamer chunk is read by the plugin", (double)n);
        fx->dispatcher(fx, VST2_CLOSE, 0, 0, 0, 0);
    }
    printf(fails ? "%d FAILED\n" : "all passed\n", fails);
    return fails;
}
