//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.Collections.Generic;

//^ using Microsoft.Contracts;

namespace Microsoft.Cci {
  /// <summary>
  /// An expression that adds the value of the left operand to the value of the right operand.
  /// Both operands must be primitive numeric types.
  /// </summary>
  public interface IAddition : IBinaryOperation {
    /// <summary>
    /// The addition must be performed with a check for arithmetic overflow if the operands are integers.
    /// </summary>
    bool CheckOverflow {
      get;
    }

  }

  /// <summary>
  /// An expression that denotes a value that has an address in memory, such as a local variable, parameter, field, array element, pointer target, or method.
  /// </summary>
  public interface IAddressableExpression : IExpression {

    /// <summary>
    /// The local variable, parameter, field, array element, pointer target or method that this expression denotes.
    /// </summary>
    object/*?*/ Definition {
      get;
      //^ ensures result == null || result is ILocalDefinition || result is IParameterDefinition || result is IFieldReference || result is IArrayIndexer 
      //^   || result is IAddressDereference || result is IMethodReference || result is IThisReference;
    }

    /// <summary>
    /// The instance to be used if this.Definition is an instance field/method or array indexer.
    /// </summary>
    IExpression/*?*/ Instance { get; }

  }

  /// <summary>
  /// An expression that takes the address of a target expression.
  /// </summary>
  public interface IAddressOf : IExpression {
    /// <summary>
    /// An expression that represents an addressable location in memory.
    /// </summary>
    IAddressableExpression Expression { get; }

    /// <summary>
    /// If true, the address can only be used in operations where defining type of the addressed
    /// object has control over whether or not the object is mutated. For example, a value type that
    /// exposes no public fields or mutator methods cannot be changed using this address.
    /// </summary>
    bool ObjectControlsMutability { get; }
  }

  /// <summary>
  /// An expression that deferences an address (pointer).
  /// </summary>
  public interface IAddressDereference : IExpression {
    /// <summary>
    /// The address to dereference.
    /// </summary>
    IExpression Address {
      get;
      // ^ ensures result.Type is IPointerTypeReference;
    }

    /// <summary>
    /// If the addres to dereference is not aligned with the size of the target type, this property specifies the actual alignment.
    /// For example, a value of 1 specifies that the pointer is byte aligned, whereas the target type may be word sized.
    /// </summary>
    ushort Alignment {
      get;
      //^ requires this.IsUnaligned;
      //^ ensures result == 1 || result == 2 || result == 4;
    }

    /// <summary>
    /// True if the address is not aligned to the natural size of the target type. If true, the actual alignment of the
    /// address is specified by this.Alignment.
    /// </summary>
    bool IsUnaligned { get; }

    /// <summary>
    /// The location at Address is volatile and its contents may not be cached.
    /// </summary>
    bool IsVolatile { get; }
  }

  /// <summary>
  /// An expression that evaluates to an instance of a delegate type where the body of the method called by the delegate is specified by the expression.
  /// </summary>
  public interface IAnonymousDelegate : IExpression, ISignature {

    /// <summary>
    /// A block of statements providing the implementation of the anonymous method that is called by the delegate that is the result of this expression.
    /// </summary>
    IBlockStatement Body { get; }

    /// <summary>
    /// The parameters this anonymous method.
    /// </summary>
    new IEnumerable<IParameterDefinition> Parameters { get; }

    /// <summary>
    /// The return type of the delegate.
    /// </summary>
    ITypeReference ReturnType { get; }

    /// <summary>
    /// The type of delegate that this expression results in.
    /// </summary>
    new ITypeReference Type { get; }

  }

  /// <summary>
  /// An expression that represents an array element access.
  /// </summary>
  public interface IArrayIndexer : IExpression {

    /// <summary>
    /// An expression that results in value of an array type.
    /// </summary>
    IExpression IndexedObject { get; }

    /// <summary>
    /// The array indices.
    /// </summary>
    IEnumerable<IExpression> Indices { get; }

  }

  /// <summary>
  /// An expression that assigns the value of the source (right) operand to the location represented by the target (left) operand.
  /// The expression result is the value of the source expression.
  /// </summary>
  public interface IAssignment : IExpression {
    /// <summary>
    /// The expression representing the value to assign. 
    /// </summary>
    IExpression Source { get; }

    /// <summary>
    /// The expression representing the target to assign to.
    /// </summary>
    ITargetExpression Target { get; }
  }

