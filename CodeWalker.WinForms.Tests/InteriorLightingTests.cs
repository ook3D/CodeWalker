using CodeWalker.Rendering;
using CodeWalker.GameFiles;
using System.Runtime.CompilerServices;
using CodeWalker.World;
using SharpDX;
using System.Reflection;
using System.Runtime.InteropServices;
using Xunit;

namespace CodeWalker.WinForms.Tests;

public class InteriorLightingTests
{
    [Fact]
    public void InteriorWeatherColoursAreIndependentFromExterior()
    {
        var type=typeof(Renderer).Assembly.GetType("CodeWalker.Rendering.WorldLighting")!;
        var evaluate=type.GetMethod("Evaluate",BindingFlags.Static|BindingFlags.NonPublic)!;
        var weather=new WeatherValues { lightArtificialExtUp=new Vector4(1,0,0,2),
            lightArtificialIntUp=new Vector4(0,1,0,3), lightArtificialIntDown=new Vector4(0,0,1,4),
            lightAmbDownWrap=0.25f };
        var frame=evaluate.Invoke(null,[new Timecycle(),weather,12f,true,false,0d])!;
        var frameType=frame.GetType();
        var parameters=(ShaderGlobalLightParams)frameType.GetProperty("Parameters")!.GetValue(frame)!;
        Assert.Equal(2f,parameters.LightArtificialAmbUp.Red);
        Assert.Equal(new Color4(0,3,0,0),(Color4)frameType.GetProperty("InteriorUp")!.GetValue(frame)!);
        Assert.Equal(new Color4(0,0,4,0.25f),(Color4)frameType.GetProperty("InteriorDown")!.GetValue(frame)!);
    }

    [Fact]
    public void InteriorShaderConstantsStayAligned()
    {
        Assert.Equal(128,Marshal.OffsetOf<BasicShaderPSGeomVars>(nameof(BasicShaderPSGeomVars.InteriorFlags)).ToInt32());
        Assert.Equal(0,Marshal.SizeOf<BasicShaderPSSceneVars>()%16);
        Assert.Equal(0,Marshal.SizeOf<DeferredLightPSVars>()%16);
    }
    [Fact]
    public void ShaderManagerReceivesInteriorColours()
    {
        // No GPU is needed to test the CPU-to-shader-manager handoff.
        var manager = (ShaderManager)RuntimeHelpers.GetUninitializedObject(typeof(ShaderManager));
        manager.GlobalLights = new ShaderGlobalLights();
        var lights = new ShaderGlobalLights
        {
            InteriorAmbientUp = new Color4(0.961f, 1, 0.855f, 0),
            InteriorAmbientDown = new Color4(1.3995f, 1.329f, 1.1415f, 0.25f)
        };
        manager.SetGlobalLightParams(lights);
        Assert.Equal(lights.InteriorAmbientUp, manager.GlobalLights.InteriorAmbientUp);
        Assert.Equal(lights.InteriorAmbientDown, manager.GlobalLights.InteriorAmbientDown);
        lights.InteriorAmbientUp = Color4.Black;
        manager.SetGlobalLightParams(lights);
        Assert.Equal(Color4.Black, manager.GlobalLights.InteriorAmbientUp);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RoomModifierUsesLocalBoundsAndResetsOnExit(bool useCalculatedBounds)
    {
        var type = typeof(Renderer).Assembly.GetType("CodeWalker.Rendering.InteriorLighting")!;
        var state = Activator.CreateInstance(type)!;
        void Call(string name, params object[] args) => type.GetMethod(name)!.Invoke(state, args);
        var instance = new YmapEntityDef
        {
            Position = new Vector3(220, -1745, 29),
            Orientation = Quaternion.RotationAxis(Vector3.UnitZ, MathF.PI / 2),
            Scale = new Vector3(2, 2, 1),
            Archetype = new MloArchetype { rooms = [
                new MCMloRoomDef { Index = 0, _Data = new CMloRoomDef {
                    bbMin = new Vector3(-100), bbMax = new Vector3(100), timecycleName = 2 } },
                new MCMloRoomDef { Index = 1, _Data = new CMloRoomDef {
                    bbMin = Vector3.Zero, bbMax = new Vector3(3, 2, 3),
                    timecycleName = 1, secondaryTimecycleName = 2 } }
            ] }
        };
        if (useCalculatedBounds)
        {
            var room = ((MloArchetype)instance.Archetype).rooms[1];
            room.BBMin_CW = room.BBMin;
            room.BBMax_CW = room.BBMax;
            room._Data.bbMin = Vector3.Zero;
            room._Data.bbMax = Vector3.Zero;
        }
        var primary = new TimecycleMod();
        var secondary = new TimecycleMod();
        primary.Dict["light_artificial_int_up_col_r"] = new() { value1 = 0.961f, value2 = 1 };
        primary.Dict["light_artificial_int_up_col_g"] = new() { value1 = 1, value2 = 1 };
        primary.Dict["light_artificial_int_up_col_b"] = new() { value1 = 0.855f, value2 = 1 };
        primary.Dict["light_artificial_int_up_intensity"] = new() { value1 = 1, value2 = 0 };
        secondary.Dict["light_artificial_int_up_col_g"] = new() { value1 = 0.8f, value2 = 1 };
        var mods = new Dictionary<uint, TimecycleMod> { [1] = primary, [2] = secondary };
        var lights = new ShaderGlobalLights();
        var weather = new WeatherValues(); // Weather alone provides no interior illumination.
        Call("Reset");
        var inside = instance.Position + instance.Orientation.Multiply(new Vector3(1, 1, 1) * instance.Scale);
        Call("Consider", instance, inside);
        Call("Apply", lights, weather, mods, true);
        Assert.Equal(new Color4(0.961f, 0.8f, 0.855f, 0), lights.InteriorAmbientUp);
        // A second frame outside must not keep the previous room or select limbo.
        Call("Reset");
        lights.InteriorAmbientUp = Color4.Black;
        Call("Consider", instance, instance.Position - Vector3.UnitZ);
        Call("Apply", lights, weather, mods, true);
        Assert.Equal(Color4.Black, lights.InteriorAmbientUp);
    }
}
