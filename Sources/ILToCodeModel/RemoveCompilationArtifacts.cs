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
using Microsoft.Cci.MutableCodeModel;

namespace Microsoft.Cci.ILToCodeModel {

  internal class CompilationArtifactRemover : MethodBodyCodeMutator {

    internal CompilationArtifactRemover(SourceMethodBody sourceMethodBody)
      : base(sourceMethodBody.host, true) {
      this.containingType = sourceMethodBody.ilMethodBody.MethodDefinition.ContainingTypeDefinition;
      this.sourceMethodBody = sourceMethodBody;
    }

    internal Dictionary<string, object> capturedLocalOrParameter = new Dictionary<string, object>();
    ITypeDefinition containingType;
    ILocalDefinition currentClosureLocal = Dummy.LocalVariable;
    SourceMethodBody sourceMethodBody;
    Dictionary<ILocalDefinition, IExpression> expressionToSubstituteForCompilerGeneratedSingleAssignmentLocal = new Dictionary<ILocalDefinition, IExpression>();
    Dictionary<IParameterDefinition, IParameterDefinition> parameterMap = new Dictionary<IParameterDefinition, IParameterDefinition>();
    internal Dictionary<int, bool>/*?*/ referencedLabels;

    public override IAddressableExpression Visit(AddressableExpression addressableExpression) {
      var field = addressableExpression.Definition as IFieldReference;
      if (field != null) {
        object localOrParameter = null;
        if (this.capturedLocalOrParameter.TryGetValue(field.Name.Value, out localOrParameter)) {
          addressableExpression.Definition = localOrParameter;
          addressableExpression.Instance = null;
          return addressableExpression;
        }
      }
      var parameter = addressableExpression.Definition as IParameterDefinition;
      if (parameter != null) {
        IParameterDefinition parToSubstitute;
        if (this.parameterMap.TryGetValue(parameter, out parToSubstitute)) {
          addressableExpression.Definition = parToSubstitute;
          return addressableExpression;
        }
      }
      return base.Visit(addressableExpression);
    }

    public override ITargetExpression Visit(TargetExpression targetExpression) {
      var field = targetExpression.Definition as IFieldReference;
      if (field != null) {
        object localOrParameter = null;
        if (this.capturedLocalOrParameter.TryGetValue(field.Name.Value, out localOrParameter)) {
          targetExpression.Definition = localOrParameter;
          targetExpression.Instance = null;
          return targetExpression;
        }
      }
      return base.Visit(targetExpression);
    }

    public override IBlockStatement Visit(BlockStatement blockStatement) {
      if (this.referencedLabels == null) {
        LabelReferenceFinder finder = new LabelReferenceFinder();
        finder.Visit(blockStatement.Statements);
        this.referencedLabels = finder.referencedLabels;
      }
      ILocalDefinition savedClosureLocal = this.currentClosureLocal;
      this.FindCapturedLocals(blockStatement.Statements);
      IBlockStatement result = base.Visit(blockStatement);
      new CachedDelegateRemover(this.sourceMethodBody).Visit(result);
      this.currentClosureLocal = savedClosureLocal;
      return result;
    }

