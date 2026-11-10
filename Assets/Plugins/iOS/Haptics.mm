// Short system haptics for Haptics.cs. style: 0 light, 1 medium, 2 heavy.
#import <UIKit/UIKit.h>

extern "C" void _gsHaptic(int style)
{
    UIImpactFeedbackStyle s = style <= 0 ? UIImpactFeedbackStyleLight
                            : style == 1 ? UIImpactFeedbackStyleMedium
                                         : UIImpactFeedbackStyleHeavy;
    UIImpactFeedbackGenerator *generator = [[UIImpactFeedbackGenerator alloc] initWithStyle:s];
    [generator prepare];
    [generator impactOccurred];
}
