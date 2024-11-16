// AMD uses 32/64, NVIDIA uses 16/32 wavefront/warp size
#define AMD


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
Texture2D<uint4> inputTexture : register(t0);
RWTexture2D<uint4> outputTexture : register(u0);


// Constant buffer for precomputed color ranges
cbuffer ColorRangeBuffer : register(b0)
{
    ColorRanges colorRanges;
};


inline int IsInRange(uint value, Range range)
{
    if (range.maximum >= 1 &&
        value >= range.minimum && value <= range.maximum)
    {
        return 1;
    } else
        return -1;
}


#ifdef AMD
[numthreads(16, 4 , 1)]
#else 
[numthreads(8, 4, 1)]]
#endif
void main(uint3 DTid : SV_DispatchThreadID)
{
    
    // load the pixel 
    uint4 pixelColor = inputTexture.Load(int3(DTid.xy, 0));
    
    //check if 
    bool inRange = false;
    
    for (uint i = 0; i < colorRanges.numOfRanges; i++)
    {
        ColorRange range = colorRanges.ranges[i];
        if (IsInRange(pixelColor.z, range.redRange) && IsInRange(pixelColor.y, range.greenRange) && IsInRange(pixelColor.x, range.blueRange) > 0)
        {
            inRange = true;
            break;
        }
    }
    
    // write the pixel
    outputTexture[DTid.xy] = inRange ? pixelColor : colorRanges.swapColor;
}