  /// <summary>
  /// An expression that represents a reference to the base class instance of the current object instance. 
  /// Used to qualify calls to base class methods from inside overrides, and so on.
  /// </summary>
  public interface IBaseClassReference : IExpression {
  }

  /// <summary>
  /// A binary operation performed on a left and right operand.
  /// </summary>
  public interface IBinaryOperation : IExpression {
    /// <summary>
    /// The left operand.
    /// </summary>
    IExpression LeftOperand { get; }

    /// <summary>
    /// The right operand.
    /// </summary>
    IExpression RightOperand { get; }
  }

  /// <summary>
  /// An expression that computes the bitwise and of the left and right operands. 
  /// </summary>
  public interface IBitwiseAnd : IBinaryOperation {
  }

  /// <summary>
  /// An expression that computes the bitwise or of the left and right operands. 
  /// </summary>
  public interface IBitwiseOr : IBinaryOperation {
  }

  /// <summary>
  /// An expression that introduces a new block scope and that references local variables
  /// that are defined and initialized by embedded statements when control reaches the expression.
  /// </summary>
  public interface IBlockExpression : IExpression {

    /// <summary>
    /// A block of statements that typically introduce local variables to hold sub expressions.
    /// The scope of these declarations coincides with the block expression. 
    /// The statements are executed before evaluation of Expression occurs.
    /// </summary>
    IBlockStatement BlockStatement { get; }

    /// <summary>
    /// The expression that computes the result of the entire block expression.
    /// This expression can contain references to the local variables that are declared inside BlockStatement.
    /// </summary>
    IExpression Expression { get; }
  }

  /// <summary>
  /// An expression that binds to a local variable, parameter or field.
  /// </summary>
  public interface IBoundExpression : IExpression {

    /// <summary>
    /// If Definition is a field and the field is not aligned with natural size of its type, this property specifies the actual alignment.
    /// For example, if the field is byte aligned, then the result of this property is 1. Likewise, 2 for word (16-bit) alignment and 4 for
    /// double word (32-bit alignment). 
    /// </summary>
    byte Alignment {
      get;
      //^ requires IsUnaligned;
      //^ ensures result == 1 || result == 2 || result == 4;
    }

    /// <summary>
    /// The local variable, parameter or field that this expression binds to.
    /// </summary>
    object Definition {
      get;
      //^ ensures result is ILocalDefinition || result is IParameterDefinition || result is IFieldReference;
    }

    /// <summary>
    /// If the expression binds to an instance field then this property is not null and contains the instance.
    /// </summary>
    IExpression/*?*/ Instance {
      get;
      //^ ensures this.Definition is IFieldReference && !((IFieldReference)this.Definition).ResolvedField.IsStatic <==> result != null;
    }

    /// <summary>
    /// True if the definition is a field and the field is not aligned with the natural size of its type.
    /// For example if the field type is Int32 and the field is aligned on an Int16 boundary.
    /// </summary>
    bool IsUnaligned { get; }

    /// <summary>
    /// The bound Definition is a volatile field and its contents may not be cached.
    /// </summary>
    bool IsVolatile { get; }

  }

  /// <summary>
  /// An expression that casts the value to the given type, resulting in a null value if the cast does not succeed.
  /// </summary>
  public interface ICastIfPossible : IExpression {
    /// <summary>
    /// The value to cast if possible.
    /// </summary>
    IExpression ValueToCast { get; }

    /// <summary>
    /// The type to which the value must be cast. If the value is not of this type, the expression results in a null value of this type.
    /// </summary>
    ITypeReference TargetType { get; }
  }

  /// <summary>
  /// An expression that results in true if the given operand is an instance of the given type.
  /// </summary>
  public interface ICheckIfInstance : IExpression {
    /// <summary>
    /// The value to check.
    /// </summary>
    IExpression Operand { get; }

    /// <summary>
    /// The type to which the value must belong for this expression to evaluate as true.
    /// </summary>
    ITypeReference TypeToCheck { get; }
  }

  /// <summary>
  /// Converts a value to a given type.
  /// </summary>
  public interface IConversion : IExpression {
    /// <summary>
    /// The value to convert.
    /// </summary>
    IExpression ValueToConvert { get; }

    /// <summary>
    /// If true and ValueToConvert is a number and ResultType is a numeric type, check that ValueToConvert falls within the range of ResultType and throw an exception if not.
    /// </summary>
    bool CheckNumericRange { get; }

    /// <summary>
    /// The type to which the value is to be converted.
    /// </summary>
    ITypeReference TypeAfterConversion { get; }
  }