    private void FindCapturedLocals(List<IStatement> statements) {
      LocalDeclarationStatement/*?*/ locDecl = null;
      INestedTypeReference/*?*/ closureType = null;
      int i = 0;
      while (i < statements.Count && closureType == null) {
        var statement = statements[i++];
        locDecl = statement as LocalDeclarationStatement;
        if (locDecl == null || !(locDecl.InitialValue is ICreateObjectInstance)) continue;
        closureType = UnspecializedMethods.AsUnspecializedNestedTypeReference(locDecl.LocalVariable.Type);
      }
      if (closureType == null) return;
      //REVIEW: need to avoid resolving types that are not defined in the module we are analyzing.
      ITypeReference t1 = UnspecializedMethods.AsUnspecializedTypeReference(closureType.ContainingType.ResolvedType);
      ITypeReference t2 = UnspecializedMethods.AsUnspecializedTypeReference(this.containingType);
      if (t1 != t2) return;
      if (!UnspecializedMethods.IsCompilerGenerated(closureType.ResolvedType)) return;
      if (this.sourceMethodBody.privateHelperTypesToRemove == null) this.sourceMethodBody.privateHelperTypesToRemove = new List<ITypeDefinition>();
      this.sourceMethodBody.privateHelperTypesToRemove.Add(closureType.ResolvedType);
      this.currentClosureLocal = locDecl.LocalVariable;
      statements.RemoveAt(i-1);
      for (int j = i-1; j < statements.Count; j++) {
        IExpressionStatement/*?*/ es = statements[j] as IExpressionStatement;
        if (es == null) break;
        IAssignment/*?*/ assignment = es.Expression as IAssignment;
        if (assignment == null) break;
        IFieldReference/*?*/ closureField = assignment.Target.Definition as IFieldReference;
        if (closureField == null) break;
        ITypeReference closureFieldContainingType = UnspecializedMethods.AsUnspecializedNestedTypeReference(assignment.Target.Instance.Type);
        if (closureFieldContainingType == null) break;
        if (!TypeHelper.TypesAreEquivalent(closureFieldContainingType, closureType)) break;
        IThisReference thisReference = assignment.Source as IThisReference;
        if (thisReference == null) {
          IBoundExpression/*?*/ binding = assignment.Source as IBoundExpression;
          if (binding != null && (binding.Definition is IParameterDefinition || binding.Definition is ILocalDefinition)) {
            this.capturedLocalOrParameter.Add(closureField.Name.Value, binding.Definition);
          } else {
            // Corner case: csc generated closure class captures a local that does not appear in the original method.
            // Assume this local is always holding a constant;
            ICompileTimeConstant ctc = assignment.Source as ICompileTimeConstant;
            if (ctc != null) {
              LocalDefinition localDefinition = new LocalDefinition() {
                Name = closureField.ResolvedField.Name, Type = closureField.Type
              };
              LocalDeclarationStatement localDeclStatement = new LocalDeclarationStatement() {
                LocalVariable = localDefinition, InitialValue = ctc
              };
              statements.Insert(j, localDeclStatement); j++;
              this.capturedLocalOrParameter.Add(closureField.Name.Value, localDefinition);
            } else continue;
          }
        } else {
          this.capturedLocalOrParameter.Add(closureField.Name.Value, thisReference);
        }
        statements.RemoveAt(j--);
      }
      foreach (var field in closureType.ResolvedType.Fields) {
        if (this.capturedLocalOrParameter.ContainsKey(field.Name.Value)) continue;
        var newLocal = new LocalDefinition() { Name = field.Name, Type = field.Type };
        var newLocalDecl = new LocalDeclarationStatement() { LocalVariable = newLocal };
        statements.Insert(i-1, newLocalDecl);
        this.capturedLocalOrParameter.Add(field.Name.Value, newLocal);
      }
    }

    public override IExpression Visit(BoundExpression boundExpression) {
      ILocalDefinition/*?*/ local = boundExpression.Definition as ILocalDefinition;
      if (local != null) {
        IExpression substitute = boundExpression;
        if (this.expressionToSubstituteForCompilerGeneratedSingleAssignmentLocal.TryGetValue(local, out substitute)) {
          this.sourceMethodBody.numberOfReferences[local]--;
          return substitute;
        }
      }
      IFieldReference/*?*/ closureField = boundExpression.Definition as IFieldReference;
      if (closureField != null) {
        object/*?*/ localOrParameter = null;
        this.capturedLocalOrParameter.TryGetValue(closureField.Name.Value, out localOrParameter);
        if (localOrParameter != null) {
          IThisReference thisReference = localOrParameter as IThisReference;
          if (thisReference != null) return thisReference;
          boundExpression.Definition = localOrParameter;
          boundExpression.Instance = null;
          return boundExpression;
        }
      }
      IParameterDefinition/*?*/ parameter = boundExpression.Definition as IParameterDefinition;
      if (parameter != null) {
        IParameterDefinition parToSubstitute;
        if (this.parameterMap.TryGetValue(parameter, out parToSubstitute)) {
          boundExpression.Definition = parToSubstitute;
          return boundExpression;
        }
      }
      return base.Visit(boundExpression);
    }

