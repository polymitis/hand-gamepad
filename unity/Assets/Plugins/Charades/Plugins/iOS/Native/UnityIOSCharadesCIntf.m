#import <ARKit/ARKit.h>
#import <Charades/Charades.h>
#import <Foundation/Foundation.h>

typedef void(*UnityIOSCharadesCIntf_DidOutputPixelBufferCb)(intptr_t buffer, int width, int height);

UnityIOSCharadesCIntf_DidOutputPixelBufferCb UnityIOSCharadesCIntf_DidOutputPixelBuffer = NULL;

@interface UnityIOSCharadesCIntf: NSObject <CharadesDelegate>

+ (UnityIOSCharadesCIntf *)cintf;

+ (Charades *)instance;

@end

@implementation UnityIOSCharadesCIntf

+ (UnityIOSCharadesCIntf*)cintf {
    static UnityIOSCharadesCIntf *_cintf = nil;
    if (!_cintf) {
        _cintf = [[UnityIOSCharadesCIntf alloc] init];
    }
    return _cintf;
}

+ (Charades*)instance {
    static Charades *_instance = nil;
    if (!_instance) {
        _instance = [[Charades alloc] init];
        _instance.delegate = [UnityIOSCharadesCIntf cintf];
    }
    return _instance;
}

- (void)charades:charades didOutputPixelBuffer:(CVPixelBufferRef)pixelBuffer {
    UnityIOSCharadesCIntf_DidOutputPixelBuffer(CVPixelBufferGetBaseAddress(pixelBuffer),
                                               (int)CVPixelBufferGetWidth(pixelBuffer),
                                               (int)CVPixelBufferGetHeight(pixelBuffer));
}

void UnityIOSCharadesCIntf_SetDidOutputPixelBufferCb(UnityIOSCharadesCIntf_DidOutputPixelBufferCb callback) {
    UnityIOSCharadesCIntf_DidOutputPixelBuffer = callback;
}

void UnityIOSCharadesCIntf_ProcessSRGBImage(intptr_t buffer, int width, int height) {
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

    [[UnityIOSCharadesCIntf instance] processVideoFrame:image];
}

@end
