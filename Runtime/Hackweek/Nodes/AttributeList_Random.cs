﻿using System.Collections.Generic;
using UnityEngine;
using GraphProcessor;
using System.Linq;
using UnityEngine.Rendering;
//using System;
using Net3dBool;

namespace Mixture
{
	[System.Serializable, NodeMenuItem("Attribute List Random")]
	public class AttributeList_Random : MixtureNode
    {
        [Output("Output")]
        public MixtureAttributeList attributes = new MixtureAttributeList();

		public override string	name => "Attribute List Random";

		public override bool    hasPreview => false;
		public override bool    showDefaultInspector => true;

        public int              attributeCount = 24;

        [Input("Seed")]
        public int seed = 0;
        public float dirRange = 1;
        public Vector3 scaleRange = new Vector3(0,0,0);
        public float uniformscaleRange = 0;
        public Vector3 posRange = new Vector3(0, 0, 0);
        public float scaleFromDir = 0;


        protected override void Enable()
		{
		}

		// Functions with Attributes must be either protected or public otherwise they can't be accessed by the reflection code
		// [CustomPortBehavior(nameof(inputMeshes))]
		// public IEnumerable< PortData > ListMaterialProperties(List< SerializableEdge > edges)
		// {
        //     yield return new PortData
        //     {
        //         identifier = nameof(inputMeshes),
        //         displayName = "Input Meshes",
        //         allowMultiple = true,
        //         displayType()
        //     };
		// }

		// [CustomPortInput(nameof(inputMeshes), typeof(MixtureMesh))]
		// protected void GetMaterialInputs(List< SerializableEdge > edges)
		// {
        //     if (inputMeshes == null)
        //         inputMeshes = new List<MixtureMesh>();
        //     inputMeshes.Clear();
		// 	foreach (var edge in edges)
        //     {
        //         if (edge.passThroughBuffer is MixtureMesh m)
        //             inputMeshes.Add(m);
        //     }
		// }

		protected override bool ProcessNode(CommandBuffer cmd)
		{
            attributes.Clear();

            for (int i = 0; i < attributeCount; i++)
            {
                Random.InitState((seed*2345 + i)*1234);

                Vector3 normal = new Vector3(Random.Range(-dirRange, dirRange), Random.Range(0, dirRange), Random.Range(-dirRange, dirRange));
                normal += new Vector3(0, 0.5f, 0);
                normal.Normalize();
                //Debug.Log(normal);

                Vector3 scale = new Vector3(Random.Range(-scaleRange.x, scaleRange.x), Random.Range(-scaleRange.y, scaleRange.y), Random.Range(-scaleRange.z, scaleRange.z));
                scale += Vector3.one;
                scale *= Random.Range(1 - uniformscaleRange, 1 + uniformscaleRange);


                Vector3 pos = new Vector3(Random.Range(-posRange.x, posRange.x), Random.Range(-posRange.y, posRange.y), Random.Range(-posRange.z, posRange.z));

                attributes.Add(new MixtureAttribute{
                    {"position", pos},
                    {"scale", scale},
                    {"normal", normal},
                });
            }
			return true;
		}
    }
}