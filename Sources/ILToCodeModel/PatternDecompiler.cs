﻿//-----------------------------------------------------------------------------
//
// Copyright (c) Microsoft. All rights reserved.
// This code is licensed under the Microsoft Public License.
// THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
// IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//-----------------------------------------------------------------------------
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Cci.MutableCodeModel;
using System;

namespace Microsoft.Cci.ILToCodeModel {

  internal class PatternDecompiler : BaseCodeTraverser {

    INameTable nameTable;
    SourceMethodBody sourceMethodBody;
    List<ILocalDefinition> blockLocalVariables;
    Dictionary<ILabeledStatement, List<IGotoStatement>> predecessors;

    internal PatternDecompiler(SourceMethodBody sourceMethodBody, Dictionary<ILabeledStatement, List<IGotoStatement>> predecessors) {
      this.nameTable = sourceMethodBody.nameTable;
      this.sourceMethodBody = sourceMethodBody;
      this.predecessors = predecessors;
    }

    public override void Visit(IBlockStatement block) {
      BasicBlock/*?*/ b = (BasicBlock)block;
      var blockTreeAsList = new List<BasicBlock>();
      for (; ; ) {
        blockTreeAsList.Add(b);
        var i = b.Statements.Count-1;
        if (i <= 0) break;
        b = b.Statements[i] as BasicBlock;
        if (b == null) break;
      }
      for (int i = blockTreeAsList.Count-1; i >= 0; i--)
        this.Visit(blockTreeAsList[i]);
    }

    private void Visit(BasicBlock b) {
      this.blockLocalVariables = b.LocalVariables;
      for (int i = 0; i < b.Statements.Count; i++) {
        DeleteGotoNextStatement(b.Statements, i);
        this.ReplaceLocalArrayInitializerPattern(b.Statements, i);
        this.ReplaceShortCircuitPattern(b.Statements, i);
        this.ReplaceShortCircuitPattern2(b.Statements, i);
        this.ReplacePushPopPattern(b.Statements, i);
      }
      if (this.blockLocalVariables != b.LocalVariables)
        b.LocalVariables = this.blockLocalVariables;
    }

    private void ReplacePushPopPattern(List<IStatement> statements, int i) {
      if (i > statements.Count - 2) return;
      //First identify count consecutive push statements
      int count = 0;
      while (i+count < statements.Count-1 && statements[i+count] is Push) count++;
      while (i > 1 && statements[i-1] is Push) { i--; count++; }
      if (count == 0) return;
      for (int j = 0; j < count; j++) {
        if (((Push)statements[j+i]).ValueToPush is Dup) return;
      }
      //If any of the push statements (other than the first one) contains pop expressions, replace them with the corresponding push values and remove the pushes.
      int pushcount = 1; //the number of push statements that are eligble for removal at this point.
      for (int j = i + 1; j < i + count; j++) {
        Push st = (Push)statements[j];
        PopCounter pcc = new PopCounter();
        pcc.Visit(st.ValueToPush);
        int numberOfPushesToRemove = pushcount;
        if (pcc.count > 0) {
          if (pcc.count < numberOfPushesToRemove) numberOfPushesToRemove = pcc.count;
          int firstPushToRemove = j - numberOfPushesToRemove;
          PopReplacer prr = new PopReplacer(this.sourceMethodBody.host, statements, firstPushToRemove, pcc.count-numberOfPushesToRemove);
          st.ValueToPush = prr.Visit(st.ValueToPush);
          statements.RemoveRange(firstPushToRemove, numberOfPushesToRemove);
          j = j - pcc.count;
          count = count - pcc.count;
          pushcount = pushcount - pcc.count;
        } 
        pushcount++;
      }
      //If the next statement is an expression statement that pops some of the just pushed values, replace those pops with the pushed values and remove the pushes.
      ExpressionStatement/*?*/ expressionStatement = statements[i + count] as ExpressionStatement;
      if (expressionStatement == null) return;
      PopCounter pc = new PopCounter();
      pc.Visit(expressionStatement.Expression);
      if (pc.count == 0) return;
      if (pc.count < count) {
        i += count-pc.count; //adjust i to point to the first push statement to remove because of subsequent pops.
        count = pc.count;
      }
      PopReplacer pr = new PopReplacer(this.sourceMethodBody.host, statements, i, pc.count-count);
      expressionStatement.Expression = pr.Visit(expressionStatement.Expression);
      statements.RemoveRange(i, count);
    }