    public override IExpression Visit(CreateDelegateInstance createDelegateInstance) {
      BoundExpression/*?*/ bexpr = createDelegateInstance.Instance as BoundExpression;
      if (bexpr != null && bexpr.Definition == this.currentClosureLocal)
        return ConvertToAnonymousDelegate(createDelegateInstance);
      CompileTimeConstant/*?*/ cc = createDelegateInstance.Instance as CompileTimeConstant;
      IMethodDefinition delegateMethodDefinition = createDelegateInstance.MethodToCallViaDelegate.ResolvedMethod;
      delegateMethodDefinition = UnspecializedMethods.UnspecializedMethodDefinition(delegateMethodDefinition);
      ITypeReference delegateContainingType = createDelegateInstance.MethodToCallViaDelegate.ContainingType;
      delegateContainingType = UnspecializedMethods.AsUnspecializedTypeReference(delegateContainingType);
      bool IsNullInstanceOrThis = (cc != null && cc.Value == null) || createDelegateInstance.Instance is IThisReference;
      if (IsNullInstanceOrThis && TypeHelper.TypesAreEquivalent(delegateContainingType.ResolvedType, this.containingType) &&
        UnspecializedMethods.IsCompilerGenerated(delegateMethodDefinition))
        return ConvertToAnonymousDelegate(createDelegateInstance);
      return base.Visit(createDelegateInstance);
    }

    private IExpression ConvertToAnonymousDelegate(CreateDelegateInstance createDelegateInstance) {
      IMethodDefinition closureMethod = createDelegateInstance.MethodToCallViaDelegate.ResolvedMethod;
      IMethodBody closureMethodBody = UnspecializedMethods.GetMethodBodyFromUnspecializedVersion(closureMethod);
      AnonymousDelegate anonDel = new AnonymousDelegate();
      anonDel.CallingConvention = closureMethod.CallingConvention;
      anonDel.Parameters = new List<IParameterDefinition>(closureMethod.Parameters);
      for (int i = 0, n = anonDel.Parameters.Count; i < n; i++) {
        IParameterDefinition closureMethodPar = UnspecializedParameterDefinition(anonDel.Parameters[i]);
        ParameterDefinition par = new ParameterDefinition();
        this.parameterMap.Add(closureMethodPar, par);
        par.Copy(closureMethodPar, this.host.InternFactory);
        par.ContainingSignature = anonDel;
        anonDel.Parameters[i] = par;
      }
      anonDel.Body = new SourceMethodBody(closureMethodBody, this.sourceMethodBody.host,
        this.sourceMethodBody.sourceLocationProvider, this.sourceMethodBody.localScopeProvider).Block;
      anonDel.ReturnValueIsByRef = closureMethod.ReturnValueIsByRef;
      if (closureMethod.ReturnValueIsModified)
        anonDel.ReturnValueCustomModifiers = new List<ICustomModifier>(closureMethod.ReturnValueCustomModifiers);
      anonDel.ReturnType = closureMethod.Type;
      anonDel.Type = createDelegateInstance.Type;
      return this.Visit(anonDel);
    }

    /// <summary>
    /// Given a parameter definition, if it is a specialized parameter definition, get the unspecialized version, or
    /// otherwise return the parameter itself. 
    /// </summary>
    /// <param name="parameter"></param>
    /// <returns></returns>
    internal static IParameterDefinition UnspecializedParameterDefinition(IParameterDefinition parameter) {
      SpecializedParameterDefinition specializedParameter = parameter as SpecializedParameterDefinition;
      if (specializedParameter != null) {
        return UnspecializedParameterDefinition(specializedParameter.PartiallySpecializedParameter);
      }
      return parameter;
    }

    public override IExpression Visit(CreateObjectInstance createObjectInstance) {
      if (createObjectInstance.Arguments.Count == 2) {
        AddressOf/*?*/ aexpr = createObjectInstance.Arguments[1] as AddressOf;
        if (aexpr != null && aexpr.Expression.Definition is IMethodReference) {
          CreateDelegateInstance createDel = new CreateDelegateInstance();
          createDel.Instance = createObjectInstance.Arguments[0];
          createDel.MethodToCallViaDelegate = (IMethodReference)aexpr.Expression.Definition;
          createDel.Locations = createObjectInstance.Locations;
          createDel.Type = createObjectInstance.Type;
          return this.Visit(createDel);
        }
      }
      return base.Visit(createObjectInstance);
    }

    public override IExpression Visit(Equality equality) {
      if (equality.LeftOperand.Type.TypeCode == PrimitiveTypeCode.Boolean) {
        if (ExpressionHelper.IsIntegralZero(equality.RightOperand))
          return InvertCondition(this.Visit(equality.LeftOperand));
      }
      return base.Visit(equality);
    }

