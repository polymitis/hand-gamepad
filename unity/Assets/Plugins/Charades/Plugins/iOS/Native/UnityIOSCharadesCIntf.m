#import <ARKit/ARKit.h>
#import <Charades/Charades.h>
#import <Foundation/Foundation.h>

// XRSessionExtensions.GetNativePtr
struct UnityXRNativeSession
{
    int version;
    void* framePtr;
};

@interface UnityIOSCharadesCIntf : NSObject

+ (Charades *)instance;

@end

@implementation UnityIOSCharadesCIntf

+ (Charades*)instance
{
    static Charades *_instance = nil;
    if (!_instance) {
        _instance = [[Charades alloc] init];
    }
    return _instance;
}

void UnityIOSCharadesCIntf_ProcessSRGBImage(intptr_t buffer, int width, int height)
{
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
