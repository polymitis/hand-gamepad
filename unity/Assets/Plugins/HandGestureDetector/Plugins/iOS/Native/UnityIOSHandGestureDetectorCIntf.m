#import <ARKit/ARKit.h>
#import <HandGestureDetector/HandGestureDetector.h>
#import <Foundation/Foundation.h>

typedef void(*UnityIOSHandGestureDetectorCIntf_DidOutputPixelBufferCb)(intptr_t buffer, int width, int height);

UnityIOSHandGestureDetectorCIntf_DidOutputPixelBufferCb UnityIOSHandGestureDetectorCIntf_DidOutputPixelBuffer = NULL;

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
    NSLog(@"UnityIOSHandGestureDetectorCIntf: didOutputPixelBuffer: (intptr) %@", CVPixelBufferGetBaseAddress(pixelBuffer));
    UnityIOSHandGestureDetectorCIntf_DidOutputPixelBuffer(CVPixelBufferGetBaseAddress(pixelBuffer),
                                               (int)CVPixelBufferGetWidth(pixelBuffer),
                                               (int)CVPixelBufferGetHeight(pixelBuffer));
}

void UnityIOSHandGestureDetectorCIntf_SetDidOutputPixelBufferCb(UnityIOSHandGestureDetectorCIntf_DidOutputPixelBufferCb callback) {
    UnityIOSHandGestureDetectorCIntf_DidOutputPixelBuffer = callback;
}

void UnityIOSHandGestureDetectorCIntf_ProcessSRGBImage(intptr_t buffer, int width, int height) {
    // In case of invalid buffer ref
    if (!buffer) {
        NSLog(@"Null pointer to XR frame passed");
        return;
    }

    CVPixelBufferRef image;
    CVPixelBufferCreateWithBytes(NULL,
                                 width,
                                 height,
                                 kCVPixelFormatType_32BGRA,
                                 (void*)buffer,
                                 width * 4,
                                 NULL,
                                 0,
                                 NULL,
                                 &image);

    [[UnityIOSHandGestureDetectorCIntf instance] processVideoFrame:image];
}

@end