    public override IExpression Visit(GreaterThan greaterThan) {
      var castIfPossible = greaterThan.LeftOperand as ICastIfPossible;
      if (castIfPossible != null) {
        var compileTimeConstant = greaterThan.RightOperand as ICompileTimeConstant;
        if (compileTimeConstant != null && compileTimeConstant.Value == null) {
          return this.Visit(new CheckIfInstance() {
            Operand = castIfPossible.ValueToCast, TypeToCheck = castIfPossible.TargetType,
            Type = greaterThan.Type, Locations = greaterThan.Locations
          });
        }
      }
      castIfPossible = greaterThan.RightOperand as ICastIfPossible;
      if (castIfPossible != null) {
        var compileTimeConstant = greaterThan.LeftOperand as ICompileTimeConstant;
        if (compileTimeConstant != null && compileTimeConstant.Value == null) {
          return this.Visit(new CheckIfInstance() {
            Operand = castIfPossible.ValueToCast, TypeToCheck = castIfPossible.TargetType,
            Type = greaterThan.Type, Locations = greaterThan.Locations
          });
        }
      }
      return base.Visit(greaterThan);
    }

    public override IStatement Visit(LabeledStatement labeledStatement) {
      if (!this.referencedLabels.ContainsKey(labeledStatement.Label.UniqueKey))
        return CodeDummy.Block;
      return base.Visit(labeledStatement);
    }

    public override IStatement Visit(LocalDeclarationStatement localDeclarationStatement) {
      ILocalDefinition local = localDeclarationStatement.LocalVariable;
      base.Visit(localDeclarationStatement);
      int numberOfAssignments = 0;
      if (!this.sourceMethodBody.numberOfAssignments.TryGetValue(local, out numberOfAssignments) || numberOfAssignments > 1)
        return localDeclarationStatement;
      int numReferences = 0;
      if (!this.sourceMethodBody.numberOfReferences.TryGetValue(local, out numReferences) || numReferences > 1)
        return localDeclarationStatement;
      if (this.sourceMethodBody.sourceLocationProvider != null) {
        bool isCompilerGenerated = false;
        this.sourceMethodBody.sourceLocationProvider.GetSourceNameFor(local, out isCompilerGenerated);
        if (!isCompilerGenerated) return localDeclarationStatement;
      }
      var val = localDeclarationStatement.InitialValue;
      if (numReferences > 0 && (val is CompileTimeConstant || val is TypeOf || val is ThisReference)) {
        this.expressionToSubstituteForCompilerGeneratedSingleAssignmentLocal.Add(local, val);
        return CodeDummy.Block; //Causes the caller to omit this statement from the containing statement list.
      }
      if (numReferences == 0) return CodeDummy.Block; //unused declaration
      return localDeclarationStatement;
    }

    public override IExpression Visit(LogicalNot logicalNot) {
      if (logicalNot.Type == Dummy.TypeReference)
        return InvertCondition(this.Visit(logicalNot.Operand));
      else if (logicalNot.Operand.Type.TypeCode == PrimitiveTypeCode.Int32)
        return new Equality() {
          LeftOperand = logicalNot.Operand,
          RightOperand = new CompileTimeConstant() { Value = 0, Type = this.host.PlatformType.SystemInt32 },
          Type = this.host.PlatformType.SystemBoolean,
          Locations = logicalNot.Locations
        };
      else
        return base.Visit(logicalNot);
    }

