using CodeWalker.GameFiles;
using CodeWalker.Rendering;
using SharpDX;
using Xunit;

namespace CodeWalker.WinForms.Tests;

public class FacialAnimationTests
{
    private static ClipMapEntry Clip(byte track, Vector3 value) => new()
    {
        OverridePlayTime = true, PlayTime = 0,
        Clip = new ClipAnimation { StartTime = 0, EndTime = 10, Rate = 1,
            Animation = new Animation { Frames = 2, SequenceFrameLimit = 2, Duration = 10,
                BoneIds = new ResourceSimpleList64_s<AnimationBoneId> { data_items = [new() { BoneId = 1, Track = track }] },
                Sequences = new ResourcePointerList64<Sequence> { data_items = [new Sequence { Sequences =
                    [new AnimSequence { Channels = [new AnimChannelStaticVector3 { Value = value }] }] }] } } }
    };

    private static (Renderable Renderable, crBoneData Bone) Create()
    {
        var bone = new crBoneData { BoneId = 1, DefaultTranslation = new Vector3(1, 2, 3), DefaultRotation = Quaternion.Identity, DefaultScale = Vector3.One };
        var renderable = new Renderable { Skeleton = new crSkeletonData { BonesSorted = [bone], BonesMap = new() { [1] = bone } } };
        return (renderable, bone);
    }

    [Theory]
    [InlineData(24)]
    [InlineData(25)]
    [InlineData(26)]
    [InlineData(37)]
    public void ExpressionInputsDoNotDirectlyDeformBones(byte track)
    {
        var (renderable, bone) = Create();
        renderable.ClipMapEntry = Clip(track, new Vector3(50));
        renderable.UpdateAnims(0);
        Assert.Equal(bone.DefaultTranslation, bone.AnimTranslation);
        Assert.Equal(bone.DefaultRotation, bone.AnimRotation);
        Assert.Equal(bone.DefaultScale, bone.AnimScale);
    }

    [Fact]
    public void FirstFrameAndReplacementAtSameTimeAreEvaluated()
    {
        var (renderable, bone) = Create();
        renderable.ClipMapEntry = Clip(0, new Vector3(4));
        renderable.UpdateAnims(0);
        Assert.Equal(new Vector3(4), bone.AnimTranslation);
        renderable.ClipMapEntry = Clip(0, new Vector3(5));
        renderable.UpdateAnims(0);
        Assert.Equal(new Vector3(5), bone.AnimTranslation);
        renderable.ClipMapEntry = Clip(25, new Vector3(90));
        renderable.UpdateAnims(0);
        Assert.Equal(bone.DefaultTranslation, bone.AnimTranslation);
        Assert.Equal(Quaternion.Identity, bone.AnimRotation);
    }

    private static Expression Program(params ExpressionInstrBase[] ops) => new()
    {
        Streams = new ResourcePointerList64<ExpressionStream> { data_items = [new ExpressionStream { Instructions = ops }] }
    };

    [Fact]
    public void FacialInputsDriveExpressionOutputsBeforeSkinning()
    {
        var (renderable,bone)=Create();
        renderable.ClipMapEntry=Clip(25,new Vector3(0.25f));
        renderable.Expression=Program(
            new ExpressionInstrBone { Type=ExpressionInstrType.TrackGet, BoneId=1, Track=25 },
            new ExpressionInstrBone { Type=ExpressionInstrType.TrackSetOffset, BoneId=1, Track=0 });
        renderable.UpdateAnims(0);
        Assert.Equal(bone.DefaultTranslation+new Vector3(0.25f),bone.AnimTranslation);
        Assert.Null(renderable.FacialExpressionError);
    }

    [Fact]
    public void EmbeddedClipExpressionIsEvaluatedWithoutAnExternalExpression()
    {
        var (renderable, bone) = Create();
        var entry = Clip(25, new Vector3(0.25f));
        var animation = Assert.IsType<ClipAnimation>(entry.Clip);
        entry.Clip = new ClipAnimationExpression
        {
            StartTime = animation.StartTime,
            EndTime = animation.EndTime,
            Rate = animation.Rate,
            Animation = animation.Animation,
            Expressions = Program(
                new ExpressionInstrBone { Type = ExpressionInstrType.TrackGet, BoneId = 1, Track = 25 },
                new ExpressionInstrBone { Type = ExpressionInstrType.TrackSetOffset, BoneId = 1, Track = 0 }),
        };
        renderable.ClipMapEntry = entry;

        renderable.UpdateAnims(0);

        Assert.Equal(bone.DefaultTranslation + new Vector3(0.25f), bone.AnimTranslation);
        Assert.Null(renderable.FacialExpressionError);
    }