    private static void DeleteGotoNextStatement(List<IStatement> statements, int i) {
      if (i > statements.Count-2) return;
      GotoStatement/*?*/ gotoStatement = statements[i] as GotoStatement;
      if (gotoStatement == null) return;
      BasicBlock/*?*/ basicBlock = statements[i+1] as BasicBlock;
      if (basicBlock == null) return;
      if (basicBlock.Statements.Count < 1) return;
      if (gotoStatement.TargetStatement != basicBlock.Statements[0]) return;
      statements.RemoveAt(i);
    }

    /// <summary>
    /// Finds the following pattern:
    /// i   :  c ? A : B; // either A or B must be an empty statement and the other is "goto L1;"
    /// i+1 :  push x;
    /// i+2 :  goto L2;
    /// i+3 :  Block1
    ///        0  : L1;
    ///        1  : push y;
    ///        2  : Block2
    ///             0 : L2;
    ///             1 : (rest of statements in Block2)
    ///             
    /// Transforms it into:
    /// i   : push d ? X : Y;
    /// i+1 : (rest of statements in Block2)
    /// 
    /// Where if A is the empty statement, then
    ///   d == c, X == x, Y == y
    /// If B is the empty statement, then if y is zero,
    ///   d == !c, X == x, Y == y
    /// If B is the empty statement, then if y is not zero,
    ///   d == c, X == y, Y == x
    /// </summary>
    private bool ReplaceShortCircuitPattern(List<IStatement> statements, int i) {
      if (i > statements.Count - 4) return false;
      ConditionalStatement/*?*/ conditionalStatement = statements[i] as ConditionalStatement;
      if (conditionalStatement == null) return false;
      if (statements[i+1] is ConditionalStatement)
        return this.ReplaceChainedShortCircuitBooleanPattern(statements, i);
      Push/*?*/ push = statements[i+1] as Push;
      if (push == null) return false;
      GotoStatement/*?*/ Goto = statements[i+2] as GotoStatement;
      if (Goto == null) return false;
      BasicBlock/*?*/ block = statements[i+3] as BasicBlock;
      if (block == null) return false;
      if (block.Statements.Count < 3) return false;
      LabeledStatement/*?*/ label = block.Statements[0] as LabeledStatement;
      if (label == null) return false;
      List<IGotoStatement> branchesToThisLabel;
      if (this.predecessors.TryGetValue(label, out branchesToThisLabel)) {
        if (1 < branchesToThisLabel.Count) return false;
      }
      Push/*?*/ push2 = block.Statements[1] as Push;
      if (push2 == null) return false;
      BasicBlock/*?*/ block2 = block.Statements[2] as BasicBlock;
      if (block2 == null || block2.Statements.Count < 1 || block2.Statements[0] != Goto.TargetStatement) return false;
      if (conditionalStatement.TrueBranch is EmptyStatement) {
        Conditional conditional = new Conditional();
        conditional.Condition = conditionalStatement.Condition;
        conditional.ResultIfTrue = push.ValueToPush;
        conditional.ResultIfFalse = push2.ValueToPush;
        this.sourceMethodBody.CombineLocations(conditional.Locations, conditional.Condition.Locations);
        this.sourceMethodBody.CombineLocations(conditional.Locations, conditional.ResultIfTrue.Locations);
        this.sourceMethodBody.CombineLocations(conditional.Locations, conditional.ResultIfFalse.Locations);
        push.ValueToPush = conditional;
        push.Locations = conditional.Locations;
        statements[i] = push;
        statements.RemoveRange(i+1, 3);
        block2.Statements.RemoveAt(0);
        statements.InsertRange(i+1, block2.Statements);

        return true;
      }
      if (conditionalStatement.FalseBranch is EmptyStatement) {
        Conditional conditional = new Conditional();
        if (ExpressionHelper.IsIntegralZero(push2.ValueToPush)) {
          conditional.Condition = InvertCondition(conditionalStatement.Condition);
          conditional.ResultIfTrue = push.ValueToPush;
          conditional.ResultIfFalse = push2.ValueToPush;
        } else {
          conditional.Condition = conditionalStatement.Condition;
          conditional.ResultIfTrue = push2.ValueToPush;
          conditional.ResultIfFalse = push.ValueToPush;
        }
        this.sourceMethodBody.CombineLocations(conditional.Locations, conditional.Condition.Locations);
        this.sourceMethodBody.CombineLocations(conditional.Locations, conditional.ResultIfTrue.Locations);
        this.sourceMethodBody.CombineLocations(conditional.Locations, conditional.ResultIfFalse.Locations);
        push.ValueToPush = conditional;
        push.Locations = conditional.Locations;
        statements[i] = push;
        statements.RemoveRange(i+1, 3);
        block2.Statements.RemoveAt(0);
        statements.InsertRange(i+1, block2.Statements);

        return true;
      }
      return false;
    }