  /// <summary>
  /// An expression that does not change its value at runtime and can be evaluated at compile time.
  /// </summary>
  public interface ICompileTimeConstant : IExpression {

    /// <summary>
    /// The compile time value of the expression. Can be null.
    /// </summary>
    object/*?*/ Value { get; }
  }

  /// <summary>
  /// An expression that results in one of two values, depending on the value of a condition.
  /// </summary>
  public interface IConditional : IExpression {
    /// <summary>
    /// The condition that determines which subexpression to evaluate.
    /// </summary>
    IExpression Condition { get; }

    /// <summary>
    /// The expression to evaluate as the value of the overall expression if the condition is true.
    /// </summary>
    IExpression ResultIfTrue { get; }

    /// <summary>
    /// The expression to evaluate as the value of the overall expression if the condition is false.
    /// </summary>
    IExpression ResultIfFalse { get; }
  }

  /// <summary>
  /// An expression that creates an array instance.
  /// </summary>
  public interface ICreateArray : IExpression {
    /// <summary>
    /// The element type of the array.
    /// </summary>
    ITypeReference ElementType { get; }

    /// <summary>
    /// The initial values of the array elements. May be empty.
    /// </summary>
    IEnumerable<IExpression> Initializers {
      get;
    }

    /// <summary>
    /// The index value of the first element in each dimension.
    /// </summary>
    IEnumerable<int> LowerBounds {
      get;
      // ^ ensures count{int lb in result} == Rank;
    }

    /// <summary>
    /// The number of dimensions of the array.
    /// </summary>
    uint Rank {
      get;
      //^ ensures result > 0;
    }

    /// <summary>
    /// The number of elements allowed in each dimension.
    /// </summary>
    IEnumerable<IExpression> Sizes {
      get;
      // ^ ensures count{int size in result} == Rank;
    }

  }

  /// <summary>
  /// Creates an instance of the delegate type return by this.Type, using the method specified by this.MethodToCallViaDelegate.
  /// If the method is an instance method, then this.Instance specifies the expression that results in the instance on which the 
  /// method will be called.
  /// </summary>
  public interface ICreateDelegateInstance : IExpression {

    /// <summary>
    /// An expression that evaluates to the instance (if any) on which this.MethodToCallViaDelegate must be called (via the delegate).
    /// </summary>
    IExpression/*?*/ Instance {
      get;
      //^ ensures this.MethodToCallViaDelegate.ResolvedMethod.IsStatic <==> result == null;
    }

    /// <summary>
    /// The method that is to be be called when the delegate instance is invoked.
    /// </summary>
    IMethodReference MethodToCallViaDelegate { get; }

  }

  /// <summary>
  /// An expression that invokes an object constructor.
  /// </summary>
  public interface ICreateObjectInstance : IExpression {
    /// <summary>
    /// The arguments to pass to the constructor.
    /// </summary>
    IEnumerable<IExpression> Arguments { get; }

    /// <summary>
    /// The contructor method to call.
    /// </summary>
    IMethodReference MethodToCall { get; }

  }

  /// <summary>
  /// An expression that results in the default value of a given type.
  /// </summary>
  public interface IDefaultValue : IExpression {
    /// <summary>
    /// The type whose default value is the result of this expression.
    /// </summary>
    ITypeReference DefaultValueType { get; }
  }

  /// <summary>
  /// An expression that divides the value of the left operand by the value of the right operand. 
  /// </summary>
  public interface IDivision : IBinaryOperation {
    /// <summary>
    /// The division must be performed with a check for arithmetic overflow if the operands are integers.
    /// </summary>
    bool CheckOverflow {
      get;
    }
  }

  /// <summary>
  /// An expression that results in true if both operands represent the same value or object.
  /// </summary>
  public interface IEquality : IBinaryOperation {
  }

  /// <summary>
  /// An expression that computes the bitwise exclusive or of the left and right operands. 
  /// </summary>
  public interface IExclusiveOr : IBinaryOperation {
  }

  /// <summary>
  /// An expression results in a value of some type.
  /// </summary>
  public interface IExpression : IErrorCheckable, IObjectWithLocations {

    /// <summary>
    /// Calls the visitor.Visit(T) method where T is the most derived object model node interface type implemented by the concrete type
    /// of the object implementing IStatement. The dispatch method does not invoke Dispatch on any child objects. If child traversal
    /// is desired, the implementations of the Visit methods should do the subsequent dispatching.
    /// </summary>
    void Dispatch(ICodeVisitor visitor);