    [Fact]
    public void FaceClipAndExpressionWorkWithoutABodyClip()
    {
        var (renderable, bone) = Create();
        renderable.FaceClip = Clip(25, new Vector3(0.5f));
        renderable.Expression = Program(
            new ExpressionInstrBone { Type = ExpressionInstrType.TrackGet, BoneId = 1, Track = 25 },
            new ExpressionInstrBone { Type = ExpressionInstrType.TrackSetOffset, BoneId = 1, Track = 0 });

        renderable.UpdateAnims(0);

        Assert.Equal(bone.DefaultTranslation + new Vector3(0.5f), bone.AnimTranslation);
    }

    [Fact]
    public void SeparateFaceClipSuppliesExpressionInputsAtBodyTime()
    {
        var (renderable,bone)=Create();
        renderable.ClipMapEntry=Clip(0,new Vector3(9));
        renderable.FaceClip=Clip(25,new Vector3(0.5f));
        renderable.Expression=Program(
            new ExpressionInstrBone { Type=ExpressionInstrType.TrackGet, BoneId=1, Track=25 },
            new ExpressionInstrBone { Type=ExpressionInstrType.TrackSetOffset, BoneId=1, Track=0 });
        renderable.UpdateAnims(0);
        Assert.Equal(bone.DefaultTranslation+new Vector3(0.5f),bone.AnimTranslation);
        renderable.FaceClip=null;
        renderable.UpdateAnims(0);
        Assert.Equal(bone.DefaultTranslation,bone.AnimTranslation);
    }

    [Fact]
    public void UnsupportedExpressionDoesNotPartiallyOverwriteBodyPose()
    {
        var (renderable,bone)=Create();
        renderable.ClipMapEntry=Clip(0,new Vector3(9));
        renderable.Expression=Program(
            new ExpressionInstrVector { Type=ExpressionInstrType.PushVector, Value=Vector4.One },
            new ExpressionInstrBone { Type=ExpressionInstrType.TrackSet, BoneId=1, Track=0 },
            new ExpressionInstrEmpty { Type=(ExpressionInstrType)255 });
        renderable.UpdateAnims(0);
        Assert.Equal(new Vector3(9),bone.AnimTranslation);
        Assert.Contains("Unsupported",renderable.FacialExpressionError);
    }

    [Fact]
    public void PiecewiseBlendUsesSourceTrackAndThreshold()
    {
        var (renderable,bone)=Create();
        renderable.ClipMapEntry=Clip(25,new Vector3(2));
        var values=new Vector4[15];
        values[0]=new Vector4(1); // x = input, until threshold
        values[6]=new Vector4(1); // x threshold
        values[9]=new Vector4(3); // x = 3 * input after threshold
        var expression=Program(
            new ExpressionInstrBlend { Type=ExpressionInstrType.BlendVector, SourceCount=1, NumSourceWeights=2,
                SourceInfos=[new() { TrackIndex=0, ComponentOffset=0 }], Values=values },
            new ExpressionInstrBone { Type=ExpressionInstrType.TrackSetOffset, BoneId=1, Track=0 });
        expression.Tracks.data_items=[new ExpressionTrack { BoneId=1,Track=25 }];
        renderable.Expression=expression;
        renderable.UpdateAnims(0);
        Assert.Equal(bone.DefaultTranslation+new Vector3(6,0,0),bone.AnimTranslation);
    }

    [Fact]
    public void MalformedBlendDoesNotCommitOutputs()
    {
        var (renderable,bone)=Create();
        renderable.ClipMapEntry=Clip(0,new Vector3(9));
        renderable.Expression=Program(new ExpressionInstrBlend { Type=ExpressionInstrType.BlendVector,
            SourceInfos=[new() { TrackIndex=99 }] });
        renderable.UpdateAnims(0);
        Assert.Equal(new Vector3(9),bone.AnimTranslation);
        Assert.NotNull(renderable.FacialExpressionError);
    }

    [Fact]
    public void NonFiniteExpressionDoesNotCommitOutputs()
    {
        var (renderable,bone)=Create();
        renderable.ClipMapEntry=Clip(0,new Vector3(9));
        renderable.Expression=Program(new ExpressionInstrVector { Type=ExpressionInstrType.PushVector, Value=new Vector4(float.NaN) },
            new ExpressionInstrBone { Type=ExpressionInstrType.TrackSet, BoneId=1, Track=0 });
        renderable.UpdateAnims(0);
        Assert.Equal(new Vector3(9),bone.AnimTranslation);
        Assert.NotNull(renderable.FacialExpressionError);
    }