    private bool ReplaceShortCircuitPattern2(List<IStatement> statements, int i) {
      if (i > statements.Count - 3) return false;
      ConditionalStatement/*?*/ conditionalStatement = statements[i] as ConditionalStatement;
      if (conditionalStatement == null) return false;
      ConditionalStatement/*?*/ conditionalStatement2 = statements[i+1] as ConditionalStatement;
      if (conditionalStatement2 == null) return false;
      if (statements[i+2] is ConditionalStatement) {
        if (!ReplaceShortCircuitPattern2(statements, i+1)) return false;
        if (i > statements.Count - 3) return false;
        conditionalStatement2 = statements[i+1] as ConditionalStatement;
        if (conditionalStatement2 == null) return false;
      }
      BasicBlock/*?*/ block = statements[i+2] as BasicBlock;
      if (block == null) {
        return this.ReplaceShortCircuitPattern3(statements, i);
      }
      if (block.Statements.Count < 1) return false;
      GotoStatement/*?*/ gotoStatement = conditionalStatement.TrueBranch as GotoStatement;
      if (gotoStatement == null) {
        return this.ReplaceShortCircuitPattern3(statements, i);
      }
      if (!(conditionalStatement.FalseBranch is EmptyStatement)) return false;
      if (gotoStatement.TargetStatement != block.Statements[0]) return false;
      if (!(conditionalStatement2.TrueBranch is EmptyStatement)) {
        if (!(conditionalStatement2.TrueBranch is GotoStatement)) return false;
        if (!(conditionalStatement2.FalseBranch is EmptyStatement)) return false;
        conditionalStatement2.Condition = InvertCondition(conditionalStatement2.Condition);
        IStatement temp = conditionalStatement2.TrueBranch;
        conditionalStatement2.TrueBranch = conditionalStatement2.FalseBranch;
        conditionalStatement2.FalseBranch = temp;
      } else {
        if (!(conditionalStatement2.FalseBranch is GotoStatement)) return false;
      }
      Conditional conditional = new Conditional();
      conditional.Condition = conditionalStatement.Condition;
      conditional.ResultIfTrue = new CompileTimeConstant() { Value = 1, Type = this.sourceMethodBody.MethodDefinition.Type.PlatformType.SystemInt32 };
      conditional.ResultIfFalse = conditionalStatement2.Condition;
      conditionalStatement2.Condition = conditional;
      this.sourceMethodBody.CombineLocations(conditionalStatement2.Locations, conditionalStatement.Locations);
      statements.RemoveAt(i);
      return true;
    }

