﻿//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Cci;

namespace CSharpSourceEmitter {
  public partial class SourceEmitter : BaseCodeTraverser, ICSharpSourceEmitter {
    public override void Visit(IEventDefinition eventDefinition) {

      PrintAttributes(eventDefinition);
      PrintToken(CSharpToken.Indent);
      IMethodDefinition eventMeth = eventDefinition.Adder == null ?
        eventDefinition.Remover.ResolvedMethod :
        eventDefinition.Adder.ResolvedMethod;
      if (!eventDefinition.ContainingTypeDefinition.IsInterface && 
        IteratorHelper.EnumerableIsEmpty(MemberHelper.GetExplicitlyOverriddenMethods(eventMeth)))
        PrintEventDefinitionVisibility(eventDefinition);
      PrintMethodDefinitionModifiers(eventMeth);
      PrintToken(CSharpToken.Event);
      PrintEventDefinitionDelegateType(eventDefinition);
      PrintToken(CSharpToken.Space);
      PrintEventDefinitionName(eventDefinition);
      PrintToken(CSharpToken.Semicolon);
      PrintToken(CSharpToken.NewLine);
    }

    public virtual void PrintEventDefinitionVisibility(IEventDefinition eventDefinition) {
      PrintTypeMemberVisibility(eventDefinition.Visibility);
    }

    public virtual void PrintEventDefinitionDelegateType(IEventDefinition eventDefinition) {
      PrintTypeReference(eventDefinition.Type);
    }

    public virtual void PrintEventDefinitionName(IEventDefinition eventDefinition) {
      PrintIdentifier(eventDefinition.Name);
    }

  }
}