    [Fact]
    public void ExpressionDoesNotWriteSampledInputsBackToBodyBones()
    {
        var (_,bone)=Create();
        bone.AnimTranslation=new Vector3(100); // Pose already adjusted by another animation layer.
        bone.AnimRotation=Quaternion.RotationAxis(Vector3.UnitZ,0.5f);
        bone.AnimScale=new Vector3(2);
        var expectedRotation=bone.AnimRotation;
        var evaluator=new ExpressionEvaluator();
        evaluator.Frame[(1,0)]=new Vector4(9,9,9,0);
        evaluator.Frame[(1,1)]=Quaternion.Identity.ToVector4();
        evaluator.Frame[(1,2)]=Vector4.One;
        var skeleton=new crSkeletonData { BonesMap=new() { [1]=bone } };
        Assert.True(evaluator.Evaluate(Program(new ExpressionInstrEmpty { Type=ExpressionInstrType.End }),skeleton,0));
        Assert.Equal(new Vector3(100),bone.AnimTranslation);
        Assert.Equal(expectedRotation,bone.AnimRotation);
        Assert.Equal(new Vector3(2),bone.AnimScale);
    }

    [Fact]
    public void ExpressionCommitsOnlyTracksExplicitlyWritten()
    {
        var (_,bone)=Create();
        bone.AnimTranslation=new Vector3(100);
        bone.AnimScale=new Vector3(2);
        var evaluator=new ExpressionEvaluator();
        evaluator.Frame[(1,0)]=new Vector4(9);
        evaluator.Frame[(1,2)]=Vector4.One;
        var skeleton=new crSkeletonData { BonesMap=new() { [1]=bone } };
        var program=Program(new ExpressionInstrVector { Type=ExpressionInstrType.PushVector,Value=new Vector4(3,3,3,0) },
            new ExpressionInstrBone { Type=ExpressionInstrType.TrackSet,BoneId=1,Track=2 });
        Assert.True(evaluator.Evaluate(program,skeleton,0));
        Assert.Equal(new Vector3(100),bone.AnimTranslation);
        Assert.Equal(new Vector3(3),bone.AnimScale);
    }

    [Theory]
    [InlineData(0x2C, 0, 7)]
    [InlineData(0x2C, 1, 3)]
    [InlineData(0x2D, 0, 3)]
    [InlineData(0x2D, 1, 7)]
    public void BranchOpcodesFollowReferenceConditionsRatherThanLegacyNames(int opcode, float condition, float expected)
    {
        var (_,bone)=Create();
        var evaluator=new ExpressionEvaluator();
        var skeleton=new crSkeletonData { BonesMap=new() { [1]=bone } };
        var program=Program(
            new ExpressionInstrFloat { Type=ExpressionInstrType.PushFloat, Value=condition },
            new ExpressionInstrJump { Type=(ExpressionInstrType)opcode, Data3Offset=3 },
            new ExpressionInstrEmpty { Type=ExpressionInstrType.Pop },
            new ExpressionInstrFloat { Type=ExpressionInstrType.PushFloat, Value=3 },
            new ExpressionInstrJump { Type=ExpressionInstrType.Jump, Data3Offset=2 },
            new ExpressionInstrEmpty { Type=ExpressionInstrType.Pop },
            new ExpressionInstrFloat { Type=ExpressionInstrType.PushFloat, Value=7 },
            new ExpressionInstrBone { Type=ExpressionInstrType.TrackSetComp, BoneId=1, Track=0, ComponentIndex=0 });
        Assert.True(evaluator.Evaluate(program,skeleton,0),evaluator.LastError);
        Assert.Equal(expected,bone.AnimTranslation.X);
    }

