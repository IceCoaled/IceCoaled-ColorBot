// Constants for GPU wave front sizes

//#define NVIDIA 32
#define AMD 64 // Uncomment the appropriate line based on GPU

// Screen dimensions
#define SCREEN_WIDTH 2560
#define SCREEN_HEIGHT 1440

// Define the Range structure for color limits
struct Range
{
    uint minimum;
    uint maximum;
};

// Define ColorRange to hold RGB ranges
struct ColorRange
{
    Range redRange;
    Range greenRange;
    Range blueRange;
};

// Define ColorRanges to hold multiple ranges and the swap color
struct ColorRanges
{
    uint numOfRanges;
    ColorRange ranges[45]; // Define a fixed maximum size
    uint4 swapColor;
};

// Buffer definitions
StructuredBuffer<uint4> rawInput : register(t0);
RWStructuredBuffer<uint4> rawOutput : register(u0);

// Constant buffer for precomputed color ranges
cbuffer ColorRangeBuffer : register(b0)
{
    ColorRanges colorRanges;
};


inline bool IsInRange(uint value, Range range)
{
    return value >= range.minimum && value <= range.maximum;
}


#if AMD
[numthreads(AMD, 1, 1)]
#else
[numthreads(NVIDIA, 1, 1)]
#endif
void main(uint3 DTid : SV_DispatchThreadID)
{
    // Get the pixel index
    uint pixelIndex = DTid.y * SCREEN_WIDTH + DTid.x;
    
    // load the pixel 
    uint4 pixelColor = rawInput[pixelIndex];
    
    //check if 
    bool inRange = false;
    
    for (uint i = 0; i < colorRanges.numOfRanges; i++)
    {
        ColorRange range = colorRanges.ranges[i];
        if (IsInRange(pixelColor.x, range.redRange) && IsInRange(pixelColor.y, range.greenRange) && IsInRange(pixelColor.z, range.blueRange))
        {
            inRange = true;
            break;
        }
    }
    
    // write the pixel
    rawOutput[pixelIndex] = inRange ? pixelColor : colorRanges.swapColor;
}