    /// <summary>
    /// The type of value the expression will evaluate to, as determined at compile time.
    /// </summary>
    ITypeReference Type { get; }

    /// <summary>
    /// if <c>true</c>, then the expression is side
    /// </summary>
    bool IsPure { get; }
  }

  /// <summary>
  /// An expression that results in an instance of System.Type that represents the compile time type that has been paired with a runtime value via a typed reference.
  /// This corresponds to the __reftype operator in C#.
  /// </summary>
  public interface IGetTypeOfTypedReference : IExpression {
    /// <summary>
    /// An expression that results in a value of type System.TypedReference.
    /// </summary>
    IExpression TypedReference { get; }
  }

  /// <summary>
  /// An expression that converts the typed reference value resulting from evaluating TypedReference to a value of the type specified by TargetType.
  /// This corresponds to the __refvalue operator in C#.
  /// </summary>
  public interface IGetValueOfTypedReference : IExpression {
    /// <summary>
    /// An expression that results in a value of type System.TypedReference.
    /// </summary>
    IExpression TypedReference { get; }

    /// <summary>
    /// The type to which the value part of the typed reference must be converted.
    /// </summary>
    ITypeReference TargetType { get; }
  }

  /// <summary>
  /// An expression that results in true if the value of the left operand is greater than the value of the right operand.
  /// </summary>
  public interface IGreaterThan : IBinaryOperation {
  }

  /// <summary>
  /// An expression that results in true if the value of the left operand is greater than or equal to the value of the right operand.
  /// </summary>
  public interface IGreaterThanOrEqual : IBinaryOperation {
  }

  /// <summary>
  /// An expression that results in the value of the left operand, shifted left by the number of bits specified by the value of the right operand.
  /// </summary>
  public interface ILeftShift : IBinaryOperation {
  }

  /// <summary>
  /// An expression that results in true if the value of the left operand is less than the value of the right operand.
  /// </summary>
  public interface ILessThan : IBinaryOperation {
  }

  /// <summary>
  /// An expression that results in true if the value of the left operand is less than or equal to the value of the right operand.
  /// </summary>
  public interface ILessThanOrEqual : IBinaryOperation {
  }

  /// <summary>
  /// An expression that results in the logical negation of the boolean value of the given operand.
  /// </summary>
  public interface ILogicalNot : IUnaryOperation {
  }

  /// <summary>
  /// An expression that creates a typed reference (a pair consisting of a reference to a runtime value and a compile time type).
  /// This is similar to what happens when a value type is boxed, except that the boxed value can be an object and
  /// the runtime type of the boxed value can be a subtype of the compile time type that is associated with the boxed valued.
  /// </summary>
  public interface IMakeTypedReference : IExpression {
    /// <summary>
    /// The value to box in a typed reference.
    /// </summary>
    IExpression Operand { get; }

  }

  /// <summary>
  /// An expression that invokes a method.
  /// </summary>
  public interface IMethodCall : IExpression {

    /// <summary>
    /// The arguments to pass to the method, after they have been converted to match the parameters of the resolved method.
    /// </summary>
    IEnumerable<IExpression> Arguments { get; }

    /// <summary>
    /// True if the method to call is determined at run time, based on the runtime type of ThisArgument.
    /// </summary>
    bool IsVirtualCall {
      get;
      //^ ensures result ==> this.MethodToCall.ResolvedMethod.IsVirtual;
    }

    /// <summary>
    /// True if the method to call is static (has no this parameter).
    /// </summary>
    bool IsStaticCall {
      get;
      //^ ensures result ==> this.MethodToCall.ResolvedMethod.IsStatic;
    }

    /// <summary>
    /// True if this method call terminates the calling method. It indicates that the calling method's stack frame is not required
    /// and can be removed before executing the call.
    /// </summary>
    bool IsTailCall {
      get;
    }

    /// <summary>
    /// The method to call.
    /// </summary>
    IMethodReference MethodToCall { get; }

    /// <summary>
    /// The expression that results in the value that must be passed as the value of the this argument of the resolved method.
    /// </summary>
    IExpression ThisArgument {
      get;
      //^ requires !this.IsStaticCall;
    }

  }

  /// <summary>
  /// An expression that results in the remainder of dividing value the left operand by the value of the right operand. 
  /// </summary>
  public interface IModulus : IBinaryOperation {
  }

  /// <summary>
  /// An expression that multiplies the value of the left operand by the value of the right operand. 
  /// </summary>
  public interface IMultiplication : IBinaryOperation {
    /// <summary>
    /// The multiplication must be performed with a check for arithmetic overflow if the operands are integers.
    /// </summary>
    bool CheckOverflow {
      get;
    }
  }