    [Theory]
    [InlineData(-0.2f,-0.2f)]
    [InlineData(0,0)]
    [InlineData(0.4f,0.4f)]
    [InlineData(1.4f,1.0f)]
    public void FacialLimitBranchPreservesValuesBelowUpperLimit(float input,float expected)
    {
        var (_,bone)=Create();
        var evaluator=new ExpressionEvaluator();
        var skeleton=new crSkeletonData { BonesMap=new() { [1]=bone } };
        // Same comparison/branch/pop structure found in choice_int's head expression.
        var program=Program(
            new ExpressionInstrFloat { Type=ExpressionInstrType.PushFloat, Value=input },
            new ExpressionInstrFloat { Type=ExpressionInstrType.PushFloat, Value=1 },
            new ExpressionInstrEmpty { Type=ExpressionInstrType.VectorGreaterThan },
            new ExpressionInstrJump { Type=(ExpressionInstrType)0x2C, Data3Offset=3 },
            new ExpressionInstrEmpty { Type=ExpressionInstrType.Pop },
            new ExpressionInstrFloat { Type=ExpressionInstrType.PushFloat, Value=1 },
            new ExpressionInstrJump { Type=ExpressionInstrType.Jump, Data3Offset=2 },
            new ExpressionInstrEmpty { Type=ExpressionInstrType.Pop },
            new ExpressionInstrFloat { Type=ExpressionInstrType.PushFloat, Value=input },
            new ExpressionInstrBone { Type=ExpressionInstrType.TrackSetComp, BoneId=1, Track=0, ComponentIndex=0 });
        Assert.True(evaluator.Evaluate(program,skeleton,0),evaluator.LastError);
        Assert.Equal(expected,bone.AnimTranslation.X);
    }

    [Fact]
    public void ComponentAnimationIncludesAncestorsOutsideItsSkinningPalette()
    {
        var root = new crBoneData { BoneId=1, Index=0, DefaultRotation=Quaternion.Identity, DefaultScale=Vector3.One };
        var hand = new crBoneData { BoneId=2, Index=1, Parent=root, DefaultTranslation=Vector3.UnitX, DefaultRotation=Quaternion.Identity, DefaultScale=Vector3.One };
        var actor = new crSkeletonData { Bones=new crBoneDataArrayBlock { Items=[root,hand] }, BonesSorted=[root,hand], BonesMap=new() { [1]=root,[2]=hand } };
        var component = new crSkeletonData { Bones=new crBoneDataArrayBlock { Items=[new crBoneData { BoneId=2,Index=0 }] }, BonesMap=new() };
        component.BindAnimationSkeleton(actor);
        var renderable = new Renderable { Skeleton=component, Key=new gtaDrawable(), ClipMapEntry=Clip(0,new Vector3(4)) };
        renderable.UpdateAnims(0);
        Assert.Equal(new Vector3(4),root.AnimTranslation);
        Assert.Single(component.Bones.Items);
        Assert.Same(hand,component.Bones.Items[0]);
        Assert.Same(root,component.BonesMap[1]);
        Assert.Same(actor.BonesSorted,component.BonesSorted);
    }

    [Fact]
    public void BindingRetainsComponentPaletteOrderAcrossRepeatedUpdates()
    {
        var a=new crBoneData { BoneId=1 }; var b=new crBoneData { BoneId=2 };
        var actor=new crSkeletonData { Bones=new crBoneDataArrayBlock { Items=[a,b] }, BonesSorted=[a,b], BonesMap=new() { [1]=a,[2]=b } };
        var component=new crSkeletonData { Bones=new crBoneDataArrayBlock { Items=[new crBoneData { BoneId=2 },new crBoneData { BoneId=1 }] } };
        component.BindAnimationSkeleton(actor);
        component.BindAnimationSkeleton(actor);
        Assert.Same(b,component.Bones.Items[0]);
        Assert.Same(a,component.Bones.Items[1]);
    }
    [Fact]
    public void LaterBodyUpdateDoesNotOverwriteQueuedFacialSkinningMatrices()
    {
        var jaw = new crBoneData { BoneId=1, Index=0, ParentIndex=-1, DefaultRotation=Quaternion.Identity,
            DefaultScale=Vector3.One, BindTransformInv=Matrix.Identity };
        var actor = new crSkeletonData { Bones=new crBoneDataArrayBlock { Items=[jaw] },
            BonesSorted=[jaw], BonesMap=new() { [1]=jaw } };
        // Components with no embedded skeleton inherit the actor's palette, with independent storage.
        var teeth=actor.Clone(); teeth.BindAnimationSkeleton(actor);
        var body=actor.Clone(); body.BindAnimationSkeleton(actor);
        var clip=Clip(0,Vector3.Zero);
        var face=new Renderable { Skeleton=teeth, Key=new gtaDrawable(), ClipMapEntry=clip,
            Expression=Program(new ExpressionInstrVector { Type=ExpressionInstrType.PushVector, Value=new Vector4(0,0,0.05f,0) },
                new ExpressionInstrBone { Type=ExpressionInstrType.TrackSet, BoneId=1, Track=0 }) };
        face.UpdateAnims(0);
        var queuedJaw=teeth.BoneTransforms[0];
        var torso=new Renderable { Skeleton=body, Key=new gtaDrawable(), ClipMapEntry=clip };
        torso.UpdateAnims(0);
        Assert.NotSame(teeth.BoneTransforms,body.BoneTransforms);
        Assert.Equal(queuedJaw,teeth.BoneTransforms[0]);
        Assert.NotEqual(queuedJaw,body.BoneTransforms[0]);
        // Paused playback skips evaluation and must still keep the previously queued facial pose.
        face.UpdateAnims(0);
        Assert.Equal(queuedJaw,teeth.BoneTransforms[0]);
    }
    [Theory]
    [InlineData(37, false, false, 1f)]
    [InlineData(38, false, false, 1f)]
    [InlineData(37, true, false, 1.25f)]
    [InlineData(37, true, true, 1f)]
    public void FacialScaleComponentDefaultsAreZeroDeltas(int track, bool sampled, bool forceDefault, float expected)
    {
        var (renderable,bone)=Create();
        var evaluator=new ExpressionEvaluator();
        if (sampled) evaluator.Frame[(99,(byte)track)]=new Vector4(0.25f);
        // Devin's expression adds a component scale delta to the unit bone scale.
        var program=Program(new ExpressionInstrEmpty { Type=ExpressionInstrType.Push1 },
            new ExpressionInstrBone { Type=ExpressionInstrType.TrackGetComp, BoneId=99, Track=(byte)track,
                Format=0, ComponentIndex=0, UseDefaults=forceDefault },
            new ExpressionInstrEmpty { Type=ExpressionInstrType.VectorAdd },
            new ExpressionInstrBone { Type=ExpressionInstrType.TrackSet, BoneId=1, Track=2 });
        Assert.True(evaluator.Evaluate(program,renderable.Skeleton!,0),evaluator.LastError);
        Assert.Equal(new Vector3(expected),bone.AnimScale);
    }

