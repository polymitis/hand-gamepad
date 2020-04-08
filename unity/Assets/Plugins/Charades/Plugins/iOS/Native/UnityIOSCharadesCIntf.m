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

void UnityIOSCharadesCIntf_ProcessVideoFrame(intptr_t ptr)
{
    // In case of invalid buffer ref
    if (!ptr) {
        NSLog(@"Null pointer to XR frame passed");
        return;
    }

    struct UnityXRNativeSession *unityXRFrame = (struct UnityXRNativeSession *) ptr;
    ARFrame* frame = (__bridge ARFrame*)unityXRFrame->framePtr;
    CVPixelBufferRef buffer = frame.capturedImage;

    [[UnityIOSCharadesCIntf instance] processVideoFrame:(CVPixelBufferRef)buffer];
}

@end
