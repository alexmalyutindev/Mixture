using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using GraphProcessor;

namespace Mixture
{
	[System.Serializable, NodeMenuItem("Simulation/2D Fire (Experimental)")]
	public class Fire2DNode : BaseFluidSimulationNode 
	{
		[Input("Temperature")]
		public Texture inputTemperature;
		[Input("Fuel")]
		public Texture inputReaction;
		[Input("Velocity")]
		public Texture inputVelocity;
		[Input("Obstacles")]
		public Texture inputObstacles;
		
		[Output("Fire")]
		public Texture outputReaction;
		[Output("Smoke")]
		public Texture outputDensity;
		[Output("Velocity")]
		public Texture outputVelocity;
		[Output("Pressure")]
		public Texture outputPressure;
		[Output("Temperature")]
		public Texture outputTemperature;
		[Output("Divergence")]
		public Texture outputDivergence;

		public BorderMode borderMode = BorderMode.NoBorders;

		public int iterations = 50;
		public float vorticityStrength = 1.0f;
		public float densityDissipation = 0.99f;
		public float densityBuoyancy = 1.0f;
		public float densityWeight = 9.807f;
		public float temperatureAmount = 100.0f;
		public float temperatureDissipation = 0.995f;
		public float velocityDissipation = 0.95f;
		float ambientTemperature = 0.0f;
		public float reactionDecay = 0.001f;
		public float reactionAmount = 10.0f;
		public float reactionExtinguishment = 0.05f;

		public override string name => "2D Fire (Experimental)";

		protected override string computeShaderResourcePath => "Mixture/Fire2D";

		public override bool showDefaultInspector => true;
		public override Texture previewTexture => outputReaction;

		protected override MixtureSettings defaultSettings
		{
			get
			{
				var settings = base.defaultSettings;
				settings.dimension = OutputDimension.Texture2D;
				return settings;
			}
		}

		public override List<OutputDimension> supportedDimensions => new List<OutputDimension>() {
			OutputDimension.Texture2D,
		};

		// For now only available in realtime mixtures, we'll see later for static with a spritesheet mode maybe
		[IsCompatibleWithGraph]
		static bool IsCompatibleWithRealtimeGraph(BaseGraph graph) => MixtureUtils.IsRealtimeGraph(graph);

		Vector3 m_size;

		[SerializeField, HideInInspector]
		RenderTexture[] density, velocity, pressure, temperature, reaction;
		[SerializeField, HideInInspector]
		RenderTexture temp3f, obstacles;

        protected override void Enable()
        {
			base.Enable();

			settings.doubleBuffered = true;
			settings.outputChannels = OutputChannel.RGBA;

			density = new RenderTexture[2];
			density[READ] = AllocateRenderTexture("densityR", GraphicsFormat.R16_SFloat);
			density[WRITE] = AllocateRenderTexture("densityW", GraphicsFormat.R16_SFloat);
			
			temperature = new RenderTexture[2];
			temperature[READ] = AllocateRenderTexture("temperatureR", GraphicsFormat.R16_SFloat);
			temperature[WRITE] = AllocateRenderTexture("temperatureW", GraphicsFormat.R16_SFloat);
			
			velocity = new RenderTexture[2];
			velocity[READ] = AllocateRenderTexture("velocityR", GraphicsFormat.R16G16_SFloat);
			velocity[WRITE] = AllocateRenderTexture("velocityW", GraphicsFormat.R16G16_SFloat);
			
			pressure = new RenderTexture[2];
			pressure[READ] = AllocateRenderTexture("pressureR", GraphicsFormat.R16_SFloat);
			pressure[WRITE] = AllocateRenderTexture("pressureW", GraphicsFormat.R16_SFloat);
            
			reaction = new RenderTexture[2];
			reaction[READ] = AllocateRenderTexture("reactionR", GraphicsFormat.R16_SFloat);
			reaction[WRITE] = AllocateRenderTexture("reactionW", GraphicsFormat.R16_SFloat);
			
			obstacles = AllocateRenderTexture("Obstacles", GraphicsFormat.R8_SNorm);

			temp3f = AllocateRenderTexture("Temp", GraphicsFormat.R16G16_SFloat);
        }