    private static IExpression InvertCondition(IExpression condition) {
      var equality = condition as Equality;
      if (equality != null) {
        if (equality.LeftOperand.Type.TypeCode != PrimitiveTypeCode.Float32 && equality.LeftOperand.Type.TypeCode != PrimitiveTypeCode.Float64)
          return new NotEquality() { LeftOperand = equality.LeftOperand, Locations = equality.Locations, RightOperand = equality.RightOperand, Type = equality.Type };
      } else {
        var greaterThan = condition as GreaterThan;
        if (greaterThan != null) {
          if (TypeHelper.IsPrimitiveInteger(greaterThan.LeftOperand.Type))
            return new LessThanOrEqual() { LeftOperand = greaterThan.LeftOperand, Locations = greaterThan.Locations, RightOperand = greaterThan.RightOperand, Type = greaterThan.Type };
        } else {
          var greaterThanOrEqual = condition as GreaterThanOrEqual;
          if (greaterThanOrEqual != null) {
            if (TypeHelper.IsPrimitiveInteger(greaterThanOrEqual.LeftOperand.Type))
              return new LessThan() { LeftOperand = greaterThanOrEqual.LeftOperand, Locations = greaterThanOrEqual.Locations, RightOperand = greaterThanOrEqual.RightOperand, Type = greaterThanOrEqual.Type };
          } else {
            var lessThan = condition as LessThan;
            if (lessThan != null) {
              if (TypeHelper.IsPrimitiveInteger(lessThan.LeftOperand.Type))
                return new GreaterThanOrEqual() { LeftOperand = lessThan.LeftOperand, Locations = lessThan.Locations, RightOperand = lessThan.RightOperand, Type = lessThan.Type };
            } else {
              var lessThanOrEqual = condition as LessThanOrEqual;
              if (lessThanOrEqual != null) {
                if (TypeHelper.IsPrimitiveInteger(lessThanOrEqual.LeftOperand.Type))
                  return new GreaterThan() { LeftOperand = lessThanOrEqual.LeftOperand, Locations = lessThanOrEqual.Locations, RightOperand = lessThanOrEqual.RightOperand, Type = lessThanOrEqual.Type };
              } else {
                var logicalNot = condition as LogicalNot;
                if (logicalNot != null)
                  return logicalNot.Operand;
                else {
                  var notEquality = condition as NotEquality;
                  if (notEquality != null) {
                    if (notEquality.LeftOperand.Type.TypeCode != PrimitiveTypeCode.Float32 && notEquality.LeftOperand.Type.TypeCode != PrimitiveTypeCode.Float64)
                      return new Equality() { LeftOperand = notEquality.LeftOperand, Locations = new List<ILocation>(notEquality.Locations), RightOperand = notEquality.RightOperand, Type = notEquality.Type };
                  }
                }
              }
            }
          }
        }
      }
      return new LogicalNot() { Locations = new List<ILocation>(condition.Locations), Operand = condition, Type = condition.Type };
    }

    public override IExpression Visit(MethodCall methodCall) {
      if (methodCall.Arguments.Count == 1) {
        var tokenOf = methodCall.Arguments[0] as TokenOf;
        if (tokenOf != null) {
          var typeRef = tokenOf.Definition as ITypeReference;
          if (typeRef != null && methodCall.MethodToCall.InternedKey == this.GetTypeFromHandle.InternedKey) {
            return new TypeOf() { Locations = methodCall.Locations, Type = methodCall.Type, TypeToGet = typeRef };
          }
        }
      }
      return base.Visit(methodCall);
    }

    /// <summary>
    /// A reference to System.Type.GetTypeFromHandle(System.Runtime.TypeHandle).
    /// </summary>
    IMethodReference GetTypeFromHandle {
      get {
        if (this.getTypeFromHandle == null) {
          this.getTypeFromHandle = new MethodReference(this.host, this.host.PlatformType.SystemType, CallingConvention.Default, this.host.PlatformType.SystemType,
          this.host.NameTable.GetNameFor("GetTypeFromHandle"), 0, this.host.PlatformType.SystemRuntimeTypeHandle);
        }
        return this.getTypeFromHandle;
      }
    }
    IMethodReference/*?*/ getTypeFromHandle;

    public override IStatement Visit(ReturnStatement returnStatement) {
      if (returnStatement.Expression != null) {
        returnStatement.Expression = this.Visit(returnStatement.Expression);
        this.sourceMethodBody.CombineLocations(returnStatement.Locations, returnStatement.Expression.Locations);
      }
      if (this.sourceMethodBody.MethodDefinition.Type.TypeCode == PrimitiveTypeCode.Boolean) {
        CompileTimeConstant/*?*/ cc = returnStatement.Expression as CompileTimeConstant;
        if (cc != null) {
          if (ExpressionHelper.IsIntegralZero(cc))
            cc.Value = false;
          else
            cc.Value = true;
          cc.Type = this.containingType.PlatformType.SystemBoolean;
        }
      }
      return returnStatement;
    }

  }