    private bool ReplaceShortCircuitPattern3(List<IStatement> statements, int i) {
      if (i > statements.Count - 3) return false;
      ConditionalStatement/*?*/ conditionalStatement = statements[i] as ConditionalStatement;
      if (conditionalStatement == null) return false;
      ConditionalStatement/*?*/ conditionalStatement2 = statements[i+1] as ConditionalStatement;
      if (conditionalStatement2 == null) return false;
      if (statements[i+2] is ConditionalStatement) {
        if (!this.ReplaceShortCircuitPattern2(statements, i+1)) return false;
        if (i > statements.Count - 3) return false;
        conditionalStatement2 = statements[i+1] as ConditionalStatement;
        if (conditionalStatement2 == null) return false;
      }
      GotoStatement/*?*/ gotoStatement = conditionalStatement.FalseBranch as GotoStatement;
      if (gotoStatement == null) return false;
      if (!(conditionalStatement.TrueBranch is EmptyStatement)) return false;
      GotoStatement/*?*/ gotoStatement2 = conditionalStatement2.FalseBranch as GotoStatement;
      if (gotoStatement2 == null) {
        gotoStatement2 = conditionalStatement2.TrueBranch as GotoStatement;
        if (gotoStatement2 == null) return false;
        if (!(conditionalStatement2.FalseBranch is EmptyStatement)) return false;
        //brfalse, brtrue, ... could be A && B || C
        BasicBlock/*?*/ bb = statements[i+2] as BasicBlock;
        if (bb == null) return false;
        if (bb.Statements.Count < 1 || !(bb.Statements[0] == gotoStatement.TargetStatement)) return false;
        statements.RemoveAt(i+2);
        statements.InsertRange(i+2, bb.Statements);
        statements.RemoveAt(i+2);
        Conditional conditional = new Conditional();
        conditional.Condition = conditionalStatement.Condition;
        conditional.ResultIfTrue = conditionalStatement2.Condition;
        conditional.ResultIfFalse = new CompileTimeConstant() { Value = 0, Type = this.sourceMethodBody.MethodDefinition.Type.PlatformType.SystemInt32 };
        conditionalStatement2.Condition = conditional;
        this.sourceMethodBody.CombineLocations(conditionalStatement2.Locations, conditionalStatement.Locations);
        statements.RemoveAt(i);
        return this.ReplaceShortCircuitPattern2(statements, i);
      }
      if (!(conditionalStatement2.TrueBranch is EmptyStatement)) return false;
      if (gotoStatement.TargetStatement == gotoStatement2.TargetStatement) {
        Conditional conditional = new Conditional();
        conditional.Condition = conditionalStatement.Condition;
        conditional.ResultIfTrue = conditionalStatement2.Condition;
        conditional.ResultIfFalse = new CompileTimeConstant() { Value = 0, Type = this.sourceMethodBody.MethodDefinition.Type.PlatformType.SystemInt32 };
        conditionalStatement2.Condition = conditional;
        this.sourceMethodBody.CombineLocations(conditionalStatement2.Locations, conditionalStatement.Locations);
        statements.RemoveAt(i);
        return true;
      }
      return false;
    }

    private static IExpression InvertCondition(IExpression expression) {
      IBinaryOperation/*?*/ binOp = expression as IBinaryOperation;
      if (binOp != null) return InvertBinaryOperation(binOp);
      LogicalNot logicalNot = new LogicalNot();
      logicalNot.Operand = expression;
      logicalNot.Locations.AddRange(expression.Locations);
      return logicalNot;
    }

