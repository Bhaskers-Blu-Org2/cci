//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.Collections.Generic;

//^ using Microsoft.Contracts;

namespace Microsoft.Cci.MutableCodeModel {

  /// <summary>
  /// 
  /// </summary>
  public sealed class Addition : BinaryOperation, IAddition {

    /// <summary>
    /// 
    /// </summary>
    public Addition() {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="addition"></param>
    public Addition(IAddition addition)
      : base(addition) {
      this.CheckOverflow = addition.CheckOverflow;
    }

    /// <summary>
    /// The addition must be performed with a check for arithmetic overflow if the operands are integers.
    /// </summary>
    /// <value></value>
    public bool CheckOverflow { get; set; }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="visitor"></param>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }
  }

  /// <summary>
  /// 
  /// </summary>
  public sealed class AddressableExpression : Expression, IAddressableExpression {

    /// <summary>
    /// 
    /// </summary>
    public AddressableExpression() {
      this.definition = null;
      this.instance = null;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="addressableExpression"></param>
    public AddressableExpression(IAddressableExpression addressableExpression)
      : base(addressableExpression) {
      this.definition = addressableExpression.Definition;
      this.instance = addressableExpression.Instance;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="visitor"></param>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }

    /// <summary>
    /// The local variable, parameter, field, array element, pointer target or method that this expression denotes.
    /// </summary>
    /// <value></value>
    public object/*?*/ Definition {
      get { return this.definition; }
      set
        //^ requires result == null || result is ILocalDefinition || result is IParameterDefinition || result is IFieldReference || result is IArrayIndexer 
        //^   || result is IAddressDereference || result is IMethodReference || result is IThisReference;
      {
        this.definition = value;
      }
    }
    object/*?*/ definition;
    //^ invariant definition == null || definition is ILocalDefinition || definition is IParameterDefinition || definition is IFieldReference || definition is IArrayIndexer
    //^   || definition is IAddressDereference || definition is IMethodReference || definition is IThisReference;

    /// <summary>
    /// The instance to be used if this.Definition is an instance field/method or array indexer.
    /// </summary>
    /// <value></value>
    public IExpression/*?*/ Instance {
      get { return this.instance; }
      set { this.instance = value; }
    }
    IExpression/*?*/ instance;

  }

  /// <summary>
  /// 
  /// </summary>
  public sealed class AddressOf : Expression, IAddressOf {

    /// <summary>
    /// 
    /// </summary>
    public AddressOf() {
      this.expression = CodeDummy.AddressableExpression;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="addressOf"></param>
    public AddressOf(IAddressOf addressOf)
      : base(addressOf) {
      this.expression = addressOf.Expression;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="visitor"></param>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }

    /// <summary>
    /// An expression that represents an addressable location in memory.
    /// </summary>
    /// <value></value>
    public IAddressableExpression Expression {
      get { return this.expression; }
      set { this.expression = value; }
    }
    IAddressableExpression expression;

    /// <summary>
    /// If true, the address can only be used in operations where defining type of the addressed
    /// object has control over whether or not the object is mutated. For example, a value type that
    /// exposes no public fields or mutator methods cannot be changed using this address.
    /// </summary>
    /// <value></value>
    public bool ObjectControlsMutability {
      get { return this.objectControlsMutability; }
      set { this.objectControlsMutability = value; }
    }
    bool objectControlsMutability;

  }

  /// <summary>
  /// 
  /// </summary>
  public sealed class AddressDereference : Expression, IAddressDereference {

    /// <summary>
    /// 
    /// </summary>
    public AddressDereference() {
      this.address = CodeDummy.Expression;
      this.alignment = 0;
      this.isVolatile = false;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="addressDereference"></param>
    public AddressDereference(IAddressDereference addressDereference)
      : base(addressDereference) {
      this.address = addressDereference.Address;
      if (addressDereference.IsUnaligned)
        this.alignment = addressDereference.Alignment;
      else
        this.alignment = 0;
      this.isVolatile = addressDereference.IsVolatile;
    }

    /// <summary>
    /// The address to dereference.
    /// </summary>
    /// <value></value>
    public IExpression Address {
      get { return this.address; }
      set { this.address = value; }
    }
    IExpression address;

    /// <summary>
    /// If the addres to dereference is not aligned with the size of the target type, this property specifies the actual alignment.
    /// For example, a value of 1 specifies that the pointer is byte aligned, whereas the target type may be word sized.
    /// </summary>
    /// <value></value>
    public ushort Alignment {
      get { return this.alignment; }
      set
        //^ requires value == 1 || value == 2 || value == 4;
      {
        this.alignment = value;
      }
    }
    ushort alignment;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="visitor"></param>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }

    /// <summary>
    /// True if the address is not aligned to the natural size of the target type. If true, the actual alignment of the
    /// address is specified by this.Alignment.
    /// </summary>
    /// <value></value>
    public bool IsUnaligned {
      get { return this.alignment > 0; }
    }

    /// <summary>
    /// The location at Address is volatile and its contents may not be cached.
    /// </summary>
    /// <value></value>
    public bool IsVolatile {
      get { return this.isVolatile; }
      set { this.isVolatile = value; }
    }
    bool isVolatile;

  }

  /// <summary>
  /// 
  /// </summary>
  public sealed class AnonymousDelegate : Expression, IAnonymousDelegate {

    /// <summary>
    /// 
    /// </summary>
    public AnonymousDelegate() {
      this.body = CodeDummy.Block;
      this.callingConvention = CallingConvention.Default;
      this.parameters = new List<IParameterDefinition>();
      this.returnType = Dummy.TypeReference;
      this.returnValueCustomModifiers = new List<ICustomModifier>();
      this.returnValueIsByRef = false;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="anonymousDelegate"></param>
    public AnonymousDelegate(IAnonymousDelegate anonymousDelegate)
      : base(anonymousDelegate) {
      this.body = anonymousDelegate.Body;
      this.callingConvention = anonymousDelegate.CallingConvention;
      this.parameters = new List<IParameterDefinition>(anonymousDelegate.Parameters);
      this.returnType = anonymousDelegate.ReturnType;
      this.returnValueCustomModifiers = new List<ICustomModifier>(anonymousDelegate.ReturnValueCustomModifiers);
      this.returnValueIsByRef = anonymousDelegate.ReturnValueIsByRef;
    }

    /// <summary>
    /// A block of statements providing the implementation of the anonymous method that is called by the delegate that is the result of this expression.
    /// </summary>
    /// <value></value>
    public IBlockStatement Body {
      get { return this.body; }
      set { this.body = value; }
    }
    IBlockStatement body;

    /// <summary>
    /// Calling convention of the signature.
    /// </summary>
    /// <value></value>
    public CallingConvention CallingConvention {
      get { return this.callingConvention; }
      set { this.callingConvention = value; }
    }
    CallingConvention callingConvention;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="visitor"></param>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }

    /// <summary>
    /// The parameters this anonymous method.
    /// </summary>
    /// <value></value>
    public List<IParameterDefinition> Parameters {
      get { return this.parameters; }
      set { this.parameters = value; }
    }
    List<IParameterDefinition> parameters;

    /// <summary>
    /// The return type of the delegate.
    /// </summary>
    /// <value></value>
    public ITypeReference ReturnType {
      get { return this.returnType; }
      set { this.returnType = value; }
    }
    ITypeReference returnType;

    /// <summary>
    /// Returns the list of custom modifiers, if any, associated with the returned value. Evaluate this property only if ReturnValueIsModified is true.
    /// </summary>
    /// <value></value>
    public List<ICustomModifier> ReturnValueCustomModifiers {
      get { return this.returnValueCustomModifiers; }
      set { this.returnValueCustomModifiers = value; }
    }
    List<ICustomModifier> returnValueCustomModifiers;

    /// <summary>
    /// True if the return value is passed by reference (using a managed pointer).
    /// </summary>
    /// <value></value>
    public bool ReturnValueIsByRef {
      get { return this.returnValueIsByRef; }
      set { this.returnValueIsByRef = value; }
    }
    bool returnValueIsByRef;

    /// <summary>
    /// True if the return value has one or more custom modifiers associated with it.
    /// </summary>
    /// <value></value>
    public bool ReturnValueIsModified {
      get { return this.returnValueCustomModifiers.Count > 0; }
    }

    #region IAnonymousDelegate Members

    IEnumerable<IParameterDefinition> IAnonymousDelegate.Parameters {
      get { return this.Parameters.AsReadOnly(); }
    }

    #endregion

    #region ISignature Members

    IEnumerable<IParameterTypeInformation> ISignature.Parameters {
      get { return IteratorHelper.GetConversionEnumerable<IParameterDefinition, IParameterTypeInformation>(this.parameters); }
    }

    IEnumerable<ICustomModifier> ISignature.ReturnValueCustomModifiers {
      get { return this.returnValueCustomModifiers.AsReadOnly(); }
    }

    #endregion
  }

  /// <summary>
  /// An expression that represents an array element access.
  /// </summary>
  public sealed class ArrayIndexer : Expression, IArrayIndexer {