  /// <summary>
  /// An expression that represents a (name, value) pair and that is typically used in method calls, custom attributes and object initializers.
  /// </summary>
  public interface INamedArgument : IExpression {
    /// <summary>
    /// The name of the parameter or property or field that corresponds to the argument.
    /// </summary>
    IName ArgumentName { get; }

    /// <summary>
    /// The value of the argument.
    /// </summary>
    IExpression ArgumentValue { get; }

    /// <summary>
    /// Returns either null or the parameter or property or field that corresponds to this argument.
    /// </summary>
    object/*?*/ ResolvedDefinition {
      get;
      //^ ensures result == null || result is IParameterDefinition || result is IPropertyDefinition || result is IFieldDefinition;
    }
  }

  /// <summary>
  /// An expression that results in false if both operands represent the same value or object.
  /// </summary>
  public interface INotEquality : IBinaryOperation {
  }

  /// <summary>
  /// An expression that represents the value that a target expression had at the start of the method that has a postcondition that includes this expression.
  /// </summary>
  public interface IOldValue : IExpression {
    /// <summary>
    /// The expression whose value at the start of method execution is referred to in the method postcondition.
    /// </summary>
    IExpression Expression { get; }
  }

  /// <summary>
  /// An expression that results in the bitwise not (1's complement) of the operand.
  /// </summary>
  public interface IOnesComplement : IUnaryOperation {
  }

  /// <summary>
  /// An expression that must match an out parameter of a method. The method assigns a value to the target Expression.
  /// </summary>
  public interface IOutArgument : IExpression {
    /// <summary>
    /// The target that is assigned to as a result of the method call.
    /// </summary>
    ITargetExpression Expression { get; }
  }

  /// <summary>
  /// An expression that calls a method indirectly via a function pointer.
  /// </summary>
  public interface IPointerCall : IExpression {

    /// <summary>
    /// The arguments to pass to the method, after they have been converted to match the parameters of the method.
    /// </summary>
    IEnumerable<IExpression> Arguments { get; }

    /// <summary>
    /// True if this method call terminates the calling method. It indicates that the calling method's stack frame is not required
    /// and can be removed before executing the call.
    /// </summary>
    bool IsTailCall {
      get;
    }

    /// <summary>
    /// An expression that results at runtime in a function pointer that points to the actual method to call.
    /// </summary>
    IExpression Pointer {
      get;
      //^ ensures result.Type is IFunctionPointer;
    }

  }

  /// <summary>
  /// An expression that must match a ref parameter of a method. 
  /// The value, before the call, of the addressable Expression is passed to the method and the method may assign a new value to the 
  /// addressable Expression during the call.
  /// </summary>
  public interface IRefArgument : IExpression {
    /// <summary>
    /// The target that is assigned to as a result of the method call, but whose value is also passed to the method at the start of the call.
    /// </summary>
    IAddressableExpression Expression { get; }
  }

  /// <summary>
  /// An expression that refers to the return value of a method.
  /// </summary>
  public interface IReturnValue : IExpression {
  }

  /// <summary>
  /// An expression that results in the value of the left operand, shifted right by the number of bits specified by the value of the right operand, duplicating the sign bit.
  /// </summary>
  public interface IRightShift : IBinaryOperation {
  }

  /// <summary>
  /// An expression that denotes the runtime argument handle of a method that accepts extra arguments. 
  /// This expression corresponds to __arglist in C# and results in a value that can be used as the argument to the constructor for System.ArgIterator.
  /// </summary>
  public interface IRuntimeArgumentHandleExpression : IExpression {
  }

  /// <summary>
  /// An expression that computes the memory size of instances of a given type at runtime.
  /// </summary>
  public interface ISizeOf : IExpression {
    /// <summary>
    /// The type to size.
    /// </summary>
    ITypeReference TypeToSize { get; }
  }

  /// <summary>
  /// An expression that allocates an array on the call stack.
  /// </summary>
  public interface IStackArrayCreate : IExpression {

    /// <summary>
    /// The type of the elements of the stack array. This type must be unmanaged (contain no pointers to objects on the heap managed by the garbage collector).
    /// </summary>
    ITypeReference ElementType { get; }

    /// <summary>
    /// The size (number of bytes) of the stack array.
    /// </summary>
    IExpression Size { get; }

  }

