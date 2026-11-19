/* Hallow Mic Chain - the small part of the VST 2.4 plugin ABI this plugin needs, written from the public
   binary interface (struct layout, opcode numbers, the VSTPluginMain entry point). Not the Steinberg SDK. */
#ifndef HALLOW_VST2ABI_H
#define HALLOW_VST2ABI_H
#include <stdint.h>

#define VST2_MAGIC 0x56737450 /* 'VstP' */

typedef struct AEffect AEffect;
typedef intptr_t (*Vst2HostCallback)(AEffect *fx, int32_t opcode, int32_t index, intptr_t value, void *ptr, float opt);
typedef intptr_t (*Vst2Dispatcher)(AEffect *fx, int32_t opcode, int32_t index, intptr_t value, void *ptr, float opt);
typedef void (*Vst2Process)(AEffect *fx, float **in, float **out, int32_t frames);
typedef void (*Vst2ProcessDouble)(AEffect *fx, double **in, double **out, int32_t frames);
typedef void (*Vst2SetParam)(AEffect *fx, int32_t index, float value);
typedef float (*Vst2GetParam)(AEffect *fx, int32_t index);

struct AEffect {
    int32_t magic;
    Vst2Dispatcher dispatcher;
    Vst2Process process_deprecated;
    Vst2SetParam setParameter;
    Vst2GetParam getParameter;
    int32_t numPrograms;
    int32_t numParams;
    int32_t numInputs;
    int32_t numOutputs;
    int32_t flags;
    intptr_t reserved1;
    intptr_t reserved2;
    int32_t initialDelay;
    int32_t realQualities_deprecated;
    int32_t offQualities_deprecated;
    float ioRatio_deprecated;
    void *object;
    void *user;
    int32_t uniqueID;
    int32_t version;
    Vst2Process processReplacing;
    Vst2ProcessDouble processDoubleReplacing;
    char future[56];
};

/* AEffect.flags */
#define VST2_FLAG_CAN_REPLACING   (1 << 4)
#define VST2_FLAG_PROGRAM_CHUNKS  (1 << 5)

/* host -> plugin opcodes */
enum {
    VST2_OPEN = 0, VST2_CLOSE = 1, VST2_SET_PROGRAM = 2, VST2_GET_PROGRAM = 3, VST2_SET_PROGRAM_NAME = 4, VST2_GET_PROGRAM_NAME = 5,
    VST2_GET_PARAM_LABEL = 6, VST2_GET_PARAM_DISPLAY = 7, VST2_GET_PARAM_NAME = 8,
    VST2_SET_SAMPLE_RATE = 10, VST2_SET_BLOCK_SIZE = 11, VST2_MAINS_CHANGED = 12,
    VST2_GET_CHUNK = 23, VST2_SET_CHUNK = 24, VST2_CAN_BE_AUTOMATED = 26,
    VST2_GET_PLUG_CATEGORY = 35, VST2_GET_EFFECT_NAME = 45, VST2_GET_VENDOR_STRING = 47, VST2_GET_PRODUCT_STRING = 48,
    VST2_GET_VENDOR_VERSION = 49, VST2_CAN_DO = 51, VST2_GET_TAIL_SIZE = 52, VST2_GET_VST_VERSION = 58,
    VST2_START_PROCESS = 71, VST2_STOP_PROCESS = 72
};

/* plugin -> host */
#define VST2_HOST_VERSION 1

#endif