    /// <summary>
    /// 
    /// </summary>
    public ArrayIndexer() {
      this.indexedObject = CodeDummy.Expression;
      this.indices = new List<IExpression>();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="arrayIndexer"></param>
    public ArrayIndexer(IArrayIndexer arrayIndexer)
      : base(arrayIndexer) {
      this.indexedObject = arrayIndexer.IndexedObject;
      this.indices = new List<IExpression>(arrayIndexer.Indices);
    }

    /// <summary>
    /// An expression that results in value of an array type.
    /// </summary>
    /// <value></value>
    public IExpression IndexedObject {
      get { return this.indexedObject; }
      set { this.indexedObject = value; }
    }
    IExpression indexedObject;

    /// <summary>
    /// The array indices.
    /// </summary>
    /// <value></value>
    public List<IExpression> Indices {
      get { return this.indices; }
      set { this.indices = value; }
    }
    List<IExpression> indices;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="visitor"></param>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }

    #region IArrayIndexer Members

    IEnumerable<IExpression> IArrayIndexer.Indices {
      get { return this.indices.AsReadOnly(); }
    }

    #endregion
  }

  /// <summary>
  /// 
  /// </summary>
  public sealed class Assignment : Expression, IAssignment {

    /// <summary>
    /// Initializes a new instance of the <see cref="Assignment"/> class.
    /// </summary>
    public Assignment() {
      this.source = CodeDummy.Expression;
      this.target = CodeDummy.TargetExpression;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="assignment"></param>
    public Assignment(IAssignment assignment)
      : base(assignment) {
      this.source = assignment.Source;
      this.target = assignment.Target;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="visitor"></param>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }

    /// <summary>
    /// The expression representing the value to assign.
    /// </summary>
    /// <value></value>
    public IExpression Source {
      get { return this.source; }
      set { this.source = value; }
    }
    IExpression source;

    /// <summary>
    /// The expression representing the target to assign to.
    /// </summary>
    /// <value></value>
    public ITargetExpression Target {
      get { return this.target; }
      set { this.target = value; }
    }
    ITargetExpression target;

  }

  /// <summary>
  /// 
  /// </summary>
  public sealed class BaseClassReference : Expression, IBaseClassReference {

    /// <summary>
    /// 
    /// </summary>
    public BaseClassReference() {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="baseClassReference"></param>
    public BaseClassReference(IBaseClassReference baseClassReference)
      : base(baseClassReference) {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="visitor"></param>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }

  }

  /// <summary>
  /// A binary operation performed on a left and right operand.
  /// </summary>
  public abstract class BinaryOperation : Expression, IBinaryOperation {

    /// <summary>
    /// 
    /// </summary>
    internal BinaryOperation() {
      this.leftOperand = CodeDummy.Expression;
      this.rightOperand = CodeDummy.Expression;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="binaryOperation"></param>
    internal BinaryOperation(IBinaryOperation binaryOperation)
      : base(binaryOperation) {
      this.leftOperand = binaryOperation.LeftOperand;
      this.rightOperand = binaryOperation.RightOperand;
    }

    /// <summary>
    /// The left operand.
    /// </summary>
    public IExpression LeftOperand {
      get { return this.leftOperand; }
      set { this.leftOperand = value; }
    }
    IExpression leftOperand;

    /// <summary>
    /// The right operand.
    /// </summary>
    public IExpression RightOperand {
      get { return this.rightOperand; }
      set { this.rightOperand = value; }
    }
    IExpression rightOperand;

  }

  /// <summary>
  /// 
  /// </summary>
  public sealed class BitwiseAnd : BinaryOperation, IBitwiseAnd {

    /// <summary>
    /// 
    /// </summary>
    public BitwiseAnd() {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="bitwiseAnd"></param>
    public BitwiseAnd(IBitwiseAnd bitwiseAnd)
      : base(bitwiseAnd) {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="visitor"></param>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }

  }

  /// <summary>
  /// 
  /// </summary>
  public sealed class BitwiseOr : BinaryOperation, IBitwiseOr {

    /// <summary>
    /// 
    /// </summary>
    public BitwiseOr() {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="bitwiseOr"></param>
    public BitwiseOr(IBitwiseOr bitwiseOr)
      : base(bitwiseOr) {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="visitor"></param>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }

  }

  /// <summary>
  /// 
  /// </summary>
  public sealed class BlockExpression : Expression, IBlockExpression {

    /// <summary>
    /// 
    /// </summary>
    public BlockExpression() {
      this.blockStatement = CodeDummy.Block;
      this.expression = CodeDummy.Expression;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="blockExpression"></param>
    public BlockExpression(IBlockExpression blockExpression)
      : base(blockExpression) {
      this.blockStatement = blockExpression.BlockStatement;
      this.expression = blockExpression.Expression;
    }

    /// <summary>
    /// A block of statements that typically introduce local variables to hold sub expressions.
    /// The scope of these declarations coincides with the block expression.
    /// The statements are executed before evaluation of Expression occurs.
    /// </summary>
    /// <value></value>
    public IBlockStatement BlockStatement {
      get { return this.blockStatement; }
      set { this.blockStatement = value; }
    }
    IBlockStatement blockStatement;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="visitor"></param>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }

    /// <summary>
    /// The expression that computes the result of the entire block expression.
    /// This expression can contain references to the local variables that are declared inside BlockStatement.
    /// </summary>
    /// <value></value>
    public IExpression Expression {
      get { return this.expression; }
      set { this.expression = value; }
    }
    IExpression expression;

  }

  /// <summary>
  /// 
  /// </summary>
  public sealed class BoundExpression : Expression, IBoundExpression {

    /// <summary>
    /// 
    /// </summary>
    public BoundExpression() {
      this.alignment = 0;
      this.definition = CodeDummy.Expression;
      this.instance = null;
      this.isVolatile = false;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="boundExpression"></param>
    public BoundExpression(IBoundExpression boundExpression)
      : base(boundExpression) {
      if (boundExpression.IsUnaligned)
        this.alignment = boundExpression.Alignment;
      else
        this.alignment = 0;
      this.definition = boundExpression.Definition;
      this.instance = boundExpression.Instance;
      this.isVolatile = boundExpression.IsVolatile;
    }

    /// <summary>
    /// If Definition is a field and the field is not aligned with natural size of its type, this property specifies the actual alignment.
    /// For example, if the field is byte aligned, then the result of this property is 1. Likewise, 2 for word (16-bit) alignment and 4 for
    /// double word (32-bit alignment).
    /// </summary>
    /// <value></value>
    public byte Alignment {
      get
        //^^ requires !this.IsUnaligned;
        //^^ ensures result == 1 || result == 2 || result == 4;
      {
        //^ assume this.alignment == 1 || this.alignment == 2 || this.alignment == 4;
        return this.alignment;
      }
      set
        //^ requires value == 1 || value == 2 || value == 4;
      {
        this.alignment = value;
      }
    }
    //^ [SpecPublic]
    byte alignment;
    //^ invariant alignment == 0 || alignment == 1 || alignment == 2 || alignment == 4;

    /// <summary>
    /// The local variable, parameter or field that this expression binds to.
    /// </summary>
    /// <value></value>
    public object Definition {
      get { return this.definition; }
      set { this.definition = value; }
    }
    object definition;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="visitor"></param>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }

    /// <summary>
    /// If the expression binds to an instance field then this property is not null and contains the instance.
    /// </summary>
    /// <value></value>
    public IExpression/*?*/ Instance {
      get { return this.instance; }
      set { this.instance = value; }
    }
    IExpression/*?*/ instance;

    /// <summary>
    /// True if the definition is a field and the field is not aligned with the natural size of its type.
    /// For example if the field type is Int32 and the field is aligned on an Int16 boundary.
    /// </summary>
    /// <value></value>
    public bool IsUnaligned {
      get
        //^^ ensures result == (this.alignment != 1 && this.alignment != 2 && this.alignment != 4; )
      {
        return this.alignment != 1 && this.alignment != 2 && this.alignment != 4;
      }
    }

    /// <summary>
    /// The bound Definition is a volatile field and its contents may not be cached.
    /// </summary>
    /// <value></value>
    public bool IsVolatile {
      get { return this.isVolatile; }
      set { this.isVolatile = value; }
    }
    bool isVolatile;
  }

  /// <summary>
  /// 
  /// </summary>
  public sealed class CastIfPossible : Expression, ICastIfPossible {

    /// <summary>
    /// 
    /// </summary>
    public CastIfPossible() {
      this.targetType = Dummy.TypeReference;
      this.valueToCast = CodeDummy.Expression;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="castIfPossible"></param>
    public CastIfPossible(ICastIfPossible castIfPossible)
      : base(castIfPossible) {
      this.targetType = castIfPossible.TargetType;
      this.valueToCast = castIfPossible.ValueToCast;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="visitor"></param>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }

    /// <summary>
    /// The type to which the value must be cast. If the value is not of this type, the expression results in a null value of this type.
    /// </summary>
    /// <value></value>
    public ITypeReference TargetType {
      get { return this.targetType; }
      set { this.targetType = value; }
    }
    ITypeReference targetType;

    /// <summary>
    /// The value to cast if possible.
    /// </summary>
    /// <value></value>
    public IExpression ValueToCast {
      get { return this.valueToCast; }
      set { this.valueToCast = value; }
    }
    IExpression valueToCast;

  }

  /// <summary>
  /// 
  /// </summary>
  public sealed class CheckIfInstance : Expression, ICheckIfInstance {

