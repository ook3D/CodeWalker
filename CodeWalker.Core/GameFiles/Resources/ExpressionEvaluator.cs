using SharpDX;
using System;
using System.Collections.Generic;

namespace CodeWalker.GameFiles;

/// <summary>Evaluates stateless YED expression streams against an animation frame.</summary>
public sealed class ExpressionEvaluator
{
    public Dictionary<(ushort Bone, byte Track), Vector4> Frame { get; } = new();
    private readonly Dictionary<(ushort Bone, byte Track), Vector4> working = new();
    private readonly Stack<Vector4> stack = new();
    private readonly HashSet<(ushort Bone, byte Track)> outputs = new();
    public string? LastError { get; private set; }
    private Skeleton skeleton = null!;

    public bool Evaluate(Expression expression, Skeleton target, float time)
    {
        skeleton = target;
        working.Clear();
        outputs.Clear();
        foreach (var pair in Frame) working.Add(pair.Key, pair.Value);
        LastError = null;
        try
        {
            foreach (var stream in expression.Streams.data_items)
            {
                stack.Clear();
                var instructions = stream.Instructions;
                for (int pc = 0; pc < instructions.Length; pc++)
                {
                    var op = instructions[pc];
                    if (stack.Count > 256) throw new InvalidOperationException("Expression stack exceeds 256 values.");
                    if (op.Type == ExpressionInstrType.End) break;
                    if (op is ExpressionInstrBone bone) { ExecuteTrack(bone); continue; }
                    if (op is ExpressionInstrBlend blend) { stack.Push(Blend(expression, blend)); continue; }
                    if (op is ExpressionInstrJump jump)
                    {
                        bool take = op.Type == ExpressionInstrType.Jump ||
                            // Legacy enum names are reversed: 0x2C is BranchZero, 0x2D is BranchNotZero.
                            (op.Type == ExpressionInstrType.JumpIfTrue ? stack.Peek() == Vector4.Zero : stack.Peek() != Vector4.Zero);
                        if (take)
                        {
                            long next = (long)pc + 1 + jump.Data3Offset;
                            if (next < 0 || next >= instructions.Length) throw new InvalidOperationException("Invalid expression jump.");
                            pc = (int)next - 1;
                        }
                        continue;
                    }
                    switch (op.Type)
                    {
                        case ExpressionInstrType.Push0: stack.Push(Vector4.Zero); break;
                        case ExpressionInstrType.Push1: stack.Push(Vector4.One); break;
                        case ExpressionInstrType.PushFloat: stack.Push(new Vector4(((ExpressionInstrFloat)op).Value)); break;
                        case ExpressionInstrType.PushVector: stack.Push(((ExpressionInstrVector)op).Value); break;
                        case ExpressionInstrType.PushTime: stack.Push(new Vector4(time)); break;
                        case ExpressionInstrType.Dup: stack.Push(stack.Peek()); break;
                        case ExpressionInstrType.Pop: stack.Pop(); break;
                        case ExpressionInstrType.VectorNeg: stack.Push(-stack.Pop()); break;
                        case ExpressionInstrType.VectorNeg3: stack.Push(Quaternion.Invert(stack.Pop().ToQuaternion()).ToVector4()); break;
                        case ExpressionInstrType.VectorAbs: Unary(MathF.Abs); break;
                        case ExpressionInstrType.VectorSquare: Unary(x => x*x); break;
                        case ExpressionInstrType.VectorRcp: Unary(x => 1/x); break;
                        case ExpressionInstrType.VectorSqrt: Unary(MathF.Sqrt); break;
                        case ExpressionInstrType.VectorDeg2Rad: Unary(x => x * (MathF.PI / 180)); break;
                        case ExpressionInstrType.VectorRad2Deg: Unary(x => x * (180 / MathF.PI)); break;
                        case ExpressionInstrType.VectorSaturate: Unary(x => Math.Clamp(x, 0, 1)); break;
                        case ExpressionInstrType.ToEuler: stack.Push(ToEuler(stack.Pop())); break;
                        case ExpressionInstrType.FromEuler: stack.Push(FromEuler(stack.Pop()).ToVector4()); break;
                        case ExpressionInstrType.VectorAdd: Binary((a,b) => a+b); break;
                        case ExpressionInstrType.VectorSub: Binary((a,b) => a-b); break;
                        case ExpressionInstrType.VectorMul: Binary((a,b) => a*b); break;
                        case ExpressionInstrType.VectorMin: Binary(Vector4.Min); break;
                        case ExpressionInstrType.VectorMax: Binary(Vector4.Max); break;
                        case ExpressionInstrType.QuatMul: Binary((a,b) => (a.ToQuaternion()*b.ToQuaternion()).ToVector4()); break;
                        case ExpressionInstrType.VectorTransform: Binary((a,b) => new Vector4(b.ToQuaternion().Multiply(a.XYZ()), 0)); break;
                        case ExpressionInstrType.VectorGreaterThan: Compare((a,b) => a>b); break;
                        case ExpressionInstrType.VectorLessThan: Compare((a,b) => a<b); break;
                        case ExpressionInstrType.VectorGreaterEqual: Compare((a,b) => a>=b); break;
                        case ExpressionInstrType.VectorLessEqual: Compare((a,b) => a<=b); break;
                        case ExpressionInstrType.VectorEqual: Compare((a,b) => a==b); break;
                        case ExpressionInstrType.VectorNotEqual: Compare((a,b) => a!=b); break;
                        case ExpressionInstrType.QuatSlerp: Ternary((a,b,c) => Quaternion.Lerp(a.ToQuaternion(),b.ToQuaternion(),c.X).ToVector4()); break;
                        case ExpressionInstrType.VectorClamp: Ternary((a,b,c) => Vector4.Min(Vector4.Max(a,b),c)); break;
                        case ExpressionInstrType.VectorLerp: Ternary((a,b,c) => a+(b-a)*c); break;
                        case ExpressionInstrType.VectorMad: Ternary((a,b,c) => a*b+c); break;
                        case ExpressionInstrType.ToVector: Ternary((a,b,c) => new Vector4(a.X,b.X,c.X,0)); break;
                        default: throw new InvalidOperationException($"Unsupported expression operation: {op.Type}.");
                    }
                }
            }
            // Validate all outputs before changing any bones.
            foreach (var pair in working)
                if (outputs.Contains(pair.Key) && pair.Key.Track <= 2 && !Finite(pair.Value)) throw new InvalidOperationException("Non-finite expression output.");
            foreach (var pair in working)
            {
                if (!outputs.Contains(pair.Key) || !target.BonesMap.TryGetValue(pair.Key.Bone, out var bone)) continue;
                switch (pair.Key.Track)
                {
                    case 0: bone.AnimTranslation = pair.Value.XYZ(); break;
                    case 1:
                        var q = pair.Value.ToQuaternion();
                        if (q.LengthSquared() > 1e-12f) bone.AnimRotation = Quaternion.Normalize(q);
                        break;
                    case 2: bone.AnimScale = pair.Value.XYZ(); break;
                }
            }
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or IndexOutOfRangeException or ArgumentException or OverflowException)
        {
            LastError = ex.Message;
            return false;
        }
    }