    private static IExpression InvertBinaryOperation(IBinaryOperation binOp) {
      BinaryOperation/*?*/ result = null;
      if (binOp is IEquality)
        result = new NotEquality();
      else if (binOp is INotEquality)
        result = new Equality();
      else if (binOp is ILessThan)
        result = new GreaterThanOrEqual();
      else if (binOp is ILessThanOrEqual)
        result = new GreaterThan();
      else if (binOp is IGreaterThan)
        result = new LessThanOrEqual();
      else if (binOp is IGreaterThanOrEqual)
        result = new LessThan();
      if (result != null) {
        result.LeftOperand = binOp.LeftOperand;
        result.RightOperand = binOp.RightOperand;
        result.Locations.AddRange(binOp.Locations);
        return result;
      }
      LogicalNot logicalNot = new LogicalNot();
      logicalNot.Operand = binOp;
      logicalNot.Locations.AddRange(binOp.Locations);
      return logicalNot;
    }

    private bool ReplaceChainedShortCircuitBooleanPattern(List<IStatement> statements, int i) {
      ConditionalStatement conditionalStatement = (ConditionalStatement)statements[i];
      if (!this.ReplaceShortCircuitPattern(statements, i + 1)) {
        if (!this.ReplaceShortCircuitPatternCreatedByCCI2(statements, i + 1)) return false;
      }
      //if (!this.ReplaceShortCircuitPattern(statements, i+1)) return false;
      Push/*?*/ push = statements[i+1] as Push;
      if (push == null) return false;
      Conditional/*?*/ chainedConditional = push.ValueToPush as Conditional;
      if (chainedConditional == null) return false;

      return this.ReplaceShortCircuitPattern(statements, i);
    }

    /// <summary>
    /// Finds the following pattern:
    /// i   :  c ? A : B; // either A or B must be an empty statement and the other is "goto L1;"
    /// i+1 :  push x;
    /// i+2 :  goto L2;
    /// i+3 :  Block1
    ///        0  : L1;
    ///        1  : push y;
    ///        2  : goto L2;
    ///        3  : Block2
    ///             0 : whatever (but presumably it is the label L2)
    ///             
    /// Transforms it into:
    /// i   : push d ? X : Y;
    /// i+1 : goto L1;
    /// i+2 : Block 2
    /// 
    /// Where if A is the empty statement,
    ///   d == c, X == x, Y == y
    /// If B is the empty statement and y is zero
    ///   d == !c, X == x, Y == y
    /// If B is the empty statement and y is not zero
    ///   d == c, X == y, Y == x
    /// And Block1 is deleted from the list.
    /// </summary>
    private bool ReplaceShortCircuitPatternCreatedByCCI2(List<IStatement> statements, int i) {
      if (i > statements.Count - 4) return false;
      ConditionalStatement/*?*/ conditionalStatement = statements[i] as ConditionalStatement;
      if (conditionalStatement == null) return false;
      Push/*?*/ push1 = statements[i + 1] as Push;
      if (push1 == null) return false;
      GotoStatement/*?*/ Goto = statements[i + 2] as GotoStatement;
      if (Goto == null) return false;
      BasicBlock/*?*/ block1 = statements[i + 3] as BasicBlock;
      if (block1 == null) return false;
      if (block1.Statements.Count < 4) return false;
      LabeledStatement/*?*/ label = block1.Statements[0] as LabeledStatement;
      if (label == null) return false;
      List<IGotoStatement> branchesToThisLabel;
      if (this.predecessors.TryGetValue(label, out branchesToThisLabel)) {
        if (1 < branchesToThisLabel.Count) return false;
      }
      // TODO? Should we make sure that one of the branches in the conditionalStatement is
      // to label?
      Push/*?*/ push2 = block1.Statements[1] as Push;
      if (push2 == null) return false;
      GotoStatement/*?*/ Goto2 = block1.Statements[2] as GotoStatement;
      if (Goto2 == null) return false;
      if (Goto.TargetStatement != Goto2.TargetStatement) return false;
      BasicBlock/*?*/ block2 = block1.Statements[3] as BasicBlock;
      if (block2 == null) return false;
      if (conditionalStatement.TrueBranch is EmptyStatement) {
        Conditional conditional = new Conditional();
        conditional.Condition = conditionalStatement.Condition;
        conditional.ResultIfTrue = push1.ValueToPush;
        conditional.ResultIfFalse = push2.ValueToPush;
        push1.ValueToPush = conditional;
        push1.Locations = conditionalStatement.Locations;
        statements[i] = push1;
        statements[i + 1] = statements[i + 2]; // move the goto up
        statements[i + 2] = block2;
        statements.RemoveRange(i + 3, 1);
        return true;
      }
      if (conditionalStatement.FalseBranch is EmptyStatement) {
        Conditional conditional = new Conditional();
        if (ExpressionHelper.IsIntegralZero(push2.ValueToPush)) {
          conditional.Condition = InvertCondition(conditionalStatement.Condition);
          conditional.ResultIfTrue = push1.ValueToPush;
          conditional.ResultIfFalse = push2.ValueToPush;
        } else {
          conditional.Condition = conditionalStatement.Condition;
          conditional.ResultIfTrue = push2.ValueToPush;
          conditional.ResultIfFalse = push1.ValueToPush;
        }
        push1.ValueToPush = conditional;
        push1.Locations = conditionalStatement.Locations;
        statements[i] = push1;
        statements[i + 1] = statements[i + 2]; // move the goto up
        statements[i + 2] = block2;
        statements.RemoveRange(i + 3, 1);
        return true;
      }
      return false;
    }

