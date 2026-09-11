using CodeWalker.GameFiles;
using SharpDX;
using Xunit;

namespace CodeWalker.Core.Tests;

public class ExpressionFormatTests
{
    [Fact]
    public void EveryNativeOpcodeHasAReader()
    {
        for (byte value = 0; value <= (byte)ExpressionInstrType.Exponent; value++)
        {
            var instruction = ExpressionStream.CreateInstruction((ExpressionInstrType)value);
            Assert.Equal(value, (byte)(ExpressionInstrType)value);
            Assert.NotNull(instruction);
        }
    }

    [Fact]
    public void CurveAndPaddedOperationsRoundTripTheirBuffers()
    {
        var stream = new ExpressionStream
        {
            Instructions =
            [
                new ExpressionInstrCurve
                {
                    Type = ExpressionInstrType.Curve,
                    Keys =
                    [
                        new() { Input = 1.25f, Output = -2.5f },
                        new() { Input = 3.5f, Output = 4.75f },
                    ],
                },
                new ExpressionInstrMotion { Type = ExpressionInstrType.Motion },
                new ExpressionInstrLookAt { Type = ExpressionInstrType.LookAt },
                new ExpressionInstrEmpty { Type = ExpressionInstrType.QuatScale },
                new ExpressionInstrEmpty { Type = ExpressionInstrType.Xor },
                new ExpressionInstrEmpty { Type = ExpressionInstrType.QuatIdentity },
                new ExpressionInstrEmpty { Type = ExpressionInstrType.CosH },
                new ExpressionInstrEmpty { Type = ExpressionInstrType.SinH },
                new ExpressionInstrEmpty { Type = ExpressionInstrType.TanH },
                new ExpressionInstrEmpty { Type = ExpressionInstrType.Exponent },
                new ExpressionInstrEmpty { Type = ExpressionInstrType.Halt },
            ],
        };

        stream.WriteInstructions();
        Assert.Equal(208u, stream.AlignedParametersSize);
        Assert.Equal(24u, stream.ParametersSize);
        Assert.Equal(11, stream.OperationCount);

        stream.Instructions = [];
        stream.ReadInstructions();
        var curve = Assert.IsType<ExpressionInstrCurve>(stream.Instructions[0]);
        Assert.Equal(2, curve.Keys.Length);
        Assert.Equal(4.75f, curve.Keys[1].Output);
        Assert.IsType<ExpressionInstrMotion>(stream.Instructions[1]);
    }

    [Fact]
    public void BranchOffsetsUseNativePostParameterCursor()
    {
        var branch = new ExpressionInstrJump { Type = ExpressionInstrType.Branch, OperationOffset = 1 };
        var expression = new Expression
        {
            Streams = new ResourcePointerList64<ExpressionStream>
            {
                data_items =
                [
                    new()
                    {
                        Instructions =
                        [
                            new ExpressionInstrEmpty { Type = ExpressionInstrType.Zero },
                            branch,
                            new ExpressionInstrFloat { Type = ExpressionInstrType.ConstantFloat, Value = 1 },
                            new ExpressionInstrEmpty { Type = ExpressionInstrType.Halt },
                        ],
                    },
                ],
            },
        };

        expression.UpdateStreamBuffers();

        Assert.Equal(0u, branch.AlignedParameterOffset);
        Assert.Equal(4u, branch.ParameterOffset);
        Assert.Equal(1u, branch.OperationOffset);
    }
}