    private static bool Finite(Vector4 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z) && float.IsFinite(v.W);
    private static Quaternion FromEuler(Vector4 v) => Quaternion.RotationAxis(Vector3.UnitZ, v.Z) *
        Quaternion.RotationAxis(Vector3.UnitY, v.Y) * Quaternion.RotationAxis(Vector3.UnitX, v.X);
    private static Vector4 ToEuler(Vector4 v)
    {
        var q = Quaternion.Normalize(v.ToQuaternion());
        return new Vector4(MathF.Atan2(2*(q.W*q.X+q.Y*q.Z),1-2*(q.X*q.X+q.Y*q.Y)),
            MathF.Asin(Math.Clamp(2*(q.W*q.Y-q.Z*q.X),-1,1)),
            MathF.Atan2(2*(q.W*q.Z+q.X*q.Y),1-2*(q.Y*q.Y+q.Z*q.Z)),0);
    }
    private void Compare(Func<float,float,bool> f)
    {
        var b=stack.Pop(); var a=stack.Pop();
        float Mask(float x,float y) => f(x,y) ? BitConverter.Int32BitsToSingle(-1) : 0;
        stack.Push(new Vector4(Mask(a.X,b.X),Mask(a.Y,b.Y),Mask(a.Z,b.Z),Mask(a.W,b.W)));
    }
    private void Unary(Func<float,float> f) { var a = stack.Pop(); stack.Push(new Vector4(f(a.X),f(a.Y),f(a.Z),f(a.W))); }
    private void Binary(Func<Vector4,Vector4,Vector4> f) { var b = stack.Pop(); var a = stack.Pop(); stack.Push(f(a,b)); }
    private void Ternary(Func<Vector4,Vector4,Vector4,Vector4> f) { var c = stack.Pop(); var b = stack.Pop(); var a = stack.Pop(); stack.Push(f(a,b,c)); }
    private Vector4 Default(ushort id, byte track)
    {
        if (skeleton.BonesMap.TryGetValue(id, out var bone))
            switch (track)
            {
                case 0: return new Vector4(bone.Translation, 0);
                case 1: return bone.Rotation.ToVector4();
                case 2: return new Vector4(bone.Scale, 0);
            }
        return track is 2 or 37 or 38 ? new Vector4(1,1,1,0) : new Vector4(0,0,0,1);
    }
    private Vector4 Read(ushort id, byte track, bool defaults = false) => !defaults && working.TryGetValue((id,track), out var value) ? value : Default(id,track);
    private void ExecuteTrack(ExpressionInstrBone op)
    {
        var key = (op.BoneId, op.Track);
        bool relative = op.Type is ExpressionInstrType.TrackGetOffset or ExpressionInstrType.TrackSetOffset or ExpressionInstrType.TrackGetOffsetComp or ExpressionInstrType.TrackSetOffsetComp;
        bool component = op.Type is ExpressionInstrType.TrackGetComp or ExpressionInstrType.TrackSetComp or ExpressionInstrType.TrackGetOffsetComp or ExpressionInstrType.TrackSetOffsetComp;
        bool write = op.Type is ExpressionInstrType.TrackSet or ExpressionInstrType.TrackSetComp or ExpressionInstrType.TrackSetOffset or ExpressionInstrType.TrackSetOffsetComp;
        if (component && (op.ComponentIndex > 3 || (op.Format == 1 && op.ComponentIndex > 2))) throw new InvalidOperationException("Invalid expression component index.");
        if (op.Type == ExpressionInstrType.TrackValid) { stack.Push(working.ContainsKey(key) ? Vector4.One : Vector4.Zero); return; }
        if (!write && op.Type is not (ExpressionInstrType.TrackGet or ExpressionInstrType.TrackGetComp or ExpressionInstrType.TrackGetOffset or ExpressionInstrType.TrackGetOffsetComp))
            throw new InvalidOperationException($"Unsupported track operation: {op.Type}.");
        var basis = Default(op.BoneId, op.Track);
        if (write)
        {
            var v = stack.Pop();
            if (component)
            {
                var current = Read(op.BoneId,op.Track);
                if (op.Format == 1)
                {
                    if (relative) current = (Quaternion.Invert(basis.ToQuaternion())*current.ToQuaternion()).ToVector4();
                    var angles = ToEuler(current);
                    angles[op.ComponentIndex] = v.X;
                    v = FromEuler(angles).ToVector4();
                    if (relative) v = (basis.ToQuaternion()*v.ToQuaternion()).ToVector4();
                }
                else
                {
                    current[op.ComponentIndex] = v.X + (relative ? basis[op.ComponentIndex] : 0);
                    v = current;
                }
            }
            else if (relative && op.Track <= 2)
                v = op.Track == 1 ? (basis.ToQuaternion()*v.ToQuaternion()).ToVector4() : basis+v;
            working[key] = v;
            outputs.Add(key);
        }
        else
        {
            // GetComp has different missing-input semantics from a whole-track Get:
            // non-skeletal components default to zero, including facial scale deltas.
            if (component && op.Track > 2 && (op.UseDefaults || !working.ContainsKey(key)))
            {
                stack.Push(Vector4.Zero);
                return;
            }
            var v = Read(op.BoneId,op.Track,op.UseDefaults);
            if (relative) v = op.Track == 1 ? (Quaternion.Invert(basis.ToQuaternion())*v.ToQuaternion()).ToVector4() : v-basis;
            if (component && op.Format == 1) v = ToEuler(v);
            stack.Push(component ? new Vector4(v[op.ComponentIndex]) : v);
        }
    }
    private Vector4 Blend(Expression expression, ExpressionInstrBlend blend)
    {
        Vector3 sum = Vector3.Zero;
        Quaternion[] lanes = [Quaternion.Identity,Quaternion.Identity,Quaternion.Identity,Quaternion.Identity];
        int stride = checked(6 + ((int)blend.NumSourceWeights-1)*9);
        for (int i=0; i<blend.SourceInfos.Length; i++)
        {
            var source = blend.SourceInfos[i];
            var track = expression.Tracks.data_items[source.TrackIndex];
            float input = working.TryGetValue((track.BoneId,track.Track), out var v) ? v[source.ComponentOffset/4] : 0;
            int lane = i%4, offset = i/4*stride;
            float Component(int axis)
            {
                float result = blend.Values[offset+axis][lane]*input + blend.Values[offset+3+axis][lane];
                for (int n=1;n<blend.NumSourceWeights;n++)
                {
                    int k=offset+6+(n-1)*9;
                    if (input>blend.Values[k+axis][lane]) result=blend.Values[k+3+axis][lane]*input+blend.Values[k+6+axis][lane];
                }
                return result;
            }
            var part = new Vector3(Component(0),Component(1),Component(2));
            if (blend.Type == ExpressionInstrType.BlendQuaternion) lanes[lane] *= FromEuler(new Vector4(part,0));
            else sum += part;
        }
        return blend.Type == ExpressionInstrType.BlendQuaternion ? ((lanes[0]*lanes[1])*(lanes[2]*lanes[3])).ToVector4() : new Vector4(sum,0);
    }
}