    /// <summary>
    /// 
    /// </summary>
    public CheckIfInstance() {
      this.operand = CodeDummy.Expression;
      this.type = Dummy.TypeReference;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="checkIfInstance"></param>
    public CheckIfInstance(ICheckIfInstance checkIfInstance)
      : base(checkIfInstance) {
      this.operand = checkIfInstance.Operand;
      this.type = checkIfInstance.TypeToCheck;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="visitor"></param>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }

    /// <summary>
    /// The value to check.
    /// </summary>
    /// <value></value>
    public IExpression Operand {
      get { return this.operand; }
      set { this.operand = value; }
    }
    IExpression operand;

    /// <summary>
    /// The type to which the value must belong for this expression to evaluate as true.
    /// </summary>
    /// <value></value>
    public ITypeReference TypeToCheck {
      get { return this.type; }
      set { this.type = value; }
    }
    ITypeReference type;

  }

  /// <summary>
  /// 
  /// </summary>
  public sealed class Conversion : Expression, IConversion {

    /// <summary>
    /// 
    /// </summary>
    public Conversion() {
      this.checkNumericRange = false;
      this.typeAfterConversion = Dummy.TypeReference;
      this.valueToConvert = CodeDummy.Expression;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="conversion"></param>
    public Conversion(IConversion conversion)
      : base(conversion) {
      this.checkNumericRange = conversion.CheckNumericRange;
      this.typeAfterConversion = conversion.TypeAfterConversion;
      this.valueToConvert = conversion.ValueToConvert;
    }

    /// <summary>
    /// If true and ValueToConvert is a number and ResultType is a numeric type, check that ValueToConvert falls within the range of ResultType and throw an exception if not.
    /// </summary>
    /// <value></value>
    public bool CheckNumericRange {
      get { return this.checkNumericRange; }
      set { this.checkNumericRange = value; }
    }
    bool checkNumericRange;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="visitor"></param>
    //^ [MustOverride]
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }

    /// <summary>
    /// The type to which the value is to be converted.
    /// </summary>
    /// <value></value>
    public ITypeReference TypeAfterConversion {
      get { return this.typeAfterConversion; }
      set { this.typeAfterConversion = value; }
    }
    ITypeReference typeAfterConversion;

    /// <summary>
    /// The value to convert.
    /// </summary>
    /// <value></value>
    public IExpression ValueToConvert {
      get { return this.valueToConvert; }
      set { this.valueToConvert = value; }
    }
    IExpression valueToConvert;

  }

  /// <summary>
  /// An expression that does not change its value at runtime and that can be evaluated at compile time.
  /// </summary>
  public sealed class CompileTimeConstant : Expression, ICompileTimeConstant, IMetadataConstant {

    /// <summary>
    /// 
    /// </summary>
    public CompileTimeConstant() {
      this.value = null;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="compileTimeConstant"></param>
    public CompileTimeConstant(ICompileTimeConstant compileTimeConstant)
      : base(compileTimeConstant) {
      this.value = compileTimeConstant.Value;
      this.Type = compileTimeConstant.Type;
    }

    /// <summary>
    /// Calls the visitor.Visit(T) method where T is the most derived object model node interface type implemented by the concrete type
    /// of the object implementing IExpression. The dispatch method does not invoke Dispatch on any child objects. If child traversal
    /// is desired, the implementations of the Visit methods should do the subsequent dispatching.
    /// </summary>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }

    /// <summary>
    /// Calls the visitor.Visit(ICompileTimeConstant) method.
    /// </summary>
    public void Dispatch(IMetadataVisitor visitor) {
      visitor.Visit(this);
    }

    /// <summary>
    /// The compile time value of the expression. Can be null.
    /// </summary>
    public object/*?*/ Value {
      get { return this.value; }
      set { this.value = value; }
    }
    object/*?*/ value;

    #region IMetadataExpression Members

    ITypeReference IMetadataExpression.Type {
      get { return this.Type; }
    }

    #endregion
  }

  /// <summary>
  /// 
  /// </summary>
  public sealed class Conditional : Expression, IConditional {

    /// <summary>
    /// 
    /// </summary>
    public Conditional() {
      this.condition = CodeDummy.Expression;
      this.resultIfFalse = CodeDummy.Expression;
      this.resultIfTrue = CodeDummy.Expression;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="conditional"></param>
    public Conditional(IConditional conditional)
      : base(conditional) {
      this.condition = conditional.Condition;
      this.resultIfFalse = conditional.ResultIfFalse;
      this.resultIfTrue = conditional.ResultIfTrue;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="visitor"></param>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }

    /// <summary>
    /// The condition that determines which subexpression to evaluate.
    /// </summary>
    /// <value></value>
    public IExpression Condition {
      get { return this.condition; }
      set { this.condition = value; }
    }
    IExpression condition;

    /// <summary>
    /// The expression to evaluate as the value of the overall expression if the condition is false.
    /// </summary>
    /// <value></value>
    public IExpression ResultIfFalse {
      get { return this.resultIfFalse; }
      set { this.resultIfFalse = value; }
    }
    IExpression resultIfFalse;

    /// <summary>
    /// The expression to evaluate as the value of the overall expression if the condition is true.
    /// </summary>
    /// <value></value>
    public IExpression ResultIfTrue {
      get { return this.resultIfTrue; }
      set { this.resultIfTrue = value; }
    }
    IExpression resultIfTrue;

  }

  /// <summary>
  /// 
  /// </summary>
  public abstract class ConstructorOrMethodCall : Expression {

