#import <Foundation/Foundation.h>

// Returns CFBundleVersion (the build number) for the `app_build` evaluation
// attribute. Unity marshals the returned malloc'd C string.
extern "C" const char *AppSpikeBundleVersion(void) {
    NSString *bundleVersion =
        [[NSBundle mainBundle] objectForInfoDictionaryKey:@"CFBundleVersion"];
    if (bundleVersion == nil) {
        return NULL;
    }
    const char *utf8 = [bundleVersion UTF8String];
    if (utf8 == NULL) {
        return NULL;
    }
    char *copy = (char *)malloc(strlen(utf8) + 1);
    if (copy == NULL) {
        return NULL;
    }
    strcpy(copy, utf8);
    return copy;
}