    private void ReplaceLocalArrayInitializerPattern(List<IStatement> statements, int i) {
      if (i > statements.Count - 4) return;
      Push/*?*/ push = statements[i] as Push;
      if (push == null) return;
      var pushDup = statements[i+1] as Push;
      if (pushDup == null || !(pushDup.ValueToPush is Dup)) return;
      CreateArray/*?*/ createArray = push.ValueToPush as CreateArray;
      if (createArray == null) return;
      ExpressionStatement/*?*/ expressionStatement = statements[i+2] as ExpressionStatement;
      if (expressionStatement == null) return;
      MethodCall/*?*/ methodCall = expressionStatement.Expression as MethodCall;
      if (methodCall == null || !methodCall.IsStaticCall || methodCall.Arguments.Count != 2) return;
      var pop = methodCall.Arguments[0] as Pop;
      if (pop == null) return;
      TokenOf/*?*/ tokenOf = methodCall.Arguments[1] as TokenOf;
      if (tokenOf == null) return;
      IFieldDefinition/*?*/ initialValueField = tokenOf.Definition as IFieldDefinition;
      if (initialValueField == null || !initialValueField.IsMapped) return;
      if (methodCall.MethodToCall.Name.UniqueKey != this.InitializeArray.UniqueKey) return;
      expressionStatement = statements[i+3] as ExpressionStatement;
      if (expressionStatement == null) return;
      Assignment/*?*/ assignment = expressionStatement.Expression as Assignment;
      if (assignment == null) return;
      var pop2 = assignment.Source as Pop;
      if (pop2 == null) return;
      List<ulong> sizes = new List<ulong>();
      foreach (IExpression expr in createArray.Sizes) {
        IMetadataConstant mdc = expr as IMetadataConstant;
        if (mdc == null) return;
        sizes.Add(ConvertToUlong(mdc));
      }
      AddArrayInitializers(createArray, initialValueField, sizes.ToArray());
      assignment.Source = createArray;
      statements[i] = expressionStatement;
      statements.RemoveRange(i+1, 3);
    }

