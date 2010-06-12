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
using System;
using System.IO;
using Microsoft.Cci;
using Microsoft.Cci.MutableCodeModel;
using System.Collections.Generic;
using Microsoft.Cci.Contracts;

namespace Microsoft.Cci.MutableContracts {

  /// <summary>
  /// Helper class for performing common tasks on mutable contracts
  /// </summary>
  public class ContractHelper {

    /// <summary>
    /// Accumulates all elements from <paramref name="sourceContract"/> into <paramref name="targetContract"/>
    /// </summary>
    /// <param name="targetContract">Contract which is target of accumulator</param>
    /// <param name="sourceContract">Contract which is source of accumulator</param>
    public static void AddMethodContract(MethodContract targetContract, IMethodContract sourceContract) {
      targetContract.Preconditions.AddRange(sourceContract.Preconditions);
      targetContract.Postconditions.AddRange(sourceContract.Postconditions);
      targetContract.ThrownExceptions.AddRange(sourceContract.ThrownExceptions);
      targetContract.IsPure |= sourceContract.IsPure; // need the disjunction
      return;
    }

  }

  /// <summary>
  /// A mutator that substitutes parameters defined in one method with those from another method.
  /// </summary>
  public sealed class SubstituteParameters : MethodBodyCodeAndContractMutator {
    private IMethodDefinition targetMethod;
    private IMethodDefinition sourceMethod;
    private ITypeReference targetType;
    List<IParameterDefinition> parameters;

    /// <summary>
    /// Creates a mutator that replaces all occurrences of parameters from the target method with those from the source method.
    /// </summary>
    public SubstituteParameters(IMetadataHost host, IMethodDefinition targetMethodDefinition, IMethodDefinition sourceMethodDefinition)
      : base(host, false) { // NB: Important to pass "false": this mutator needs to make a copy of the entire contract!
      this.targetMethod = targetMethodDefinition;
      this.sourceMethod = sourceMethodDefinition;
      this.targetType = targetMethodDefinition.ContainingType;
      this.parameters = new List<IParameterDefinition>(targetMethod.Parameters);
    }

    /// <summary>
    /// If the <paramref name="boundExpression"/> represents a parameter of the source method,
    /// it is replaced with the equivalent parameter of the target method.
    /// </summary>
    /// <param name="boundExpression">The bound expression.</param>
    public override IExpression Visit(BoundExpression boundExpression) {
      ParameterDefinition/*?*/ par = boundExpression.Definition as ParameterDefinition;
      if (par != null && par.ContainingSignature == this.sourceMethod) {
        boundExpression.Definition = this.parameters[par.Index];
        return boundExpression;
      } else {
        return base.Visit(boundExpression);
      }
    }

    /// <summary>
    /// Replaces the specified this reference with a this reference to the containing type of the target method
    /// </summary>
    /// <param name="thisReference">The this reference.</param>
    /// <returns>a this reference to the containing type of the target method</returns>
    public override IExpression Visit(ThisReference thisReference) {
      return new ThisReference() {
        Type = this.targetType,
      };
    }
  }

}