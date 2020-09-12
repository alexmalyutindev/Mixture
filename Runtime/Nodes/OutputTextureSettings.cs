using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;
using System;
using System.Linq;
using UnityEngine.Rendering;

namespace Mixture
{
    [Serializable]
    public class OutputTextureSettings
    {
        public Texture						inputTexture = null;
        public string						name = "Input #";
        public bool							enableCompression = true;
        public MixtureCompressionFormat		compressionFormat = MixtureCompressionFormat.DXT5;
        public MixtureCompressionQuality	compressionQuality = MixtureCompressionQuality.Best;
        public bool							hasMipMaps = false;
        public Shader						customMipMapShader = null;
        public bool							mainAsset = false;

        public Material						finalCopyMaterial = null;
        [NonSerialized]
        public CustomRenderTexture			finalCopyRT = null;

        [System.NonSerialized]
        Material							_customMipMapMaterial = null;
        public Material						customMipMapMaterial
        {
            get
            {
                if (_customMipMapMaterial == null || _customMipMapMaterial.shader != customMipMapShader)
                {
                    if (_customMipMapMaterial != null)
                        Material.DestroyImmediate(_customMipMapMaterial, false);
                    _customMipMapMaterial = new Material(customMipMapShader) { hideFlags = HideFlags.HideAndDontSave };
                }

                return _customMipMapMaterial;
            }
        }
        // A second temporary render texture with mip maps is needed to generate the custom mip maps.
        // It's needed because we can't read/write to the same render target even between different mips
        [NonSerialized]
        public CustomRenderTexture			mipmapTempRT = null;
        [NonSerialized]
        public MaterialPropertyBlock		mipMapPropertyBlock = null;
    }
}