		public override void RealtimeReset()
		{
			// Reset all temp textures

			ClearRenderTexture(density[READ]);
			ClearRenderTexture(velocity[READ]);
			ClearRenderTexture(temperature[READ]);
			ClearRenderTexture(pressure[READ]);
			ClearRenderTexture(obstacles);
			ClearRenderTexture(reaction[READ]);
		}

        protected override void Disable()
        {
			base.Disable();

			density[READ].Release();
			density[WRITE].Release();
			
			temperature[READ].Release();
			temperature[WRITE].Release();
			
			velocity[READ].Release();
			velocity[WRITE].Release();
			
			pressure[READ].Release();
			pressure[WRITE].Release();
			
			reaction[READ].Release();
			reaction[WRITE].Release();
			
			obstacles.Release();
			
			temp3f.Release();
        }

		// Source: GPU Gems 3 ch 38: Fast Fluid Dynamics Simulation on the GPU
		// and https://github.com/Scrawk/GPU-GEMS-3D-Fluid-Simulation
		protected override bool ProcessNode(CommandBuffer cmd)
		{
			if (!base.ProcessNode(cmd))
				return false;
			
			outputDensity = density[READ];
			outputVelocity = velocity[READ];
			outputPressure = pressure[READ];
			outputDivergence = temp3f;
			outputTemperature = temperature[READ];
            outputReaction = reaction[READ];

			m_size = new Vector3(settings.GetResolvedWidth(graph), settings.GetResolvedHeight(graph), settings.GetResolvedDepth(graph));

			ComputeObstacles(cmd, obstacles, borderMode);

			InjectObstacles(cmd, inputObstacles, obstacles);

			InjectVelocity(cmd, inputVelocity, velocity);

			//First off advect any buffers that contain physical quantities like density or temperature by the 
			//velocity field. Advection is what moves values around.
            ApplyAdvection(cmd, temperatureDissipation, 0.0f, temperature, velocity[READ], obstacles);
			ApplyAdvection(cmd, densityDissipation, 0.0f, density, velocity[READ], obstacles);
            ApplyAdvection(cmd, reactionDecay, 1.0f, reaction, velocity[READ], obstacles);

			//The velocity field also advects its self. 
			ApplyAdvectionVelocity(cmd, velocityDissipation, velocity, obstacles, density[READ], densityWeight);
			
			//Apply the effect the sinking colder smoke has on the velocity field
            ApplyBuoyancy(cmd, temperature[READ], velocity, densityBuoyancy, ambientTemperature);
			
			// Inject external data into the simulation
			InjectDensity(cmd, temperatureAmount, inputTemperature, temperature);
			InjectDensity(cmd, reactionAmount, inputReaction, reaction);
			
			//The smoke is formed when the reaction is extinguished. When the reaction amount
			//falls below the extinguishment factor smoke is added
            ApplyExtinguishmentImpulse(cmd, 1, reaction[READ], density, reactionExtinguishment);
			
			//The fluid sim math tends to remove the swirling movement of fluids.
			//This step will try and add it back in
			ComputeVorticityAndConfinement(cmd, temp3f, velocity, vorticityStrength);
			
			//Compute the divergence of the velocity field. In fluid simulation the
			//fluid is modelled as being incompressible meaning that the volume of the fluid
			//does not change over time. The divergence is the amount the field has deviated from being divergence free
			ComputeDivergence(cmd, velocity[READ], obstacles, temp3f);
			
			//This computes the pressure need return the fluid to a divergence free condition
			ComputePressure(cmd, temp3f, obstacles, pressure, iterations);
			
			//Subtract the pressure field from the velocity field enforcing the divergence free conditions
			ComputeProjection(cmd, obstacles, pressure[READ], velocity);

			return true;
		}
	}
}