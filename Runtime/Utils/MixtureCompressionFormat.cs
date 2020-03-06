using UnityEngine;

namespace Mixture
{
    // Maybe this is too much ¯\_(ツ)_/¯
    public enum MixtureCompressionFormat
    {
        DXT1 = TextureFormat.DXT1,
        DXT5 = TextureFormat.DXT5,
        RGB9e5Float = TextureFormat.RGB9e5Float,
        BC4 = TextureFormat.BC4,
        BC5 = TextureFormat.BC5,
        BC6H = TextureFormat.BC6H,
        DXT1Crunched = TextureFormat.DXT1Crunched,
        DXT5Crunched = TextureFormat.DXT5Crunched,
        PVRTC_RGB2 = TextureFormat.PVRTC_RGB2,
        PVRTC_RGBA2 = TextureFormat.PVRTC_RGBA2,
        PVRTC_RGB4 = TextureFormat.PVRTC_RGB4,
        PVRTC_RGBA4 = TextureFormat.PVRTC_RGBA4,
        ETC_RGB4 = TextureFormat.ETC_RGB4,
        EAC_R = TextureFormat.EAC_R,
        EAC_R_SIGNED = TextureFormat.EAC_R_SIGNED,
        EAC_RG = TextureFormat.EAC_RG,
        EAC_RG_SIGNED = TextureFormat.EAC_RG_SIGNED,
        ETC2_RGB = TextureFormat.ETC2_RGB,
        ETC2_RGBA1 = TextureFormat.ETC2_RGBA1,
        ETC2_RGBA8 = TextureFormat.ETC2_RGBA8,
        ASTC_4x4 = TextureFormat.ASTC_4x4,
        ASTC_5x5 = TextureFormat.ASTC_5x5,
        ASTC_6x6 = TextureFormat.ASTC_6x6,
        ASTC_8x8 = TextureFormat.ASTC_8x8,
        ASTC_10x10 = TextureFormat.ASTC_10x10,
        ASTC_12x12 = TextureFormat.ASTC_12x12,
        ETC_RGB4Crunched = TextureFormat.ETC_RGB4Crunched,
        ETC2_RGBA8Crunched = TextureFormat.ETC2_RGBA8Crunched,
        ASTC_HDR_4x4 = TextureFormat.ASTC_HDR_4x4,
        ASTC_HDR_5x5 = TextureFormat.ASTC_HDR_5x5,
        ASTC_HDR_6x6 = TextureFormat.ASTC_HDR_6x6,
        ASTC_HDR_8x8 = TextureFormat.ASTC_HDR_8x8,
        ASTC_HDR_10x10 = TextureFormat.ASTC_HDR_10x10,
        ASTC_HDR_12x12 = TextureFormat.ASTC_HDR_12x12,
        ASTC_RGB_4x4 = TextureFormat.ASTC_4x4,
        ASTC_RGB_5x5 = TextureFormat.ASTC_5x5,
        ASTC_RGB_6x6 = TextureFormat.ASTC_6x6,
        ASTC_RGB_8x8 = TextureFormat.ASTC_8x8,
        ASTC_RGB_10x10 = TextureFormat.ASTC_10x10,
        ASTC_RGB_12x12 = TextureFormat.ASTC_12x12,
        ASTC_RGBA_4x4 = TextureFormat.ASTC_4x4,
        ASTC_RGBA_5x5 = TextureFormat.ASTC_5x5,
        ASTC_RGBA_6x6 = TextureFormat.ASTC_6x6,
        ASTC_RGBA_8x8 = TextureFormat.ASTC_8x8,
        ASTC_RGBA_10x10 = TextureFormat.ASTC_10x10,
        ASTC_RGBA_12x12 = TextureFormat.ASTC_12x12,
    }
}