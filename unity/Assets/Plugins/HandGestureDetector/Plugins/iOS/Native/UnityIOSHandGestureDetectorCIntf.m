#import <ARKit/ARKit.h>
#import <HandGestureDetector/HandGestureDetector.h>
#import <Foundation/Foundation.h>

typedef void(*UnityIOSHandGestureDetectorCIntf_DidOutputPixelBufferCb)(intptr_t pixelBuffer, int width, int height);

typedef void(*UnityIOSHandGestureDetectorCIntf_DidOutputHandLandmarksCb)(intptr_t hlmPkt);

typedef void(*UnityIOSHandGestureDetectorCIntf_DidOutputHandRectsCb)(intptr_t hrcPkt);

typedef void(*UnityIOSHandGestureDetectorCIntf_DidOutputPalmRectsCb)(intptr_t hrcPkt);

UnityIOSHandGestureDetectorCIntf_DidOutputPixelBufferCb UnityIOSHandGestureDetectorCIntf_DidOutputPixelBuffer = NULL;
UnityIOSHandGestureDetectorCIntf_DidOutputHandLandmarksCb UnityIOSHandGestureDetectorCIntf_DidOutputHandLandmarks = NULL;
UnityIOSHandGestureDetectorCIntf_DidOutputHandLandmarksCb UnityIOSHandGestureDetectorCIntf_DidOutputHandRects = NULL;
UnityIOSHandGestureDetectorCIntf_DidOutputHandLandmarksCb UnityIOSHandGestureDetectorCIntf_DidOutputPalmRects = NULL;

@interface UnityIOSHandGestureDetectorCIntf: NSObject <HandGestureDetectorDelegate>

+ (UnityIOSHandGestureDetectorCIntf *)cintf;

+ (HandGestureDetector *)instance;

@end

@implementation UnityIOSHandGestureDetectorCIntf

+ (UnityIOSHandGestureDetectorCIntf*)cintf {
    static UnityIOSHandGestureDetectorCIntf *_cintf = nil;
    if (!_cintf) {
        _cintf = [[UnityIOSHandGestureDetectorCIntf alloc] init];
    }
    return _cintf;
}

+ (HandGestureDetector*)instance {
    static HandGestureDetector *_instance = nil;
    if (!_instance) {
        _instance = [[HandGestureDetector alloc] init];
        _instance.delegate = [UnityIOSHandGestureDetectorCIntf cintf];
    }
    return _instance;
}

- (void)handGestureDetector:hgd didOutputPixelBuffer:(CVPixelBufferRef)pixelBuffer {
    CVPixelBufferLockBaseAddress(pixelBuffer, 0);
    if (CVPixelBufferGetBaseAddress(pixelBuffer))
        UnityIOSHandGestureDetectorCIntf_DidOutputPixelBuffer(CVPixelBufferGetBaseAddress(pixelBuffer), (int)CVPixelBufferGetWidth(pixelBuffer), (int)CVPixelBufferGetHeight(pixelBuffer));
    CVPixelBufferUnlockBaseAddress(pixelBuffer, 0);
}

- (void)handGestureDetector:hgd didOutputHandLandmarks:(float*)hlmPkt {
    if (hlmPkt)
        UnityIOSHandGestureDetectorCIntf_DidOutputHandLandmarks(hlmPkt);
}

- (void)handGestureDetector:hgd didOutputHandRects:(float*)hrcPkt {
    if (hrcPkt)
        UnityIOSHandGestureDetectorCIntf_DidOutputHandRects(hrcPkt);
}

- (void)handGestureDetector:hgd didOutputPalmRects:(float*)hrcPkt {
    if (hrcPkt)
        UnityIOSHandGestureDetectorCIntf_DidOutputPalmRects(hrcPkt);
}

void UnityIOSHandGestureDetectorCIntf_SetDidOutputPixelBufferCb(UnityIOSHandGestureDetectorCIntf_DidOutputPixelBufferCb callback) {
    UnityIOSHandGestureDetectorCIntf_DidOutputPixelBuffer = callback;
}

void UnityIOSHandGestureDetectorCIntf_SetDidOutputHandLandmarksCb(UnityIOSHandGestureDetectorCIntf_DidOutputHandLandmarksCb callback) {
    UnityIOSHandGestureDetectorCIntf_DidOutputHandLandmarks = callback;
}

void UnityIOSHandGestureDetectorCIntf_SetDidOutputHandRectsCb(UnityIOSHandGestureDetectorCIntf_DidOutputHandRectsCb callback) {
    UnityIOSHandGestureDetectorCIntf_DidOutputHandRects = callback;
}

void UnityIOSHandGestureDetectorCIntf_SetDidOutputPalmRectsCb(UnityIOSHandGestureDetectorCIntf_DidOutputPalmRectsCb callback) {
    UnityIOSHandGestureDetectorCIntf_DidOutputPalmRects = callback;
}

void UnityIOSHandGestureDetectorCIntf_ProcessSRGBImage(intptr_t imageBuffer, int width, int height) {
    // In case of invalid buffer ref
    if (!imageBuffer) {
        NSLog(@"Null pointer to SRGB image buffer passed");
        return;
    }
    @autoreleasepool {
        CVPixelBufferRef pixelBuffer = NULL;
        NSDictionary *attributes = @{
            @"IOSurfaceCoreAnimationCompatibility": @YES
        };
        CVPixelBufferCreate(kCFAllocatorDefault, width, height, kCVPixelFormatType_32BGRA, (__bridge CFDictionaryRef)attributes, &pixelBuffer);
        CVPixelBufferLockBaseAddress(pixelBuffer, 0);
        memcpy(CVPixelBufferGetBaseAddress(pixelBuffer), imageBuffer, CVPixelBufferGetDataSize(pixelBuffer));
        CVPixelBufferUnlockBaseAddress(pixelBuffer, kCVPixelBufferLock_ReadOnly);
        [[UnityIOSHandGestureDetectorCIntf instance] processPixelBuffer:pixelBuffer];
        CVPixelBufferRelease(pixelBuffer);
    }
}

@end