  internal class CachedDelegateRemover : MethodBodyCodeMutator {
    internal CachedDelegateRemover(SourceMethodBody sourceMethodBody)
      : base(sourceMethodBody.host, true) {
      this.containingType = sourceMethodBody.ilMethodBody.MethodDefinition.ContainingTypeDefinition;
      this.sourceMethodBody = sourceMethodBody;
    }
    ITypeDefinition containingType;
    SourceMethodBody sourceMethodBody;
    class CachedDelegateInfo {
      public int State;
      public IName Label;
      public AnonymousDelegate theDelegate;
      readonly public string Fname;
      public CachedDelegateInfo(string fname) {
        this.Fname = fname;
      }
    }
    Dictionary<string, CachedDelegateInfo> cachedDelegateFields = new Dictionary<string, CachedDelegateInfo>();
    static string CachedDelegateId = "CachedAnonymousMethodDelegate";

    public override IBlockStatement Visit(BlockStatement blockStatement) {
      FindAndRemoveAssignmentToCachedDelegate(blockStatement.Statements);
      return base.Visit(blockStatement);
    }

    public override IExpression Visit(BoundExpression boundExpression) {
      IFieldReference fieldReference = boundExpression.Definition as IFieldReference;
      if (fieldReference != null && this.cachedDelegateFields.ContainsKey(fieldReference.Name.Value) && this.cachedDelegateFields[fieldReference.Name.Value].State == 3) {
        return this.cachedDelegateFields[fieldReference.Name.Value].theDelegate;
      }
      return base.Visit(boundExpression);
    }

    void FindAndRemoveAssignmentToCachedDelegate(List<IStatement> statements) {
      for (int/*change in the body*/ i = 0; i < statements.Count; i++) {
        IConditionalStatement conditionalStatement = statements[i] as IConditionalStatement;
        if (conditionalStatement != null) {
          IBoundExpression boundExpression = conditionalStatement.Condition as IBoundExpression;
          if (boundExpression != null) {
            var fieldReference = boundExpression.Definition as IFieldReference;
            if (fieldReference != null && UnspecializedMethods.IsCompilerGenerated(fieldReference) && fieldReference.Name.Value.Contains(CachedDelegateId)) {
              CachedDelegateInfo info = new CachedDelegateInfo(fieldReference.Name.Value);
              IGotoStatement gotoStatement = conditionalStatement.TrueBranch as IGotoStatement;
              if (gotoStatement == null) gotoStatement = conditionalStatement.FalseBranch as IGotoStatement;
              if (gotoStatement != null) {
                info.Label = gotoStatement.TargetStatement.Label;
                info.State = 1;
              }
              if (!this.cachedDelegateFields.ContainsKey(fieldReference.Name.Value))
                this.cachedDelegateFields.Add(fieldReference.Name.Value, info);
              statements.RemoveAt(i--);
              continue;
            }
          }
        }
        IExpressionStatement expressionStatement = statements[i] as IExpressionStatement;
        if (expressionStatement != null) {
          IAssignment assignment = expressionStatement.Expression as IAssignment;
          if (assignment != null && assignment.Source is AnonymousDelegate) {
            IFieldReference/*?*/ fieldReference = assignment.Target.Definition as IFieldReference;
            if (fieldReference != null) {
              if (this.cachedDelegateFields.ContainsKey(fieldReference.Name.Value) && this.cachedDelegateFields[fieldReference.Name.Value].State == 1) {
                this.cachedDelegateFields[fieldReference.Name.Value].State = 3;
                this.cachedDelegateFields[fieldReference.Name.Value].theDelegate = assignment.Source as AnonymousDelegate;
                statements.RemoveAt(i--);
                continue;
              }
              if (!this.cachedDelegateFields.ContainsKey(fieldReference.Name.Value)) {
                CachedDelegateInfo info = new CachedDelegateInfo(fieldReference.Name.Value);
                info.State = 3;
                info.theDelegate = assignment.Source as AnonymousDelegate;
                this.cachedDelegateFields[fieldReference.Name.Value] = info;
                statements.RemoveAt(i--);
                continue;
              }
            }
          }
          continue;
        }
      }
    }
  }

  internal class LabelReferenceFinder : BaseCodeTraverser {

    internal Dictionary<int, bool> referencedLabels = new Dictionary<int, bool>();

    public override void Visit(IGotoStatement gotoStatement) {
      this.referencedLabels[gotoStatement.TargetStatement.Label.UniqueKey] = true;
    }

  }

}