    [Theory]
    [InlineData(37)]
    [InlineData(38)]
    public void WholeScaleTrackStillDefaultsToUnitScale(int track)
    {
        var (renderable,bone)=Create();
        var evaluator=new ExpressionEvaluator();
        Assert.True(evaluator.Evaluate(Program(
            new ExpressionInstrBone { Type=ExpressionInstrType.TrackGet, BoneId=99, Track=(byte)track },
            new ExpressionInstrBone { Type=ExpressionInstrType.TrackSet, BoneId=1, Track=2 }),renderable.Skeleton!,0));
        Assert.Equal(Vector3.One,bone.AnimScale);
    }

    [Fact]
    public void MissingSkeletalScaleComponentUsesBindScale()
    {
        var (renderable,bone)=Create();
        bone.DefaultScale=new Vector3(2,3,4);
        var evaluator=new ExpressionEvaluator();
        Assert.True(evaluator.Evaluate(Program(
            new ExpressionInstrBone { Type=ExpressionInstrType.TrackGetComp, BoneId=1, Track=2, ComponentIndex=1 },
            new ExpressionInstrBone { Type=ExpressionInstrType.TrackSetComp, BoneId=1, Track=0, ComponentIndex=0 }),renderable.Skeleton!,0));
        Assert.Equal(3f,bone.AnimTranslation.X);
    }
    [Fact]
    public void SpringDeclarationsDoNotDiscardFacialOutputsOrConsumeStackValues()
    {
        var (renderable, bone) = Create();
        var evaluator = new ExpressionEvaluator();
        var program = Program(
            new ExpressionInstrVector { Type = ExpressionInstrType.PushVector, Value = new Vector4(1, 2, 3, 0) },
            new ExpressionInstrBone { Type = ExpressionInstrType.TrackSet, BoneId = 1, Track = 0 },
            new ExpressionInstrEmpty { Type = ExpressionInstrType.Push1 },
            new ExpressionInstrSpring { Type = ExpressionInstrType.DefineSpring },
            new ExpressionInstrSpring { Type = ExpressionInstrType.DefineSpring },
            new ExpressionInstrFloat { Type = ExpressionInstrType.PushFloat, Value = 0.5f },
            new ExpressionInstrEmpty { Type = ExpressionInstrType.VectorMul },
            new ExpressionInstrBone { Type = ExpressionInstrType.TrackSet, BoneId = 1, Track = 2 });

        Assert.True(evaluator.Evaluate(program, renderable.Skeleton!, 0), evaluator.LastError);
        Assert.Equal(new Vector3(1, 2, 3), bone.AnimTranslation);
        Assert.Equal(new Vector3(0.5f), bone.AnimScale);
    }
}
