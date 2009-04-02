﻿//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System.Collections.Generic;
using Microsoft.Cci.MutableCodeModel;

namespace Microsoft.Cci.ILToCodeModel {

  public sealed class BasicBlock : BlockStatement {

    public BasicBlock(uint startOffset) {
      this.StartOffset = startOffset;
    }

    internal uint EndOffset;

    internal IOperationExceptionInformation/*?*/ ExceptionInformation;

    internal List<ILocalDefinition>/*?*/ LocalVariables;

    internal int NumberOfTryBlocksStartingHere;

    internal uint StartOffset;

    internal bool StartsSwitchCase;

  }

  internal sealed class Dup : Expression {
    public override void Dispatch(ICodeVisitor visitor) {
      //Debug.Assert(false); //Objects of this class are not supposed to escape.
    }
  }

  internal sealed class EndFilter : Statement {
    internal Expression FilterResult;

    public override void Dispatch(ICodeVisitor visitor) {
      //Debug.Assert(false); //Objects of this class are not supposed to escape.
    }
  }

  internal sealed class EndFinally : Statement {
    public override void Dispatch(ICodeVisitor visitor) {
      //Debug.Assert(false); //Objects of this class are not supposed to escape.
    }
  }

  internal class Pop : Expression {
    public override void Dispatch(ICodeVisitor visitor) {
      //Debug.Assert(false); //Objects of this class are not supposed to escape.
    }
  }

  internal sealed class PopAsUnsigned : Pop {
    public override void Dispatch(ICodeVisitor visitor) {
      //Debug.Assert(false); //Objects of this class are not supposed to escape.
    }
  }

  internal sealed class Push : Statement {
    internal IExpression ValueToPush;

    public override void Dispatch(ICodeVisitor visitor) {
      this.ValueToPush.Dispatch(visitor);
      //Debug.Assert(false); //Objects of this class are not supposed to escape.
    }
  }

  internal sealed class SwitchInstruction : Statement {
    internal IExpression switchExpression;
    internal readonly List<BasicBlock> switchCases = new List<BasicBlock>();

    public override void Dispatch(ICodeVisitor visitor) {
      //Debug.Assert(false); //Objects of this class are not supposed to escape.
    }
  }


}