  /// <summary>
  /// An expression that subtracts the value of the right operand from the value of the left operand. 
  /// </summary>
  public interface ISubtraction : IBinaryOperation {
    /// <summary>
    /// The subtraction must be performed with a check for arithmetic overflow if the operands are integers.
    /// </summary>
    bool CheckOverflow {
      get;
    }
  }

  /// <summary>
  /// An expression that can be the target of an assignment statement or that can be passed an argument to an out parameter.
  /// </summary>
  public interface ITargetExpression : IExpression {

    /// <summary>
    /// If Definition is a field and the field is not aligned with natural size of its type, this property specifies the actual alignment.
    /// For example, if the field is byte aligned, then the result of this property is 1. Likewise, 2 for word (16-bit) alignment and 4 for
    /// double word (32-bit alignment). 
    /// </summary>
    byte Alignment {
      get;
      //^ requires IsUnaligned;
      //^ ensures result == 1 || result == 2 || result == 4;
    }

    /// <summary>
    /// The local variable, parameter, field, property, array element or pointer target that this expression denotes.
    /// </summary>
    object/*?*/ Definition {
      get;
      //^ ensures result == null || result is ILocalDefinition || result is IParameterDefinition || result is IFieldReference || result is IArrayIndexer 
      //^   || result is IAddressDereference || result is IPropertyDefinition;
      //^ ensures result is IPropertyDefinition ==> ((IPropertyDefinition)result).Setter != null;
    }

    /// <summary>
    /// The instance to be used if this.Definition is an instance field/property or array indexer.
    /// </summary>
    IExpression/*?*/ Instance { get; }

    /// <summary>
    /// True if the definition is a field and the field is not aligned with the natural size of its type.
    /// For example if the field type is Int32 and the field is aligned on an Int16 boundary.
    /// </summary>
    bool IsUnaligned { get; }

    /// <summary>
    /// The bound Definition is a volatile field and its contents may not be cached.
    /// </summary>
    bool IsVolatile { get; }

  }

  /// <summary>
  /// Wraps an expression that represents a storage location that can be assigned to or whose address can be computed and passed as a parameter.
  /// Furthermore, this storage location must a string. Also wraps expressions supplying the starting position and length of a substring of the target string.
  /// An assignment of a string to a slice results in a new string where the slice has been replaced with given string.
  /// </summary>
  public interface ITargetSliceExpression : IAddressableExpression {
    /// <summary>
    /// An expression that represents the index of the first character of a string slice.
    /// </summary>
    IExpression StartOfSlice { get; }

    /// <summary>
    /// An expression that represents the length of the slice.
    /// </summary>
    IExpression LengthOfSlice { get; }
  }

  /// <summary>
  /// An expression that binds to the current object instance.
  /// </summary>
  public interface IThisReference : IExpression {
  }

  /// <summary>
  /// An expression that results in an instance of RuntimeFieldHandle, RuntimeMethodHandle or RuntimeTypeHandle.
  /// </summary>
  public interface ITokenOf : IExpression {
    /// <summary>
    /// An instance of IFieldReference, IMethodReference or ITypeReference.
    /// </summary>
    object Definition {
      get;
      //^ ensures result is IFieldReference || result is IMethodReference || result is ITypeReference;
    }
  }

  /// <summary>
  /// An expression that results in a System.Type instance.
  /// </summary>
  public interface ITypeOf : IExpression {
    /// <summary>
    /// The type that will be represented by the System.Type instance.
    /// </summary>
    ITypeReference TypeToGet { get; }
  }

  /// <summary>
  /// An expression that results in the arithmetic negation of the given operand.
  /// </summary>
  public interface IUnaryNegation : IUnaryOperation {
    /// <summary>
    /// The negation must be performed with a check for arithmetic overflow if the operands are integers.
    /// </summary>
    bool CheckOverflow {
      get;
    }
  }

  /// <summary>
  /// An operation performed on a single operand.
  /// </summary>
  public interface IUnaryOperation : IExpression {
    /// <summary>
    /// The value on which the operation is performed.
    /// </summary>
    IExpression Operand { get; }
  }

  /// <summary>
  /// An expression that results in the arithmetic value of the given operand.
  /// </summary>
  public interface IUnaryPlus : IUnaryOperation {
  }

  /// <summary>
  /// An expression that results in the length of a vector (zero-based one-dimensional array).
  /// </summary>
  public interface IVectorLength : IExpression {

    /// <summary>
    /// An expression that results in a value of a vector (zero-based one-dimensional array) type.
    /// </summary>
    IExpression Vector { get; }
  }

}