    /// <summary>
    /// 
    /// </summary>
    internal ConstructorOrMethodCall() {
      this.arguments = new List<IExpression>();
      this.methodToCall = Dummy.MethodReference;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="createObjectInstance"></param>
    internal ConstructorOrMethodCall(ICreateObjectInstance createObjectInstance)
      : base(createObjectInstance) {
      this.arguments = new List<IExpression>(createObjectInstance.Arguments);
      this.methodToCall = createObjectInstance.MethodToCall;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="methodCall"></param>
    internal ConstructorOrMethodCall(IMethodCall methodCall)
      : base(methodCall) {
      this.arguments = new List<IExpression>(methodCall.Arguments);
      this.methodToCall = methodCall.MethodToCall;
    }

    /// <summary>
    /// Gets or sets the arguments.
    /// </summary>
    /// <value>The arguments.</value>
    public List<IExpression> Arguments {
      get { return this.arguments; }
      set { this.arguments = value; }
    }
    internal List<IExpression> arguments;

    /// <summary>
    /// Gets or sets the method to call.
    /// </summary>
    /// <value>The method to call.</value>
    public IMethodReference MethodToCall {
      get { return this.methodToCall; }
      set { this.methodToCall = value; }
    }
    IMethodReference methodToCall;

  }

  /// <summary>
  /// 
  /// </summary>
  public sealed class CreateArray : Expression, ICreateArray, IMetadataCreateArray {

    /// <summary>
    /// 
    /// </summary>
    public CreateArray() {
      this.elementType = Dummy.TypeReference;
      this.initializers = new List<IExpression>();
      this.lowerBounds = new List<int>();
      this.rank = 0;
      this.sizes = new List<IExpression>();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="createArray"></param>
    public CreateArray(ICreateArray createArray)
      : base(createArray) {
      this.elementType = createArray.ElementType;
      this.initializers = new List<IExpression>(createArray.Initializers);
      this.lowerBounds = new List<int>(createArray.LowerBounds);
      this.rank = createArray.Rank;
      this.sizes = new List<IExpression>(createArray.Sizes);
    }

    /// <summary>
    /// Calls the visitor.Visit(T) method where T is the most derived object model node interface type implemented by the concrete type
    /// of the object implementing IDoubleDispatcher. The dispatch method does not invoke Dispatch on any child objects. If child traversal
    /// is desired, the implementations of the Visit methods should do the subsequent dispatching.
    /// </summary>
    /// <param name="visitor"></param>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }

    /// <summary>
    /// Calls the visitor.Visit(ICreateArray) method.
    /// </summary>
    public void Dispatch(IMetadataVisitor visitor) {
      visitor.Visit(this);
    }

    /// <summary>
    /// The element type of the array.
    /// </summary>
    /// <value></value>
    public ITypeReference ElementType {
      get { return this.elementType; }
      set { this.elementType = value; }
    }
    ITypeReference elementType;

    /// <summary>
    /// The initial values of the array elements. May be empty.
    /// </summary>
    /// <value></value>
    public List<IExpression> Initializers {
      get { return this.initializers; }
      set { this.initializers = value; }
    }
    List<IExpression> initializers;

    /// <summary>
    /// The index value of the first element in each dimension.
    /// </summary>
    /// <value></value>
    public List<int> LowerBounds {
      get { return this.lowerBounds; }
      set { this.lowerBounds = value; }
    }
    List<int> lowerBounds;

    /// <summary>
    /// The number of dimensions of the array.
    /// </summary>
    /// <value></value>
    public uint Rank {
      get { return this.rank; }
      set
        //^ requires value > 0;
      {
        this.rank = value;
      }
    }
    uint rank;

    /// <summary>
    /// The number of elements allowed in each dimension.
    /// </summary>
    /// <value></value>
    public List<IExpression> Sizes {
      get { return this.sizes; }
      set { this.sizes = value; }
    }
    List<IExpression> sizes;

    #region IArrayCreate Members

    IEnumerable<IExpression> ICreateArray.Initializers {
      get { return this.initializers.AsReadOnly(); }
    }

    IEnumerable<int> ICreateArray.LowerBounds {
      get { return this.lowerBounds.AsReadOnly(); }
    }

    IEnumerable<IExpression> ICreateArray.Sizes {
      get { return this.sizes.AsReadOnly(); }
    }

    #endregion

    #region IMetadataCreateArray Members

    IEnumerable<IMetadataExpression> IMetadataCreateArray.Initializers {
      get {
        foreach (IExpression expression in this.initializers) {
          IMetadataExpression/*?*/ metadataExpression = expression as IMetadataExpression;
          if (metadataExpression != null) yield return metadataExpression;
        }
      }
    }

    IEnumerable<int> IMetadataCreateArray.LowerBounds {
      get { return this.lowerBounds.AsReadOnly(); }
    }

    IEnumerable<ulong> IMetadataCreateArray.Sizes {
      get {
        foreach (IExpression size in this.Sizes) {
          ulong s = 0;
          IMetadataConstant/*?*/ cconst = size as IMetadataConstant;
          if (cconst != null && cconst.Value is ulong)
            s = (ulong)cconst.Value;
          yield return s;
        }
      }
    }
    #endregion

    #region IMetadataExpression Members

    ITypeReference IMetadataExpression.Type {
      get { return this.Type; }
    }

    #endregion

  }

  /// <summary>
  /// 
  /// </summary>
  public sealed class CreateDelegateInstance : Expression, ICreateDelegateInstance {

    /// <summary>
    /// 
    /// </summary>
    public CreateDelegateInstance() {
      this.instance = null;
      this.methodToCallViaDelegate = Dummy.MethodReference;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="createDelegateInstance"></param>
    public CreateDelegateInstance(ICreateDelegateInstance createDelegateInstance)
      : base(createDelegateInstance) {
      this.instance = createDelegateInstance.Instance;
      this.methodToCallViaDelegate = createDelegateInstance.MethodToCallViaDelegate;
    }

    /// <summary>
    /// Calls the visitor.Visit(T) method where T is the most derived object model node interface type implemented by the concrete type
    /// of the object implementing IDoubleDispatcher. The dispatch method does not invoke Dispatch on any child objects. If child traversal
    /// is desired, the implementations of the Visit methods should do the subsequent dispatching.
    /// </summary>
    /// <param name="visitor"></param>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }

    /// <summary>
    /// An expression that evaluates to the instance (if any) on which this.MethodToCallViaDelegate must be called (via the delegate).
    /// </summary>
    /// <value></value>
    public IExpression/*?*/ Instance {
      get { return this.instance; }
      set
        //^ requires this.MethodToCallViaDelegate.ResolvedMethod.IsStatic <==> value == null;
      {
        this.instance = value;
      }
    }
    IExpression/*?*/ instance;

    /// <summary>
    /// The method that is to be be called when the delegate instance is invoked.
    /// </summary>
    /// <value></value>
    public IMethodReference MethodToCallViaDelegate {
      get { return this.methodToCallViaDelegate; }
      set { this.methodToCallViaDelegate = value; }
    }
    IMethodReference methodToCallViaDelegate;

  }

  /// <summary>
  /// 
  /// </summary>
  public sealed class CreateObjectInstance : ConstructorOrMethodCall, ICreateObjectInstance {

    /// <summary>
    /// 
    /// </summary>
    public CreateObjectInstance() {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="createObjectInstance"></param>
    public CreateObjectInstance(ICreateObjectInstance createObjectInstance)
      : base(createObjectInstance) {
    }

    /// <summary>
    /// Calls the visitor.Visit(T) method where T is the most derived object model node interface type implemented by the concrete type
    /// of the object implementing IDoubleDispatcher. The dispatch method does not invoke Dispatch on any child objects. If child traversal
    /// is desired, the implementations of the Visit methods should do the subsequent dispatching.
    /// </summary>
    /// <param name="visitor"></param>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }

    #region ICreateObjectInstance Members

    IEnumerable<IExpression> ICreateObjectInstance.Arguments {
      get { return this.arguments.AsReadOnly(); }
    }

    #endregion
  }

  /// <summary>
  /// 
  /// </summary>
  public sealed class DefaultValue : Expression, IDefaultValue {

    /// <summary>
    /// 
    /// </summary>
    public DefaultValue() {
      this.defaultValueType = Dummy.TypeReference;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="defaultValue"></param>
    public DefaultValue(IDefaultValue defaultValue)
      : base(defaultValue) {
      this.defaultValueType = defaultValue.DefaultValueType;
    }

    /// <summary>
    /// The type whose default value is the result of this expression.
    /// </summary>
    /// <value></value>
    public ITypeReference DefaultValueType {
      get { return this.defaultValueType; }
      set { this.defaultValueType = value; }
    }
    ITypeReference defaultValueType;

    /// <summary>
    /// Calls the visitor.Visit(T) method where T is the most derived object model node interface type implemented by the concrete type
    /// of the object implementing IDoubleDispatcher. The dispatch method does not invoke Dispatch on any child objects. If child traversal
    /// is desired, the implementations of the Visit methods should do the subsequent dispatching.
    /// </summary>
    /// <param name="visitor"></param>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }

  }

  /// <summary>
  /// 
  /// </summary>
  public sealed class Division : BinaryOperation, IDivision {

    /// <summary>
    /// 
    /// </summary>
    public Division() {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="division"></param>
    public Division(IDivision division)
      : base(division) {
      this.CheckOverflow = division.CheckOverflow;
    }

    /// <summary>
    /// The division must be performed with a check for arithmetic overflow if the operands are integers.
    /// </summary>
    /// <value></value>
    public bool CheckOverflow { get; set; }

    /// <summary>
    /// Calls the visitor.Visit(T) method where T is the most derived object model node interface type implemented by the concrete type
    /// of the object implementing IDoubleDispatcher. The dispatch method does not invoke Dispatch on any child objects. If child traversal
    /// is desired, the implementations of the Visit methods should do the subsequent dispatching.
    /// </summary>
    /// <param name="visitor"></param>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }
  }

  /// <summary>
  /// 
  /// </summary>
  public sealed class Equality : BinaryOperation, IEquality {

    /// <summary>
    /// 
    /// </summary>
    public Equality() {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="equality"></param>
    public Equality(IEquality equality)
      : base(equality) {
    }

    /// <summary>
    /// Calls the visitor.Visit(T) method where T is the most derived object model node interface type implemented by the concrete type
    /// of the object implementing IDoubleDispatcher. The dispatch method does not invoke Dispatch on any child objects. If child traversal
    /// is desired, the implementations of the Visit methods should do the subsequent dispatching.
    /// </summary>
    /// <param name="visitor"></param>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }

  }

  /// <summary>
  /// 
  /// </summary>
  public sealed class ExclusiveOr : BinaryOperation, IExclusiveOr {

    /// <summary>
    /// 
    /// </summary>
    public ExclusiveOr() {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="exclusiveOr"></param>
    public ExclusiveOr(IExclusiveOr exclusiveOr)
      : base(exclusiveOr) {
    }

    /// <summary>
    /// Calls the visitor.Visit(T) method where T is the most derived object model node interface type implemented by the concrete type
    /// of the object implementing IDoubleDispatcher. The dispatch method does not invoke Dispatch on any child objects. If child traversal
    /// is desired, the implementations of the Visit methods should do the subsequent dispatching.
    /// </summary>
    /// <param name="visitor"></param>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }

  }

  /// <summary>
  /// An expression results in a value of some type.
  /// </summary>
  public abstract class Expression : IExpression {

    /// <summary>
    /// 
    /// </summary>
    protected Expression() {
      this.locations = new List<ILocation>(1);
      this.type = Dummy.TypeReference;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="expression"></param>
    protected Expression(IExpression expression) {
      this.locations = new List<ILocation>(expression.Locations);
      this.type = expression.Type;
    }

    /// <summary>
    /// Calls the visitor.Visit(T) method where T is the most derived object model node interface type implemented by the concrete type
    /// of the object implementing IDoubleDispatcher. The dispatch method does not invoke Dispatch on any child objects. If child traversal
    /// is desired, the implementations of the Visit methods should do the subsequent dispatching.
    /// </summary>
    public abstract void Dispatch(ICodeVisitor visitor);

    /// <summary>
    /// Checks the expression for errors and returns true if any were found.
    /// </summary>
    public bool HasErrors() {
      return false;
    }

    /// <summary>
    /// True if the expression has no observable side effects.
    /// </summary>
    /// <value></value>
    public bool IsPure {
      get { return false; }
    }

    /// <summary>
    /// A potentially empty collection of locations that correspond to this instance.
    /// </summary>
    /// <value></value>
    public List<ILocation> Locations {
      get { return this.locations; }
      set { this.locations = value; }
    }
    List<ILocation> locations;

    /// <summary>
    /// The type of value the expression will evaluate to, as determined at compile time.
    /// </summary>
    /// <value></value>
    public ITypeReference Type {
      get { return this.type; }
      set { this.type = value; }
    }
    ITypeReference type;

    #region IExpression Members

    IEnumerable<ILocation> IObjectWithLocations.Locations {
      get { return this.locations.AsReadOnly(); }
    }

    #endregion
  }

  /// <summary>
  /// 
  /// </summary>
  public sealed class GetTypeOfTypedReference : Expression, IGetTypeOfTypedReference {

    /// <summary>
    /// 
    /// </summary>
    public GetTypeOfTypedReference() {
      this.typedReference = CodeDummy.Expression;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="getTypeOfTypedReference"></param>
    public GetTypeOfTypedReference(IGetTypeOfTypedReference getTypeOfTypedReference)
      : base(getTypeOfTypedReference) {
      this.typedReference = getTypeOfTypedReference.TypedReference;
    }

    /// <summary>
    /// Calls the visitor.Visit(T) method where T is the most derived object model node interface type implemented by the concrete type
    /// of the object implementing IDoubleDispatcher. The dispatch method does not invoke Dispatch on any child objects. If child traversal
    /// is desired, the implementations of the Visit methods should do the subsequent dispatching.
    /// </summary>
    /// <param name="visitor"></param>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }

    /// <summary>
    /// An expression that results in a value of type System.TypedReference.
    /// </summary>
    /// <value></value>
    public IExpression TypedReference {
      get { return this.typedReference; }
      set { this.typedReference = value; }
    }
    IExpression typedReference;

  }

  /// <summary>
  /// 
  /// </summary>
  public sealed class GetValueOfTypedReference : Expression, IGetValueOfTypedReference {

    /// <summary>
    /// 
    /// </summary>
    public GetValueOfTypedReference() {
      this.targetType = Dummy.TypeReference;
      this.typedReference = CodeDummy.Expression;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="getValueOfTypedReference"></param>
    public GetValueOfTypedReference(IGetValueOfTypedReference getValueOfTypedReference)
      : base(getValueOfTypedReference) {
      this.targetType = getValueOfTypedReference.TargetType;
      this.typedReference = getValueOfTypedReference.TypedReference;
    }

    /// <summary>
    /// Calls the visitor.Visit(T) method where T is the most derived object model node interface type implemented by the concrete type
    /// of the object implementing IDoubleDispatcher. The dispatch method does not invoke Dispatch on any child objects. If child traversal
    /// is desired, the implementations of the Visit methods should do the subsequent dispatching.
    /// </summary>
    /// <param name="visitor"></param>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }

    /// <summary>
    /// The type to which the value part of the typed reference must be converted.
    /// </summary>
    /// <value></value>
    public ITypeReference TargetType {
      get { return this.targetType; }
      set { this.targetType = value; }
    }
    ITypeReference targetType;

    /// <summary>
    /// An expression that results in a value of type System.TypedReference.
    /// </summary>
    /// <value></value>
    public IExpression TypedReference {
      get { return this.typedReference; }
      set { this.typedReference = value; }
    }
    IExpression typedReference;

  }

  /// <summary>
  /// 
  /// </summary>
  public sealed class GreaterThan : BinaryOperation, IGreaterThan {

    /// <summary>
    /// 
    /// </summary>
    public GreaterThan() {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="greaterThan"></param>
    public GreaterThan(IGreaterThan greaterThan)
      : base(greaterThan) {
    }

    /// <summary>
    /// Calls the visitor.Visit(T) method where T is the most derived object model node interface type implemented by the concrete type
    /// of the object implementing IDoubleDispatcher. The dispatch method does not invoke Dispatch on any child objects. If child traversal
    /// is desired, the implementations of the Visit methods should do the subsequent dispatching.
    /// </summary>
    /// <param name="visitor"></param>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }

  }

  /// <summary>
  /// 
  /// </summary>
  public sealed class GreaterThanOrEqual : BinaryOperation, IGreaterThanOrEqual {

    /// <summary>
    /// 
    /// </summary>
    public GreaterThanOrEqual() {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="greaterThanOrEqual"></param>
    public GreaterThanOrEqual(IGreaterThanOrEqual greaterThanOrEqual)
      : base(greaterThanOrEqual) {
    }

    /// <summary>
    /// Calls the visitor.Visit(T) method where T is the most derived object model node interface type implemented by the concrete type
    /// of the object implementing IDoubleDispatcher. The dispatch method does not invoke Dispatch on any child objects. If child traversal
    /// is desired, the implementations of the Visit methods should do the subsequent dispatching.
    /// </summary>
    /// <param name="visitor"></param>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }

  }

  /// <summary>
  /// 
  /// </summary>
  public sealed class LeftShift : BinaryOperation, ILeftShift {

    /// <summary>
    /// 
    /// </summary>
    public LeftShift() {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="leftShift"></param>
    public LeftShift(ILeftShift leftShift)
      : base(leftShift) {
    }

    /// <summary>
    /// Calls the visitor.Visit(T) method where T is the most derived object model node interface type implemented by the concrete type
    /// of the object implementing IDoubleDispatcher. The dispatch method does not invoke Dispatch on any child objects. If child traversal
    /// is desired, the implementations of the Visit methods should do the subsequent dispatching.
    /// </summary>
    /// <param name="visitor"></param>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }

  }

  /// <summary>
  /// 
  /// </summary>
  public sealed class LessThan : BinaryOperation, ILessThan {

    /// <summary>
    /// 
    /// </summary>
    public LessThan() {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="lessThan"></param>
    public LessThan(ILessThan lessThan)
      : base(lessThan) {
    }

    /// <summary>
    /// Calls the visitor.Visit(T) method where T is the most derived object model node interface type implemented by the concrete type
    /// of the object implementing IDoubleDispatcher. The dispatch method does not invoke Dispatch on any child objects. If child traversal
    /// is desired, the implementations of the Visit methods should do the subsequent dispatching.
    /// </summary>
    /// <param name="visitor"></param>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }

  }

  /// <summary>
  /// 
  /// </summary>
  public sealed class LessThanOrEqual : BinaryOperation, ILessThanOrEqual {

    /// <summary>
    /// 
    /// </summary>
    public LessThanOrEqual() {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="lessThanOrEqual"></param>
    public LessThanOrEqual(ILessThanOrEqual lessThanOrEqual)
      : base(lessThanOrEqual) {
    }

    /// <summary>
    /// Calls the visitor.Visit(T) method where T is the most derived object model node interface type implemented by the concrete type
    /// of the object implementing IDoubleDispatcher. The dispatch method does not invoke Dispatch on any child objects. If child traversal
    /// is desired, the implementations of the Visit methods should do the subsequent dispatching.
    /// </summary>
    /// <param name="visitor"></param>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }

  }

  /// <summary>
  /// 
  /// </summary>
  public sealed class LogicalNot : UnaryOperation, ILogicalNot {

    /// <summary>
    /// 
    /// </summary>
    public LogicalNot() {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="logicalNot"></param>
    public LogicalNot(ILogicalNot logicalNot)
      : base(logicalNot) {
    }

    /// <summary>
    /// Calls the visitor.Visit(T) method where T is the most derived object model node interface type implemented by the concrete type
    /// of the object implementing IDoubleDispatcher. The dispatch method does not invoke Dispatch on any child objects. If child traversal
    /// is desired, the implementations of the Visit methods should do the subsequent dispatching.
    /// </summary>
    /// <param name="visitor"></param>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }

  }

  /// <summary>
  /// 
  /// </summary>
  public sealed class MakeTypedReference : Expression, IMakeTypedReference {

    /// <summary>
    /// 
    /// </summary>
    public MakeTypedReference() {
      this.operand = CodeDummy.Expression;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="makeTypedReference"></param>
    public MakeTypedReference(IMakeTypedReference makeTypedReference)
      : base(makeTypedReference) {
      this.operand = makeTypedReference.Operand;
    }

    /// <summary>
    /// Calls the visitor.Visit(T) method where T is the most derived object model node interface type implemented by the concrete type
    /// of the object implementing IDoubleDispatcher. The dispatch method does not invoke Dispatch on any child objects. If child traversal
    /// is desired, the implementations of the Visit methods should do the subsequent dispatching.
    /// </summary>
    /// <param name="visitor"></param>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }

    /// <summary>
    /// The value to box in a typed reference.
    /// </summary>
    /// <value></value>
    public IExpression Operand {
      get { return this.operand; }
      set { this.operand = value; }
    }
    IExpression operand;


  }

  /// <summary>
  /// An expression that invokes a method.
  /// </summary>
  public sealed class MethodCall : ConstructorOrMethodCall, IMethodCall {

    /// <summary>
    /// 
    /// </summary>
    public MethodCall() {
      this.isVirtualCall = false;
      this.isStaticCall = false;
      this.thisArgument = CodeDummy.Expression;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="methodCall"></param>
    public MethodCall(IMethodCall methodCall)
      : base(methodCall) {
      this.isVirtualCall = methodCall.IsVirtualCall;
      this.isStaticCall = methodCall.IsStaticCall;
      if (!methodCall.IsStaticCall) {
        this.thisArgument = methodCall.ThisArgument;
      } else
        this.thisArgument = CodeDummy.Expression;
      //^ assume false; //invariant involves properties
    }

    /// <summary>
    /// Calls the visitor.Visit(T) method where T is the most derived object model node interface type implemented by the concrete type
    /// of the object implementing IDoubleDispatcher. The dispatch method does not invoke Dispatch on any child objects. If child traversal
    /// is desired, the implementations of the Visit methods should do the subsequent dispatching.
    /// </summary>
    /// <param name="visitor"></param>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }

    /// <summary>
    /// True if the method to call is determined at run time, based on the runtime type of ThisArgument.
    /// </summary>
    /// <value></value>
    public bool IsVirtualCall {
      get
        //^ ensures result ==> this.MethodToCall.ResolvedMethod.IsVirtual;
      {
        //^ assume false;
        return this.isVirtualCall;
      }
      set
        //^ requires value ==> this.MethodToCall.ResolvedMethod.IsVirtual;
      {
        this.isVirtualCall = value;
      }
    }
    bool isVirtualCall;
    //^ invariant isVirtualCall ==> this.MethodToCall.ResolvedMethod.IsVirtual;

    /// <summary>
    /// True if the method to call is static (has no this parameter).
    /// </summary>
    /// <value></value>
    public bool IsStaticCall {
      get
        //^ ensures result ==> this.MethodToCall.ResolvedMethod.IsStatic;
      {
        //^ assume false;
        return this.isStaticCall;
      }
      set
        //^ requires value ==> this.MethodToCall.ResolvedMethod.IsStatic;
      {
        this.isStaticCall = value;
      }
    }
    bool isStaticCall;
    //^ invariant isStaticCall ==> this.MethodToCall.ResolvedMethod.IsStatic;

    /// <summary>
    /// True if this method call terminates the calling method. It indicates that the calling method's stack frame is not required
    /// and can be removed before executing the call.
    /// </summary>
    /// <value></value>
    public bool IsTailCall {
      get { return this.isTailCall; }
      set { this.isTailCall = value; }
    }
    bool isTailCall;

    /// <summary>
    /// The expression that results in the value that must be passed as the value of the this argument of the resolved method.
    /// </summary>
    /// <value></value>
    public IExpression ThisArgument {
      get { return this.thisArgument; }
      set { this.thisArgument = value; }
    }
    IExpression thisArgument;

    #region IMethodCall Members

    IEnumerable<IExpression> IMethodCall.Arguments {
      get { return this.arguments.AsReadOnly(); }
    }

    #endregion
  }

  /// <summary>
  /// 
  /// </summary>
  public sealed class Modulus : BinaryOperation, IModulus {

    /// <summary>
    /// 
    /// </summary>
    public Modulus() {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="modulus"></param>
    public Modulus(IModulus modulus)
      : base(modulus) {
    }

    /// <summary>
    /// Calls the visitor.Visit(T) method where T is the most derived object model node interface type implemented by the concrete type
    /// of the object implementing IDoubleDispatcher. The dispatch method does not invoke Dispatch on any child objects. If child traversal
    /// is desired, the implementations of the Visit methods should do the subsequent dispatching.
    /// </summary>
    /// <param name="visitor"></param>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }

  }

  /// <summary>
  /// 
  /// </summary>
  public sealed class Multiplication : BinaryOperation, IMultiplication {

    /// <summary>
    /// 
    /// </summary>
    public Multiplication() {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="multiplication"></param>
    public Multiplication(IMultiplication multiplication)
      : base(multiplication) {
      this.CheckOverflow = multiplication.CheckOverflow;
    }

    /// <summary>
    /// The multiplication must be performed with a check for arithmetic overflow if the operands are integers.
    /// </summary>
    /// <value></value>
    public bool CheckOverflow { get; set; }

    /// <summary>
    /// Calls the visitor.Visit(T) method where T is the most derived object model node interface type implemented by the concrete type
    /// of the object implementing IDoubleDispatcher. The dispatch method does not invoke Dispatch on any child objects. If child traversal
    /// is desired, the implementations of the Visit methods should do the subsequent dispatching.
    /// </summary>
    /// <param name="visitor"></param>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }
  }

  /// <summary>
  /// 
  /// </summary>
  public sealed class NamedArgument : Expression, INamedArgument, IMetadataNamedArgument {

    /// <summary>
    /// 
    /// </summary>
    public NamedArgument() {
      this.argumentName = Dummy.Name;
      this.argumentValue = CodeDummy.Expression;
      this.resolvedDefinition = null;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="namedArgument"></param>
    public NamedArgument(INamedArgument namedArgument)
      : base(namedArgument) {
      this.argumentName = namedArgument.ArgumentName;
      this.argumentValue = namedArgument.ArgumentValue;
      this.resolvedDefinition = namedArgument.ResolvedDefinition;
    }

    /// <summary>
    /// Calls the visitor.Visit(T) method where T is the most derived object model node interface type implemented by the concrete type
    /// of the object implementing IDoubleDispatcher. The dispatch method does not invoke Dispatch on any child objects. If child traversal
    /// is desired, the implementations of the Visit methods should do the subsequent dispatching.
    /// </summary>
    /// <param name="visitor"></param>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }

    /// <summary>
    /// The name of the parameter or property or field that corresponds to the argument.
    /// </summary>
    /// <value></value>
    public IName ArgumentName {
      get { return this.argumentName; }
      set { this.argumentName = value; }
    }
    IName argumentName;

    /// <summary>
    /// The value of the argument.
    /// </summary>
    /// <value></value>
    public IExpression ArgumentValue {
      get { return this.argumentValue; }
      set { this.argumentValue = value; }
    }
    IExpression argumentValue;

    /// <summary>
    /// Returns either null or the parameter or property or field that corresponds to this argument.
    /// </summary>
    public object/*?*/ ResolvedDefinition {
      get { return this.resolvedDefinition; }
      set
        //^ requires value == null || value is IParameterDefinition || value is IPropertyDefinition || value is IFieldDefinition;
      {
        this.resolvedDefinition = value;
      }
    }
    object/*?*/ resolvedDefinition;

    #region IMetadataNamedArgument

    IMetadataExpression IMetadataNamedArgument.ArgumentValue {
      get {
        IMetadataExpression/*?*/ result = this.ArgumentValue as IMetadataExpression;
        if (result == null) result = Dummy.Expression;
        return result;
      }
    }

    bool IMetadataNamedArgument.IsField {
      get { return this.ResolvedDefinition is IFieldDefinition; }
    }

    #endregion

    #region IMetadataExpression Members

    void IMetadataExpression.Dispatch(IMetadataVisitor visitor) {
      visitor.Visit(this);
    }

    ITypeReference IMetadataExpression.Type {
      get { return this.Type; }
    }

    #endregion
  }

  //TODO: nested method to which both anon del and lambda can project without needing to introduce explicit closure classes.

  /// <summary>
  /// 
  /// </summary>
  public sealed class NotEquality : BinaryOperation, INotEquality {

    /// <summary>
    /// 
    /// </summary>
    public NotEquality() {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="notEquality"></param>
    public NotEquality(INotEquality notEquality)
      : base(notEquality) {
    }

    /// <summary>
    /// Calls the visitor.Visit(T) method where T is the most derived object model node interface type implemented by the concrete type
    /// of the object implementing IDoubleDispatcher. The dispatch method does not invoke Dispatch on any child objects. If child traversal
    /// is desired, the implementations of the Visit methods should do the subsequent dispatching.
    /// </summary>
    /// <param name="visitor"></param>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }

  }

  /// <summary>
  /// 
  /// </summary>
  public sealed class OldValue : Expression, IOldValue {

    /// <summary>
    /// 
    /// </summary>
    public OldValue() {
      this.expression = CodeDummy.Expression;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="oldValue"></param>
    public OldValue(IOldValue oldValue)
      : base(oldValue) {
      this.expression = oldValue.Expression;
    }

    /// <summary>
    /// Calls the visitor.Visit(T) method where T is the most derived object model node interface type implemented by the concrete type
    /// of the object implementing IDoubleDispatcher. The dispatch method does not invoke Dispatch on any child objects. If child traversal
    /// is desired, the implementations of the Visit methods should do the subsequent dispatching.
    /// </summary>
    /// <param name="visitor"></param>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }

    /// <summary>
    /// The expression whose value at the start of method execution is referred to in the method postcondition.
    /// </summary>
    /// <value></value>
    public IExpression Expression {
      get { return this.expression; }
      set { this.expression = value; }
    }
    IExpression expression;

  }

  /// <summary>
  /// 
  /// </summary>
  public sealed class OutArgument : Expression, IOutArgument {

    /// <summary>
    /// 
    /// </summary>
    public OutArgument() {
      this.expression = CodeDummy.TargetExpression;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="outArgument"></param>
    public OutArgument(IOutArgument outArgument)
      : base(outArgument) {
      this.expression = outArgument.Expression;
    }

    /// <summary>
    /// Calls the visitor.Visit(T) method where T is the most derived object model node interface type implemented by the concrete type
    /// of the object implementing IDoubleDispatcher. The dispatch method does not invoke Dispatch on any child objects. If child traversal
    /// is desired, the implementations of the Visit methods should do the subsequent dispatching.
    /// </summary>
    /// <param name="visitor"></param>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }

    /// <summary>
    /// The target that is assigned to as a result of the method call.
    /// </summary>
    /// <value></value>
    public ITargetExpression Expression {
      get { return this.expression; }
      set { this.expression = value; }
    }
    ITargetExpression expression;

  }

  /// <summary>
  /// 
  /// </summary>
  public sealed class OnesComplement : UnaryOperation, IOnesComplement {

    /// <summary>
    /// 
    /// </summary>
    public OnesComplement() {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="onesComplement"></param>
    public OnesComplement(IOnesComplement onesComplement)
      : base(onesComplement) {
    }

    /// <summary>
    /// Calls the visitor.Visit(T) method where T is the most derived object model node interface type implemented by the concrete type
    /// of the object implementing IDoubleDispatcher. The dispatch method does not invoke Dispatch on any child objects. If child traversal
    /// is desired, the implementations of the Visit methods should do the subsequent dispatching.
    /// </summary>
    /// <param name="visitor"></param>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }

  }

  /// <summary>
  /// 
  /// </summary>
  public sealed class PointerCall : Expression, IPointerCall {

    /// <summary>
    /// 
    /// </summary>
    public PointerCall() {
      this.arguments = new List<IExpression>();
      this.pointer = CodeDummy.Expression;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="pointerCall"></param>
    public PointerCall(IPointerCall pointerCall)
      : base(pointerCall) {
      this.arguments = new List<IExpression>(pointerCall.Arguments);
      this.pointer = pointerCall.Pointer;
    }

    /// <summary>
    /// Calls the visitor.Visit(T) method where T is the most derived object model node interface type implemented by the concrete type
    /// of the object implementing IDoubleDispatcher. The dispatch method does not invoke Dispatch on any child objects. If child traversal
    /// is desired, the implementations of the Visit methods should do the subsequent dispatching.
    /// </summary>
    /// <param name="visitor"></param>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }

    /// <summary>
    /// The arguments to pass to the method, after they have been converted to match the parameters of the method.
    /// </summary>
    /// <value></value>
    public List<IExpression> Arguments {
      get { return this.arguments; }
      set { this.arguments = value; }
    }
    internal List<IExpression> arguments;

    /// <summary>
    /// An expression that results at runtime in a function pointer that points to the actual method to call.
    /// </summary>
    /// <value></value>
    public IExpression Pointer {
      get { return this.pointer; }
      set { this.pointer = value; }
    }
    IExpression pointer;

    /// <summary>
    /// True if this method call terminates the calling method. It indicates that the calling method's stack frame is not required
    /// and can be removed before executing the call.
    /// </summary>
    /// <value></value>
    public bool IsTailCall {
      get { return this.isTailCall; }
      set { this.isTailCall = value; }
    }
    bool isTailCall;

    #region IPointerCall Members

    IEnumerable<IExpression> IPointerCall.Arguments {
      get { return this.arguments.AsReadOnly(); }
    }

    #endregion
  }

  /// <summary>
  /// 
  /// </summary>
  public sealed class RefArgument : Expression, IRefArgument {

    /// <summary>
    /// 
    /// </summary>
    public RefArgument()
      : base() {
      this.expression = CodeDummy.AddressableExpression;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="refArgument"></param>
    public RefArgument(IRefArgument refArgument)
      : base(refArgument) {
      this.expression = refArgument.Expression;
    }

    /// <summary>
    /// Calls the visitor.Visit(T) method where T is the most derived object model node interface type implemented by the concrete type
    /// of the object implementing IDoubleDispatcher. The dispatch method does not invoke Dispatch on any child objects. If child traversal
    /// is desired, the implementations of the Visit methods should do the subsequent dispatching.
    /// </summary>
    /// <param name="visitor"></param>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }

    /// <summary>
    /// The target that is assigned to as a result of the method call, but whose value is also passed to the method at the start of the call.
    /// </summary>
    /// <value></value>
    public IAddressableExpression Expression {
      get { return this.expression; }
      set { this.expression = value; }
    }
    IAddressableExpression expression;

  }

  /// <summary>
  /// 
  /// </summary>
  public sealed class ReturnValue : Expression, IReturnValue {

    /// <summary>
    /// 
    /// </summary>
    public ReturnValue() {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="returnValue"></param>
    public ReturnValue(IReturnValue returnValue)
      : base(returnValue) {
    }

    /// <summary>
    /// Calls the visitor.Visit(T) method where T is the most derived object model node interface type implemented by the concrete type
    /// of the object implementing IDoubleDispatcher. The dispatch method does not invoke Dispatch on any child objects. If child traversal
    /// is desired, the implementations of the Visit methods should do the subsequent dispatching.
    /// </summary>
    /// <param name="visitor"></param>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }

  }

  /// <summary>
  /// 
  /// </summary>
  public sealed class RightShift : BinaryOperation, IRightShift {

    /// <summary>
    /// 
    /// </summary>
    public RightShift() {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="rightShift"></param>
    public RightShift(IRightShift rightShift)
      : base(rightShift) {
    }

    /// <summary>
    /// Calls the visitor.Visit(T) method where T is the most derived object model node interface type implemented by the concrete type
    /// of the object implementing IDoubleDispatcher. The dispatch method does not invoke Dispatch on any child objects. If child traversal
    /// is desired, the implementations of the Visit methods should do the subsequent dispatching.
    /// </summary>
    /// <param name="visitor"></param>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }

  }

  /// <summary>
  /// 
  /// </summary>
  public sealed class RuntimeArgumentHandleExpression : Expression, IRuntimeArgumentHandleExpression {

    /// <summary>
    /// 
    /// </summary>
    public RuntimeArgumentHandleExpression() {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="runtimeArgumentHandleExpression"></param>
    public RuntimeArgumentHandleExpression(IRuntimeArgumentHandleExpression runtimeArgumentHandleExpression)
      : base(runtimeArgumentHandleExpression) {
    }

    /// <summary>
    /// Calls the visitor.Visit(T) method where T is the most derived object model node interface type implemented by the concrete type
    /// of the object implementing IDoubleDispatcher. The dispatch method does not invoke Dispatch on any child objects. If child traversal
    /// is desired, the implementations of the Visit methods should do the subsequent dispatching.
    /// </summary>
    /// <param name="visitor"></param>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }

  }

  /// <summary>
  /// 
  /// </summary>
  public sealed class SizeOf : Expression, ISizeOf {

    /// <summary>
    /// 
    /// </summary>
    public SizeOf() {
      this.typeToSize = Dummy.TypeReference;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sizeOf"></param>
    public SizeOf(ISizeOf sizeOf)
      : base(sizeOf) {
      this.typeToSize = sizeOf.TypeToSize;
    }

    /// <summary>
    /// Calls the visitor.Visit(T) method where T is the most derived object model node interface type implemented by the concrete type
    /// of the object implementing IDoubleDispatcher. The dispatch method does not invoke Dispatch on any child objects. If child traversal
    /// is desired, the implementations of the Visit methods should do the subsequent dispatching.
    /// </summary>
    /// <param name="visitor"></param>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }

    /// <summary>
    /// The type to size.
    /// </summary>
    /// <value></value>
    public ITypeReference TypeToSize {
      get { return this.typeToSize; }
      set { this.typeToSize = value; }
    }
    ITypeReference typeToSize;

  }

  /// <summary>
  /// 
  /// </summary>
  public sealed class StackArrayCreate : Expression, IStackArrayCreate {

    /// <summary>
    /// 
    /// </summary>
    public StackArrayCreate() {
      this.elementType = Dummy.TypeReference;
      this.size = CodeDummy.Expression;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="stackArrayCreate"></param>
    public StackArrayCreate(IStackArrayCreate stackArrayCreate)
      : base(stackArrayCreate) {
      this.elementType = stackArrayCreate.ElementType;
      this.size = stackArrayCreate.Size;
    }

    /// <summary>
    /// Calls the visitor.Visit(T) method where T is the most derived object model node interface type implemented by the concrete type
    /// of the object implementing IDoubleDispatcher. The dispatch method does not invoke Dispatch on any child objects. If child traversal
    /// is desired, the implementations of the Visit methods should do the subsequent dispatching.
    /// </summary>
    /// <param name="visitor"></param>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }

    /// <summary>
    /// The type of the elements of the stack array. This type must be unmanaged (contain no pointers to objects on the heap managed by the garbage collector).
    /// </summary>
    /// <value></value>
    public ITypeReference ElementType {
      get { return this.elementType; }
      set { this.elementType = value; }
    }
    ITypeReference elementType;

    /// <summary>
    /// The size (number of bytes) of the stack array.
    /// </summary>
    /// <value></value>
    public IExpression Size {
      get { return this.size; }
      set { this.size = value; }
    }
    IExpression size;

  }

  /// <summary>
  /// 
  /// </summary>
  public sealed class Subtraction : BinaryOperation, ISubtraction {

    /// <summary>
    /// 
    /// </summary>
    public Subtraction() {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="subtraction"></param>
    public Subtraction(ISubtraction subtraction)
      : base(subtraction) {
      this.CheckOverflow = subtraction.CheckOverflow;
    }

    /// <summary>
    /// The subtraction must be performed with a check for arithmetic overflow if the operands are integers.
    /// </summary>
    /// <value></value>
    public bool CheckOverflow { get; set; }

    /// <summary>
    /// Calls the visitor.Visit(T) method where T is the most derived object model node interface type implemented by the concrete type
    /// of the object implementing IDoubleDispatcher. The dispatch method does not invoke Dispatch on any child objects. If child traversal
    /// is desired, the implementations of the Visit methods should do the subsequent dispatching.
    /// </summary>
    /// <param name="visitor"></param>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }
  }

  /// <summary>
  /// 
  /// </summary>
  public sealed class TargetExpression : Expression, ITargetExpression {

    /// <summary>
    /// 
    /// </summary>
    public TargetExpression() {
      this.definition = null;
      this.instance = null;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="targetExpression"></param>
    public TargetExpression(ITargetExpression targetExpression)
      : base(targetExpression) {
      this.definition = targetExpression.Definition;
      this.instance = targetExpression.Instance;
    }

    /// <summary>
    /// If Definition is a field and the field is not aligned with natural size of its type, this property specifies the actual alignment.
    /// For example, if the field is byte aligned, then the result of this property is 1. Likewise, 2 for word (16-bit) alignment and 4 for
    /// double word (32-bit alignment).
    /// </summary>
    /// <value></value>
    public byte Alignment {
      get
        //^^ requires !this.IsUnaligned;
        //^^ ensures result == 1 || result == 2 || result == 4;
      {
        //^ assume this.alignment == 1 || this.alignment == 2 || this.alignment == 4;
        return this.alignment;
      }
      set
        //^ requires value == 1 || value == 2 || value == 4;
      {
        this.alignment = value;
      }
    }
    //^ [SpecPublic]
    byte alignment;
    //^ invariant alignment == 0 || alignment == 1 || alignment == 2 || alignment == 4;

    /// <summary>
    /// Calls the visitor.Visit(T) method where T is the most derived object model node interface type implemented by the concrete type
    /// of the object implementing IDoubleDispatcher. The dispatch method does not invoke Dispatch on any child objects. If child traversal
    /// is desired, the implementations of the Visit methods should do the subsequent dispatching.
    /// </summary>
    /// <param name="visitor"></param>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }

    /// <summary>
    /// The local variable, parameter, field, property, array element or pointer target that this expression denotes.
    /// </summary>
    /// <value></value>
    public object/*?*/ Definition {
      get { return this.definition; }
      set
        //^ requires value == null || value is ILocalDefinition || value is IParameterDefinition || value is IFieldDefinition || value is IArrayIndexer || value is IAddressDereference;
      {
        this.definition = value;
      }
    }
    object/*?*/ definition;
    //^ invariant definition == null || definition is ILocalDefinition || definition is IParameterDefinition || definition is IFieldDefinition || definition is IArrayIndexer || definition is IAddressDereference;

    /// <summary>
    /// The instance to be used if this.Definition is an instance field/property or array indexer.
    /// </summary>
    /// <value></value>
    public IExpression/*?*/ Instance {
      get { return this.instance; }
      set { this.instance = value; }
    }
    IExpression/*?*/ instance;

    /// <summary>
    /// True if the definition is a field and the field is not aligned with the natural size of its type.
    /// For example if the field type is Int32 and the field is aligned on an Int16 boundary.
    /// </summary>
    public bool IsUnaligned {
      get { return this.alignment != 0; }
    }

    /// <summary>
    /// The bound Definition is a volatile field and its contents may not be cached.
    /// </summary>
    /// <value></value>
    public bool IsVolatile {
      get { return this.isVolatile; }
      set { this.isVolatile = value; }
    }
    bool isVolatile;
  }

  /// <summary>
  /// 
  /// </summary>
  public sealed class ThisReference : Expression, IThisReference {

    /// <summary>
    /// 
    /// </summary>
    public ThisReference() {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="thisReference"></param>
    public ThisReference(IThisReference thisReference)
      : base(thisReference) {
    }

    /// <summary>
    /// Calls the visitor.Visit(T) method where T is the most derived object model node interface type implemented by the concrete type
    /// of the object implementing IDoubleDispatcher. The dispatch method does not invoke Dispatch on any child objects. If child traversal
    /// is desired, the implementations of the Visit methods should do the subsequent dispatching.
    /// </summary>
    /// <param name="visitor"></param>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }

  }

  /// <summary>
  /// 
  /// </summary>
  public sealed class TokenOf : Expression, ITokenOf {

    /// <summary>
    /// 
    /// </summary>
    public TokenOf() {
      this.definition = Dummy.TypeReference;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="tokenOf"></param>
    public TokenOf(ITokenOf tokenOf)
      : base(tokenOf) {
      this.definition = tokenOf.Definition;
    }

    /// <summary>
    /// Calls the visitor.Visit(T) method where T is the most derived object model node interface type implemented by the concrete type
    /// of the object implementing IDoubleDispatcher. The dispatch method does not invoke Dispatch on any child objects. If child traversal
    /// is desired, the implementations of the Visit methods should do the subsequent dispatching.
    /// </summary>
    /// <param name="visitor"></param>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }

    /// <summary>
    /// An instance of IFieldReference, IMethodReference or ITypeReference.
    /// </summary>
    /// <value></value>
    public object Definition {
      get { return this.definition; }
      set { this.definition = value; }
    }
    object definition;

  }

  /// <summary>
  /// 
  /// </summary>
  public sealed class TypeOf : Expression, ITypeOf, IMetadataTypeOf {

    /// <summary>
    /// 
    /// </summary>
    public TypeOf() {
      this.typeToGet = Dummy.TypeReference;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="typeOf"></param>
    public TypeOf(ITypeOf typeOf)
      : base(typeOf) {
      this.typeToGet = typeOf.TypeToGet;
    }

    /// <summary>
    /// Calls the visitor.Visit(T) method where T is the most derived object model node interface type implemented by the concrete type
    /// of the object implementing IDoubleDispatcher. The dispatch method does not invoke Dispatch on any child objects. If child traversal
    /// is desired, the implementations of the Visit methods should do the subsequent dispatching.
    /// </summary>
    /// <param name="visitor"></param>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }

    /// <summary>
    /// The type that will be represented by the System.Type instance.
    /// </summary>
    /// <value></value>
    public ITypeReference TypeToGet {
      get { return this.typeToGet; }
      set { this.typeToGet = value; }
    }
    ITypeReference typeToGet;

    #region IMetadataExpression Members

    void IMetadataExpression.Dispatch(IMetadataVisitor visitor) {
      visitor.Visit(this);
    }

    ITypeReference IMetadataExpression.Type {
      get { return this.Type; }
    }

    #endregion
  }

  /// <summary>
  /// 
  /// </summary>
  public sealed class UnaryNegation : UnaryOperation, IUnaryNegation {

    /// <summary>
    /// 
    /// </summary>
    public UnaryNegation() {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="unaryNegation"></param>
    public UnaryNegation(IUnaryNegation unaryNegation)
      : base(unaryNegation) {
      this.CheckOverflow = unaryNegation.CheckOverflow;
    }

    /// <summary>
    /// The negation must be performed with a check for arithmetic overflow if the operands are integers.
    /// </summary>
    /// <value></value>
    public bool CheckOverflow { get; set; }

    /// <summary>
    /// Calls the visitor.Visit(T) method where T is the most derived object model node interface type implemented by the concrete type
    /// of the object implementing IDoubleDispatcher. The dispatch method does not invoke Dispatch on any child objects. If child traversal
    /// is desired, the implementations of the Visit methods should do the subsequent dispatching.
    /// </summary>
    /// <param name="visitor"></param>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }

  }

  /// <summary>
  /// 
  /// </summary>
  public abstract class UnaryOperation : Expression, IUnaryOperation {

    /// <summary>
    /// 
    /// </summary>
    internal UnaryOperation() {
      this.operand = CodeDummy.Expression;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="unaryOperation"></param>
    internal UnaryOperation(IUnaryOperation unaryOperation)
      : base(unaryOperation) {
      this.operand = unaryOperation.Operand;
    }

    /// <summary>
    /// The value on which the operation is performed.
    /// </summary>
    /// <value></value>
    public IExpression Operand {
      get { return this.operand; }
      set { this.operand = value; }
    }
    IExpression operand;

  }

  /// <summary>
  /// 
  /// </summary>
  public sealed class UnaryPlus : UnaryOperation, IUnaryPlus {

    /// <summary>
    /// 
    /// </summary>
    public UnaryPlus() {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="unaryPlus"></param>
    public UnaryPlus(IUnaryPlus unaryPlus)
      : base(unaryPlus) {
    }

    /// <summary>
    /// Calls the visitor.Visit(T) method where T is the most derived object model node interface type implemented by the concrete type
    /// of the object implementing IDoubleDispatcher. The dispatch method does not invoke Dispatch on any child objects. If child traversal
    /// is desired, the implementations of the Visit methods should do the subsequent dispatching.
    /// </summary>
    /// <param name="visitor"></param>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }

  }

  /// <summary>
  /// 
  /// </summary>
  public sealed class VectorLength : Expression, IVectorLength {

    /// <summary>
    /// 
    /// </summary>
    public VectorLength() {
      this.vector = CodeDummy.Expression;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="vectorLength"></param>
    public VectorLength(IVectorLength vectorLength)
      : base(vectorLength) {
      this.vector = vectorLength.Vector;
    }

    /// <summary>
    /// Calls the visitor.Visit(T) method where T is the most derived object model node interface type implemented by the concrete type
    /// of the object implementing IDoubleDispatcher. The dispatch method does not invoke Dispatch on any child objects. If child traversal
    /// is desired, the implementations of the Visit methods should do the subsequent dispatching.
    /// </summary>
    /// <param name="visitor"></param>
    public override void Dispatch(ICodeVisitor visitor) {
      visitor.Visit(this);
    }

    /// <summary>
    /// An expression that results in a value of a vector (zero-based one-dimensional array) type.
    /// </summary>
    /// <value></value>
    public IExpression Vector {
      get { return this.vector; }
      set { this.vector = value; }
    }
    IExpression vector;

  }
}