    private static void AddArrayInitializers(CreateArray createArray, IFieldDefinition initialValueField, ulong[] sizes) {
      ITypeReference elemType = createArray.ElementType;
      MemoryStream memoryStream = new MemoryStream(new List<byte>(initialValueField.FieldMapping.Data).ToArray());
      BinaryReader reader = new BinaryReader(memoryStream, Encoding.Unicode);
      ulong flatSize = 1;
      foreach (ulong dimensionSize in sizes) flatSize *= dimensionSize;
      while (flatSize-- > 0) {
        CompileTimeConstant cc = new CompileTimeConstant();
        cc.Value = ReadValue(elemType.TypeCode, reader);
        cc.Type = elemType;
        createArray.Initializers.Add(cc);
      }
    }

    private static ulong ConvertToUlong(IMetadataConstant c) {
      IConvertible/*?*/ ic = c.Value as IConvertible;
      if (ic == null) return 0; //TODO: error
      switch (ic.GetTypeCode()) {
        case TypeCode.SByte:
        case TypeCode.Int16:
        case TypeCode.Int32:
        case TypeCode.Int64:
          return (ulong)ic.ToInt64(null); //TODO: error if < 0
        case TypeCode.Byte:
        case TypeCode.UInt16:
        case TypeCode.UInt32:
        case TypeCode.UInt64:
          return ic.ToUInt64(null);
      }
      return 0; //TODO: error
    }

    private static object ReadValue(PrimitiveTypeCode primitiveTypeCode, BinaryReader reader) {
      switch (primitiveTypeCode) {
        case PrimitiveTypeCode.Boolean: return reader.ReadBoolean();
        case PrimitiveTypeCode.Char: return reader.ReadChar();
        case PrimitiveTypeCode.Float32: return reader.ReadSingle();
        case PrimitiveTypeCode.Float64: return reader.ReadDouble();
        case PrimitiveTypeCode.Int16: return reader.ReadInt16();
        case PrimitiveTypeCode.Int32: return reader.ReadInt32();
        case PrimitiveTypeCode.Int64: return reader.ReadInt64();
        case PrimitiveTypeCode.Int8: return reader.ReadSByte();
        case PrimitiveTypeCode.UInt16: return reader.ReadUInt16();
        case PrimitiveTypeCode.UInt32: return reader.ReadUInt32();
        case PrimitiveTypeCode.UInt64: return reader.ReadUInt64();
        case PrimitiveTypeCode.UInt8: return reader.ReadByte();
        default:
          Debug.Assert(false);
          break;
      }
      return null;
    }

    IName InitializeArray {
      get {
        if (this.initializeArray == null)
          this.initializeArray = this.nameTable.GetNameFor("InitializeArray");
        return this.initializeArray;
      }
    }
    IName/*?*/ initializeArray;
  }

  internal class PopCounter : BaseCodeTraverser {
    internal int count;

    public override void Visit(IExpression expression) {
      if (expression is Pop) this.count++;
      base.Visit(expression);
    }

  }

  internal class PopReplacer : MethodBodyCodeMutator {
    List<IStatement> statements;
    int i;
    internal int numberOfPopsToIgnore;

    internal PopReplacer(IMetadataHost host, List<IStatement> statements, int i, int numberOfPopsToIgnore)
      : base(host) {
      this.statements = statements;
      this.i = i;
      this.numberOfPopsToIgnore = numberOfPopsToIgnore;
    }

    public override IExpression Visit(IExpression expression) {
      Pop pop = expression as Pop;
      if (pop != null) {
        if (this.numberOfPopsToIgnore-- > 0) return expression;
        Push push = (Push)this.statements[this.i++];
        if (pop is PopAsUnsigned)
          return new ConvertToUnsigned(push.ValueToPush);
        else
          return push.ValueToPush;
      }
      return base.Visit(expression);
    }

  }

  internal class TempVariable : LocalDefinition {
    internal bool turnIntoPopValueExpression;
    internal TempVariable() {
    }

  }

}

